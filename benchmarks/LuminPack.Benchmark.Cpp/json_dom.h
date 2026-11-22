#pragma once

#include "rapid_json.h"

namespace json_dom
{
template<class Unit>
struct source
{
    using Ch = Unit;
    std::basic_string_view<Unit> data;
    std::size_t position = 0;

    Unit Take() { return position < data.size() ? data[position++] : Unit{}; }
    Unit Peek() const { return position < data.size() ? data[position] : Unit{}; }
};

template<class Unit>
struct destination
{
    using Ch = Unit;
    std::basic_string<Unit> data;

    void Put(Unit character) { data.push_back(character); }
};

template<class From, class To>
std::basic_string<typename To::Ch> transcode(std::basic_string_view<typename From::Ch> data)
{
    source<typename From::Ch> input{data};
    destination<typename To::Ch> output;
    output.data.reserve(data.size());
    while (input.position < data.size())
    {
        if (!rapidjson::Transcoder<From, To>::Transcode(input, output))
            throw std::runtime_error("Invalid Unicode in JSON benchmark data.");
    }
    return std::move(output.data);
}

inline std::string to_utf8(std::u16string_view value)
{
    return transcode<rapid_reference::encoding, rapid_reference::utf8>(value);
}

inline std::u16string to_utf16(std::string_view value)
{
    return transcode<rapid_reference::utf8, rapid_reference::encoding>(value);
}

template<luminpack::json_name Name>
consteval auto make_name_utf8()
{
    // The fixture models use ASCII property names. Dictionary keys use transcoding.
    std::array<char, Name.view().size()> result{};
    for (std::size_t index = 0; index < result.size(); ++index)
    {
        const auto unit = Name.view()[index];
        if (unit >= 0x80)
            throw std::logic_error("The fixture property name must be ASCII.");
        result[index] = static_cast<char>(unit);
    }
    return result;
}

template<luminpack::json_name Name>
inline constexpr auto name_utf8 = make_name_utf8<Name>();

template<luminpack::json_name Name>
constexpr std::string_view property_name() { return {name_utf8<Name>.data(), name_utf8<Name>.size()}; }

template<class Codec>
struct decode;

template<class T>
struct number
{
    template<class Dom>
    static T read(typename Dom::node input)
    {
        using wide = std::conditional_t<std::is_floating_point_v<T>, double,
            std::conditional_t<std::is_signed_v<T>, std::int64_t, std::uint64_t>>;
        const auto parsed = Dom::template number<wide>(input);
        if constexpr (std::is_integral_v<T>)
            if (!std::in_range<T>(parsed))
                throw std::runtime_error("Unexpected JSON integer width.");
        return static_cast<T>(parsed);
    }
};

template<luminpack::scalar T>
struct decode<luminpack::json_codec<luminpack::scalar_codec<T>>> : number<T> {};

template<>
struct decode<luminpack::json_codec<luminpack::boolean_codec>>
{
    template<class Dom>
    static bool read(typename Dom::node input) { return Dom::boolean(input); }
};

template<class Enum>
struct decode<luminpack::json_codec<luminpack::enum_codec<Enum>>>
{
    template<class Dom>
    static Enum read(typename Dom::node input)
    {
        return static_cast<Enum>(number<std::underlying_type_t<Enum>>::template read<Dom>(input));
    }
};

template<class Codec>
struct decode<luminpack::json_optional_codec<Codec>>
{
    template<class Dom>
    static std::optional<typename Codec::value_type> read(typename Dom::node input)
    {
        if (Dom::is_null(input))
            return std::nullopt;
        return decode<Codec>::template read<Dom>(input);
    }
};

template<>
struct decode<luminpack::json_string_codec>
{
    template<class Dom>
    static std::u16string read(typename Dom::node input) { return to_utf16(Dom::string(input)); }
};

template<luminpack::string_profile Profile>
struct decode<luminpack::json_codec<luminpack::string_codec<Profile>>>
    : decode<luminpack::json_optional_codec<luminpack::json_string_codec>> {};

template<luminpack::string_profile Profile>
struct decode<luminpack::json_codec<luminpack::required_codec<luminpack::string_codec<Profile>>>>
    : decode<luminpack::json_string_codec> {};

template<class Element>
struct decode<luminpack::json_list_codec<Element>>
{
    using list = typename luminpack::json_list_codec<Element>::value_type;

