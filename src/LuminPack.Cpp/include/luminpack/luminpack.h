#pragma once

#include <array>
#include <bit>
#include <climits>
#include <concepts>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <limits>
#include <optional>
#include <span>
#include <stdexcept>
#include <string>
#include <string_view>
#include <type_traits>
#include <vector>

namespace luminpack
{
static_assert(CHAR_BIT == 8 && std::endian::native == std::endian::little,
    "LuminPack requires a little-endian target with eight-bit bytes.");
static_assert(sizeof(char16_t) == 2);
static_assert(sizeof(float) == 4 && std::numeric_limits<float>::is_iec559);
static_assert(sizeof(double) == 8 && std::numeric_limits<double>::is_iec559);

/// Reports an invalid or truncated wire record.
class wire_error : public std::runtime_error
{
public:
    using std::runtime_error::runtime_error;
};

/// Fixed-width primitives whose C++ object representations match the wire.
template<class T>
concept scalar =
    std::same_as<T, std::int8_t> || std::same_as<T, std::uint8_t> ||
    std::same_as<T, std::int16_t> || std::same_as<T, std::uint16_t> ||
    std::same_as<T, std::int32_t> || std::same_as<T, std::uint32_t> ||
    std::same_as<T, std::int64_t> || std::same_as<T, std::uint64_t> ||
    std::same_as<T, float> || std::same_as<T, double> || std::same_as<T, char16_t>;

namespace detail
{
[[noreturn]] void truncated_input();
[[noreturn]] void short_output();

inline std::size_t checked_input_width(std::size_t count, std::size_t width, std::size_t available)
{
    if (count > available / width)
        truncated_input();
    return count * width;
}

inline std::size_t checked_output_width(std::size_t count, std::size_t width, std::size_t available)
{
    if (count > available / width)
        short_output();
    return count * width;
}

inline std::int32_t collection_count(std::size_t count)
{
    if (count > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
        throw std::length_error("LuminPack collection exceeds the wire count.");
    return static_cast<std::int32_t>(count);
}

inline void store_count(std::span<std::byte> header, std::int32_t count) noexcept
{
    std::memcpy(header.data(), &count, sizeof(count));
}

bool overlaps(std::span<const std::byte> source, std::span<const std::byte> destination) noexcept;
}

/// Binary collection and UTF-16 code-unit limits for a read operation.
struct reader_limits
{
    std::uint32_t max_collection_length = std::numeric_limits<std::int32_t>::max();
    std::uint32_t max_string_length = std::numeric_limits<std::int32_t>::max();
};

/// Reads a contiguous input window. The input must outlive this cursor.
class reader
{
public:
    explicit reader(std::span<const std::byte> input, reader_limits limits = {}) noexcept
        : input_(input), limits_(limits) {}

    [[nodiscard]] std::size_t remaining() const noexcept { return input_.size() - position_; }
    [[nodiscard]] std::span<const std::byte> unread() const noexcept { return input_.subspan(position_); }

    /// Checks the complete range before returning it or advancing the cursor.
    [[nodiscard]] std::span<const std::byte> take(std::size_t size)
    {
        if (size > remaining())
            detail::truncated_input();
        const auto result = input_.subspan(position_, size);
        position_ += size;
        return result;
    }

    /// Restricts a child reader to a validated member payload while inheriting the current limits.
    [[nodiscard]] reader subrecord(std::size_t size) { return reader(take(size), limits_); }

    /// Reads one unaligned scalar after validating its complete storage.
    template<scalar T>
    [[nodiscard]] T read()
    {
        const auto bytes = take(sizeof(T));
        T value;
        std::memcpy(&value, bytes.data(), sizeof(T));
        return value;
    }

    /// Validates the Boolean marker before publishing the cursor.
    [[nodiscard]] bool read_bool()
    {
        reader next = *this;
        const auto marker = next.read<std::uint8_t>();
        if (marker > 1)
            throw wire_error("Invalid LuminPack Boolean.");
        *this = next;
        return marker != 0;
    }

    /// Reads a collection count bounded by the supplied minimum element width and input limit.
    [[nodiscard]] std::optional<std::size_t> read_collection_count(std::size_t minimum_element_width = 0);

    /// Rejects a string that exceeds the configured code-unit limit.
    void check_string_length(std::size_t length) const;

    /// Checks that an independent document has been consumed completely.
    void require_end() const;

private:
    std::span<const std::byte> input_;
    std::size_t position_ = 0;
    reader_limits limits_;
};

/// Writes into caller-owned contiguous storage, which must outlive this cursor.
class writer
{
public:
    explicit writer(std::span<std::byte> output) noexcept : output_(output) {}

