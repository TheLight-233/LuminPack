#include "compression_match.h"

#include "cpu_isa.h"

#include <algorithm>

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>
#elif defined(LUMINPACK_CPP_COMPRESSION_NEON)
#if defined(_MSC_VER) && !defined(__clang__)
#include <arm64_neon.h>
#else
#include <arm_neon.h>
#endif
#endif

namespace luminpack::detail
{
namespace
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
std::uint16_t equal_mask(__m128i difference) noexcept
{
    return static_cast<std::uint16_t>(_mm_movemask_epi8(_mm_cmpeq_epi8(difference, _mm_setzero_si128())));
}

std::size_t match128(const std::byte* a, const std::byte* b, std::size_t length) noexcept
{
    return compression_match_blocks<16>(a, b, length,
        [](const std::byte* first, const std::byte* last) {
            return _mm_xor_si128(load_match_word<__m128i>(first), load_match_word<__m128i>(last));
        },
        [](__m128i first, __m128i last) { return _mm_or_si128(first, last); },
        [](__m128i value) { return equal_mask(value) == 0xffff; },
        [](__m128i value) { return static_cast<std::size_t>(std::countr_zero(static_cast<std::uint16_t>(~equal_mask(value)))); });
}

void copy128(std::byte* destination, const std::byte* source, std::size_t length) noexcept
{
    for (std::size_t i = 0; i < length; i += 16)
    {
        const auto value = load_match_word<__m128i>(source + i);
        std::memcpy(destination + i, &value, sizeof(value));
    }
}
#elif defined(LUMINPACK_CPP_COMPRESSION_NEON)
std::size_t match128(const std::byte* a, const std::byte* b, std::size_t length) noexcept
{
    return compression_match_blocks<16>(a, b, length,
        [](const std::byte* first, const std::byte* last) {
            return veorq_u8(vld1q_u8(reinterpret_cast<const std::uint8_t*>(first)),
                vld1q_u8(reinterpret_cast<const std::uint8_t*>(last)));
        },
        [](uint8x16_t first, uint8x16_t last) { return vorrq_u8(first, last); },
        [](uint8x16_t value) { return vmaxvq_u8(value) == 0; },
        [](uint8x16_t value) {
            const auto words = vreinterpretq_u64_u8(value);
            const auto low = vgetq_lane_u64(words, 0);
            return low != 0 ? matching_word_bytes(low) : 8 + matching_word_bytes(vgetq_lane_u64(words, 1));
        });
}

void copy128(std::byte* destination, const std::byte* source, std::size_t length) noexcept
{
    for (std::size_t i = 0; i < length; i += 16)
    {
        const auto value = vld1q_u8(reinterpret_cast<const std::uint8_t*>(source + i));
        vst1q_u8(reinterpret_cast<std::uint8_t*>(destination + i), value);
    }
}
#endif

void copy_long_match(std::byte* destination, const std::byte* source, std::size_t length, std::size_t distance) noexcept
{
    std::size_t copied = 0;
    while (copied < length)
    {
        const auto count = std::min(distance + copied, length - copied);
        std::memcpy(destination + copied, source, count);
        copied += count;
    }
}
}

#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_COMPRESSION_NEON)
std::size_t compression_match_length_wide(const std::byte* a, const std::byte* b, std::size_t length) noexcept
{
    std::size_t matched = 0;
#if defined(LUMINPACK_CPP_AVX512BW)
    if (length >= 64 && has_avx512bw())
    {
        const auto width = length & ~std::size_t{63};
        matched = compression_match_avx512(a, b, width);
        if (matched != width)
            return matched;
    }
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (length - matched >= 32 && has_avx2())
    {
        const auto width = (length - matched) & ~std::size_t{31};
        const auto count = compression_match_avx2(a + matched, b + matched, width);
        matched += count;
        if (count != width)
            return matched;
    }
#endif
    const auto width = (length - matched) & ~std::size_t{15};
    const auto count = match128(a + matched, b + matched, width);
    matched += count;
    if (count != width)
        return matched;
    return matched + compression_match_length_scalar(a + matched, b + matched, length - matched);
}
#endif

void compression_copy_periodic(std::byte* destination, std::size_t length, std::size_t distance) noexcept
{
    const auto* source = destination - distance;
    if (length >= 256 || (distance < 8 && length >= 64))
    {
        copy_long_match(destination, source, length, distance);
        return;
    }

    std::size_t copied = 0;
#if defined(LUMINPACK_CPP_AVX512BW)
    if (distance >= 64 && length >= 128 && has_avx512bw())
    {
        copied = length & ~std::size_t{63};
        compression_copy_avx512(destination, source, copied);
    }
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (distance >= 32 && (copied != 0 || length >= 64) && length - copied >= 32 && has_avx2())
    {
        const auto width = (length - copied) & ~std::size_t{31};
        compression_copy_avx2(destination + copied, source + copied, width);
        copied += width;
    }
#endif
#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_COMPRESSION_NEON)
    if (distance >= 16 && (copied != 0 || length >= 32))
    {
        const auto width = (length - copied) & ~std::size_t{15};
        copy128(destination + copied, source + copied, width);
        copied += width;
    }
#endif
    if (distance >= 8)
    {
        for (; length - copied >= 8; copied += 8)
            std::memcpy(destination + copied, source + copied, 8);
    }
    for (; copied < length; ++copied)
        destination[copied] = source[copied];
}
}
