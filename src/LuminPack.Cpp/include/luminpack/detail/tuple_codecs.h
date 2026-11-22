#pragma once

#include <tuple>

// Included by luminpack.h after the binary and JSON codecs.
namespace luminpack
{
/// The element-wise or packed CLR ValueTuple writes its ordered components without an object header.
template<class... Elements>
struct tuple_codec
{
    using value_type = std::tuple<typename Elements::value_type...>;
    static constexpr auto minimum_size = (std::size_t{0} + ... + Elements::minimum_size);
    static_assert(sizeof...(Elements) >= 1 && sizeof...(Elements) <= 8);

    template<class... Context>
    static value_type read(reader& input, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        value_type result{detail::read_codec<Elements>(next, context...)...};
        input = next;
        transaction.commit();
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        writer next = output;
        auto transaction = detail::version_transaction(context...);
        std::apply([&](const auto&... items) { (detail::write_codec<Elements>(next, items, context...), ...); }, value);
        output = next;
        transaction.commit();
    }
};

/// The JSON ValueTuple uses an array, with a nested eighth Rest tuple when present.
template<class... Elements>
struct json_tuple_codec
{
    using value_type = std::tuple<typename Elements::value_type...>;
    static_assert(sizeof...(Elements) >= 1 && sizeof...(Elements) <= 8);

    static value_type read(auto& input)
    {
        const auto array = input.begin('[');
        value_type result = read_elements(input, std::index_sequence_for<Elements...>{});
        input.expect(']');
        return result;
    }

    static void write(auto& output, const value_type& value)
    {
        output.token('[');
        write_elements(output, value, std::index_sequence_for<Elements...>{});
        output.token(']');
    }

private:
    template<std::size_t Index, class Element>
    static typename Element::value_type read_element(auto& input)
    {
        if constexpr (Index != 0)
            input.expect(',');
        return Element::read(input);
    }

    template<std::size_t... Indices>
    static value_type read_elements(auto& input, std::index_sequence<Indices...>)
    {
        return {read_element<Indices, Elements>(input)...};
    }

    template<std::size_t Index, class Element>
    static void write_element(auto& output, const typename Element::value_type& value)
    {
        if constexpr (Index != 0)
            output.token(',');
        Element::write(output, value);
    }

    template<std::size_t... Indices>
    static void write_elements(auto& output, const value_type& value, std::index_sequence<Indices...>)
    {
        (write_element<Indices, Elements>(output, std::get<Indices>(value)), ...);
    }
};

template<class... Elements>
struct json_codec<tuple_codec<Elements...>> : json_tuple_codec<json_codec<Elements>...>
{
};

namespace detail
{
template<class Codec>
struct tuple_storage
{
    using value_type = typename Codec::value_type;
    static constexpr bool is_boolean = std::is_same_v<Codec, boolean_codec>;
    static constexpr std::size_t width = is_boolean ? 1 : sizeof(value_type);
    static_assert(is_boolean || scalar<value_type> || std::is_enum_v<value_type>);
    static_assert(Codec::minimum_size == width);
    static constexpr std::array widths{width};
    static constexpr std::array<std::size_t, is_boolean ? 1 : 0> boolean_fields{};

    static void read(value_type& value, const std::byte* source, const auto& offsets, std::size_t& field)
    {
        const auto* bytes = source + offsets[field++];
        if constexpr (is_boolean)
            value = *bytes == std::byte{1};
        else
            std::memcpy(&value, bytes, width);
    }

    static void write(const value_type& value, std::byte* target, const auto& offsets, std::size_t& field)
    {
        auto* bytes = target + offsets[field++];
        if constexpr (is_boolean)
            *bytes = value ? std::byte{1} : std::byte{0};
        else
            std::memcpy(bytes, &value, width);
    }
};

template<class... Elements>
struct tuple_storage<tuple_codec<Elements...>>
{
    using value_type = typename tuple_codec<Elements...>::value_type;
    static constexpr auto widths = [] {
        constexpr auto size = (std::size_t{0} + ... + tuple_storage<Elements>::widths.size());
        std::array<std::size_t, size> result{};
        std::size_t next = 0;
        auto append = [&](const auto& part) {
            for (const auto width : part)
                result[next++] = width;
        };
        (append(tuple_storage<Elements>::widths), ...);
        return result;
    }();
    static constexpr auto boolean_fields = [] {
        constexpr auto count = (std::size_t{0} + ... + tuple_storage<Elements>::boolean_fields.size());
        std::array<std::size_t, count> result{};
        std::size_t base = 0;
        std::size_t next = 0;
        auto append = [&](const auto& fields, std::size_t width_count) {
            for (const auto field : fields)
                result[next++] = base + field;
            base += width_count;
        };
        (append(tuple_storage<Elements>::boolean_fields, tuple_storage<Elements>::widths.size()), ...);
        return result;
    }();

