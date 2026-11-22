#include "utf8_runs.h"

#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
__m256i load256(const char16_t* source) noexcept
{
    __m256i chars;
    std::memcpy(&chars, source, sizeof(chars));
    return chars;
}

__m256i valid_two_byte(__m256i chars) noexcept
{
    return _mm256_and_si256(_mm256_cmpgt_epi16(chars, _mm256_set1_epi16(127)), _mm256_cmpgt_epi16(_mm256_set1_epi16(2048), chars));
}

void store_pairs(std::byte* destination, __m256i chars) noexcept
{
    const auto low = _mm256_or_si256(_mm256_srli_epi16(chars, 6), _mm256_set1_epi16(0xc0));
    const auto high = _mm256_slli_epi16(_mm256_or_si256(_mm256_and_si256(chars, _mm256_set1_epi16(63)), _mm256_set1_epi16(0x80)), 8);
    const auto pairs = _mm256_or_si256(low, high);
    std::memcpy(destination, &pairs, sizeof(pairs));
}
}

bool is_two_byte_sample_avx2(std::span<const char16_t, 16> source) noexcept
{
    return _mm256_movemask_epi8(valid_two_byte(load256(source.data()))) == -1;
}

std::size_t encode_two_byte_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t index = 0;
    for (; source.size() - index >= 32 && destination.size() - index * 2 >= 64; index += 32)
    {
        const auto first = load256(source.data() + index);
        const auto second = load256(source.data() + index + 16);
        if (_mm256_movemask_epi8(_mm256_and_si256(valid_two_byte(first), valid_two_byte(second))) != -1)
            break;
        store_pairs(destination.data() + index * 2, first);
        store_pairs(destination.data() + index * 2 + 32, second);
    }
    for (; source.size() - index >= 16 && destination.size() - index * 2 >= 32; index += 16)
    {
        const auto chars = load256(source.data() + index);
        if (_mm256_movemask_epi8(valid_two_byte(chars)) != -1)
            break;
        store_pairs(destination.data() + index * 2, chars);
    }
    return index;
}
}
