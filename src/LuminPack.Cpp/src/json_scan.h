#pragma once

#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <span>
#include <type_traits>

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>
#elif defined(LUMINPACK_CPP_SIMD) && (defined(_M_ARM64) || (defined(__aarch64__) && defined(__ARM_NEON)))
#define LUMINPACK_CPP_JSON_NEON
#if defined(_MSC_VER) && !defined(__clang__)
#include <arm64_neon.h>
#else
#include <arm_neon.h>
#endif
#endif

namespace luminpack::detail
{
std::size_t json_literal_prefix_wide(std::span<const std::byte> source, bool utf16) noexcept;

#if defined(LUMINPACK_CPP_AVX2)
std::size_t json_special_avx2(std::span<const std::byte> source, bool utf16) noexcept;
#endif
#if defined(LUMINPACK_CPP_AVX512BW)
std::size_t json_special_avx512(std::span<const std::byte> source, bool utf16) noexcept;
#endif

// These kernels consume full blocks. The caller passes the tail to a narrower kernel.
template<std::size_t Width, std::size_t MaskUnit = 1>
std::size_t scan_json_blocks(std::span<const std::byte> source, auto special_mask) noexcept
{
    for (std::size_t offset = 0; source.size() - offset >= Width; offset += Width)
    {
        const auto mask = special_mask(source.data() + offset);
        if (mask != 0)
            return offset + static_cast<std::size_t>(std::countr_zero(mask)) * MaskUnit;
    }
    return source.size();
}

template<bool Utf16>
std::size_t json_special_scalar(std::span<const std::byte> source) noexcept
{
    using unit = std::conditional_t<Utf16, std::uint16_t, std::uint8_t>;
    for (std::size_t offset = 0; source.size() - offset >= sizeof(unit); offset += sizeof(unit))
    {
        unit value;
        std::memcpy(&value, source.data() + offset, sizeof(unit));
        if (value < 0x20 || value == '"' || value == '\\' || (!Utf16 && value >= 0x80))
            return offset;
    }
    return source.size();
}

#if defined(LUMINPACK_CPP_INLINE_SSE2)
template<bool Utf16>
std::uint32_t json_special_mask128(const std::byte* source) noexcept
{
    __m128i lanes;
    std::memcpy(&lanes, source, sizeof(lanes));
    if constexpr (Utf16)
    {
        const auto biased = _mm_xor_si128(lanes, _mm_set1_epi16(-32768));
        const auto control = _mm_cmpgt_epi16(_mm_set1_epi16(0x20 - 32768), biased);
        const auto punctuation = _mm_or_si128(_mm_cmpeq_epi16(lanes, _mm_set1_epi16('"')),
            _mm_cmpeq_epi16(lanes, _mm_set1_epi16('\\')));
        return static_cast<std::uint32_t>(_mm_movemask_epi8(_mm_or_si128(control, punctuation)));
    }
    else
    {
        // The signed compare also selects bytes with the high bit set.
        const auto control_or_nonascii = _mm_cmpgt_epi8(_mm_set1_epi8(0x20), lanes);
        const auto punctuation = _mm_or_si128(_mm_cmpeq_epi8(lanes, _mm_set1_epi8('"')),
            _mm_cmpeq_epi8(lanes, _mm_set1_epi8('\\')));
        return static_cast<std::uint32_t>(_mm_movemask_epi8(_mm_or_si128(control_or_nonascii, punctuation)));
    }
}
#endif

// Returns a byte offset. UTF-8 stops at non-ASCII so the Unicode decoder can validate it.
// Probe one complete vector inline before dispatching a potentially long literal run.
template<bool Utf16>
std::size_t json_literal_prefix(std::span<const std::byte> source) noexcept
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
    if (source.size() >= 16)
    {
        const auto mask = json_special_mask128<Utf16>(source.data());
        if (mask != 0)
            return static_cast<std::size_t>(std::countr_zero(mask));
        const auto tail = source.subspan(16);
        return 16 + (tail.size() < 16 ? json_special_scalar<Utf16>(tail) : json_literal_prefix_wide(tail, Utf16));
    }
#elif defined(LUMINPACK_CPP_JSON_NEON)
    if (source.size() >= 16)
        return json_literal_prefix_wide(source, Utf16);
#endif
    return json_special_scalar<Utf16>(source);
}
}
