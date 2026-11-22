#pragma once

#include <memory>
#include <tuple>
#include <variant>

// Included by luminpack.h after the cursor and closed codec declarations.
namespace luminpack
{
/// Binds a DTO field to the codec selected by its .NET wire contract.
/// The generator enables batching for eligible fields in its own DTO layouts.
template<auto Field, class Codec, bool AllowBatch = false>
struct record_member
{
    static constexpr auto minimum_size = Codec::minimum_size;

    template<class... Context>
    static void read(reader& input, auto& object, Context&... context)
    {
        object.*Field = detail::read_codec<Codec>(input, context...);
    }

    template<class... Context>
    static void write(writer& output, const auto& object, Context&... context)
    {
        detail::write_codec<Codec>(output, object.*Field, context...);
    }
};

namespace detail
{
template<class T, class Member>
struct record_read_field
{
    static constexpr std::size_t width = 0;
};

template<class T, auto Field, class Codec, bool AllowBatch>
struct record_read_field<T, record_member<Field, Codec, AllowBatch>>
{
    using field_type = std::remove_reference_t<decltype(std::declval<T&>().*Field)>;
    static constexpr bool boolean = std::same_as<Codec, boolean_codec>;
    static constexpr std::size_t width = []
    {
        if constexpr (AllowBatch && (contiguous_codec<Codec> || boolean))
        {
            if constexpr (std::same_as<field_type, typename Codec::value_type> &&
                (boolean || Codec::minimum_size == sizeof(field_type)))
                return Codec::minimum_size;
        }
        return std::size_t{0};
    }();
    static constexpr bool copy_representation = []
    {
        if constexpr (!boolean)
            return width == sizeof(field_type);
        else if constexpr (sizeof(field_type) == 1)
            return std::bit_cast<std::byte>(field_type{false}) == std::byte{0} &&
                std::bit_cast<std::byte>(field_type{true}) == std::byte{1};
        else
            return false;
    }();

    static const std::byte* address(T& value) noexcept
    {
        return reinterpret_cast<const std::byte*>(std::addressof(value.*Field));
    }

    static void validate(const std::byte* source)
    {
        if constexpr (boolean)
            if (std::to_integer<unsigned>(*source) > 1)
                throw wire_error("Invalid LuminPack Boolean.");
    }

    static void read(const std::byte* source, T& value)
    {
        if constexpr (boolean)
        {
            validate(source);
            value.*Field = *source != std::byte{0};
        }
        else
            std::memcpy(std::addressof(value.*Field), source, width);
    }
};
}

/// The ordered fields following an existing framing boundary, such as a generated Union tag.
template<class T, class... Members>
struct record_payload_codec
{
    using value_type = T;
    static constexpr auto minimum_size = (std::size_t{0} + ... + Members::minimum_size);

    template<class... Context>
    static T read(reader& input, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        if (minimum_size > next.remaining())
            detail::truncated_input();
        T result{};
        if constexpr (has_batch)
            read_fields(next, result, std::index_sequence_for<Members...>{}, context...);
        else
            (Members::read(next, result, context...), ...);
        input = next;
        transaction.commit();
        return result;
    }

    template<class... Context>
    static void write(writer& output, const T& value, Context&... context)
    {
        writer next = output;
        auto transaction = detail::version_transaction(context...);
        (Members::write(next, value, context...), ...);
        output = next;
        transaction.commit();
    }

private:
    using members = std::tuple<Members...>;
    template<std::size_t Index>
    using field = detail::record_read_field<T, std::tuple_element_t<Index, members>>;

    static constexpr std::array widths{detail::record_read_field<T, Members>::width..., std::size_t{0}};
    static constexpr bool has_batch = []
    {
        for (std::size_t index = 1; index < sizeof...(Members); ++index)
            if (widths[index - 1] != 0 && widths[index] != 0)
                return true;
        return false;
    }();
    static constexpr auto offsets = []
    {
        std::array<std::size_t, widths.size()> result{};
        for (std::size_t index = 1; index < result.size(); ++index)
            result[index] = result[index - 1] + widths[index - 1];
        return result;
    }();

