#include "pair.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppPairFixtures_x002ePairPacket;

namespace
{
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
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the pair model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<packet::root_binary, packet::root_json>(directory, "pair", true);
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
