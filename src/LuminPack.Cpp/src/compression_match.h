#pragma once

#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>

#if defined(LUMINPACK_CPP_SIMD) && (defined(_M_ARM64) || (defined(__aarch64__) && defined(__ARM_NEON)))
#define LUMINPACK_CPP_COMPRESSION_NEON
#endif

namespace luminpack::detail
{
template<class Word>
Word load_match_word(const std::byte* source) noexcept
{
    Word word;
    std::memcpy(&word, source, sizeof(word));
    return word;
}

inline std::size_t matching_word_bytes(std::uint64_t difference) noexcept
{
    return static_cast<std::size_t>(std::countr_zero(difference)) / 8;
}

inline std::size_t compression_match_length_scalar(const std::byte* a, const std::byte* b,
    std::size_t length) noexcept
{
    std::size_t matched = 0;
    while (length - matched >= 16)
    {
        const auto first = load_match_word<std::uint64_t>(a + matched) ^ load_match_word<std::uint64_t>(b + matched);
        const auto last = load_match_word<std::uint64_t>(a + matched + 8) ^ load_match_word<std::uint64_t>(b + matched + 8);
        if ((first | last) != 0)
            return matched + (first != 0 ? matching_word_bytes(first) : 8 + matching_word_bytes(last));
        matched += 16;
    }
    if (length - matched >= 8)
    {
        const auto difference = load_match_word<std::uint64_t>(a + matched) ^ load_match_word<std::uint64_t>(b + matched);
        if (difference != 0)
            return matched + matching_word_bytes(difference);
        matched += 8;
    }
    while (matched < length && a[matched] == b[matched])
        ++matched;
    return matched;
}

#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_COMPRESSION_NEON)
std::size_t compression_match_length_wide(const std::byte* a, const std::byte* b, std::size_t length) noexcept;
#endif

inline std::size_t compression_match_length(const std::byte* a, const std::byte* b, std::size_t length) noexcept
{
#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_COMPRESSION_NEON)
    if (length >= 16)
    {
        const auto difference = load_match_word<std::uint64_t>(a) ^ load_match_word<std::uint64_t>(b);
        if (difference != 0)
            return matching_word_bytes(difference);
        return compression_match_length_wide(a, b, length);
    }
#endif
    return compression_match_length_scalar(a, b, length);
}

void compression_copy_periodic(std::byte* destination, std::size_t length, std::size_t distance) noexcept;

inline void compression_copy_match(std::byte* destination, std::size_t length, std::size_t distance) noexcept
{
    const auto* source = destination - distance;
    if (distance == 1)
        std::memset(destination, std::to_integer<unsigned char>(*source), length);
    else if (length <= distance)
        std::memcpy(destination, source, length);
    else
        compression_copy_periodic(destination, length, distance);
}

// Only complete vectors are supplied. A nonmatching group locates the byte in the loaded XORs.
template<std::size_t Width>
std::size_t compression_match_blocks(const std::byte* a, const std::byte* b, std::size_t length,
    auto difference, auto combine, auto empty, auto first_difference) noexcept
{
    std::size_t matched = 0;
    for (; length - matched >= 4 * Width; matched += 4 * Width)
    {
        const auto d0 = difference(a + matched, b + matched);
        const auto d1 = difference(a + matched + Width, b + matched + Width);
        const auto d2 = difference(a + matched + 2 * Width, b + matched + 2 * Width);
        const auto d3 = difference(a + matched + 3 * Width, b + matched + 3 * Width);
        if (!empty(combine(combine(d0, d1), combine(d2, d3))))
        {
            if (!empty(d0))
                return matched + first_difference(d0);
            if (!empty(d1))
                return matched + Width + first_difference(d1);
            if (!empty(d2))
                return matched + 2 * Width + first_difference(d2);
            return matched + 3 * Width + first_difference(d3);
        }
    }
    for (; length - matched >= Width; matched += Width)
    {
        const auto next = first_difference(difference(a + matched, b + matched));
        if (next != Width)
            return matched + next;
    }
    return matched;
}

#if defined(LUMINPACK_CPP_AVX2)
std::size_t compression_match_avx2(const std::byte* a, const std::byte* b, std::size_t length) noexcept;
void compression_copy_avx2(std::byte* destination, const std::byte* source, std::size_t length) noexcept;
#endif
#if defined(LUMINPACK_CPP_AVX512BW)
std::size_t compression_match_avx512(const std::byte* a, const std::byte* b, std::size_t length) noexcept;
void compression_copy_avx512(std::byte* destination, const std::byte* source, std::size_t length) noexcept;
#endif
}
