#pragma once

#include <algorithm>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <span>

namespace luminpack::detail
{
std::size_t first_non_ascii_utf16(std::span<const char16_t> source) noexcept;

#if defined(LUMINPACK_CPP_AVX2)
std::size_t first_non_ascii_utf16_avx2(std::span<const char16_t> source) noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX512BW)
std::size_t first_non_ascii_utf16_avx512(std::span<const char16_t> source) noexcept;
#endif

inline std::size_t first_non_ascii_utf16_scalar(std::span<const char16_t> source) noexcept
{
    constexpr std::uint64_t mask = 0xff80ff80ff80ff80ull;
    std::size_t index = 0;
    for (; source.size() - index >= 4; index += 4)
    {
        std::uint64_t word;
        std::memcpy(&word, source.data() + index, sizeof(word));
        if (const auto invalid = word & mask; invalid != 0)
        {
            const auto bits = std::endian::native == std::endian::little
                ? std::countr_zero(invalid) : std::countl_zero(invalid);
            return index + static_cast<std::size_t>(bits) / 16;
        }
    }
    while (index < source.size() && source[index] < 0x80)
        ++index;
    return index;
}

// Check the leading vector, walk aligned complete vectors, then drain the bounded tail.
template<std::size_t Lanes, class FirstInvalid>
std::size_t first_non_ascii_utf16_vectors(std::span<const char16_t> source, FirstInvalid first_invalid) noexcept
{
    const auto* chars = source.data();
    const auto first = first_invalid(chars);
    if (first != Lanes)
        return first;

    constexpr auto width = Lanes * sizeof(char16_t);
    const auto misalignment = reinterpret_cast<std::uintptr_t>(chars) & (width - 1);
    auto index = (width - misalignment) / sizeof(char16_t);
    const auto last = source.size() - Lanes;
    for (; index <= last; index += Lanes)
    {
        const auto invalid = first_invalid(chars + index);
        if (invalid != Lanes)
            return index + invalid;
    }
    return index + first_non_ascii_utf16_scalar(source.subspan(index));
}

#if defined(LUMINPACK_CPP_AVX2)
// Core validates a complete group before storing any narrowed byte in that group.
template<class Narrow32, class Narrow64>
std::size_t encode_ascii_prefix_blocks(std::span<const char16_t> source, std::span<std::byte> destination,
    Narrow32 narrow32, Narrow64 narrow64) noexcept
{
    const auto length = source.size();
    const auto limit = std::min(length, destination.size());
    const auto* chars = source.data();
    auto* output = destination.data();
    std::size_t index = 0;

    for (; limit - index >= 128; index += 64)
        if (!narrow64(chars + index, output + index))
            break;
    for (; limit - index >= 32; index += 32)
        if (!narrow32(chars + index, output + index))
            break;

    // The overlapping final block covers the tail and repeats only equivalent output.
    if (index < length && limit == length && length - index < 32 && index != 0)
    {
        const auto tail = length - 32;
        if (narrow32(chars + tail, output + tail))
            return length;
    }
    return index;
}
#endif
}
