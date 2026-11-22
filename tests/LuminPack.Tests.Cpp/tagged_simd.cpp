#include <luminpack/luminpack.h>

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <span>
#include <stdexcept>
#include <vector>

namespace
{
using namespace luminpack;

constexpr std::array<std::size_t, 3> widths{4, 4, 4};
constexpr storage_model<3> compact{12, {0, 4, 8}};
constexpr storage_model<3> reordered{12, {8, 0, 4}};
constexpr storage_model<3> padded{16, {4, 12, 0}};
constexpr storage_model<3> padded_dwords{24, {4, 12, 20}};
constexpr storage_model<3> padded_bytes{24, {1, 9, 17}};
constexpr auto untouched = std::byte{0xcc};
constexpr std::size_t header_size = sizeof(std::int32_t) + 1;

void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<auto Model>
using record = std::array<std::byte, Model.stride>;

template<auto Model>
std::vector<record<Model>> make_records(std::size_t count)
{
    const auto fill = [](std::size_t element, std::size_t field, std::size_t byte) {
        return static_cast<std::byte>((element * 47 + field * 17 + byte * 5 + 11) & 255);
    };
    std::vector<record<Model>> result(count);
    for (std::size_t element = 0; element < count; ++element)
    {
        result[element].fill(untouched);
        for (std::size_t field = 0; field < widths.size(); ++field)
            for (std::size_t byte = 0; byte < widths[field]; ++byte)
                result[element][Model.offsets[field] + byte] = fill(element, field, byte);
    }
    return result;
}

template<auto Model>
void check_fields(std::span<const record<Model>> actual)
{
    const auto expected = make_records<Model>(actual.size());
    for (std::size_t element = 0; element < actual.size(); ++element)
        for (std::size_t field = 0; field < widths.size(); ++field)
        {
            const auto offset = Model.offsets[field];
            check(std::equal(actual[element].data() + offset, actual[element].data() + offset + widths[field],
                expected[element].data() + offset), "Tagged SIMD copied the wrong field bytes.");
        }
}

template<auto Source, auto Destination>
void check_models(std::size_t count, std::size_t offset)
{
    constexpr tagged_layout<3, 2> from{widths, Source, {Source, Destination}};
    constexpr tagged_layout<3, 2> to{widths, Destination, {Source, Destination}};
    auto input = make_records<Source>(count);
    std::vector<std::byte> wire(offset + header_size + Source.stride * count, untouched);
    writer output{std::span(wire).subspan(offset)};
    write_tagged_array<record<Source>, from>(output, std::span<const record<Source>>(input));
    const auto encoded = output.written();
    check(encoded[sizeof(std::int32_t)] == std::byte{1}, "Matching writer did not keep its native tag.");

    auto destination = make_records<Destination>(count + 1);
    for (auto& item : destination)
        item.fill(untouched);
    reader cursor(encoded);
    auto converted = read_tagged_array_into<record<Destination>, to>(cursor, destination);
    cursor.require_end();
    check(converted && converted->size() == count, "Cross-layout reader did not read the complete block.");
    check_fields<Destination>(*converted);
    check(std::ranges::equal(*converted, make_records<Destination>(count)),
        "Cross-layout reader did not preserve the destination padding.");
    check(std::ranges::all_of(destination.back(), [](auto value) { return value == untouched; }),
        "Cross-layout reader touched the next destination record.");

    auto matching = std::vector<record<Source>>(count);
    cursor = reader(encoded);
    const auto copied = read_tagged_array_into<record<Source>, from>(cursor, matching);
    check(copied && matching == input, "Matching Tagged model did not preserve raw bytes and padding.");

    // A destination that holds its own input must stage the input first.
    std::vector<record<Destination>> alias((encoded.size() + Destination.stride - 1) / Destination.stride + count);
    auto alias_bytes = std::as_writable_bytes(std::span(alias));
    std::ranges::copy(encoded, alias_bytes.begin());
    cursor = reader(alias_bytes.first(encoded.size()));
    const auto overlapping = read_tagged_array_into<record<Destination>, to>(cursor, alias);
    check(overlapping && overlapping->size() == count, "Cross-layout overlapping read lost the record.");
    check_fields<Destination>(*overlapping);
}

void check_canonical_write()
{
    constexpr tagged_layout<3, 2> layout{widths, padded_bytes, {padded_dwords, compact}};
    constexpr auto count = 73;
    auto input = make_records<padded_bytes>(count);
    const auto wire_size = header_size + count * padded_dwords.stride;
    std::vector<record<padded_bytes>> alias((wire_size + padded_bytes.stride - 1) / padded_bytes.stride);
    std::copy(input.begin(), input.end(), alias.begin());
    writer output(std::as_writable_bytes(std::span(alias)).first(wire_size));
    write_tagged_array<record<padded_bytes>, layout>(output, std::span<const record<padded_bytes>>(alias).first(count));
    check(output.written().size() == wire_size && output.written()[sizeof(std::int32_t)] == std::byte{1},
        "Unmodeled Tagged writer did not choose the canonical tag and stride.");

    constexpr tagged_layout<3, 2> canonical{widths, padded_dwords, layout.wire};
    std::vector<record<padded_dwords>> result(count);
    reader cursor(output.written());
    const auto decoded = read_tagged_array_into<record<padded_dwords>, canonical>(cursor, result);
    check(decoded.has_value(), "Canonical Tagged output did not decode.");
    check_fields<padded_dwords>(*decoded);
    for (const auto& item : result)
        for (const auto byte : {0, 8, 16})
            check(item[byte] == std::byte{}, "Canonical Tagged writer exposed destination padding.");
}

struct bool_record
{
    std::uint8_t first;
    bool value;
    std::uint32_t last;
};

void check_boolean_validation()
{
    constexpr storage_model<3> local{sizeof(bool_record), {offsetof(bool_record, first),
        offsetof(bool_record, value), offsetof(bool_record, last)}};
    constexpr storage_model<3> alternate{8, {2, 0, 4}};
    constexpr tagged_layout<3, 2> layout{{1, 1, 4}, local, {local, alternate}};
    using codec = storage_list_codec<bool_record, layout, std::array<std::size_t, 1>{1}>;
    constexpr std::size_t count = 48;
    std::vector<std::byte> bytes(header_size + count * alternate.stride);
    writer output(bytes);
    output.write(static_cast<std::int32_t>(count));
    output.write<std::uint8_t>(2);
    bytes[header_size + (count - 1) * alternate.stride + alternate.offsets[1]] = std::byte{2};
    codec::value_type destination(std::in_place, 1, bool_record{43, true, 97});
    reader cursor(bytes);
    try
    {
        codec::read_into(cursor, destination);
    }
    catch (const wire_error&)
    {
        check(cursor.remaining() == bytes.size() && destination->size() == 1 &&
            destination->front().first == 43 && destination->front().value && destination->front().last == 97,
            "Invalid Boolean published a converted Tagged prefix.");
        return;
    }
    throw std::runtime_error("Cross-layout Tagged reader accepted an invalid Boolean.");
}
}

