#pragma once

#include <utility>

// Included by luminpack.h after the binary and JSON codec declarations.
namespace luminpack
{
/// The standalone CLR KeyValuePair writes the key followed by the value without an object header.
template<class Key, class Value>
struct pair_codec
{
    using value_type = std::pair<typename Key::value_type, typename Value::value_type>;
    static constexpr auto minimum_size = Key::minimum_size + Value::minimum_size;

    template<class... Context>
    static value_type read(reader& input, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        value_type result{detail::read_codec<Key>(next, context...), detail::read_codec<Value>(next, context...)};
        input = next;
        transaction.commit();
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        writer next = output;
        auto transaction = detail::version_transaction(context...);
        detail::write_codec<Key>(next, value.first, context...);
        detail::write_codec<Value>(next, value.second, context...);
        output = next;
        transaction.commit();
    }
};

/// A counted KeyValuePair block using the CLR's ordered Tagged scalar storage models.
template<class Key, class Value, storage_model<2>... Models>
struct tagged_pair_list_codec
{
    using key_type = typename Key::value_type;
    using mapped_type = typename Value::value_type;
    using element_type = typename pair_codec<Key, Value>::value_type;
    using value_type = std::optional<std::vector<element_type>>;
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);

    static_assert(scalar<key_type> || std::is_enum_v<key_type>);
    static_assert(scalar<mapped_type> || std::is_enum_v<mapped_type>);
    static_assert(Key::minimum_size == sizeof(key_type) && Value::minimum_size == sizeof(mapped_type));
    static_assert(sizeof...(Models) >= 2 && sizeof...(Models) <= 250);
    static_assert((detail::valid_storage_model(std::array{sizeof(key_type), sizeof(mapped_type)}, Models) && ...));

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
                    const auto* source = block.payload.data() + index * model.stride;
                    std::memcpy(&items[index].first, source + model.offsets[0], sizeof(key_type));
                    std::memcpy(&items[index].second, source + model.offsets[1], sizeof(mapped_type));
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

        // A std::pair has no raw block contract. Use the canonical CLR model for the converted fields.
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
            auto* record = payload.data() + index * model.stride;
            std::memcpy(record + model.offsets[0], &items[index].first, sizeof(key_type));
            std::memcpy(record + model.offsets[1], &items[index].second, sizeof(mapped_type));
        }
        output = next;
    }

private:
    struct wire_layout
    {
        std::array<storage_model<2>, sizeof...(Models)> wire;
    };

    static constexpr wire_layout layout{{Models...}};
};

/// The JSON KeyValuePair is a two-element array in key, value order.
template<class Key, class Value>
struct json_pair_codec
{
    using value_type = std::pair<typename Key::value_type, typename Value::value_type>;

    static value_type read(auto& input)
    {
        const auto array = input.begin('[');
        auto key = Key::read(input);
        input.expect(',');
        auto value = Value::read(input);
        input.expect(']');
        return {std::move(key), std::move(value)};
    }

    static void write(auto& output, const value_type& value)
    {
        output.token('[');
        Key::write(output, value.first);
        output.token(',');
        Value::write(output, value.second);
        output.token(']');
    }
};

template<class Key, class Value>
struct json_codec<pair_codec<Key, Value>> : json_pair_codec<json_codec<Key>, json_codec<Value>>
{
};

template<class Key, class Value, storage_model<2>... Models>
struct json_codec<tagged_pair_list_codec<Key, Value, Models...>> : json_list_codec<json_codec<pair_codec<Key, Value>>>
{
};
}
