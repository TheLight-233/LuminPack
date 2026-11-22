#pragma once

#include "short_ascii.h"

#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <span>

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
struct utf8_width_prefix
{
    std::size_t code_units;
    std::uint64_t additional_bytes;
};

// Count the extra UTF-8 bytes over one byte per UTF-16 code unit, as in Core's validator.
// Only publish complete valid vectors. The scalar caller handles any unresolved remainder.
inline utf8_width_prefix measure_utf8_vectors(std::span<const char16_t> source) noexcept
{
    utf8_width_prefix result{};
    while (source.size() - result.code_units >= 8)
    {
        auto consumed = std::size_t{8};
#if defined(LUMINPACK_CPP_INLINE_SSE2)
        __m128i units;
        std::memcpy(&units, source.data() + result.code_units, sizeof(units));
        const auto two_byte = _mm_sub_epi16(units, _mm_subs_epu16(units, _mm_set1_epi16(0x80)));
        const auto three_byte = _mm_adds_epu16(units, _mm_set1_epi16(0x7800));
        const auto width_bits = static_cast<unsigned>(_mm_movemask_epi8(_mm_or_si128(two_byte, three_byte)));
        auto additional = std::popcount(width_bits);

        const auto biased = _mm_add_epi16(units, _mm_set1_epi16(-0x6000));
        const auto ordinary = static_cast<unsigned>(_mm_movemask_epi8(_mm_cmpgt_epi16(_mm_set1_epi16(0x7800), biased)));
        if (ordinary != 0xffff)
        {
            // The low byte of each shifted surrogate lane marks a low surrogate.
            const auto markers = static_cast<unsigned>(_mm_movemask_epi8(_mm_srli_epi16(units, 3)));
            const auto surrogates = ordinary ^ 0xffff;
            const auto low = surrogates & markers;
            const auto expected_low = (surrogates & (markers ^ 0x5555u)) << 2;
            if ((expected_low & 0xffff) != low)
                break;

            additional -= 2 * std::popcount(low);
            if (expected_low > 0xffff)
            {
                --consumed;
                additional -= 2;
            }
        }
#elif defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
        uint16x8_t units;
        std::memcpy(&units, source.data() + result.code_units, sizeof(units));
        const auto two_byte = vcgeq_u16(units, vdupq_n_u16(0x80));
        const auto three_byte = vcgeq_u16(units, vdupq_n_u16(0x800));
        const auto counts = vsubq_u16(vdupq_n_u16(0), vaddq_u16(two_byte, three_byte));
        auto additional = static_cast<int>(vaddvq_u16(counts));

        const auto biased = vsubq_u16(units, vdupq_n_u16(0xd800));
        const auto surrogates = vcltq_u16(biased, vdupq_n_u16(0x800));
        if (vmaxvq_u16(surrogates) != 0)
        {
            const auto high = vcltq_u16(biased, vdupq_n_u16(0x400));
            const auto low = vbicq_u16(surrogates, high);
            const auto expected_low = vextq_u16(vdupq_n_u16(0), high, 7);
            if (vmaxvq_u16(veorq_u16(expected_low, low)) != 0)
                break;

            additional -= 2 * vaddvq_u16(vshrq_n_u16(low, 15));
            if (vgetq_lane_u16(high, 7) != 0)
            {
                --consumed;
                additional -= 2;
            }
        }
#endif
        result.code_units += consumed;
        result.additional_bytes += static_cast<unsigned>(additional);
    }
    return result;
}
#endif
}
