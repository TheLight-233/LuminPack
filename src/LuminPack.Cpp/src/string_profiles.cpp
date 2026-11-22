#include <luminpack/luminpack.h>

#include "cpu_isa.h"
#include "latin1.h"
#include "unicode.h"
#include "utf8_runs.h"

#include <algorithm>

#if defined(LUMINPACK_CPP_INLINE_SSE2)
#include <emmintrin.h>
#endif

namespace luminpack
{
namespace
{
struct text_record
{
    std::span<const std::byte> payload;
    std::size_t length;
    bool utf8;
    bool latin1;
    bool replace;
    bool ascii;
};

bool uses_utf8(string_profile profile) noexcept
{
    return profile == string_profile::utf8_length || profile == string_profile::utf8_varint ||
        profile == string_profile::utf8_token;
}

bool uses_token(string_profile profile) noexcept
{
    return profile == string_profile::utf16_token || profile == string_profile::utf8_token;
}

bool uses_varint(string_profile profile) noexcept
{
    return profile == string_profile::utf16_varint || profile == string_profile::utf8_varint;
}

std::int32_t read_varint_length(reader& input)
{
    std::uint32_t value = 0;
    for (unsigned shift = 0; shift <= 28; shift += 7)
    {
        const auto next = input.read<std::uint8_t>();
        if (shift == 28 && next > 8)
            break;
        value |= static_cast<std::uint32_t>(next & 0x7f) << shift;
        if (next < 128)
        {
            if ((shift != 0 && next == 0) || value > 0x80000000u)
                break;
            return value == 0 ? -1 : static_cast<std::int32_t>(value - 1);
        }
    }
    throw wire_error("Invalid LuminPack variable string length.");
}

void write_length(writer& output, std::int32_t header, bool variable)
{
    if (!variable)
    {
        output.write(header);
        return;
    }
    auto remaining = static_cast<std::uint32_t>(header) + 1u;
    while (remaining >= 128)
    {
        output.write(static_cast<std::uint8_t>((remaining & 0x7fu) | 0x80u));
        remaining >>= 7;
    }
    output.write(static_cast<std::uint8_t>(remaining));
}

using detail::high_surrogate;
using detail::low_surrogate;
using detail::read_utf16_scalar;
using detail::read_utf8_scalar;

std::size_t utf8_bytes(std::u16string_view source)
{
    std::size_t bytes = 0;
    std::size_t index = 0;
    while (index < source.size())
    {
        const auto scalar = read_utf16_scalar(source, index);
        const auto width = scalar < 0x80 ? 1u : scalar < 0x800 ? 2u : scalar < 0x10000 ? 3u : 4u;
        if (bytes > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()) - width)
            throw std::length_error("LuminPack UTF-8 record exceeds the wire length.");
        bytes += width;
    }
    return bytes;
}

std::span<const std::byte> read_token_payload(reader& input, bool utf8)
{
    const auto remaining = input.unread();
    const std::size_t unit = utf8 ? 1u : 2u;
    for (std::size_t offset = 0; remaining.size() - offset >= unit; offset += unit)
    {
        if (remaining[offset] == std::byte{} && (utf8 || remaining[offset + 1] == std::byte{}))
            return input.take(offset + unit).first(offset);
    }
    throw wire_error("Missing LuminPack string token terminator.");
}

std::optional<text_record> read_text_record(reader& input, string_profile profile, bool strict_skip = false)
{
    const auto utf8 = uses_utf8(profile);
    const auto token = uses_token(profile);
    const auto variable = uses_varint(profile);
    bool latin1 = false;
    auto character_header = std::int32_t{-1};
    std::span<const std::byte> payload;
    if (token)
        payload = read_token_payload(input, utf8);
    else
    {
        const auto header = variable ? read_varint_length(input) : input.read<std::int32_t>();
        if (utf8)
            character_header = variable ? read_varint_length(input) : input.read<std::int32_t>();
        if (header < -1)
            throw wire_error("Invalid LuminPack string length.");
        if (utf8 && variable && (header == -1 ? character_header != -1 :
            character_header < 0 || character_header > header || (header != 0 && character_header == 0)))
            throw wire_error("Invalid LuminPack UTF-8 character count.");
        if (header == -1)
            return std::nullopt;
        latin1 = !utf8 && (header & 1) != 0;
        payload = input.take(static_cast<std::size_t>(latin1 ? header >> 1 : header));
    }

    if (!utf8)
    {
        const auto length = latin1 ? payload.size() : payload.size() / sizeof(char16_t);
        input.check_string_length(length);
        return text_record{payload, length, false, latin1, token, false};
    }
    if (payload.empty())
        return text_record{payload, 0, true, false, false, true};

    const bool replace = character_header <= 0 && !strict_skip;
    const auto expected = character_header > 0 ? static_cast<std::size_t>(character_header) : 0u;
    if (expected > payload.size())
        throw wire_error("Invalid LuminPack UTF-8 character count.");
    if (expected != 0)
        input.check_string_length(expected);
    const auto ascii = std::ranges::all_of(payload, [](std::byte value) { return value < std::byte{0x80}; });
    std::size_t length = payload.size();
    if (!ascii)
    {
        length = 0;
        std::size_t offset = 0;
        while (offset < payload.size())
        {
            const auto value = read_utf8_scalar(payload, offset, replace);
            length += value <= 0xffff ? 1 : 2;
        }
    }
    if (expected != 0 && expected != length)
        throw wire_error("Invalid LuminPack UTF-8 character count.");
    input.check_string_length(length);
    return text_record{payload, length, true, false, replace, ascii};
}

void replace_unpaired(std::span<char16_t> destination) noexcept
{
    for (std::size_t index = 0; index < destination.size(); ++index)
    {
        const auto character = destination[index];
        if (high_surrogate(character))
        {
            if (index + 1 < destination.size() && low_surrogate(destination[index + 1]))
                ++index;
            else
                destination[index] = 0xfffd;
        }
        else if (low_surrogate(character))
            destination[index] = 0xfffd;
    }
}

void decode_text(std::span<char16_t> destination, text_record record)
{
    if (destination.empty())
        return;
    if (!record.utf8 && !record.latin1)
    {
        std::memmove(destination.data(), record.payload.data(), record.payload.size());
        if (record.replace)
            replace_unpaired(destination);
        return;
    }

    std::vector<std::byte> scratch;
    if (detail::overlaps(record.payload, std::as_bytes(destination)))
    {
        scratch.assign(record.payload.begin(), record.payload.end());
        record.payload = scratch;
    }
    if (record.latin1 || record.ascii)
    {
        detail::widen_latin1(record.payload, destination);
        return;
    }

    std::size_t offset = 0;
    std::size_t written = 0;
    while (offset < record.payload.size())
    {
        auto code = read_utf8_scalar(record.payload, offset, record.replace);
        if (code > 0xffff)
        {
            code -= 0x10000;
            destination[written++] = static_cast<char16_t>(0xd800 | (code >> 10));
            code = 0xdc00 | (code & 0x3ff);
        }
        destination[written++] = static_cast<char16_t>(code);
    }
}

#if defined(LUMINPACK_CPP_INLINE_SSE2)
bool is_ascii(__m128i chars) noexcept
{
    const auto high = _mm_and_si128(chars, _mm_set1_epi16(-128));
    return _mm_movemask_epi8(_mm_cmpeq_epi16(high, _mm_setzero_si128())) == 0xffff;
}

bool try_encode_short_ascii(std::span<const char16_t> text, std::span<std::byte> payload) noexcept
{
    const auto length = text.size();
#if defined(LUMINPACK_CPP_AVX2)
    if (length > 16)
        return detail::try_encode_medium_ascii(text, payload);
#endif

    // Validate both bounded loads before storing either overlapping group.
    if (length < 8)
    {
        const auto tail = length - 4;
        std::uint64_t first;
        std::uint64_t last;
        std::memcpy(&first, text.data(), sizeof(first));
        std::memcpy(&last, text.data() + tail, sizeof(last));
        const auto chars = _mm_set_epi64x(static_cast<std::int64_t>(last), static_cast<std::int64_t>(first));
        if (!is_ascii(chars))
            return false;
        const auto packed = static_cast<std::uint64_t>(_mm_cvtsi128_si64(_mm_packus_epi16(chars, chars)));
        const auto head = static_cast<std::uint32_t>(packed);
        const auto end = static_cast<std::uint32_t>(packed >> 32);
        std::memcpy(payload.data(), &head, sizeof(head));
        std::memcpy(payload.data() + tail, &end, sizeof(end));
    }
    else
    {
        const auto tail = length - 8;
        __m128i first;
        __m128i last;
        std::memcpy(&first, text.data(), sizeof(first));
        std::memcpy(&last, text.data() + tail, sizeof(last));
        if (!is_ascii(_mm_or_si128(first, last)))
            return false;
        const auto packed = _mm_packus_epi16(first, last);
        const auto head = _mm_cvtsi128_si64(packed);
        const auto end = _mm_cvtsi128_si64(_mm_unpackhi_epi64(packed, packed));
        std::memcpy(payload.data(), &head, sizeof(head));
        std::memcpy(payload.data() + tail, &end, sizeof(end));
    }
    return true;
}

bool try_write_short_ascii_utf8(writer& output, std::u16string_view text)
{
    constexpr auto header_size = sizeof(std::uint64_t);
#if defined(LUMINPACK_CPP_AVX2)
    constexpr std::size_t max_length = 32;
#else
    constexpr std::size_t max_length = 16;
#endif
    const auto length = text.size();
    if (length < 4 || length > max_length || output.remaining() < header_size + length || text.front() > 0x7f)
        return false;

    writer next = output;
    const auto record = next.reserve(header_size + length);
    const auto payload = record.subspan(header_size);
    const auto source = std::span(text.data(), length);
    if (detail::overlaps(std::as_bytes(source), payload) || !try_encode_short_ascii(source, payload))
        return false;

    // The byte count and UTF-16 code-unit count are equal for ASCII.
    const auto count = static_cast<std::uint64_t>(length);
    const auto header = count | (count << 32);
    std::memcpy(record.data(), &header, sizeof(header));
    output = next;
    return true;
}
#endif

void encode_utf8(std::u16string_view value, std::span<std::byte> target)
{
    if (value.size() == target.size())
    {
        detail::narrow_latin1(std::span(value.data(), value.size()), target);
        return;
    }

    std::size_t at = 0;
    std::size_t written = 0;
    while (at < value.size())
    {
        const auto code = read_utf16_scalar(value, at);
        if (code < 0x80)
            target[written++] = static_cast<std::byte>(code);
        else if (code < 0x800)
            target[written++] = static_cast<std::byte>(0xc0 | (code >> 6));
        else
        {
            if (code < 0x10000)
                target[written++] = static_cast<std::byte>(0xe0 | (code >> 12));
            else
            {
                target[written++] = static_cast<std::byte>(0xf0 | (code >> 18));
                target[written++] = static_cast<std::byte>(0x80 | ((code >> 12) & 0x3f));
            }
            target[written++] = static_cast<std::byte>(0x80 | ((code >> 6) & 0x3f));
        }
        if (code >= 0x80)
            target[written++] = static_cast<std::byte>(0x80 | (code & 0x3f));
    }
}
}

