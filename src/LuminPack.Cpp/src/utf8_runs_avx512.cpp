#include "utf8_runs.h"

#if defined(LUMINPACK_CPP_SSSE3)
#include "utf8_bmp_shuffle.h"
#include "utf8_latin1_shuffle.h"
#endif

#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
std::size_t encode_two_byte_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t index = 0;
    for (; source.size() - index >= 32 && destination.size() - index * 2 >= 64; index += 32)
    {
        __m512i chars;
        std::memcpy(&chars, source.data() + index, sizeof(chars));
        const auto gt_min = _mm512_cmpgt_epu16_mask(chars, _mm512_set1_epi16(127));
        const auto lt_max = _mm512_cmplt_epu16_mask(chars, _mm512_set1_epi16(2048));
        if ((gt_min & lt_max) != 0xffffffffu)
            break;
        const auto low = _mm512_or_si512(_mm512_srli_epi16(chars, 6), _mm512_set1_epi16(0xc0));
        const auto high = _mm512_slli_epi16(_mm512_or_si512(_mm512_and_si512(chars, _mm512_set1_epi16(63)), _mm512_set1_epi16(0x80)), 8);
        const auto pairs = _mm512_or_si512(low, high);
        std::memcpy(destination.data() + index * 2, &pairs, sizeof(pairs));
    }
    return index;
}

#if defined(LUMINPACK_CPP_SSSE3)
std::size_t encode_bmp_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t index = 0;
    for (; source.size() - index >= 32 && destination.size() - index * 3 >= 96; index += 32)
    {
        __m512i chars;
        std::memcpy(&chars, source.data() + index, sizeof(chars));
        const auto top = _mm512_and_si512(chars, _mm512_set1_epi16(static_cast<short>(0xf800)));
        if ((_mm512_cmpeq_epi16_mask(top, _mm512_setzero_si512()) |
            _mm512_cmpeq_epi16_mask(top, _mm512_set1_epi16(static_cast<short>(0xd800)))) != 0)
            break;
        const auto lead = _mm512_or_si512(_mm512_srli_epi16(chars, 12), _mm512_set1_epi16(0xe0));
        const auto middle = _mm512_or_si512(_mm512_and_si512(_mm512_srli_epi16(chars, 6), _mm512_set1_epi16(63)), _mm512_set1_epi16(0x80));
        const auto last = _mm512_or_si512(_mm512_and_si512(chars, _mm512_set1_epi16(63)), _mm512_set1_epi16(0x80));
        const auto a = _mm512_packus_epi16(lead, lead);
        const auto b = _mm512_packus_epi16(middle, middle);
        const auto c = _mm512_packus_epi16(last, last);
        const auto first = _mm512_or_si512(_mm512_or_si512(_mm512_shuffle_epi8(a, bmp::load_shuffle<__m512i, bmp::first_lead>()),
            _mm512_shuffle_epi8(b, bmp::load_shuffle<__m512i, bmp::first_middle>())), _mm512_shuffle_epi8(c, bmp::load_shuffle<__m512i, bmp::first_last>()));
        const auto second = _mm512_or_si512(_mm512_or_si512(_mm512_shuffle_epi8(a, bmp::load_shuffle<__m512i, bmp::second_lead>()),
            _mm512_shuffle_epi8(b, bmp::load_shuffle<__m512i, bmp::second_middle>())), _mm512_shuffle_epi8(c, bmp::load_shuffle<__m512i, bmp::second_last>()));
        auto* output = destination.data() + index * 3;
        bmp::store_lane(output, _mm512_castsi512_si128(first), _mm512_castsi512_si128(second));
        bmp::store_lane(output + 24, _mm512_extracti32x4_epi32(first, 1), _mm512_extracti32x4_epi32(second, 1));
        bmp::store_lane(output + 48, _mm512_extracti32x4_epi32(first, 2), _mm512_extracti32x4_epi32(second, 2));
        bmp::store_lane(output + 72, _mm512_extracti32x4_epi32(first, 3), _mm512_extracti32x4_epi32(second, 3));
    }
    return index;
}

utf8_prefix encode_mixed_latin1_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    utf8_prefix prefix{};
    while (source.size() - prefix.consumed >= 32 && destination.size() - prefix.written >= 64)
    {
        __m512i chars;
        std::memcpy(&chars, source.data() + prefix.consumed, sizeof(chars));
        if (_mm512_test_epi16_mask(chars, _mm512_set1_epi16(static_cast<short>(0xff00))) != 0)
            break;

        const auto mask = _mm512_test_epi16_mask(chars, _mm512_set1_epi16(0x80));
        const auto mask0 = static_cast<std::uint8_t>(mask);
        const auto mask1 = static_cast<std::uint8_t>(mask >> 8);
        const auto mask2 = static_cast<std::uint8_t>(mask >> 16);
        const auto mask3 = static_cast<std::uint8_t>(mask >> 24);
        const auto lead = _mm512_or_si512(_mm512_srli_epi16(chars, 6), _mm512_set1_epi16(0xc0));
        const auto trail = _mm512_slli_epi16(_mm512_or_si512(_mm512_and_si512(chars, _mm512_set1_epi16(63)), _mm512_set1_epi16(0x80)), 8);
        const auto pairs = _mm512_mask_blend_epi16(mask, chars, _mm512_or_si512(lead, trail));
        auto controls = _mm512_castsi128_si512(mixed_latin1::load_shuffle(mask0));
        controls = _mm512_inserti32x4(controls, mixed_latin1::load_shuffle(mask1), 1);
        controls = _mm512_inserti32x4(controls, mixed_latin1::load_shuffle(mask2), 2);
        controls = _mm512_inserti32x4(controls, mixed_latin1::load_shuffle(mask3), 3);
        const auto packed = _mm512_shuffle_epi8(pairs, controls);
        prefix.written += mixed_latin1::store_lane(destination.data() + prefix.written, _mm512_castsi512_si128(packed), mask0);
        prefix.written += mixed_latin1::store_lane(destination.data() + prefix.written, _mm512_extracti32x4_epi32(packed, 1), mask1);
        prefix.written += mixed_latin1::store_lane(destination.data() + prefix.written, _mm512_extracti32x4_epi32(packed, 2), mask2);
        prefix.written += mixed_latin1::store_lane(destination.data() + prefix.written, _mm512_extracti32x4_epi32(packed, 3), mask3);
        prefix.consumed += 32;
    }
    return prefix;
}
#endif
}