    template<class Dom>
    static list read(typename Dom::node input)
    {
        if (Dom::is_null(input))
            return std::nullopt;
        list result(std::in_place);
        result->reserve(Dom::array_size(input));
        Dom::items(input, [&](typename Dom::node item) { result->push_back(decode<Element>::template read<Dom>(item)); });
        return result;
    }
};

template<class Element>
struct decode<luminpack::json_codec<luminpack::list_codec<Element>>>
    : decode<luminpack::json_list_codec<luminpack::json_codec<Element>>> {};

template<class Key, class Item, class Hash, class Equal>
struct decode<luminpack::json_dictionary_codec<Key, Item, Hash, Equal>>
{
    using codec = luminpack::json_dictionary_codec<Key, Item, Hash, Equal>;

    template<class Dom>
    static typename codec::value_type read(typename Dom::node input)
    {
        if (Dom::is_null(input))
            return std::nullopt;
        typename codec::value_type result(std::in_place);
        if constexpr (codec::string_keys)
        {
            result->reserve(Dom::object_size(input));
            Dom::members(input, [&](std::string_view key, typename Dom::node value)
            {
                result->insert_or_assign(to_utf16(key), decode<Item>::template read<Dom>(value));
            });
        }
        else
        {
            result->reserve(Dom::array_size(input));
            Dom::items(input, [&](typename Dom::node pair)
            {
                rapid_reference::require(Dom::array_size(pair) == 2);
                const auto key = decode<Key>::template read<Dom>(Dom::at(pair, 0));
                result->insert_or_assign(key, decode<Item>::template read<Dom>(Dom::at(pair, 1)));
            });
        }
        return result;
    }
};

template<class Key, class Item, class Hash, class Equal>
struct decode<luminpack::json_codec<luminpack::dictionary_codec<Key, Item, Hash, Equal>>>
    : decode<luminpack::json_dictionary_codec<luminpack::json_codec<Key>, luminpack::json_codec<Item>, Hash, Equal>> {};

template<luminpack::json_name Name, auto Field, class Codec, luminpack::json_name... Aliases>
struct decode<luminpack::json_record_member<Name, Field, Codec, Aliases...>>
{
    template<class Dom>
    static bool read_if(std::string_view key, typename Dom::node input, auto& result)
    {
        if (key != property_name<Name>() && ((key != property_name<Aliases>()) && ...))
            return false;
        result.*Field = decode<Codec>::template read<Dom>(input);
        return true;
    }
};

template<luminpack::json_name Name, auto Field, class Codec, luminpack::json_name... Aliases>
struct decode<luminpack::detail::generated_json_integer_member<Name, Field, Codec, Aliases...>>
    : decode<luminpack::json_record_member<Name, Field, Codec, Aliases...>> {};

template<class T, class... Fields>
struct decode<luminpack::json_record_body<T, Fields...>>
{
    template<class Dom>
    static T read(typename Dom::node input)
    {
        T result{};
        Dom::members(input, [&](std::string_view key, typename Dom::node value)
        {
            static_cast<void>((decode<Fields>::template read_if<Dom>(key, value, result) || ...));
        });
        return result;
    }
};

template<class T, class Lookup, class... Fields>
struct decode<luminpack::json_lookup_record_body<T, Lookup, Fields...>>
    : decode<luminpack::json_record_body<T, Fields...>> {};

template<class... Cases>
struct decode<luminpack::json_union_codec<Cases...>>
{
    using value_type = typename luminpack::json_union_codec<Cases...>::value_type;

    template<class Dom>
    static value_type read(typename Dom::node input)
    {
        if (Dom::is_null(input))
            return std::nullopt;
        const auto hash = number<std::uint32_t>::template read<Dom>(Dom::member(input, "$type"));
        decltype(auto) value = Dom::member(input, "$value");
        value_type result;
        [&]<std::size_t... I>(std::index_sequence<I...>)
        {
            rapid_reference::require((read_case<Dom, Cases, I>(hash, value, result) || ...));
        }(std::index_sequence_for<Cases...>{});
        return result;
    }

private:
    template<class Dom, class Case, std::size_t Index>
    static bool read_case(std::uint32_t hash, typename Dom::node value, value_type& result)
    {
        if (hash != Case::hash)
            return false;
        result.emplace(std::in_place_index<Index>, decode<typename Case::codec>::template read<Dom>(value));
        return true;
    }
};
}
