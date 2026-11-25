#pragma once

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>
#endif

namespace luminpack::detail
{
void write_utf16_fallback(writer& output, std::optional<std::u16string_view> value);

#if defined(LUMINPACK_CPP_INLINE_SSE2)
/// Narrows 8 to 16 UTF-16 code units. Validate both loads before writing an overlapping destination.
inline bool write_short_latin1_wide(std::byte* destination, std::u16string_view value) noexcept
{
    const auto last_offset = value.size() - 8;
    __m128i first;
    __m128i last;
    std::memcpy(&first, value.data(), sizeof(first));
    std::memcpy(&last, value.data() + last_offset, sizeof(last));
    const auto high = _mm_and_si128(_mm_or_si128(first, last), _mm_set1_epi16(-256));
    if (_mm_movemask_epi8(_mm_cmpeq_epi16(high, _mm_setzero_si128())) != 0xffff)
        return false;
    const auto packed = _mm_packus_epi16(first, last);
    const auto front = _mm_cvtsi128_si64(packed);
    const auto back = _mm_cvtsi128_si64(_mm_unpackhi_epi64(packed, packed));
    std::memcpy(destination, &front, sizeof(front));
    std::memcpy(destination + last_offset, &back, sizeof(back));
    return true;
}

/// For 17 to 32 units, snapshot the complete source before narrowing into a possibly overlapping record.
inline bool write_medium_latin1(std::byte* destination, std::u16string_view value) noexcept
{
    const auto tail = value.size() - 16;
    __m128i a, b, c, d;
    std::memcpy(&a, value.data(), sizeof(a));
    std::memcpy(&b, value.data() + 8, sizeof(b));
    std::memcpy(&c, value.data() + tail, sizeof(c));
    std::memcpy(&d, value.data() + tail + 8, sizeof(d));
    const auto lanes = _mm_or_si128(_mm_or_si128(a, b), _mm_or_si128(c, d));
    const auto high = _mm_and_si128(lanes, _mm_set1_epi16(-256));
    if (_mm_movemask_epi8(_mm_cmpeq_epi16(high, _mm_setzero_si128())) != 0xffff)
        return false;
    const auto first = _mm_packus_epi16(a, b);
    const auto last = _mm_packus_epi16(c, d);
    std::memcpy(destination, &first, sizeof(first));
    std::memcpy(destination + tail, &last, sizeof(last));
    return true;
}

/// Narrows 1 to 16 UTF-16 code units after the caller proves the raw output bound.
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
        return write_short_latin1_wide(destination.data(), value);
    return true;
}

/// Publishes one compact record only after validating the complete short payload.
inline bool try_write_short_utf16(writer& output, std::u16string_view value)
{
    constexpr auto header_size = sizeof(std::int32_t);
    const auto length = value.size();
    if (length == 0 || length > 32 || output.remaining() < header_size + length)
        return false;

    writer next = output;
    const auto record = next.reserve(header_size + length);
    const auto payload = record.subspan(header_size);
    if (!(length <= 16 ? write_short_latin1(payload, value) : write_medium_latin1(payload.data(), value)))
        return false;

    store_count(record, static_cast<std::int32_t>(length * 2 + 1));
    output = next;
    return true;
}

/// Holds the local cursor for a run of complete short UTF-16 Length records.
class utf16_length_window
{
public:
    explicit utf16_length_window(writer& output) : output_(output) { refresh(); }

    template<std::size_t Suffix = 0>
    bool try_write(std::u16string_view value, std::span<const std::byte, Suffix> suffix = {})
    {
        static_assert(Suffix <= sizeof(std::uint64_t));
        constexpr auto header_size = sizeof(std::int32_t);
        constexpr auto fixed_width = header_size + Suffix;
        const auto length = value.size();
        if (length < 8 || length > 32 || available_.size() < fixed_width + length)
            return false;

        auto* payload = available_.data() + header_size;
        if (!(length <= 16 ? write_short_latin1_wide(payload, value) : write_medium_latin1(payload, value)))
            return false;
        store_count(available_, static_cast<std::int32_t>(length * 2 + 1));
        if constexpr (Suffix != 0)
            std::memmove(payload + length, suffix.data(), Suffix);
        available_ = available_.subspan(fixed_width + length);
        return true;
    }

    void commit() { (void)output_.reserve(output_.remaining() - available_.size()); }

    void refresh()
    {
        writer window = output_;
        available_ = window.reserve(window.remaining());
    }

private:
    writer& output_;
    std::span<std::byte> available_;
};
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
