#pragma once

// Included by luminpack.h after the ordinary positional and Version record codecs.
namespace luminpack
{
namespace detail
{
inline constexpr std::uint8_t back_reference_marker = 250;
inline constexpr std::uint8_t null_reference_marker = 255;

template<scalar T>
std::uint32_t checked_reference_id(reader& input)
{
    const auto value = input.read<T>();
    if (!std::in_range<std::uint32_t>(value))
        throw wire_error("LuminPack reference ID lies outside the unsigned 32-bit range.");
    return static_cast<std::uint32_t>(value);
}

/// Accepts the nonnegative integer forms recognized by the managed UInt32 VarInt reader.
inline std::uint32_t read_reference_id(reader& input)
{
    const auto marker = input.read<std::int8_t>();
    if (marker >= 0)
        return static_cast<std::uint32_t>(marker);
    switch (marker)
    {
    case -121: return input.read<std::uint8_t>();
    case -122: return checked_reference_id<std::int8_t>(input);
    case -123: return input.read<std::uint16_t>();
    case -124: return checked_reference_id<std::int16_t>(input);
    case -125: return input.read<std::uint32_t>();
    case -126: return checked_reference_id<std::int32_t>(input);
    case -127: return checked_reference_id<std::uint64_t>(input);
    case -128: return checked_reference_id<std::int64_t>(input);
    default: throw wire_error("Invalid LuminPack reference ID.");
    }
}

/// Writes the object's marker and the managed UInt32 VarInt encoding of its explicit ID.
inline void write_reference_header(writer& output, std::uint8_t marker, std::uint32_t id)
{
    if (id <= 127)
    {
        output.write<std::uint16_t>(static_cast<std::uint16_t>(marker | (id << 8)));
        return;
    }
    if (id <= std::numeric_limits<std::uint16_t>::max())
    {
        const auto target = output.reserve(4);
        target[0] = static_cast<std::byte>(marker);
        target[1] = static_cast<std::byte>(static_cast<std::uint8_t>(-123));
        const auto narrowed = static_cast<std::uint16_t>(id);
        std::memcpy(target.data() + 2, &narrowed, sizeof(narrowed));
        return;
    }
    const auto target = output.reserve(6);
    target[0] = static_cast<std::byte>(marker);
    target[1] = static_cast<std::byte>(static_cast<std::uint8_t>(-125));
    std::memcpy(target.data() + 2, &id, sizeof(id));
}
}

/// Wraps a concrete positional or Version body with the managed tracked-object framing.
/// Tracked pointers borrow from the reference graph returned by the document reader.
template<class T, class Body, std::size_t MemberCount>
struct tracked_binary_codec
{
    using value_type = T*;
    static constexpr std::size_t minimum_size = 1;
    static_assert(MemberCount <= 249);

    [[nodiscard]] static value_type read(reader& input, version_state& version, reference_read_state& references)
    {
        value_type result = nullptr;
        read_into(input, result, version, references);
        return result;
    }

    /// Registers a shell before reading its body. A nonthrowing destination can be claimed once per operation.
    static void read_into(reader& input, value_type& destination, version_state& version, reference_read_state& references)
    {
        reader next = input;
        const auto marker = next.read<std::uint8_t>();
        if (marker == detail::null_reference_marker)
            destination = nullptr;
        else if (marker == detail::back_reference_marker)
            destination = references.get<T>(detail::read_reference_id(next));
        else
        {
            if (marker > 249)
                throw wire_error("Invalid LuminPack tracked object header.");
            const auto id = detail::read_reference_id(next);
            if (next.unread().empty())
                detail::truncated_input();
            if (next.unread().front() == static_cast<std::byte>(detail::null_reference_marker))
                throw wire_error("A LuminPack reference definition must contain a non-null body.");

            reference_read_state::depth_scope depth(references);
            auto transaction = detail::version_transaction(version, references);
            const bool reuse = std::is_nothrow_move_assignable_v<T> && references.claim(id, destination);
            auto* shell = reuse ? destination : references.add<T>(id);
            Body::read_into(next, *shell, version, references);
            destination = shell;
            transaction.commit();
        }
        input = next;
    }

    static void write(writer& output, const T* value, version_state& version, reference_write_state& references)
    {
        if (!value)
        {
            output.write(detail::null_reference_marker);
            return;
        }

        writer next = output;
        auto transaction = detail::version_transaction(version, references);
        const auto id = references.track(value);
        if (id < 0)
            detail::write_reference_header(next, detail::back_reference_marker, static_cast<std::uint32_t>(~id));
        else
        {
            detail::write_reference_header(next, static_cast<std::uint8_t>(MemberCount), static_cast<std::uint32_t>(id));
            Body::write_body(next, *value, version, references);
        }
        output = next;
        transaction.commit();
    }
};

/// Reads a binary value and owns every tracked shell. RequireComplete checks the document boundary.
template<class Codec, bool RequireComplete = true>
[[nodiscard]] reference_document<typename Codec::value_type> read_binary_reference_document(
    std::span<const std::byte> input, reader_limits limits = {}, std::size_t max_depth = default_binary_reference_depth)
{
    detail::check_document_size(input.size(), limits.max_input_bytes);
    reader cursor(input, limits);
    reference_document<typename Codec::value_type> result;
    version_state version;
    reference_read_state references(result.graph, max_depth);
    result.value = detail::read_codec<Codec>(cursor, version, references);
    if constexpr (RequireComplete)
        cursor.require_end();
    return result;
}

/// Writes a binary document with one identity table and Version registry shared by all descendants.
template<class Codec>
[[nodiscard]] std::span<const std::byte> write_binary_reference_document(
    std::span<std::byte> output, const typename Codec::value_type& value)
{
    writer cursor(output);
    version_state version;
    reference_write_state references;
    detail::write_codec<Codec>(cursor, value, version, references);
    return cursor.written();
}

template<class Codec>
[[nodiscard]] std::span<const std::byte> write_binary_reference_document(
    std::span<std::byte> output, const reference_document<typename Codec::value_type>& document)
{
    return write_binary_reference_document<Codec>(output, document.value);
}
}
