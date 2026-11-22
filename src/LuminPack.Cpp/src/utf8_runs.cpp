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

std::size_t encode_two_byte_blocks(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
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

#if defined(LUMINPACK_CPP_SSSE3)
bool is_three_byte_bmp(char16_t unit) noexcept
{
    return unit >= 0x800 && (unit < 0xd800 || unit > 0xdfff);
}

bool is_bmp_sample(std::span<const char16_t, 16> sample) noexcept
{
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
        return is_bmp_sample_avx2(sample);
#endif
    return is_bmp_sample_ssse3(sample);
}

std::size_t encode_bmp_blocks(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t consumed = 0;
#if defined(LUMINPACK_CPP_AVX512BW)
    if (has_avx512bw())
        consumed = encode_bmp_avx512(source, destination);
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
        consumed += encode_bmp_avx2(source.subspan(consumed), destination.subspan(consumed * 3));
#endif
    return consumed + encode_bmp_ssse3(source.subspan(consumed), destination.subspan(consumed * 3));
}

utf8_prefix encode_mixed_latin1_prefix(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    utf8_prefix prefix{};
#if defined(LUMINPACK_CPP_AVX512BW)
    if (has_avx512bw())
        prefix = encode_mixed_latin1_avx512(source, destination);
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
    {
        const auto next = encode_mixed_latin1_avx2(source.subspan(prefix.consumed), destination.subspan(prefix.written));
        prefix.consumed += next.consumed;
        prefix.written += next.written;
    }
#endif
    const auto next = encode_mixed_latin1_ssse3(source.subspan(prefix.consumed), destination.subspan(prefix.written));
    prefix.consumed += next.consumed;
    prefix.written += next.written;

    while (prefix.consumed < source.size())
    {
        const auto unit = source[prefix.consumed];
        if (unit > 0xff)
            break;
        const std::size_t width = unit < 0x80 ? 1 : 2;
        if (destination.size() - prefix.written < width)
            break;
        destination[prefix.written++] = static_cast<std::byte>(width == 1 ? unit : 0xc0 | (unit >> 6));
        if (width == 2)
            destination[prefix.written++] = static_cast<std::byte>(0x80 | (unit & 63));
        ++prefix.consumed;
    }
    return prefix;
}
#endif
}

utf8_prefix encode_non_ascii_utf8_prefix(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    if (source.size() < 32 || !has_ssse3())
        return {};

    utf8_prefix prefix{};
    const auto lead = source.front();
    if (lead >= 0x80 && lead < 0x800)
    {
        if (destination.size() < 24 || !is_two_byte_sample(source.first<16>()))
        {
#if defined(LUMINPACK_CPP_SSSE3)
            // Core admits mixed Latin-1 only after the homogeneous probe declines.
            if (lead <= 0xff)
                return encode_mixed_latin1_prefix(source, destination);
#endif
            return {};
        }
        prefix.consumed = encode_two_byte_blocks(source, destination);
        prefix.written = prefix.consumed * 2;
    }
#if defined(LUMINPACK_CPP_SSSE3)
    else
    {
        if (destination.size() < 24 || !is_three_byte_bmp(lead) || !is_bmp_sample(source.first<16>()))
            return {};
        prefix.consumed = encode_bmp_blocks(source, destination);
        prefix.written = prefix.consumed * 3;
    }

    // Core re-enters only after a complete run, for one ASCII delimiter before a BMP run.
    while (prefix.consumed != 0 && source.size() - prefix.consumed >= 33 && destination.size() - prefix.written >= 25 &&
        source[prefix.consumed] < 0x80 && is_three_byte_bmp(source[prefix.consumed + 1]))
    {
        destination[prefix.written++] = static_cast<std::byte>(source[prefix.consumed++]);
        const auto consumed = encode_bmp_blocks(source.subspan(prefix.consumed), destination.subspan(prefix.written));
        if (consumed == 0)
            break;
        prefix.consumed += consumed;
        prefix.written += consumed * 3;
    }
#endif
    return prefix;
}
}
#endif
