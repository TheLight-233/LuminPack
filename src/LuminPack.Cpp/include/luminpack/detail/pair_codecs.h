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
}
