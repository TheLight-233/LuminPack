#pragma once

#include "version_legacy.h"

// Included by luminpack.h after Version state and the static codec declarations.
namespace luminpack
{
/// Combines the emitted name hash, low 24 type-contract bits, and width code.
[[nodiscard]] constexpr std::uint64_t version_descriptor(std::uint32_t name, std::uint32_t contract,
    std::uint8_t width) noexcept
{
    return name | (static_cast<std::uint64_t>(contract & 0xffffffu) << 32) |
        (static_cast<std::uint64_t>(width) << 56);
}

/// A field in a Version DTO. Descriptor uses the oldest name key that the writer emits.
/// The additional keys match current and former names when reading mapped records.
template<auto Field, class Codec, std::uint64_t Descriptor, std::uint32_t... Aliases>
struct version_member
{
    using field_codec = Codec;
    static constexpr auto field = Field;
    static constexpr auto descriptor = Descriptor;
    static constexpr std::array<std::uint32_t, 1 + sizeof...(Aliases)> names{
        static_cast<std::uint32_t>(descriptor), Aliases...
    };
    static constexpr auto width = static_cast<std::uint8_t>(Descriptor >> 56);
    static_assert(width <= 250 || width >= 254, "Invalid LuminPack Version field width.");

    static bool matches(std::uint32_t key) noexcept
    {
        return key == static_cast<std::uint32_t>(descriptor) || ((key == Aliases) || ...);
    }

    static void reset(auto& value) { value.*Field = typename Codec::value_type{}; }

    template<class T, class... Context>
    static void read(reader& input, version_state& state, T& value, Context&... context)
    {
        if constexpr (width == 254)
            value.*Field = detail::read_codec<Codec>(input, state, context...);
        else if constexpr (width <= 250)
        {
            auto bounded = input.subrecord(width);
            auto member = detail::read_codec<Codec>(bounded, state, context...);
            bounded.require_end();
            value.*Field = std::move(member);
        }
        else
        {
            const auto length = input.read<std::uint32_t>();
            if (length > static_cast<std::uint32_t>(std::numeric_limits<std::int32_t>::max()))
                throw wire_error("LuminPack Version member exceeds its maximum size.");
            auto bounded = input.subrecord(length);
            version_state::scope scope(state);
            auto member = detail::read_codec<Codec>(bounded, state, context...);
            bounded.require_end();
            value.*Field = std::move(member);
        }
    }

    template<class... Context>
    static void write(writer& output, version_state& state, const auto& value, Context&... context)
    {
        if constexpr (width == 254)
            detail::write_codec<Codec>(output, value.*Field, state, context...);
        else if constexpr (width <= 250)
        {
            const auto start = output.written().size();
            detail::write_codec<Codec>(output, value.*Field, state, context...);
            if (output.written().size() - start != width)
                throw std::invalid_argument("LuminPack Version member has the wrong fixed width.");
        }
        else
        {
            const auto prefix = output.reserve(sizeof(std::uint32_t));
            const auto start = output.written().size();
            version_state::scope scope(state);
            detail::write_codec<Codec>(output, value.*Field, state, context...);
            const auto length = output.written().size() - start;
            if (length > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
                throw std::length_error("LuminPack Version member exceeds its maximum size.");
            detail::store_count(prefix, static_cast<std::int32_t>(length));
        }
    }
};

/// Version revision-4 and revision-5 DTO codec with a shared operation registry.
template<class T, string_profile Profile, class... Members>
struct version_codec
{
    using value_type = std::optional<T>;
    static constexpr std::size_t minimum_size = 1;
    static constexpr std::size_t member_count = sizeof...(Members);
    static_assert(member_count <= detail::maximum_version_fields);

    static value_type read(reader& input)
    {
        version_state state;
        return read(input, state);
    }

    template<class... Context>
    static value_type read(reader& input, version_state& state, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(state, context...);
        const auto header = detail::read_version_record_header(next, state, Profile);
        if (header.form != detail::version_read_form::modern)
        {
            if (header.form == detail::version_read_form::legacy) [[unlikely]]
            {
                if constexpr ((requires { Members::legacy_field; } && ...))
                {
                    auto value = read_legacy(next, state, context...);
                    input = next;
                    transaction.commit();
                    return value;
                }
                else
                    throw wire_error("A revision-1 Version record requires its full legacy member contracts.");
            }
            input = next;
            transaction.commit();
            return std::nullopt;
        }

        T result{};
        if (std::ranges::equal(header.descriptors, descriptors))
            (Members::read(next, state, result, context...), ...);
        else
            read_mapped(next, state, result, header.descriptors, context...);
        input = next;
        transaction.commit();
        return result;
    }

    static void write(writer& output, const value_type& value)
    {
        version_state state;
        write(output, value, state);
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, version_state& state, Context&... context)
    {
        if (value)
            write_body(output, *value, state, context...);
        else
            output.write<std::uint8_t>(255);
    }

    /// Populates an existing reference shell using the complete Version header and its member operations.
    template<class... Context>
    static void read_into(reader& input, T& destination, version_state& state, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(state, context...);
        auto value = read(next, state, context...);
        if (!value)
            throw wire_error("A LuminPack reference definition must contain a non-null Version record.");
        destination = std::move(*value);
        input = next;
        transaction.commit();
    }

