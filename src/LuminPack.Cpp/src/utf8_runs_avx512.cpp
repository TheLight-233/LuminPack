#include "utf8_runs.h"

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
}
