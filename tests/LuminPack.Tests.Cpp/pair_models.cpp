#include "pair.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppPairFixtures_x002ePairPacket;

namespace
{
constexpr luminpack::storage_model<2> model64{16, {0, 8}};
constexpr luminpack::storage_model<2> model32{12, {0, 4}};
using tagged_pairs = luminpack::tagged_pair_list_codec<
    luminpack::scalar_codec<std::int32_t>, luminpack::scalar_codec<std::int64_t>, model64, model32>;

template<class Value>
constexpr bool uses_packed_layout = requires(Value value) { value.field_PaddedArray; };

void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void check_truncated_pair()
{
    using namespace luminpack;
    using pair = pair_codec<scalar_codec<std::uint8_t>, scalar_codec<std::int32_t>>;
    constexpr std::array source{std::byte{0x51}, std::byte{0x44}, std::byte{0x33}};
    reader input(source);
    try
    {
        (void)pair::read(input);
    }
    catch (const wire_error&)
    {
        if (input.remaining() == source.size())
            return;
    }
    throw std::runtime_error("A truncated pair must fail before publishing the cursor.");
}

void check_json_pair_shape()
{
    using namespace luminpack;
    using integer = json_codec<scalar_codec<std::int32_t>>;
    using pair = json_pair_codec<integer, integer>;
    for (const auto malformed : {std::string_view("[1]"), std::string_view("[1,2,3]")})
    {
        try
        {
            (void)read_json_document<pair, json_encoding::utf8>(std::as_bytes(std::span{malformed}));
        }
        catch (const wire_error&)
        {
            continue;
        }
        throw std::runtime_error("A JSON pair must have exactly two elements.");
    }
}

std::uint8_t change_pair_model(luminpack::reader& input, luminpack::writer& output)
{
    const auto count = input.read_collection_count();
    check(count && *count != 0, "Expected a nonempty Tagged pair block.");
    const auto tag = input.read<std::uint8_t>();
    check(tag == 1 || tag == 2, "Expected an exported pair layout tag.");
    const auto& source = tag == 1 ? model64 : model32;
    const auto& target = tag == 1 ? model32 : model64;
    output.write(static_cast<std::int32_t>(*count));
    output.write<std::uint8_t>(3 - tag);
    for (std::size_t index = 0; index < *count; ++index)
    {
        const auto from = input.take(source.stride);
        const auto to = output.reserve(target.stride);
        std::memcpy(to.data() + target.offsets[0], from.data() + source.offsets[0], sizeof(std::int32_t));
        std::memcpy(to.data() + target.offsets[1], from.data() + source.offsets[1], sizeof(std::int64_t));
    }
    return tag;
}

void check_tagged_storage(std::span<const std::byte> bytes, const tagged_pairs::value_type& expected)
{
    tagged_pairs::value_type values(std::in_place, 8);
    auto overlapping = std::as_writable_bytes(std::span(*values));
    std::ranges::copy(bytes, overlapping.begin());
    luminpack::reader input(overlapping.first(bytes.size()));
    tagged_pairs::read_into(input, values);
    input.require_end();
    check(values == expected, "The pair block changed fields when reusing overlapping input storage.");

    std::vector<std::byte> unknown(bytes.begin(), bytes.end());
    unknown[4] = std::byte{3};
    for (auto malformed : {bytes.first(bytes.size() - 1), bytes.first(4), std::span<const std::byte>(unknown)})
    {
        luminpack::reader invalid(malformed);
        try
        {
            tagged_pairs::read_into(invalid, values);
        }
        catch (const luminpack::wire_error&)
        {
            check(invalid.remaining() == malformed.size() && values == expected,
                "An invalid pair block must preserve the input cursor and destination.");
            continue;
        }
        throw std::runtime_error("An invalid pair block was accepted.");
    }
}

void check_pair_models(const interop_models::path& directory)
{
    const auto original = interop_models::load(directory / "pair.bin");
    luminpack::reader input(original);
    const auto header = input.read<std::uint8_t>();
    std::vector<std::byte> alternate(original.size() + 16);
    luminpack::writer output(alternate);
    output.write(header);
    const auto tag = change_pair_model(input, output);
    const auto array = output.written().subspan(1);
    check(change_pair_model(input, output) == tag, "Array and List must use the same CLR storage model for pairs.");
    std::ranges::copy(input.unread(), output.reserve(input.remaining()).begin());
    alternate.resize(output.written().size());

    const auto restored = luminpack::read_binary_document<packet::root_binary>(alternate);
    const auto expected = luminpack::read_binary_document<packet::root_binary>(original);
    check(restored && expected && restored->field_TaggedArray == expected->field_TaggedArray &&
        restored->field_TaggedList == expected->field_TaggedList, "Alternate CLR models changed the pair collection fields.");
    const auto canonical = interop_models::load(directory / "out" / "pair.bin");
    const auto canonical_model = tag == 1 ? std::span<const std::byte>(original) : std::span<const std::byte>(alternate);
    check(std::ranges::equal(canonical, canonical_model), "Canonical Tagged pair output differs from the CLR layout.");
    check_tagged_storage(array, expected->field_TaggedArray);
    interop_models::store(directory / "out" / "pair-alternate.bin", alternate);
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the pair model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        constexpr auto packed = uses_packed_layout<packet::root_value::value_type>;
        interop_models::exchange<packet::root_binary, packet::root_json>(directory, "pair", packed);
        if constexpr (!packed)
            check_pair_models(directory);
        check_truncated_pair();
        check_json_pair_shape();
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