    /// Writes a non-null Version body, including its full schema or registered ordinal.
    template<class... Context>
    static void write_body(writer& output, const T& value, version_state& state, Context&... context)
    {
        writer next = output;
        auto transaction = detail::version_transaction(state, context...);
        detail::write_version_prehashed(next, state, Profile, schema, descriptors);
        (Members::write(next, state, value, context...), ...);
        output = next;
        transaction.commit();
    }

private:
    static constexpr std::uint8_t absent = 255;
    static constexpr std::array<std::uint64_t, member_count> descriptors{Members::descriptor...};
    static constexpr auto schema = detail::version_schema_hash(descriptors);
    static constexpr std::array<bool (*)(std::uint32_t), member_count> matches{&Members::matches...};

    static consteval bool unique_keys()
    {
        constexpr auto size = (std::size_t{0} + ... + Members::names.size());
        std::array<std::uint32_t, size> keys{};
        std::size_t position = 0;
        const auto append = [&](const auto& names) {
            for (const auto name : names)
                keys[position++] = name;
        };
        (append(Members::names), ...);
        for (std::size_t index = 0; index < keys.size(); ++index)
        {
            for (std::size_t previous = 0; previous < index; ++previous)
            {
                if (keys[index] == keys[previous])
                    return false;
            }
        }
        return true;
    }

    static_assert(unique_keys(), "Version current and former name keys must be unique.");

    template<class... Context>
    static value_type read_legacy(reader& input, version_state& state, Context&... context)
    {
        constexpr std::array<detail::legacy_version_field, member_count> fields{Members::legacy_field...};
        constexpr std::array<void (*)(reader&, version_state&, T&, Context&...), member_count> read_slot{
            &Members::template read_legacy<T, Context...>...
        };
        constexpr std::array<void (*)(T&), member_count> reset_slot{&Members::template reset<T>...};
        const auto map = detail::read_legacy_version_map(input, Profile, fields);
        T result{};
        for (std::size_t index = 0; index < map.count; ++index)
        {
            auto field = input.subrecord(map.lengths[index]);
            const auto slot = map.slots[index];
            if (slot != detail::legacy_version_map::absent)
                read_slot[slot](field, state, result, context...);
        }
        for (std::size_t slot = 0; slot < member_count; ++slot)
        {
            if (!map.present[slot])
                reset_slot[slot](result);
        }
        return result;
    }

    static void skip(reader& input, std::uint8_t width)
    {
        if (width <= 250)
            (void)input.take(width);
        else if (width == 254)
            skip_string(input, Profile);
        else
        {
            const auto size = input.read<std::uint32_t>();
            if (size > static_cast<std::uint32_t>(std::numeric_limits<std::int32_t>::max()))
                throw wire_error("LuminPack Version member exceeds its maximum size.");
            (void)input.take(size);
        }
    }

    template<class... Context>
    static void read_mapped(reader& input, version_state& state, T& target, std::span<const std::uint64_t> incoming, Context&... context)
    {
        std::array<std::uint8_t, 249> slots;
        std::array<bool, member_count> seen{};
        for (std::size_t index = 0; index < incoming.size(); ++index)
        {
            const auto descriptor = incoming[index];
            auto slot = std::size_t{0};
            for (; slot < member_count; ++slot)
            {
                if (matches[slot](static_cast<std::uint32_t>(descriptor)))
                    break;
            }
            if (slot == member_count)
            {
                slots[index] = absent;
                continue;
            }
            if (seen[slot])
                throw wire_error("Duplicate LuminPack Version destination member.");
            if ((descriptor >> 32) != (descriptors[slot] >> 32))
                throw wire_error("LuminPack Version member type or width changed.");
            seen[slot] = true;
            slots[index] = static_cast<std::uint8_t>(slot);
        }

        constexpr std::array<void (*)(reader&, version_state&, T&, Context&...), member_count> read_slot{
            &Members::template read<T, Context...>...
        };
        constexpr std::array<void (*)(T&), member_count> reset_slot{&Members::template reset<T>...};
        for (std::size_t index = 0; index < incoming.size(); ++index)
        {
            if (slots[index] == absent)
                skip(input, static_cast<std::uint8_t>(incoming[index] >> 56));
            else
                read_slot[slots[index]](input, state, target, context...);
        }
        for (std::size_t slot = 0; slot < member_count; ++slot)
        {
            if (!seen[slot])
                reset_slot[slot](target);
        }
    }
};

/// Reads one binary value with a shared Version registry. RequireComplete checks the document boundary.
template<class Codec, bool RequireComplete = true>
[[nodiscard]] typename Codec::value_type read_binary_document(std::span<const std::byte> input, reader_limits limits = {})
{
    detail::check_document_size(input.size(), limits.max_input_bytes);
    reader cursor(input, limits);
    version_state state;
    auto value = detail::read_codec<Codec>(cursor, state);
    if constexpr (RequireComplete)
        cursor.require_end();
    return value;
}

/// Writes one binary document into caller-owned storage with a shared Version registry.
template<class Codec>
[[nodiscard]] std::span<const std::byte> write_binary_document(
    std::span<std::byte> output, const typename Codec::value_type& value)
{
    writer cursor(output);
    version_state state;
    detail::write_codec<Codec>(cursor, value, state);
    return cursor.written();
}

}
