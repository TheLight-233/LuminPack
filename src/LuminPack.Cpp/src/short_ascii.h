#pragma once

#include "cpu_isa.h"

#include <cstddef>
#include <cstdint>
#include <cstring>
#include <span>

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>
#elif defined(LUMINPACK_CPP_SIMD) && (defined(_M_ARM64) || (defined(__aarch64__) && defined(__ARM_NEON)))
#define LUMINPACK_CPP_SHORT_ASCII_NEON
#if defined(_MSC_VER) && !defined(__clang__)
#include <arm64_neon.h>
#else
#include <arm_neon.h>
#endif
#endif

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_AVX2)
inline constexpr std::size_t short_ascii_max_length = 32;

template<bool EscapeJson>
bool try_encode_medium_ascii_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#elif defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
inline constexpr std::size_t short_ascii_max_length = 16;
#else
inline constexpr std::size_t short_ascii_max_length = 0;
#endif

#if defined(LUMINPACK_CPP_INLINE_SSE2)
inline bool is_short_ascii(__m128i chars) noexcept
{
    const auto high = _mm_and_si128(chars, _mm_set1_epi16(-128));
    return _mm_movemask_epi8(_mm_cmpeq_epi16(high, _mm_setzero_si128())) == 0xffff;
}

inline bool short_ascii_needs_json_escape(__m128i bytes) noexcept
{
    const auto controls = _mm_cmpgt_epi8(_mm_set1_epi8(0x20), bytes);
    const auto punctuation = _mm_or_si128(_mm_cmpeq_epi8(bytes, _mm_set1_epi8('"')),
        _mm_cmpeq_epi8(bytes, _mm_set1_epi8('\\')));
    return _mm_movemask_epi8(_mm_or_si128(controls, punctuation)) != 0;
}
#endif

// Callers supply a complete supported-length source and equally sized destination.
// Validate both overlapping input loads before storing either output group.
template<bool EscapeJson>
inline bool try_encode_short_ascii(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
    const auto length = source.size();
#if defined(LUMINPACK_CPP_AVX2)
    if (length > 16)
        return has_avx2() && try_encode_medium_ascii_avx2<EscapeJson>(source, destination);
#endif

    if (length < 8)
    {
        const auto tail = length - 4;
        std::int64_t first;
        std::int64_t last;
        std::memcpy(&first, source.data(), sizeof(first));
        std::memcpy(&last, source.data() + tail, sizeof(last));
        const auto chars = _mm_set_epi64x(last, first);
        if (!is_short_ascii(chars))
            return false;
        const auto bytes = _mm_packus_epi16(chars, chars);
        if (EscapeJson && short_ascii_needs_json_escape(bytes))
            return false;
        const auto packed = static_cast<std::uint64_t>(_mm_cvtsi128_si64(bytes));
        const auto head = static_cast<std::uint32_t>(packed);
        const auto end = static_cast<std::uint32_t>(packed >> 32);
        std::memcpy(destination.data(), &head, sizeof(head));
        std::memcpy(destination.data() + tail, &end, sizeof(end));
    }
    else
    {
        const auto tail = length - 8;
        __m128i first;
        __m128i last;
        std::memcpy(&first, source.data(), sizeof(first));
        std::memcpy(&last, source.data() + tail, sizeof(last));
        if (!is_short_ascii(_mm_or_si128(first, last)))
            return false;
        const auto bytes = _mm_packus_epi16(first, last);
        if (EscapeJson && short_ascii_needs_json_escape(bytes))
            return false;
        const auto head = _mm_cvtsi128_si64(bytes);
        const auto end = _mm_cvtsi128_si64(_mm_unpackhi_epi64(bytes, bytes));
        std::memcpy(destination.data(), &head, sizeof(head));
        std::memcpy(destination.data() + tail, &end, sizeof(end));
    }
    return true;
#elif defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
    const auto length = source.size();
    if (length < 8)
    {
        const auto tail = length - 4;
        uint16x4_t first;
        uint16x4_t last;
        std::memcpy(&first, source.data(), sizeof(first));
        std::memcpy(&last, source.data() + tail, sizeof(last));
        const auto chars = vcombine_u16(first, last);
        if (vmaxvq_u16(vandq_u16(chars, vdupq_n_u16(0xff80))) != 0)
            return false;
        const auto bytes = vmovn_u16(chars);
        if constexpr (EscapeJson)
        {
            const auto controls = vclt_u8(bytes, vdup_n_u8(0x20));
            const auto punctuation = vorr_u8(vceq_u8(bytes, vdup_n_u8('"')), vceq_u8(bytes, vdup_n_u8('\\')));
            if (vmaxv_u8(vorr_u8(controls, punctuation)) != 0)
                return false;
        }
        const auto words = vreinterpret_u32_u8(bytes);
        const auto head = vget_lane_u32(words, 0);
        const auto end = vget_lane_u32(words, 1);
        std::memcpy(destination.data(), &head, sizeof(head));
        std::memcpy(destination.data() + tail, &end, sizeof(end));
    }
    else
    {
        const auto tail = length - 8;
        uint16x8_t first;
        uint16x8_t last;
        std::memcpy(&first, source.data(), sizeof(first));
        std::memcpy(&last, source.data() + tail, sizeof(last));
        if (vmaxvq_u16(vandq_u16(vorrq_u16(first, last), vdupq_n_u16(0xff80))) != 0)
            return false;
        const auto bytes = vcombine_u8(vmovn_u16(first), vmovn_u16(last));
        if constexpr (EscapeJson)
        {
            const auto controls = vcltq_u8(bytes, vdupq_n_u8(0x20));
            const auto punctuation = vorrq_u8(vceqq_u8(bytes, vdupq_n_u8('"')), vceqq_u8(bytes, vdupq_n_u8('\\')));
            if (vmaxvq_u8(vorrq_u8(controls, punctuation)) != 0)
                return false;
        }
        const auto head = vget_low_u8(bytes);
        const auto end = vget_high_u8(bytes);
        std::memcpy(destination.data(), &head, sizeof(head));
        std::memcpy(destination.data() + tail, &end, sizeof(end));
    }
    return true;
#else
    return false;
#endif
}
}
