#include <luminpack/luminpack.h>

#include "latin1.h"

#include <functional>
#include <string>

namespace luminpack
{
namespace detail
{
void truncated_input()
{
    throw wire_error("Truncated LuminPack record.");
}

void short_output()
{
    throw std::length_error("Insufficient LuminPack destination storage.");
}

bool overlaps(std::span<const std::byte> source, std::span<const std::byte> destination) noexcept
{
    if (source.empty() || destination.empty())
        return false;
    const std::less<const std::byte*> before;
    return before(source.data(), destination.data() + destination.size()) &&
        before(destination.data(), source.data() + source.size());
}
}

void reader::require_end() const
{
    if (remaining() != 0)
        throw wire_error("Trailing bytes after the LuminPack record.");
}

std::optional<std::uint8_t> read_object_header(reader& input)
{
    reader next = input;
    const auto count = next.read<std::uint8_t>();
    if (count >= 250 && count != 255)
        throw wire_error("Invalid LuminPack positional object marker.");
    input = next;
    if (count == 255)
        return std::nullopt;
    return count;
}

void write_object_header(writer& output, std::optional<std::uint8_t> count)
{
    if (count && *count >= 250)
        throw std::invalid_argument("LuminPack positional objects support at most 249 members.");
    output.write<std::uint8_t>(count.value_or(255));
}

std::optional<std::u16string_view> read_utf16_into(reader& input, std::span<char16_t> destination)
{
    reader next = input;
    const auto header = next.read<std::int32_t>();
    if (header < -1)
        throw wire_error("Invalid LuminPack UTF-16 length.");
    if (header == -1)
    {
        input = next;
        return std::nullopt;
    }

    const auto length = static_cast<std::size_t>(header) >> 1;
    const bool latin1 = (header & 1) != 0;
    const auto payload = next.take(latin1 ? length : static_cast<std::size_t>(header));
    if (length > destination.size())
        detail::short_output();

    const auto result = destination.first(length);
    if (!result.empty())
    {
        const auto bytes = std::as_writable_bytes(result);
        if (!latin1)
            std::memmove(bytes.data(), payload.data(), payload.size());
        else if (detail::overlaps(payload, bytes))
        {
            const std::vector<std::byte> scratch(payload.begin(), payload.end());
            detail::widen_latin1(scratch, result);
        }
        else
            detail::widen_latin1(payload, result);
    }
    input = next;
    if (result.empty())
        return std::u16string_view{};
    return std::u16string_view(result.data(), result.size());
}

void write_utf16(writer& output, std::optional<std::u16string_view> value)
{
    if (!value || value->empty())
    {
        output.write<std::int32_t>(value ? 0 : -1);
        return;
    }

    constexpr auto header_size = sizeof(std::int32_t);
    constexpr auto max_chars = static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()) / 2;
    const auto length = value->size();
    if (length > max_chars)
        throw std::length_error("LuminPack UTF-16 record exceeds the wire length.");
    if (output.remaining() < header_size || length > output.remaining() - header_size)
        detail::short_output();

    const auto raw_size = length * sizeof(char16_t);
    const auto raw_header = static_cast<std::int32_t>(raw_size);
    const auto compact_header = raw_header | 1;
    const auto characters = std::span(value->data(), length);
    const auto raw = std::as_bytes(characters);
    if (raw_size > output.remaining() - header_size)
    {
        // Classify first so a failed write leaves the short destination untouched.
        for (const auto character : characters)
        {
            if (character > 0xff)
                detail::short_output();
        }
    }

    writer compact = output;
    const auto record = compact.reserve(header_size + length);
    const auto destination = record.subspan(header_size);
    if (detail::overlaps(raw, destination))
    {
        const std::u16string scratch(*value);
        const std::span staged(scratch.data(), scratch.size());
        if (detail::narrow_latin1(staged, destination))
        {
            detail::store_count(record, compact_header);
            output = compact;
            return;
        }
        const auto full = output.reserve(header_size + raw_size);
        std::memcpy(full.data() + header_size, staged.data(), raw_size);
        detail::store_count(full, raw_header);
        return;
    }

    if (detail::narrow_latin1(characters, destination))
    {
        detail::store_count(record, compact_header);
        output = compact;
        return;
    }

    const auto full = output.reserve(header_size + raw_size);
    std::memmove(full.data() + header_size, raw.data(), raw_size);
    detail::store_count(full, raw_header);
}
}
