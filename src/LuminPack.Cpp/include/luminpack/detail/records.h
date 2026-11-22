#pragma once

#include <tuple>
#include <variant>

// Included by luminpack.h after the cursor and closed codec declarations.
namespace luminpack
{
/// Binds a DTO field to the codec selected by its .NET wire contract.
template<auto Field, class Codec>
struct record_member
{
    static constexpr auto minimum_size = Codec::minimum_size;

    static void read(reader& input, auto& object) { object.*Field = Codec::read(input); }
    static void write(writer& output, const auto& object) { Codec::write(output, object.*Field); }
};

/// The ordered fields following an existing framing boundary, such as a generated Union tag.
template<class T, class... Members>
struct record_payload_codec
{
    using value_type = T;
    static constexpr auto minimum_size = (std::size_t{0} + ... + Members::minimum_size);

    static T read(reader& input)
    {
        reader next = input;
        if (minimum_size > next.remaining())
            detail::truncated_input();
        T result{};
        (Members::read(next, result), ...);
        input = next;
        return result;
    }

    static void write(writer& output, const T& value)
    {
        writer next = output;
        (Members::write(next, value), ...);
        output = next;
    }
};

/// An ordinary positional DTO. Field codecs are supplied in .NET member order.
template<class T, class... Members>
struct record_codec
{
    using value_type = std::optional<T>;
    using payload = record_payload_codec<T, Members...>;
    static constexpr std::size_t minimum_size = 1;
    static_assert(sizeof...(Members) <= 249);

    static value_type read(reader& input)
    {
        reader next = input;
        const auto members = read_object_header(next);
        if (!members)
        {
            input = next;
            return std::nullopt;
        }
        if (*members != sizeof...(Members))
            throw wire_error("LuminPack positional member count does not match the codec.");
        auto result = payload::read(next);
        input = next;
        return result;
    }

    static void write(writer& output, const value_type& value)
    {
        writer next = output;
        if (value)
        {
            write_object_header(next, static_cast<std::uint8_t>(sizeof...(Members)));
            payload::write(next, *value);
        }
        else
            write_object_header(next, std::nullopt);
        output = next;
    }
};

/// Adapts app-owned methods that implement both sides of a GeneratorType.Custom contract.
template<class T, auto Read, auto Write, std::size_t MinimumSize = 0>
struct custom_codec
{
    using value_type = T;
    static constexpr auto minimum_size = MinimumSize;

    static T read(reader& input) { return Read(input); }
    static void write(writer& output, const T& value) { Write(output, value); }
};

/// Hashes the UTF-8 canonical case name emitted by the .NET generator.
[[nodiscard]] constexpr std::uint32_t union_name_hash(std::string_view name) noexcept
{
    auto hash = std::uint32_t{2166136261u};
    for (const auto character : name)
        hash = (hash ^ static_cast<unsigned char>(character)) * 16777619u;
    return hash;
}

/// A known Next-generated Union case. Payload supplies the case's own body framing.
template<std::uint32_t Hash, class Payload>
struct union_case
{
    static constexpr auto hash = Hash;
    using codec = Payload;
    using value_type = typename codec::value_type;
};

/// Static binary dispatch for a closed Next class or interface Union.
template<class... Cases>
struct union_codec
{
    using variant_type = std::variant<typename Cases::value_type...>;
    using value_type = std::optional<variant_type>;
    static constexpr auto minimum_size = sizeof(std::uint32_t);

    static value_type read(reader& input)
    {
        reader next = input;
        const auto hash = next.read<std::uint32_t>();
        if (hash == 0)
        {
            input = next;
            return std::nullopt;
        }
        auto result = read_case(hash, next, indices{});
        input = next;
        return result;
    }

    static void write(writer& output, const value_type& value)
    {
        if (!value)
        {
            output.write<std::uint32_t>(0);
            return;
        }

        writer next = output;
        if (!write_case(next, *value, indices{}))
            throw std::invalid_argument("LuminPack Union does not have an active case.");
        output = next;
    }

private:
    using cases = std::tuple<Cases...>;
    using indices = std::index_sequence_for<Cases...>;

    static consteval bool valid_tags()
    {
        constexpr std::array tags{Cases::hash...};
        for (std::size_t index = 0; index < tags.size(); ++index)
        {
            if (tags[index] == 0 || tags[index] == 0xffffffffu)
                return false;
            for (std::size_t previous = 0; previous < index; ++previous)
            {
                if (tags[index] == tags[previous])
                    return false;
            }
        }
        return true;
    }

    static_assert(valid_tags(), "LuminPack Union hashes must be unique and outside the reserved markers.");

    template<std::size_t Index>
    static bool read_if(value_type& result, std::uint32_t hash, reader& input)
    {
        using item = std::tuple_element_t<Index, cases>;
        if (hash != item::hash)
            return false;
        result.emplace(std::in_place_index<Index>, item::codec::read(input));
        return true;
    }

    template<std::size_t... Index>
    static value_type read_case(std::uint32_t hash, reader& input, std::index_sequence<Index...>)
    {
        value_type result;
        if (!(read_if<Index>(result, hash, input) || ...))
            throw wire_error("Unknown LuminPack Union case hash.");
        return result;
    }

    template<std::size_t Index>
    static bool write_if(writer& output, const variant_type& value)
    {
        using item = std::tuple_element_t<Index, cases>;
        if (value.index() != Index)
            return false;
        output.write(item::hash);
        item::codec::write(output, std::get<Index>(value));
        return true;
    }

    template<std::size_t... Index>
    static bool write_case(writer& output, const variant_type& value, std::index_sequence<Index...>)
    {
        return (write_if<Index>(output, value) || ...);
    }
};
}
