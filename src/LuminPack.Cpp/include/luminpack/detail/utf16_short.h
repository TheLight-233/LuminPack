#pragma once

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>
#endif

namespace luminpack::detail
{
void write_utf16_fallback(writer& output, std::optional<std::u16string_view> value);

#if defined(LUMINPACK_CPP_INLINE_SSE2)
/// Narrows 1–16 UTF-16 code units after the caller proves the raw output bound.
/// Every source lane is loaded and validated before either overlapping store.
inline bool write_short_latin1(std::span<std::byte> destination, std::u16string_view value) noexcept
{
    constexpr std::uint64_t high_bytes = 0xff00ff00ff00ff00ull;
    const auto length = value.size();
    if (length < 4)
    {
        // A C++ view can end at the last code unit, so load only the units supplied.
        std::uint64_t head = 0;
        std::memcpy(&head, value.data(), length * sizeof(char16_t));
        if ((head & high_bytes) != 0)
            return false;
        head |= head >> 8;
        head &= 0x0000ffff0000ffffull;
        const auto packed = static_cast<std::uint32_t>(head | (head >> 16));
        std::memcpy(destination.data(), &packed, length);
    }
    else if (length < 8)
    {
        std::uint64_t head;
        std::uint64_t tail;
        std::memcpy(&head, value.data(), sizeof(head));
        std::memcpy(&tail, value.data() + length - 4, sizeof(tail));
        if (((head | tail) & high_bytes) != 0)
            return false;
        const auto pairs = _mm_set_epi64x(static_cast<std::int64_t>(tail), static_cast<std::int64_t>(head));
        const auto packed = static_cast<std::uint64_t>(_mm_cvtsi128_si64(_mm_packus_epi16(pairs, _mm_setzero_si128())));
        const auto front = static_cast<std::uint32_t>(packed);
        const auto back = static_cast<std::uint32_t>(packed >> 32);
        std::memcpy(destination.data(), &front, sizeof(front));
        std::memcpy(destination.data() + length - 4, &back, sizeof(back));
    }
    else
    {
        __m128i first;
        __m128i last;
        std::memcpy(&first, value.data(), sizeof(first));
        std::memcpy(&last, value.data() + length - 8, sizeof(last));
        const auto high = _mm_and_si128(_mm_or_si128(first, last), _mm_set1_epi16(-256));
        if (_mm_movemask_epi8(_mm_cmpeq_epi16(high, _mm_setzero_si128())) != 0xffff)
            return false;
        const auto packed = _mm_packus_epi16(first, last);
        const auto front = _mm_cvtsi128_si64(packed);
        const auto back = _mm_cvtsi128_si64(_mm_unpackhi_epi64(packed, packed));
        std::memcpy(destination.data(), &front, sizeof(front));
        std::memcpy(destination.data() + length - 8, &back, sizeof(back));
    }
    return true;
}

/// Publishes one compact record only after validating the complete short payload.
inline bool try_write_short_utf16(writer& output, std::u16string_view value)
{
    constexpr auto header_size = sizeof(std::int32_t);
    const auto length = value.size();
    if (length == 0 || length > 16 || output.remaining() < header_size + length * sizeof(char16_t))
        return false;

    writer next = output;
    const auto record = next.reserve(header_size + length);
    if (!write_short_latin1(record.subspan(header_size), value))
        return false;

    store_count(record, static_cast<std::int32_t>(length * 2 + 1));
    output = next;
    return true;
}
#endif

/// Selects the C# UTF-16 Length short record path from the fixed profile.
inline void write_utf16_length(writer& output, std::optional<std::u16string_view> value)
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
    if (value && try_write_short_utf16(output, *value))
        return;
#endif
    write_utf16_fallback(output, value);
}
}