    [[nodiscard]] std::size_t remaining() const noexcept { return output_.size() - position_; }
    [[nodiscard]] std::span<const std::byte> written() const noexcept { return output_.first(position_); }

    /// Reserves a checked range for direct stores by a generated codec.
    [[nodiscard]] std::span<std::byte> reserve(std::size_t size)
    {
        if (size > remaining())
            detail::short_output();
        const auto result = output_.subspan(position_, size);
        position_ += size;
        return result;
    }

    /// Writes one scalar without imposing an alignment requirement on the output.
    template<scalar T>
    void write(T value)
    {
        const auto bytes = reserve(sizeof(T));
        std::memcpy(bytes.data(), &value, sizeof(T));
    }

    /// Publishes a Boolean as a single zero or one byte.
    void write_bool(bool value) { write<std::uint8_t>(value ? 1 : 0); }

private:
    std::span<std::byte> output_;
    std::size_t position_ = 0;
};

/// Reads a positional object member count, or null for the null marker.
[[nodiscard]] std::optional<std::uint8_t> read_object_header(reader& input);
/// Writes a positional object member count or null marker.
void write_object_header(writer& output, std::optional<std::uint8_t> count);

/// Reads a fixed-length UTF-16 record into reusable storage.
/// The result borrows the filled portion of destination. Null and empty are distinct.
[[nodiscard]] std::optional<std::u16string_view> read_utf16_into(reader& input, std::span<char16_t> destination);
/// Writes a fixed-length UTF-16 record, using compact Latin-1 when possible.
void write_utf16(writer& output, std::optional<std::u16string_view> value);

/// Binary string representations match the LuminPack profile tags.
enum class string_profile : std::uint8_t
{
    utf8_length = 0,
    utf8_token = 1,
    utf16_length = 2,
    utf16_token = 3,
    utf8_varint = 4,
    utf16_varint = 5
};

/// Decodes a string into reusable UTF-16 storage, preserving null where the profile carries it.
[[nodiscard]] std::optional<std::u16string_view> read_string_into(reader& input, string_profile profile,
    std::span<char16_t> destination);

/// Decodes a string into owned UTF-16 storage.
[[nodiscard]] std::optional<std::u16string> read_string(reader& input, string_profile profile);

/// Validates and consumes a string without allocating, for an unknown Version field.
void skip_string(reader& input, string_profile profile);

/// Writes a UTF-16 value using the selected wire profile.
void write_string(writer& output, string_profile profile, std::optional<std::u16string_view> value);

/// Reads a raw scalar array into reusable storage. Validation precedes any destination write.
template<scalar T>
[[nodiscard]] std::optional<std::span<T>> read_array_into(reader& input, std::span<T> destination)
{
    reader next = input;
    const auto count = next.read_collection_count(sizeof(T));
    if (!count)
    {
        input = next;
        return std::nullopt;
    }

    const auto length = *count;
    const auto payload = next.take(length * sizeof(T));
    if (length > destination.size())
        detail::short_output();
    const auto result = destination.first(length);
    if (!payload.empty())
        std::memmove(result.data(), payload.data(), payload.size());
    input = next;
    return result;
}

/// Writes a raw scalar array. Null is different from an empty span.
template<scalar T>
void write_array(writer& output, std::optional<std::span<const T>> value)
{
    if (!value)
    {
        output.write<std::int32_t>(-1);
        return;
    }

    constexpr auto header_size = sizeof(std::int32_t);
    const auto count = detail::collection_count(value->size());
    if (output.remaining() < header_size)
        detail::short_output();
    const auto bytes = detail::checked_output_width(value->size(), sizeof(T), output.remaining() - header_size);
    const auto record = output.reserve(header_size + bytes);
    if (bytes != 0)
        std::memmove(record.data() + header_size, value->data(), bytes);
    detail::store_count(record, count);
}

/// Offset of each field and stride in a single local or wire storage model.
template<std::size_t Fields>
struct storage_model
{
    std::size_t stride;
    std::array<std::size_t, Fields> offsets;

    constexpr bool operator==(const storage_model&) const = default;
};

/// Compile-time input to a generated Tagged array codec. Wire order defines tags starting at one.
template<std::size_t Fields, std::size_t Models>
struct tagged_layout
{
    std::array<std::size_t, Fields> widths;
    storage_model<Fields> local;
    std::array<storage_model<Fields>, Models> wire;

    /// Checks storage descriptors and guarantees that conversion only accesses complete fields.
    [[nodiscard]] constexpr bool valid() const noexcept
    {
        if (Fields == 0 || Models < 2 || Models > 250 || !valid_model(local))
            return false;
        for (const auto& model : wire)
        {
            if (!valid_model(model))
                return false;
        }
        return true;
    }

