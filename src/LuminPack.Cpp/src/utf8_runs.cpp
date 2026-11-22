#include "utf8_runs.h"

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include "cpu_isa.h"

#include <cstring>
#include <emmintrin.h>

namespace luminpack::detail
{
namespace
{
__m128i load128(const char16_t* source) noexcept
{
    __m128i chars;
    std::memcpy(&chars, source, sizeof(chars));
    return chars;
}

__m128i valid_two_byte(__m128i chars) noexcept
{
    return _mm_and_si128(_mm_cmpgt_epi16(chars, _mm_set1_epi16(127)), _mm_cmpgt_epi16(_mm_set1_epi16(2048), chars));
}

bool is_two_byte_sample(std::span<const char16_t, 16> sample) noexcept
{
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
        return is_two_byte_sample_avx2(sample);
#endif
    return _mm_movemask_epi8(valid_two_byte(load128(sample.data()))) == 0xffff &&
        _mm_movemask_epi8(valid_two_byte(load128(sample.data() + 8))) == 0xffff;
}

std::size_t encode_two_byte_128(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t index = 0;
    for (; source.size() - index >= 8 && destination.size() - index * 2 >= 16; index += 8)
    {
        const auto chars = load128(source.data() + index);
        if (_mm_movemask_epi8(valid_two_byte(chars)) != 0xffff)
            break;
        const auto low = _mm_or_si128(_mm_srli_epi16(chars, 6), _mm_set1_epi16(0xc0));
        const auto high = _mm_slli_epi16(_mm_or_si128(_mm_and_si128(chars, _mm_set1_epi16(63)), _mm_set1_epi16(0x80)), 8);
        const auto pairs = _mm_or_si128(low, high);
        std::memcpy(destination.data() + index * 2, &pairs, sizeof(pairs));
    }
    return index;
}
}

std::size_t encode_two_byte_prefix(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    if (source.size() < 32 || destination.size() < 24 || !has_ssse3() || !is_two_byte_sample(source.first<16>()))
        return 0;

    std::size_t consumed = 0;
#if defined(LUMINPACK_CPP_AVX512BW)
    if (has_avx512bw())
        consumed = encode_two_byte_avx512(source, destination);
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
        consumed += encode_two_byte_avx2(source.subspan(consumed), destination.subspan(consumed * 2));
#endif
    return consumed + encode_two_byte_128(source.subspan(consumed), destination.subspan(consumed * 2));
}
}
#endif