    static void read(value_type& value, const std::byte* source, const auto& offsets, std::size_t& field)
    {
        read_fields(value, source, offsets, field, std::index_sequence_for<Elements...>{});
    }

    static void write(const value_type& value, std::byte* target, const auto& offsets, std::size_t& field)
    {
        write_fields(value, target, offsets, field, std::index_sequence_for<Elements...>{});
    }

private:
    template<std::size_t... Indices>
    static void read_fields(value_type& value, const std::byte* source, const auto& offsets, std::size_t& field,
        std::index_sequence<Indices...>)
    {
        (tuple_storage<Elements>::read(std::get<Indices>(value), source, offsets, field), ...);
    }

    template<std::size_t... Indices>
    static void write_fields(const value_type& value, std::byte* target, const auto& offsets, std::size_t& field,
        std::index_sequence<Indices...>)
    {
        (tuple_storage<Elements>::write(std::get<Indices>(value), target, offsets, field), ...);
    }
};
}

/// A counted ValueTuple block using the CLR's ordered Tagged models for its fixed-width scalar leaves.
template<class Tuple, auto... Models>
struct tagged_tuple_list_codec
{
    using element_type = typename Tuple::value_type;
    using value_type = std::optional<std::vector<element_type>>;
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);

private:
    using storage = detail::tuple_storage<Tuple>;
    using model_type = storage_model<storage::widths.size()>;
    struct wire_layout
    {
        std::array<model_type, sizeof...(Models)> wire;
    };
    static constexpr wire_layout layout{{Models...}};
    static_assert(sizeof...(Models) >= 2 && sizeof...(Models) <= 250);
    static_assert((detail::valid_storage_model(storage::widths, Models) && ...));

public:
    [[nodiscard]] static value_type read(reader& input)
    {
        value_type result;
        read_into(input, result);
        return result;
    }

    static void read_into(reader& input, value_type& destination)
    {
        reader next = input;
        auto block = detail::read_tagged_block<layout>(next);
        if (block.tag != 0)
            detail::check_storage_booleans<storage::boolean_fields>(block.payload, *block.count, layout.wire[block.tag - 1]);
        if (block.count)
        {
            auto& items = destination ? *destination : destination.emplace();
            if (block.payload.empty())
                items.clear();
            else
            {
                const auto& model = layout.wire[block.tag - 1];
                std::vector<std::byte> scratch;
                if (detail::overlaps(block.payload, std::as_bytes(std::span(items))))
                {
                    scratch.assign(block.payload.begin(), block.payload.end());
                    block.payload = scratch;
                }
                items.resize(*block.count);
                for (std::size_t index = 0; index < items.size(); ++index)
                {
                    std::size_t field = 0;
                    storage::read(items[index], block.payload.data() + index * model.stride, model.offsets, field);
                }
            }
        }
        else
            destination.reset();
        input = next;
    }

    static void write(writer& output, const value_type& value)
    {
        if (!value)
        {
            output.write<std::int32_t>(-1);
            return;
        }
        write_elements(output, *value);
    }

    static void write_elements(writer& output, const std::vector<element_type>& value)
    {
        if (value.empty())
        {
            output.write<std::int32_t>(0);
            return;
        }

        // Convert logical fields into the canonical CLR model without depending on std::tuple storage.
        constexpr auto model = layout.wire.front();
        constexpr auto header_size = minimum_size + 1;
        const auto count = detail::collection_count(value.size());
        if (output.remaining() < header_size)
            detail::short_output();
        const auto size = detail::checked_output_width(value.size(), model.stride, output.remaining() - header_size);
        writer next = output;
        const auto target = next.reserve(header_size + size);
        std::span<const element_type> items(value);
        std::vector<element_type> scratch;
        if (detail::overlaps(std::as_bytes(items), target))
        {
            scratch.assign(items.begin(), items.end());
            items = scratch;
        }

        detail::store_count(target, count);
        target[minimum_size] = std::byte{1};
        const auto payload = target.subspan(header_size);
        std::memset(payload.data(), 0, payload.size());
        for (std::size_t index = 0; index < items.size(); ++index)
        {
            std::size_t field = 0;
            storage::write(items[index], payload.data() + index * model.stride, model.offsets, field);
        }
        output = next;
    }
};

template<class Tuple, auto... Models>
struct json_codec<tagged_tuple_list_codec<Tuple, Models...>> : json_list_codec<json_codec<Tuple>>
{
};
}