    /// Finds the exact local model, or returns zero if writers must use the canonical model.
    [[nodiscard]] constexpr std::size_t local_tag() const noexcept
    {
        for (std::size_t index = 0; index < Models; ++index)
        {
            if (local == wire[index])
                return index + 1;
        }
        return 0;
    }

private:
    [[nodiscard]] constexpr bool valid_model(const storage_model<Fields>& model) const noexcept
    {
        for (std::size_t field = 0; field < Fields; ++field)
        {
            const auto offset = model.offsets[field];
            if (widths[field] == 0 || offset > model.stride || widths[field] > model.stride - offset)
                return false;
            for (std::size_t previous = 0; previous < field; ++previous)
            {
                const auto previous_offset = model.offsets[previous];
                if (offset < previous_offset + widths[previous] && previous_offset < offset + widths[field])
                    return false;
            }
        }
        return true;
    }
};

namespace detail
{
template<auto Layout>
void convert_layout(std::span<const std::byte> input, std::span<std::byte> output, std::size_t count,
    const auto& source, const auto& destination, bool clear_padding)
{
    std::vector<std::byte> scratch;
    if (overlaps(input, output))
    {
        scratch.assign(input.begin(), input.end());
        input = scratch;
    }
    if (clear_padding)
        std::memset(output.data(), 0, output.size());
    for (std::size_t index = 0; index < count; ++index)
    {
        const auto* from = input.data() + index * source.stride;
        auto* to = output.data() + index * destination.stride;
        for (std::size_t field = 0; field < Layout.widths.size(); ++field)
            std::memcpy(to + destination.offsets[field], from + source.offsets[field], Layout.widths[field]);
    }
}
}

/// Reads a Tagged array with multiple storage models. Use sizeof and offsetof for the local model.
/// Describe raw, nonvolatile fields whose object representations accept every bit pattern.
template<class T, auto Layout>
[[nodiscard]] std::optional<std::span<T>> read_tagged_array_into(reader& input, std::span<T> destination)
{
    static_assert(std::is_trivially_copyable_v<T> && std::is_standard_layout_v<T>);
    static_assert(Layout.valid() && Layout.local.stride == sizeof(T));

    reader next = input;
    const auto count = next.read_collection_count();
    if (!count || *count == 0)
    {
        input = next;
        if (!count)
            return std::nullopt;
        return destination.first(0);
    }

    const auto length = *count;
    const auto tag = next.read<std::uint8_t>();
    if (tag == 0 || tag > Layout.wire.size())
        throw wire_error("Unknown LuminPack storage layout tag.");
    const auto& model = Layout.wire[tag - 1];
    const auto payload = next.take(detail::checked_input_width(length, model.stride, next.remaining()));
    if (length > destination.size())
        detail::short_output();
    const auto result = destination.first(length);
    const auto bytes = std::as_writable_bytes(result);
    if (tag == Layout.local_tag())
        std::memmove(bytes.data(), payload.data(), payload.size());
    else
        detail::convert_layout<Layout>(payload, bytes, length, model, Layout.local, false);
    input = next;
    return result;
}

/// Writes a Tagged array using a matching local model or the canonical first model.
template<class T, auto Layout>
void write_tagged_array(writer& output, std::optional<std::span<const T>> value)
{
    static_assert(std::is_trivially_copyable_v<T> && std::is_standard_layout_v<T>);
    static_assert(Layout.valid() && Layout.local.stride == sizeof(T));

    if (!value || value->empty())
    {
        output.write<std::int32_t>(value ? 0 : -1);
        return;
    }

    constexpr auto header_size = sizeof(std::int32_t) + 1;
    constexpr auto local_tag = Layout.local_tag();
    constexpr auto tag = local_tag == 0 ? 1 : local_tag;
    constexpr auto model = Layout.wire[tag - 1];
    const auto count = detail::collection_count(value->size());
    if (output.remaining() < header_size)
        detail::short_output();
    const auto bytes = detail::checked_output_width(value->size(), model.stride, output.remaining() - header_size);
    writer next = output;
    const auto record = next.reserve(header_size + bytes);
    const auto source = std::as_bytes(*value);
    const auto payload = record.subspan(header_size);
    if constexpr (local_tag != 0)
        std::memmove(payload.data(), source.data(), bytes);
    else
        detail::convert_layout<Layout>(source, payload, value->size(), Layout.local, model, true);
    detail::store_count(record, count);
    record[sizeof(std::int32_t)] = static_cast<std::byte>(tag);
    output = next;
}
}

#include "detail/version_state.h"
#include "detail/collections.h"
#include "detail/records.h"
#include "detail/version_codec.h"
#include "detail/json_reader_writer.h"
#include "detail/json_codecs.h"