    template<std::size_t First, std::size_t Width, std::size_t... Indices>
    static void read_batch(const std::byte* source, T& value, std::index_sequence<Indices...>)
    {
        // The complete wire span is checked. Copy only when every member address in the
        // actual destination agrees with the serialized order and widths.
        if constexpr (sizeof(void*) == 8 && Width <= sizeof(T) &&
            (field<First + Indices>::copy_representation && ...))
        {
            auto bytes = std::as_writable_bytes(std::span(&value, 1));
            const auto offset = reinterpret_cast<std::uintptr_t>(field<First>::address(value)) -
                reinterpret_cast<std::uintptr_t>(bytes.data());
            if (offset <= bytes.size() - Width &&
                ((field<First + Indices>::address(value) ==
                    bytes.data() + offset + (offsets[First + Indices] - offsets[First])) && ...))
            {
                (field<First + Indices>::validate(source + (offsets[First + Indices] - offsets[First])), ...);
                std::memcpy(bytes.data() + offset, source, Width);
                return;
            }
        }
        (field<First + Indices>::read(source + (offsets[First + Indices] - offsets[First]), value), ...);
    }

    template<std::size_t First, class... Context>
    static void read_field_or_batch(reader& input, T& value, Context&... context)
    {
        if constexpr (First == 0 || widths[First] == 0 || widths[First - 1] == 0)
        {
            constexpr auto end = []
            {
                auto last = First;
                while (widths[last] != 0)
                    ++last;
                return last;
            }();
            if constexpr (end > First + 1)
            {
                constexpr auto width = offsets[end] - offsets[First];
                const auto batch = input.take(width);
                read_batch<First, width>(batch.data(), value, std::make_index_sequence<end - First>{});
            }
            else
                std::tuple_element_t<First, members>::read(input, value, context...);
        }
    }

    template<std::size_t... Indices, class... Context>
    static void read_fields(reader& input, T& value, std::index_sequence<Indices...>, Context&... context)
    {
        (read_field_or_batch<Indices>(input, value, context...), ...);
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

    template<class... Context>
    static value_type read(reader& input, Context&... context)
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
        auto result = payload::read(next, context...);
        input = next;
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if (value)
            write_body(output, *value, context...);
        else
            write_object_header(output, std::nullopt);
    }

    /// Populates an already registered shell after reading its complete positional body.
    template<class... Context>
    static void read_into(reader& input, T& destination, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        auto value = read(next, context...);
        if (!value)
            throw wire_error("A LuminPack reference definition must contain a non-null record.");
        destination = std::move(*value);
        input = next;
        transaction.commit();
    }

    /// Writes the complete positional record body, including its ordinary member-count marker.
    template<class... Context>
    static void write_body(writer& output, const T& value, Context&... context)
    {
        writer next = output;
        write_object_header(next, static_cast<std::uint8_t>(sizeof...(Members)));
        payload::write(next, value, context...);
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

    template<class... Context>
    static value_type read(reader& input, Context&... context)
    {
        reader next = input;
        const auto hash = next.read<std::uint32_t>();
        if (hash == 0)
        {
            input = next;
            return std::nullopt;
        }
        auto transaction = detail::version_transaction(context...);
        auto result = read_case(hash, next, indices{}, context...);
        input = next;
        transaction.commit();
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if (!value)
        {
            output.write<std::uint32_t>(0);
            return;
        }

        writer next = output;
        auto transaction = detail::version_transaction(context...);
        if (!write_case(next, *value, indices{}, context...))
            throw std::invalid_argument("LuminPack Union does not have an active case.");
        output = next;
        transaction.commit();
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

    template<std::size_t Index, class... Context>
    static bool read_if(value_type& result, std::uint32_t hash, reader& input, Context&... context)
    {
        using item = std::tuple_element_t<Index, cases>;
        if (hash != item::hash)
            return false;
        result.emplace(std::in_place_index<Index>, detail::read_codec<typename item::codec>(input, context...));
        return true;
    }

    template<std::size_t... Index, class... Context>
    static value_type read_case(std::uint32_t hash, reader& input, std::index_sequence<Index...>, Context&... context)
    {
        value_type result;
        if (!(read_if<Index>(result, hash, input, context...) || ...))
            throw wire_error("Unknown LuminPack Union case hash.");
        return result;
    }

    template<std::size_t Index, class... Context>
    static bool write_if(writer& output, const variant_type& value, Context&... context)
    {
        using item = std::tuple_element_t<Index, cases>;
        if (value.index() != Index)
            return false;
        output.write(item::hash);
        detail::write_codec<typename item::codec>(output, std::get<Index>(value), context...);
        return true;
    }

    template<std::size_t... Index, class... Context>
    static bool write_case(writer& output, const variant_type& value, std::index_sequence<Index...>, Context&... context)
    {
        return (write_if<Index>(output, value, context...) || ...);
    }
};
}
