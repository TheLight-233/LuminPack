#include <luminpack/detail/xxhash3.h>

#include <array>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <span>
#include <vector>

namespace
{
using luminpack::detail::xxhash3;

struct reference_vector
{
    std::size_t length;
    std::uint64_t expected;
};

// Reference values were computed by LuminPack.Internal.XxHash3.Hash64Utf8 in .NET 11.
constexpr reference_vector vectors[]{
{ 0, 0x2d06800538d394c2 },
    { 1, 0x1a038927df1311d6 },
    { 2, 0x840da7c4bdde1182 },
    { 3, 0xecbf338e198d32a7 },
    { 4, 0x6dd8088190ebf469 },
    { 8, 0xb9d00b2bd98bc06f },
    { 9, 0x8d339b62f4133162 },
    { 16, 0x3260c44aee603ee5 },
    { 17, 0x90e1a2e1c81731fd },
    { 32, 0xde779967b793d3b5 },
    { 33, 0xf8eda9634120c482 },
    { 64, 0x420a969c2a16613f },
    { 65, 0x43afb156fbf702f7 },
    { 96, 0xa8690f68b0e5d462 },
    { 97, 0x2a40229a8968f203 },
    { 128, 0xb2248eb885221088 },
    { 129, 0x25f36258653b1ed3 },
    { 143, 0x3ad1d83b90ef3052 },
    { 144, 0x71a5ec6e34f9be95 },
    { 145, 0x711716bb8b719183 },
    { 223, 0xb811786814aaefeb },
    { 224, 0xeff3e639edf1446f },
    { 239, 0xfb9aed973739e44c },
    { 240, 0xddd8a7c88cee5cf4 },
    { 241, 0xf0161d1bf6acc663 },
    { 255, 0xd449b40c7c33eeb2 },
    { 256, 0xc03a5501917826f4 },
    { 257, 0x712e0b09c4ae34e7 },
    { 319, 0xfdc70b632cd7e75a },
    { 320, 0x4539712080fb077e },
    { 321, 0x52d3cfeff6d11017 },
    { 511, 0x190d81102c23ad87 },
    { 512, 0x1b122f3b195e1003 },
    { 513, 0xfa8741cf0620fef0 },
    { 1023, 0x1c4d87ac45e36093 },
    { 1024, 0x986a0b016be3afb4 },
    { 1025, 0x1546431e9bff008e },
    { 1088, 0x57d388f1ca2f9fde },
    { 2047, 0xc2a8cae3896de118 },
    { 2048, 0xb4773e0eebf3ae7c },
    { 2049, 0xc58b32355769bb74 },
    { 3137, 0x8547356bef704475 },
    { 8192, 0x364ae732c9a9bf78 },
    { 8193, 0x06caf1ec0f30ea49 },
    { 16384, 0x1bcb2d1545ab1915 },
    { 16385, 0xe5be1fd1b8c492c8 },
};

std::span<const std::byte> make_input(std::vector<std::byte>& buffer, std::size_t offset, std::size_t length)
{
    auto input = std::span(buffer).subspan(offset, length);
    for (std::size_t index = 0; index < length; ++index)
        input[index] = static_cast<std::byte>((index * 131 + (index >> 3) * 7 + length * 29 + 0x31) & 0xff);
    return input;
}

bool expect(std::span<const std::byte> input, std::uint64_t expected)
{
    const auto actual = xxhash3(input);
    if (actual == expected)
        return true;
    std::cerr << "XXH3 mismatch at length " << input.size() << ": expected "
        << std::hex << expected << ", got " << actual << '\n';
    return false;
}
}

int main()
{
    if (!expect({}, vectors[0].expected))
        return 1;

    for (const auto [length, expected] : vectors)
    {
        // Exercise ordinary and unaligned windows with no allocated suffix.
        for (const auto offset : {std::size_t{0}, std::size_t{1}, std::size_t{7}, std::size_t{15}, std::size_t{31}})
        {
            std::vector<std::byte> bytes(offset + length);
            if (!expect(make_input(bytes, offset, length), expected))
                return 1;
        }
    }

    // The aggregate covers every short, midsize and stripe boundary through two full blocks.
    auto aggregate = std::uint64_t{0xcbf29ce484222325};
    std::vector<std::byte> buffer(2050);
    for (std::size_t length = 0; length <= buffer.size(); ++length)
        aggregate = (aggregate ^ xxhash3(make_input(buffer, 0, length))) * 0x100000001b3;
    if (aggregate != 0x7e4b9803f9686c37)
    {
        std::cerr << "XXH3 aggregate differs from the C# reference: " << std::hex << aggregate << '\n';
        return 1;
    }

    constexpr auto utf8 = std::to_array<std::uint8_t>({
        0xc3, 0x89, 0xe5, 0xad, 0x97, 0xe6, 0xae, 0xb5, 0xf0, 0x9f, 0x98, 0x80
    });
    constexpr auto utf16le = std::to_array<std::uint8_t>({
        0xc9, 0x00, 0x57, 0x5b, 0xb5, 0x6b, 0x3d, 0xd8, 0x00, 0xde
    });
    if (!expect(std::as_bytes(std::span(utf8)), 0x656588c1879d0df9) ||
        !expect(std::as_bytes(std::span(utf16le)), 0xcc470c34b1e21f4a))
        return 1;
}
