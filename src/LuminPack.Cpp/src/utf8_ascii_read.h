#pragma once

#include <luminpack/luminpack.h>

#include "cpu_isa.h"

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>

namespace luminpack::detail
{
inline constexpr auto utf8_length_header_size = 2 * sizeof(std::int32_t);

[[nodiscard]] inline std::span<const std::byte> peek_ascii_utf8_length(const reader& input) noexcept
{
    const auto source = input.unread();
    if (source.size() < utf8_length_header_size)
        return {};

    std::uint64_t header;
    std::memcpy(&header, source.data(), sizeof(header));
    const auto bytes = static_cast<std::uint32_t>(header);
    if (bytes == 0 || bytes != header >> 32 || bytes > source.size() - utf8_length_header_size)
        return {};
    return source.subspan(utf8_length_header_size, bytes);
}

#if defined(LUMINPACK_CPP_AVX2)
void widen_medium_ascii_avx2(char16_t* target, std::size_t length, __m128i first, __m128i last) noexcept;
bool try_widen_long_ascii_avx2(std::span<const std::byte> source, std::span<char16_t> destination) noexcept;
#endif

template<class Word>
[[nodiscard]] Word load_ascii_word(const std::byte* source) noexcept
{
    Word bytes;
    std::memcpy(&bytes, source, sizeof(bytes));
    return bytes;
}

inline void store_wide_ascii_128(char16_t* target, __m128i bytes) noexcept
{
    const auto zero = _mm_setzero_si128();
    const auto first = _mm_unpacklo_epi8(bytes, zero);
    const auto last = _mm_unpackhi_epi8(bytes, zero);
    std::memcpy(target, &first, sizeof(first));
    std::memcpy(target + 8, &last, sizeof(last));
}

// All source bytes are captured and checked before acquiring the result storage.
// The caller can therefore supply an overlapping UTF-16 window.
template<class Output>
[[nodiscard]] bool try_read_short_ascii(std::span<const std::byte> payload, Output&& output)
{
    const auto length = payload.size();
    const auto* bytes = payload.data();
    if (length <= 3)
    {
        const auto first = std::to_integer<unsigned>(bytes[0]);
        const auto last = std::to_integer<unsigned>(bytes[length - 1]);
        const auto middle = length == 3 ? std::to_integer<unsigned>(bytes[1]) : 0u;
        if ((first | last | middle) >= 0x80)
            return false;
        auto* target = output(length);
        target[0] = static_cast<char16_t>(first);
        target[length - 1] = static_cast<char16_t>(last);
        if (length == 3)
            target[1] = static_cast<char16_t>(middle);
        return true;
    }

    const auto zero = _mm_setzero_si128();
    if (length < 8)
    {
        const auto first = load_ascii_word<std::int32_t>(bytes);
        const auto last = load_ascii_word<std::int32_t>(bytes + length - 4);
        const auto lanes = _mm_set_epi32(0, 0, last, first);
        if (_mm_movemask_epi8(lanes) != 0)
            return false;

        const auto wide = _mm_unpacklo_epi8(lanes, zero);
        auto* target = output(length);
        const auto lower = _mm_cvtsi128_si64(wide);
        const auto upper = _mm_cvtsi128_si64(_mm_unpackhi_epi64(wide, wide));
        std::memcpy(target, &lower, sizeof(lower));
        std::memcpy(target + length - 4, &upper, sizeof(upper));
        return true;
    }

    if (length <= 16)
    {
        const auto first = load_ascii_word<std::int64_t>(bytes);
        const auto last = load_ascii_word<std::int64_t>(bytes + length - 8);
        const auto lanes = _mm_set_epi64x(last, first);
        if (_mm_movemask_epi8(lanes) != 0)
            return false;

        auto* target = output(length);
        const auto lower = _mm_unpacklo_epi8(lanes, zero);
        const auto upper = _mm_unpackhi_epi8(lanes, zero);
        std::memcpy(target, &lower, sizeof(lower));
        std::memcpy(target + length - 8, &upper, sizeof(upper));
        return true;
    }

    const auto first = load_ascii_word<__m128i>(bytes);
    const auto last = load_ascii_word<__m128i>(bytes + length - 16);
    if (_mm_movemask_epi8(_mm_or_si128(first, last)) != 0)
        return false;

    auto* target = output(length);
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
    {
        widen_medium_ascii_avx2(target, length, first, last);
        return true;
    }
#endif
    store_wide_ascii_128(target, first);
    store_wide_ascii_128(target + length - 16, last);
    return true;
}
}
#endif