int main()
try
{
    constexpr auto narrow = detail::make_layout_conversion<widths, compact, reordered>();
    constexpr auto grouped_bytes = detail::make_layout_conversion<widths, padded_bytes, compact>();
    constexpr auto grouped_dwords = detail::make_layout_conversion<widths, padded_dwords, padded>();
    static_assert(narrow.shuffle.window.elements == 1 && narrow.shuffle.window.reserve == 2);
    static_assert(narrow.avx2.window.elements == 0);
    static_assert(grouped_bytes.avx2.window.elements == 2 && !grouped_bytes.avx2.dword_eligible);
    static_assert(grouped_dwords.avx2.window.elements == 2 && grouped_dwords.avx2.dword_eligible);
    static_assert(grouped_bytes.avx512.window.elements == 5 && grouped_bytes.avx512.window.reserve == 6);
    static_assert(!grouped_bytes.avx512.dword_eligible && grouped_dwords.avx512.dword_eligible);
    static_assert(grouped_bytes.arm64.window.input_vectors == 3 && grouped_bytes.arm64.window.output_vectors == 2);

    for (std::size_t count = 1; count <= 80; ++count)
        for (const auto offset : {std::size_t{0}, std::size_t{1}, std::size_t{7}})
        {
            check_models<compact, reordered>(count, offset);
            check_models<reordered, compact>(count, offset);
            check_models<padded_bytes, compact>(count, offset);
            check_models<compact, padded_bytes>(count, offset);
            check_models<padded_dwords, padded>(count, offset);
            check_models<padded, padded_dwords>(count, offset);
            check_models<padded_bytes, padded_dwords>(count, offset);
            check_models<padded_dwords, padded_bytes>(count, offset);
        }
    check_canonical_write();
    check_boolean_validation();
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