std::optional<std::u16string_view> read_string_into(reader& input, string_profile profile, std::span<char16_t> destination)
{
    if (profile == string_profile::utf16_length)
        return read_utf16_into(input, destination);
    reader next = input;
    const auto record = read_text_record(next, profile);
    if (!record)
    {
        input = next;
        return std::nullopt;
    }
    if (record->length > destination.size())
        detail::short_output();
    auto result = destination.first(record->length);
    decode_text(result, *record);
    input = next;
    if (result.empty())
        return std::u16string_view{};
    return std::u16string_view(result.data(), result.size());
}

std::optional<std::u16string> read_string(reader& input, string_profile profile)
{
    reader next = input;
    const auto record = read_text_record(next, profile);
    if (!record)
    {
        input = next;
        return std::nullopt;
    }
    std::u16string result(record->length, u'\0');
    decode_text(result, *record);
    input = next;
    return result;
}

void skip_string(reader& input, string_profile profile)
{
    reader next = input;
    (void)read_text_record(next, profile, true);
    input = next;
}

void write_string(writer& output, string_profile profile, std::optional<std::u16string_view> value)
{
    if (profile == string_profile::utf16_length)
    {
        write_utf16(output, value);
        return;
    }

#if defined(LUMINPACK_CPP_INLINE_SSE2)
    if (profile == string_profile::utf8_length && value && try_write_short_ascii_utf8(output, *value))
        return;
#endif

    const auto utf8 = uses_utf8(profile);
    const auto token = uses_token(profile);
    const auto variable = uses_varint(profile);
    std::array<std::byte, 10> header_bytes{};
    writer header(header_bytes);
    auto text = value.value_or(std::u16string_view{});
    const auto chars = detail::collection_count(text.size());
    const bool latin1 = !utf8 && !token && !text.empty() &&
        std::ranges::all_of(text, [](char16_t character) { return character <= 0xff; });
    if (!utf8 && text.size() > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()) / 2)
        throw std::length_error("LuminPack UTF-16 record exceeds the wire length.");
    const auto size = utf8 ? utf8_bytes(text) : latin1 ? text.size() : text.size() * sizeof(char16_t);
    if (!token)
    {
        const auto width = !value ? -1 : latin1 ? (chars * 2) | 1 : static_cast<std::int32_t>(size);
        write_length(header, width, variable);
        if (utf8)
            write_length(header, value ? chars : -1, variable);
    }
    const auto terminator = token ? (utf8 ? 1u : 2u) : 0u;
    writer next = output;
    const auto record = next.reserve(header.written().size() + size + terminator);
    const auto destination = record.subspan(header.written().size(), size);
    if (!text.empty())
    {
        std::u16string scratch;
        if ((utf8 || latin1) && detail::overlaps(std::as_bytes(std::span(text.data(), text.size())), destination))
        {
            scratch = text;
            text = scratch;
        }
        if (utf8)
        {
            std::size_t prefix = 0;
            std::size_t bytes_written = 0;
#if defined(LUMINPACK_CPP_AVX2)
            if (profile == string_profile::utf8_length && text.size() > 32 && text.front() < 0x80 && detail::has_avx2())
            {
                prefix = detail::encode_ascii_prefix_avx2(std::span(text.data(), text.size()), destination);
                bytes_written = prefix;
            }
#endif
#if defined(LUMINPACK_CPP_INLINE_SSE2)
            if (profile == string_profile::utf8_length && text.size() >= 32 && text.front() >= 0x80 && text.front() < 0x800)
            {
                prefix = detail::encode_two_byte_prefix(std::span(text.data(), text.size()), destination);
                bytes_written = prefix * 2;
            }
#endif
            if (prefix < text.size())
                encode_utf8(text.substr(prefix), destination.subspan(bytes_written));
        }
        else if (latin1)
            detail::narrow_latin1(std::span(text.data(), text.size()), destination);
        else
            std::memmove(destination.data(), text.data(), size);
    }
    if (!header.written().empty())
        std::memcpy(record.data(), header.written().data(), header.written().size());
    if (terminator != 0)
        std::memset(record.data() + record.size() - terminator, 0, terminator);
    output = next;
}
}
