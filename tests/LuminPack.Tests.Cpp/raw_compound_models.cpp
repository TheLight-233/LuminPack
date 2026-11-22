#include "raw-compound.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppRawCompoundFixtures_x002eRawCompoundPacket;

namespace
{
void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void check_padding(std::span<const std::byte> padding, std::byte expected)
{
    check(padding.size() == 3 && std::ranges::all_of(padding, [expected](auto value) { return value == expected; }),
        "The generated carrier did not retain the CLR padding.");
}

void check_raw_compound(const interop_models::path& directory)
{
    using luminpack::detail::contiguous_codec;
    using luminpack::raw_struct_codec;
    using root_value = packet::root_value::value_type;
    static_assert(std::same_as<decltype(root_value::field_Pair), std::pair<std::uint8_t, std::int32_t>>);
    static_assert(std::same_as<decltype(root_value::field_Tuple), decltype(root_value::field_Tuples)::value_type::value_type>);
    static_assert(contiguous_codec<raw_struct_codec<decltype(root_value::field_Tuple)>>);
    static_assert(contiguous_codec<raw_struct_codec<decltype(root_value::field_Pairs)::value_type::value_type>>);

    const auto document = interop_models::load(directory / "raw-compound.bin");
    const auto original = luminpack::read_binary_document<packet::root_binary>(document);
    const auto copy = original;
    check(copy && copy->field_Tuples && copy->field_TupleList && copy->field_Pairs && copy->field_PairList,
        "Expected non-null raw framework fields.");
    check(copy->field_Tuple.field_Item1 == 0x12345678 && copy->field_Tuple.field_Item2 == 0x67,
        "Tagged or Native scalar tuple fields are out of order.");
    check(copy->field_Pairs->front().field_Key == 1 && copy->field_Pairs->front().field_Value == 0x11223344,
        "Tagged or Native pair fields are out of order.");
    check_padding(copy->field_Tuple.padding_2, std::byte{0xA1});
    check_padding(copy->field_Tuples->at(0).padding_2, std::byte{0xA2});
    check_padding(copy->field_Tuples->at(1).padding_2, std::byte{0xA3});
    check_padding(copy->field_TupleList->front().padding_2, std::byte{0xA4});
    check_padding(copy->field_Pairs->at(0).padding_1, std::byte{0xB1});
    check_padding(copy->field_Pairs->at(1).padding_1, std::byte{0xB2});
    check_padding(copy->field_PairList->front().padding_1, std::byte{0xB3});

    std::vector<std::byte> output(document.size());
    const auto written = luminpack::write_binary_document<packet::root_binary>(output, copy);
    check(std::ranges::equal(written, document), "A C++ value copy lost bytes from a native framework document.");
    interop_models::store(directory / "out" / "raw-compound-pad.bin", written);

    const auto json_output = interop_models::load(directory / "out" / "raw-compound.bin");
    const auto from_json = luminpack::read_binary_document<packet::root_binary>(json_output);
    check(from_json && from_json->field_Pairs && !from_json->field_Pairs->empty(), "Expected a decoded JSON pair array.");
    check_padding(from_json->field_Tuple.padding_2, std::byte{});
    check_padding(from_json->field_Pairs->front().padding_1, std::byte{});
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the raw framework model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<packet::root_binary, packet::root_json>(directory, "raw-compound");
        check_raw_compound(directory);
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
