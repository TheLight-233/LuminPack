#include "tuple.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppTupleFixtures_x002eTuplePacket;

namespace
{
using namespace luminpack;

constexpr std::array flat_models{storage_model<2>{16, {0, 8}}, storage_model<2>{12, {0, 4}},
    storage_model<2>{16, {8, 0}}, storage_model<2>{12, {8, 0}}};
constexpr std::array nested_models{storage_model<3>{16, {0, 4, 8}}, storage_model<3>{16, {12, 8, 0}}};
constexpr std::array flat_widths{sizeof(std::int32_t), sizeof(std::int64_t)};
constexpr std::array nested_widths{sizeof(std::uint8_t), sizeof(std::int32_t), sizeof(std::int64_t)};

template<class Value>
constexpr bool uses_packed_layout = requires(Value value) { value.field_PackedReverse; };

void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<std::size_t Fields, std::size_t Models>
void rewrite_tuple_block(reader& input, writer& output, const std::array<std::size_t, Fields>& widths,
    const std::array<storage_model<Fields>, Models>& models, std::uint8_t target_tag)
{
    const auto count = input.read_collection_count();
    check(count && *count != 0, "Expected a nonempty Tagged tuple block.");
    const auto source_tag = input.read<std::uint8_t>();
    check(source_tag > 0 && source_tag <= models.size(), "Expected a modeled CLR tuple tag.");
    const auto& source = models[source_tag - 1];
    const auto& target = models[target_tag - 1];
    output.write(static_cast<std::int32_t>(*count));
    output.write(target_tag);
    for (std::size_t index = 0; index < *count; ++index)
    {
        const auto from = input.take(source.stride);
        const auto to = output.reserve(target.stride);
        for (std::size_t field = 0; field < Fields; ++field)
            std::memcpy(to.data() + target.offsets[field], from.data() + source.offsets[field], widths[field]);
    }
}

std::vector<std::byte> rewrite_tuple_models(std::span<const std::byte> original, std::uint8_t tag)
{
    reader input(original);
    std::vector<std::byte> alternate(original.size() + 16);
    writer output(alternate);
    output.write(input.read<std::uint8_t>());
    rewrite_tuple_block(input, output, flat_widths, flat_models, tag);
    rewrite_tuple_block(input, output, nested_widths, nested_models, (tag - 1) % 2 + 1);
    std::ranges::copy(input.unread(), output.reserve(input.remaining()).begin());
    alternate.resize(output.written().size());
    return alternate;
}

void check_truncated_tuple(std::span<const std::byte> bytes)
{
    reader input(bytes);
    try
    {
        (void)packet::root_binary::read(input);
    }
    catch (const wire_error&)
    {
        check(input.remaining() == bytes.size(), "An invalid Tagged block advanced the caller's cursor.");
        return;
    }
    throw std::runtime_error("An invalid Tagged tuple was accepted.");
}

void check_tuple_models(const interop_models::path& directory)
{
    const auto original = interop_models::load(directory / "tuple.bin");
    const auto canonical = interop_models::load(directory / "out" / "tuple.bin");
    const auto expected = read_binary_document<packet::root_binary>(original);
    for (std::uint8_t tag = 1; tag <= flat_models.size(); ++tag)
    {
        auto alternate = rewrite_tuple_models(original, tag);
        const auto decoded = read_binary_document<packet::root_binary>(alternate);
        check(decoded && expected && decoded->field_TaggedArray == expected->field_TaggedArray &&
            decoded->field_TaggedList == expected->field_TaggedList, "Alternate CLR layouts changed the tuple components.");
        if (tag == 1)
            check(canonical == alternate, "Converted tuple collections must write the canonical CLR models.");
        interop_models::store(directory / "out" / ("tuple-layout-" + std::to_string(tag) + ".bin"), alternate);
    }

    check_truncated_tuple(std::span(original).first(12));
    auto unknown_tag = original;
    unknown_tag[5] = std::byte{5};
    check_truncated_tuple(unknown_tag);
}

void check_json_tuple_shape()
{
    using scalar_json = json_codec<scalar_codec<std::int32_t>>;
    using tuple = json_tuple_codec<scalar_json, scalar_json, json_tuple_codec<scalar_json>>;
    for (const auto malformed : {std::string_view("[1,2]"), std::string_view("[1,2,3]"), std::string_view("[1,2,[3],4]")})
    {
        try
        {
            (void)read_json_document<tuple, json_encoding::utf8>(std::as_bytes(std::span{malformed}));
        }
        catch (const wire_error&)
        {
            continue;
        }
        throw std::runtime_error("A malformed JSON tuple was accepted.");
    }
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the tuple model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        constexpr bool packed = uses_packed_layout<packet::root_value::value_type>;
        interop_models::exchange<packet::root_binary, packet::root_json>(directory, "tuple", packed);
        if constexpr (!packed)
            check_tuple_models(directory);
        check_json_tuple_shape();
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
