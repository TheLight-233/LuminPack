#include "layout-packed.hpp"
#include "layout-tagged.hpp"
#include "interop_models.h"

#include <iostream>

namespace packed = luminpack_generated::root_LuminPackCppLayoutFixtures_x002eLayoutPackedPacket;
namespace tagged = luminpack_generated::root_LuminPackCppLayoutFixtures_x002eLayoutTaggedPacket;

namespace
{
constexpr std::size_t tagged_value_width = 13;
constexpr std::array<std::size_t, 2> tagged_strides{24, 16};
constexpr std::array<std::size_t, 2> amount_offsets{8, 4};
constexpr std::array<std::size_t, 2> flag_offsets{16, 12};
constexpr auto array_offset = 1 + tagged_value_width;
constexpr auto array_tag_offset = array_offset + sizeof(std::int32_t);

static_assert(packed::type_LuminPackCppLayoutFixtures_x002eLayoutPacked_binary::minimum_size == 12);
static_assert(packed::type_LuminPackCppLayoutFixtures_x002eLayoutNested_binary::minimum_size == 16);
static_assert(luminpack::detail::contiguous_codec<packed::type_LuminPackCppLayoutFixtures_x002eLayoutNative_binary>);
static_assert(tagged::type_LuminPackCppLayoutFixtures_x002eLayoutTagged_binary::minimum_size == tagged_value_width);

void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

std::vector<std::byte> alternate_tagged_array(std::span<const std::byte> fixture)
{
    luminpack::reader input(fixture);
    check(luminpack::read_object_header(input) == 4, "Expected the Tagged fixture object.");
    (void)input.take(tagged_value_width);
    const auto prefix = fixture.first(fixture.size() - input.remaining());
    const auto count = input.read_collection_count();
    check(count == 2, "Expected two Tagged array elements.");
    const auto source_tag = input.read<std::uint8_t>();
    check(source_tag == 1 || source_tag == 2, "Expected a known CLR storage tag.");
    const auto source_model = std::size_t(source_tag - 1);
    const auto target_model = 1 - source_model;
    const auto source_stride = tagged_strides[source_model];
    const auto target_stride = tagged_strides[target_model];
    const auto source = input.take(*count * source_stride);
    const auto suffix = input.unread();

    std::vector<std::byte> bytes(prefix.size() + sizeof(std::int32_t) + 1 + *count * target_stride + suffix.size());
    luminpack::writer output(bytes);
    std::ranges::copy(prefix, output.reserve(prefix.size()).begin());
    output.write(std::int32_t(*count));
    output.write(std::uint8_t(target_model + 1));
    for (std::size_t index = 0; index < *count; ++index)
    {
        const auto from = source.subspan(index * source_stride, source_stride);
        const auto to = output.reserve(target_stride);
        std::memcpy(to.data(), from.data(), sizeof(std::int32_t));
        std::memcpy(to.data() + amount_offsets[target_model], from.data() + amount_offsets[source_model], sizeof(std::int64_t));
        to[flag_offsets[target_model]] = from[flag_offsets[source_model]];
    }
    std::ranges::copy(suffix, output.reserve(suffix.size()).begin());
    return bytes;
}

template<class Codec>
void check_rejected_record(std::span<const std::byte> bytes)
{
    luminpack::reader input(bytes);
    try
    {
        (void)Codec::read(input);
    }
    catch (const luminpack::wire_error&)
    {
        check(input.remaining() == bytes.size(), "Failed layout read must leave the caller's cursor unchanged.");
        return;
    }
    throw std::runtime_error("Malformed layout record was accepted.");
}

void check_reused_storage(std::span<const std::byte> document, const tagged::root_value& expected)
{
    using codec = tagged::type_LuminPackCppLayoutFixtures_x002eLayoutTagged_sequence_binary;
    const auto tag = std::to_integer<std::size_t>(document[array_tag_offset]);
    const auto block = document.subspan(array_offset, sizeof(std::int32_t) + 1 + 2 * tagged_strides[tag - 1]);
    codec::value_type destination(std::in_place, 4);
    auto overlapping = std::as_writable_bytes(std::span(*destination)).first(block.size());
    std::ranges::copy(block, overlapping.begin());

    luminpack::reader input(overlapping);
    codec::read_into(input, destination);
    input.require_end();
    check(destination && destination->size() == 2, "Expected to reuse an overlapping two-element destination.");
    check(std::ranges::equal(*destination, *expected->field_Values, [](const auto& lhs, const auto& rhs)
        {
            return lhs.field_Code == rhs.field_Code && lhs.field_Amount == rhs.field_Amount && lhs.field_Flag == rhs.field_Flag;
        }), "Overlapping Tagged conversion failed to preserve the input fields.");
}

void check_alternate_layout(const interop_models::path& directory)
{
    const auto original = interop_models::load(directory / "layout-tagged.bin");
    auto alternate = alternate_tagged_array(original);
    const auto expected = luminpack::read_binary_document<tagged::root_binary>(original);
    const auto actual = luminpack::read_binary_document<tagged::root_binary>(alternate);
    check(expected && actual, "Expected two nonnull Tagged documents.");

    std::array<std::byte, 8192> expected_json{};
    std::array<std::byte, 8192> actual_json{};
    check(std::ranges::equal(luminpack::write_json_document<tagged::root_json>(expected_json, expected),
              luminpack::write_json_document<tagged::root_json>(actual_json, actual)),
        "Alternate CLR storage produced different fields.");
    check_reused_storage(alternate, expected);
    interop_models::store(directory / "out" / "layout-tagged-alternate.bin", alternate);

    check_rejected_record<tagged::root_binary>(std::span(alternate).first(array_tag_offset + 2));
    alternate[array_tag_offset] = std::byte{3};
    check_rejected_record<tagged::root_binary>(alternate);
}

void check_packed_boolean(const interop_models::path& directory)
{
    constexpr auto boolean_offset = 1 + sizeof(std::uint8_t) + sizeof(std::int64_t);
    auto malformed = interop_models::load(directory / "layout-packed.bin");
    check(malformed.size() > boolean_offset && malformed[boolean_offset] == std::byte{1},
        "Expected a Packed Boolean after the scalar prefix.");
    malformed[boolean_offset] = std::byte{2};
    check_rejected_record<packed::root_binary>(malformed);
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the layout model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<packed::root_binary, packed::root_json>(directory, "layout-packed", true);
        interop_models::exchange<tagged::root_binary, tagged::root_json>(directory, "layout-tagged");
        check_alternate_layout(directory);
        check_packed_boolean(directory);
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
