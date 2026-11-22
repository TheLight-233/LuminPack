#include "utf8_runs.h"
#include "short_ascii.h"

#include <algorithm>
#include <cstdint>
#include <cstring>

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include "cpu_isa.h"
#include "latin1.h"

#include <smmintrin.h>
#endif

namespace luminpack::detail
{
namespace
{
constexpr std::uint32_t ascii_pair_mask = 0xff80ff80u;

template<class Word>
Word load_utf16(const char16_t* source) noexcept
{
    Word value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

std::size_t narrow_ascii_four(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto limit = std::min(source.size(), destination.size());
    std::size_t count = 0;
    for (; limit - count >= 4; count += 4)
    {
        const auto first = load_utf16<std::uint32_t>(source.data() + count);
        const auto second = load_utf16<std::uint32_t>(source.data() + count + 2);
        if (((first | second) & ascii_pair_mask) != 0)
            break;
        const auto head = static_cast<std::uint16_t>(first | (first >> 8));
        const auto tail = static_cast<std::uint16_t>(second | (second >> 8));
        std::memcpy(destination.data() + count, &head, sizeof(head));
        std::memcpy(destination.data() + count + 2, &tail, sizeof(tail));
    }
    return count;
}

#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
#if defined(LUMINPACK_CPP_INLINE_SSE2) && (defined(__GNUC__) || defined(__clang__))
[[gnu::target("sse4.1")]]
#elif defined(_MSC_VER)
__forceinline
#endif
std::size_t narrow_ascii_eight(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto limit = std::min(source.size(), destination.size());
    std::size_t count = 0;
    for (; limit - count >= 8; count += 8)
    {
#if defined(LUMINPACK_CPP_INLINE_SSE2)
        const auto units = load_utf16<__m128i>(source.data() + count);
        if (_mm_testz_si128(units, _mm_set1_epi16(-128)) == 0)
            break;
        const auto packed = _mm_cvtsi128_si64(_mm_packus_epi16(units, units));
#else
        const auto units = load_utf16<uint16x8_t>(source.data() + count);
        if (vmaxvq_u16(vandq_u16(units, vdupq_n_u16(0xff80))) != 0)
            break;
        const auto packed = vqmovun_s16(vreinterpretq_s16_u16(units));
#endif
        std::memcpy(destination.data() + count, &packed, 8);
    }
    return count;
}
#endif

template<std::size_t Bytes, class Word>
void store_tail_prefix(std::byte* output, Word packed, std::size_t& consumed, std::size_t& written, std::size_t units) noexcept
{
    std::memcpy(output, &packed, Bytes);
    consumed += units;
    written += Bytes;
}

constexpr bool is_surrogate(std::uint32_t value) noexcept
{
    return value >= 0xd800 && value <= 0xdfff;
}

constexpr std::uint32_t utf8_two_bytes(std::uint32_t value) noexcept
{
    return 0x80c0u | (value >> 6) | ((value & 0x3f) << 8);
}

constexpr std::uint32_t utf8_three_bytes(std::uint32_t value) noexcept
{
    return 0x8080e0u | (value >> 12) | ((value & 0xfc0) << 2) | ((value & 0x3f) << 16);
}

constexpr std::uint32_t utf8_four_bytes(std::uint32_t value) noexcept
{
    return 0x808080f0u | (value >> 18) | ((value & 0x3f000) >> 4) |
        ((value & 0xfc0) << 10) | ((value & 0x3f) << 24);
}
}

utf8_prefix encode_utf8_tail(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t consumed = 0;
    std::size_t written = 0;
#if defined(LUMINPACK_CPP_INLINE_SSE2)
    const bool use_eight = has_sse41();
#elif defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
    constexpr bool use_eight = true;
#endif

#if defined(LUMINPACK_CPP_AVX2)
    constexpr std::uint64_t ascii_quad_mask = 0xff80ff80ff80ff80ull;
    const auto limit = std::min(source.size(), destination.size());
    if (limit >= 64 && (load_utf16<std::uint64_t>(source.data()) & ascii_quad_mask) == 0)
    {
#if defined(LUMINPACK_CPP_AVX512BW)
        if (limit >= 128 && has_avx512bw())
            consumed = encode_ascii_prefix_avx512(source, destination);
        else
#endif
        if (has_avx2())
            consumed = encode_ascii_prefix_avx2(source, destination);
        written = consumed;
    }
#endif

    // The platform UTF-8 encoder groups adjacent code units of equal width before handling a mixed pair.
    while (source.size() - consumed >= 2)
    {
        const auto chars = load_utf16<std::uint32_t>(source.data() + consumed);
        const auto first = chars & 0xffff;
        const auto second = chars >> 16;
        const auto room = destination.size() - written;
        if (room == 0)
            return {consumed, written};
        auto* output = destination.data() + written;

        if ((chars & ascii_pair_mask) == 0 && room >= 2)
        {
            const auto packed = static_cast<std::uint16_t>(chars | (chars >> 8));
            std::memcpy(output, &packed, sizeof(packed));
            consumed += 2;
            written += 2;

            std::size_t narrowed = 0;
#if defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_SHORT_ASCII_NEON)
            if (use_eight)
                narrowed = narrow_ascii_eight(source.subspan(consumed), destination.subspan(written));
#endif
            narrowed += narrow_ascii_four(source.subspan(consumed + narrowed),
                destination.subspan(written + narrowed));
            consumed += narrowed;
            written += narrowed;
            continue;
        }

        if (first < 0x80)
        {
            store_tail_prefix<1>(output, first, consumed, written, 1);
            continue;
        }

        if (first < 0x800)
        {
            if (room < 2)
                return {consumed, written};
            if (second >= 0x80 && second < 0x800 && room >= 4)
            {
                const auto packed = 0x80c080c0u | ((chars & 0x07c007c0u) >> 6) | ((chars & 0x003f003fu) << 8);
                store_tail_prefix<4>(output, packed, consumed, written, 2);
            }
            else if (second < 0x80 && room >= 3)
                store_tail_prefix<3>(output, utf8_two_bytes(first) | (second << 16), consumed, written, 2);
            else
                store_tail_prefix<2>(output, utf8_two_bytes(first), consumed, written, 1);
            continue;
        }

        if (!is_surrogate(first))
        {
            if (room < 3)
                return {consumed, written};
            if (second >= 0x800 && !is_surrogate(second) && room >= 6)
            {
                const auto packed = utf8_three_bytes(first) | (std::uint64_t{utf8_three_bytes(second)} << 24);
                store_tail_prefix<6>(output, packed, consumed, written, 2);
            }
            else if (second < 0x80 && room >= 4)
                store_tail_prefix<4>(output, utf8_three_bytes(first) | (second << 24), consumed, written, 2);
            else
                store_tail_prefix<3>(output, utf8_three_bytes(first), consumed, written, 1);
            continue;
        }

        if (first > 0xdbff || second < 0xdc00 || second > 0xdfff || room < 4)
            return {consumed, written};
        const auto scalar = 0x10000u + ((first - 0xd800u) << 10) + (second - 0xdc00u);
        store_tail_prefix<4>(output, utf8_four_bytes(scalar), consumed, written, 2);
    }

    if (consumed != source.size())
    {
        const auto last = static_cast<std::uint32_t>(source.back());
        if (!is_surrogate(last))
        {
            const auto bytes = last < 0x80 ? 1u : last < 0x800 ? 2u : 3u;
            if (bytes <= destination.size() - written)
            {
                const auto packed = bytes == 1 ? last : bytes == 2 ? utf8_two_bytes(last) : utf8_three_bytes(last);
                std::memcpy(destination.data() + written, &packed, bytes);
                ++consumed;
                written += bytes;
            }
        }
    }
    return {consumed, written};
}
}

#if defined(LUMINPACK_CPP_INLINE_SSE2)
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
    if (prefix.consumed != 0 && source.size() - prefix.consumed >= 64)
    {
        const auto tail = encode_utf8_tail(source.subspan(prefix.consumed), destination.subspan(prefix.written));
        prefix.consumed += tail.consumed;
        prefix.written += tail.written;
    }
    return prefix;
}
}
#endif
