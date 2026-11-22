#pragma once

// Use luminpack.h to include the public JSON codecs alongside the binary codecs.
namespace luminpack
{
/// The JSON representation of a closed binary scalar, string, sequence, or map codec.
template<class Binary>
struct json_codec;

template<scalar T>
struct json_codec<scalar_codec<T>>
{
    using value_type = T;

    static T read(auto& input)
    {
        if constexpr (std::same_as<T, char16_t>)
        {
            if constexpr (requires { input.read_character(); })
                return input.read_character();
            else
            {
                const auto text = input.read_string();
                if (text.size() != 1)
                    throw wire_error("Expected one UTF-16 code unit in a LuminPack JSON character.");
                return text.front();
            }
        }
        else
            return input.template read_number<T>();
    }

    static void write(auto& output, T value)
    {
        if constexpr (std::same_as<T, char16_t>)
        {
            if constexpr (requires { output.write_character(value); })
                output.write_character(value);
            else
                output.write_string(std::u16string_view(&value, 1));
        }
        else
            output.write_number(value);
    }
};

/// The CLR native integer JSON formatter parses a 64-bit number before the native-width conversion.
template<class T>
requires (std::same_as<T, std::intptr_t> || std::same_as<T, std::uintptr_t>)
struct json_native_integer_codec
{
    using value_type = T;
    using wide_type = std::conditional_t<std::is_signed_v<T>, std::int64_t, std::uint64_t>;

    static T read(auto& input) { return static_cast<T>(input.template read_number<wide_type>()); }
    static void write(auto& output, T value) { output.write_number(static_cast<wide_type>(value)); }
};

template<>
struct json_codec<boolean_codec>
{
    using value_type = bool;
    static bool read(auto& input) { return input.read_bool(); }
    static void write(auto& output, bool value) { output.write_bool(value); }
};

template<class T>
requires std::is_enum_v<T> && scalar<std::underlying_type_t<T>>
struct json_codec<enum_codec<T>>
{
    using value_type = T;
    using underlying = std::underlying_type_t<T>;

    static T read(auto& input) { return static_cast<T>(input.template read_number<underlying>()); }
    static void write(auto& output, T value) { output.write_number(static_cast<underlying>(value)); }
};

/// Represents JSON null independently of the wrapped value.
template<class Inner>
struct json_optional_codec
{
    using value_type = std::optional<typename Inner::value_type>;

    static value_type read(auto& input)
    {
        if (input.consume_null())
            return std::nullopt;
        return Inner::read(input);
    }

    static void write(auto& output, const value_type& value)
    {
        if (value)
            Inner::write(output, *value);
        else
            output.write_null();
    }
};

struct json_string_codec
{
    using value_type = std::u16string;
    static value_type read(auto& input) { return input.read_string(); }
    static void write(auto& output, std::u16string_view value) { output.write_string(value); }
};

template<string_profile Profile>
struct json_codec<string_codec<Profile>> : json_optional_codec<json_string_codec>
{
};

template<string_profile Profile>
struct json_codec<required_codec<string_codec<Profile>>> : json_string_codec
{
};

/// A JSON array of elements encoded by a concrete JSON codec.
template<class Element>
struct json_list_codec
{
    using element_type = typename Element::value_type;
    using value_type = std::optional<std::vector<element_type>>;

    static value_type read(auto& input)
    {
        value_type result;
        read_into(input, result);
        return result;
    }

    static void read_into(auto& input, value_type& destination)
    {
        if (input.consume_null())
        {
            destination.reset();
            return;
        }
        const auto container = input.begin('[');
        auto& values = destination ? *destination : destination.emplace();
        values.clear();
        if (input.consume(']'))
            return;
        do
            values.emplace_back(Element::read(input));
        while (input.consume(','));
        input.expect(']');
    }

    static void write(auto& output, const value_type& value)
    {
        if (!value)
        {
            output.write_null();
            return;
        }
        write_elements(output, *value);
    }

    static void write_elements(auto& output, const std::vector<element_type>& value)
    {
        output.token('[');
        bool first = true;
        for (const auto& item : value)
        {
            if (!std::exchange(first, false))
                output.token(',');
            Element::write(output, item);
        }
        output.token(']');
    }
};

template<class Element>
struct json_codec<list_codec<Element>> : json_list_codec<json_codec<Element>>
{
};

/// String-key maps write JSON objects. Other key types write an array of two-element pairs.
template<class Key, class Value, class Hash = detail::key_hash<typename Key::value_type>,
    class Equal = detail::key_equal<typename Key::value_type>>
struct json_dictionary_codec
{
    using key_type = typename Key::value_type;
    using dictionary_type = std::unordered_map<key_type, typename Value::value_type, Hash, Equal>;
    using value_type = std::optional<dictionary_type>;
    static constexpr bool string_keys = std::same_as<key_type, std::u16string> ||
        std::same_as<key_type, std::optional<std::u16string>>;

    static value_type read(auto& input)
    {
        value_type result;
        read_into(input, result);
        return result;
    }

    static void read_into(auto& input, value_type& destination)
    {
        if (input.consume_null())
        {
            destination.reset();
            return;
        }

        auto& entries = destination ? *destination : destination.emplace();
        entries.clear();
        if constexpr (string_keys)
        {
            if (input.peek() == '{')
            {
                const auto container = input.begin('{');
                if (input.consume('}'))
                    return;
                do
                {
                    auto key = input.read_string();
                    input.expect(':');
                    entries.insert_or_assign(std::move(key), Value::read(input));
                }
                while (input.consume(','));
                input.expect('}');
                return;
            }
        }

        const auto container = input.begin('[');
        if (input.consume(']'))
            return;
        do
        {
            const auto pair = input.begin('[');
            auto key = Key::read(input);
            if (detail::is_null_key(key))
                throw wire_error("Null key in a LuminPack JSON dictionary.");
            input.expect(',');
            auto value = Value::read(input);
            input.expect(']');
            entries.insert_or_assign(std::move(key), std::move(value));
        }
        while (input.consume(','));
        input.expect(']');
    }

    static void write(auto& output, const value_type& value)
    {
        if (value)
            write_entries(output, *value);
        else
            output.write_null();
    }

    /// Writes entries in traversal order.
    static void write_entries(auto& output, const auto& entries)
    {
        output.token(string_keys ? '{' : '[');
        bool first = true;
        for (const auto& [key, value] : entries)
        {
            if (detail::is_null_key(key))
                throw std::invalid_argument("Null key in a LuminPack JSON dictionary.");
            if (!std::exchange(first, false))
                output.token(',');
            if constexpr (!string_keys)
                output.token('[');
            Key::write(output, key);
            output.token(string_keys ? ':' : ',');
            Value::write(output, value);
            if constexpr (!string_keys)
                output.token(']');
        }
        output.token(string_keys ? '}' : ']');
    }
};

template<class Key, class Value, class Hash, class Equal>
struct json_codec<dictionary_codec<Key, Value, Hash, Equal>>
    : json_dictionary_codec<json_codec<Key>, json_codec<Value>, Hash, Equal>
{
};

/// Compile-time C# property name, also suitable for former-name aliases.
template<std::size_t Size>
struct json_name
{
    char16_t units[Size];

    constexpr json_name(const char16_t (&text)[Size]) noexcept
    {
        for (std::size_t index = 0; index < Size; ++index)
            units[index] = text[index];
    }

    [[nodiscard]] constexpr std::u16string_view view() const noexcept { return {units, Size - 1}; }
};

/// Names and aliases describe JSON properties independently of binary member framing.
template<json_name Name, auto Field, class Codec, json_name... FormerNames>
struct json_record_member
{
    static bool try_read_ordered(auto& input, auto& object, bool first)
        requires requires { input.template try_read_ordered_property<Name>(first); }
    {
        if (!input.template try_read_ordered_property<Name>(first))
            return false;
        read_value(input, object);
        return true;
    }

    static bool read_if(auto& input, const auto& key, auto& object)
    {
        if (!key.matches(Name.view()) && !(key.matches(FormerNames.view()) || ...))
            return false;
        read_value(input, object);
        return true;
    }

    static void write(auto& output, const auto& object, bool& first)
    {
        const bool initial = std::exchange(first, false);
        if constexpr (requires { output.template write_ordered_property<Name>(initial); })
            output.template write_ordered_property<Name>(initial);
        else
        {
            if (!initial)
                output.token(',');
            output.template write_property<Name>();
        }
        Codec::write(output, object.*Field);
    }

    static void write_opening(auto& output, const auto& object)
        requires requires { output.template write_opening_property<Name>(); }
    {
        output.template write_opening_property<Name>();
        Codec::write(output, object.*Field);
    }

private:
    template<class, class...>
    friend struct json_record_body;

    static constexpr auto ordered_name = Name.view();

    static void read_value(auto& input, auto& object)
    {
        object.*Field = Codec::read(input);
    }

    static constexpr bool accepts_name(std::u16string_view key) noexcept
    {
        return key == ordered_name || ((key == FormerNames.view()) || ...);
    }
};

/// A non-null JSON object, including a Union case body or value-type DTO.
template<class T, class... Members>
struct json_record_body
{
    using value_type = T;

    static T read(auto& input)
    {
        return read_with_lookup<void>(input);
    }

    static void write(auto& output, const T& value)
    {
        output.token('{');
        bool first = true;
        (Members::write(output, value, first), ...);
        output.token('}');
    }

protected:
    template<class Lookup>
    static T read_with_lookup(auto& input)
    {
        using reader_type = std::remove_reference_t<decltype(input)>;
        const auto container = input.begin('{');
        T result{};
        bool first = true;
        std::size_t next_property = 0;
        for (;;)
        {
            if constexpr (can_read_ordered_names<reader_type>())
            {
                if constexpr (requires { Lookup::read_int32_runs(input, result, next_property, first); })
                    Lookup::read_int32_runs(input, result, next_property, first);
                using read_member = bool (*)(reader_type&, T&, bool);
                static constexpr std::array<read_member, sizeof...(Members)> ordered{
                    &read_ordered<Members, reader_type>...
                };
                if (next_property < ordered.size() && ordered[next_property](input, result, first))
                {
                    ++next_property;
                    first = false;
                    continue;
                }
            }

            if (first)
            {
                if (input.consume('}'))
                    return result;
            }
            else if (!input.consume(','))
            {
                input.expect('}');
                return result;
            }

            const auto name = input.read_name();
            input.expect(':');
            next_property = 0;
            if constexpr (requires { Lookup::find(name); })
            {
                using read_member = void (*)(reader_type&, T&);
                static constexpr std::array<read_member, sizeof...(Members)> values{
                    &Members::template read_value<reader_type, T>...
                };
                const auto property = Lookup::find(name);
                if (property < values.size())
                {
                    values[property](input, result);
                    next_property = property + 1;
                }
            }
            else if (!((++next_property, static_cast<bool>(Members::read_if(input, name, result))) || ...))
                next_property = 0;
            if (next_property == 0)
                input.skip_value();
            first = false;
        }
    }

private:
    template<class Member, class Input>
    static constexpr bool can_read_ordered = requires(Input& input, T& value) {
        { Member::try_read_ordered(input, value, true) } -> std::same_as<bool>;
        { Member::ordered_name } -> std::convertible_to<std::u16string_view>;
        { Member::accepts_name(Member::ordered_name) } -> std::same_as<bool>;
    };

    template<class Input>
    static consteval bool can_read_ordered_names()
    {
        if constexpr (sizeof...(Members) == 0 || !(can_read_ordered<Members, Input> && ...))
            return false;
        else
        {
            const std::array<std::u16string_view, sizeof...(Members)> names{Members::ordered_name...};
            const std::array<bool (*)(std::u16string_view), sizeof...(Members)> accepts{&Members::accepts_name...};
            // Manual record contracts may share a name. Preserve the first matching member.
            for (std::size_t index = 1; index < names.size(); ++index)
                for (std::size_t earlier = 0; earlier < index; ++earlier)
                    if (accepts[earlier](names[index]))
                        return false;
            return true;
        }
    }

    template<class Member, class Input>
    static bool read_ordered(Input& input, T& value, bool first)
    {
        return Member::try_read_ordered(input, value, first);
    }
};

/// A generated object with compiled fallback name lookup.
template<class T, class Lookup, class... Members>
struct json_lookup_record_body : json_record_body<T, Members...>
{
    static T read(auto& input)
    {
        return json_record_body<T, Members...>::template read_with_lookup<Lookup>(input);
    }

    static void write(auto& output, const T& value)
    {
        if constexpr (sizeof...(Members) == 0)
            body::write(output, value);
        else
            write_members<Members...>(output, value);
    }

private:
    using body = json_record_body<T, Members...>;

    template<class First, class... Rest>
    static void write_members(auto& output, const T& value)
    {
        if constexpr (requires { First::write_opening(output, value); })
        {
            First::write_opening(output, value);
            [[maybe_unused]] bool first = false;
            (Rest::write(output, value, first), ...);
            output.token('}');
        }
        else
            body::write(output, value);
    }
};

/// JSON object codec for a nullable class DTO.
template<class T, class... Members>
using json_record_codec = json_optional_codec<json_record_body<T, Members...>>;

/// Binds app-owned JSON methods matching a Custom wire representation.
template<class T, auto Read, auto Write>
struct json_custom_codec
{
    using value_type = T;
    static T read(auto& input) { return Read(input); }
    static void write(auto& output, const T& value) { Write(output, value); }
};

/// An application specializes this for an exported Custom identity and supplies its binary and optional JSON codec types.
template<json_name Identity>
struct custom_binding;

namespace detail
{
template<class T, class Codec = void>
struct registered_custom_json_codec
{
    using value_type = T;

    template<json_encoding Encoding>
    static T read(basic_json_reader<Encoding>& input)
    {
        if constexpr (Encoding == json_encoding::utf16 && !std::same_as<Codec, void>)
            return Codec::read(input);
        else
            throw wire_error("No Custom JSON formatter is registered for this encoding.");
    }

    template<json_encoding Encoding>
    static void write(basic_json_writer<Encoding>& output, const T& value)
    {
        if constexpr (Encoding == json_encoding::utf16 && !std::same_as<Codec, void>)
            Codec::write(output, value);
        else
            throw wire_error("No Custom JSON formatter is registered for this encoding.");
    }
};

inline constexpr auto json_union_type_name = json_name{u"$type"};
inline constexpr auto json_union_value_name = json_name{u"$value"};

template<auto Name>
bool matches_json_union_name(const auto& name)
{
    if constexpr (requires { name.template matches<Name>(); })
        return name.template matches<Name>();
    else
        return name.matches(Name.view());
}

template<class Value, class ReadCase>
Value read_json_union_envelope(auto& input, ReadCase read_case)
{
    if (input.consume_null())
        return {};
    const auto container = input.begin('{');
    std::optional<std::uint32_t> hash;
    std::optional<std::remove_reference_t<decltype(input)>> deferred;
    Value result;
    if (input.consume('}'))
        throw wire_error("Incomplete LuminPack JSON Union.");
    do
    {
        const auto name = input.read_name();
        input.expect(':');
        if (matches_json_union_name<json_union_type_name>(name))
        {
            if (hash)
                throw wire_error("Duplicate LuminPack JSON Union type.");
            hash = input.template read_number<std::uint32_t>();
        }
        else if (matches_json_union_name<json_union_value_name>(name))
        {
            if (deferred || result)
                throw wire_error("Duplicate LuminPack JSON Union payload.");
            if (hash)
                result = read_case(input, *hash);
            else
                deferred.emplace(input.take_value());
        }
        else
            input.skip_value();
    }
    while (input.consume(','));
    input.expect('}');
    if (!hash)
        throw wire_error("LuminPack JSON Union has no case type.");
    if (deferred)
    {
        result = read_case(*deferred, *hash);
        deferred->require_end();
    }
    if (!result)
        throw wire_error("LuminPack JSON Union has no payload.");
    return result;
}
}

/// Next-generated JSON case hashes select their concrete object codecs.
template<class... Cases>
struct json_union_codec
{
    using variant_type = std::variant<typename Cases::value_type...>;
    using value_type = std::optional<variant_type>;

    static value_type read(auto& input)
    {
        return detail::read_json_union_envelope<value_type>(input, [](auto& payload, std::uint32_t hash)
        {
            return read_case(payload, hash, indices{});
        });
    }

    static void write(auto& output, const value_type& value)
    {
        if (!value)
        {
            output.write_null();
            return;
        }
        if (!write_case(output, *value, indices{}))
            throw std::invalid_argument("LuminPack JSON Union does not have an active case.");
    }

private:
    using cases = std::tuple<Cases...>;
    using indices = std::index_sequence_for<Cases...>;

    template<std::size_t Index>
    static bool read_if(auto& input, std::uint32_t hash, value_type& result)
    {
        using item = std::tuple_element_t<Index, cases>;
        if (hash != item::hash)
            return false;
        result.emplace(std::in_place_index<Index>, item::codec::read(input));
        return true;
    }

    template<std::size_t... Index>
    static value_type read_case(auto& input, std::uint32_t hash, std::index_sequence<Index...>)
    {
        value_type result;
        if (!(read_if<Index>(input, hash, result) || ...))
            throw wire_error("Unknown LuminPack JSON Union case hash.");
        return result;
    }

    template<std::size_t Index>
    static bool write_if(auto& output, const variant_type& value)
    {
        using item = std::tuple_element_t<Index, cases>;
        if (value.index() != Index)
            return false;
        output.token('{');
        output.template write_property<detail::json_union_type_name>();
        output.write_number(item::hash);
        output.token(',');
        output.template write_property<detail::json_union_value_name>();
        item::codec::write(output, std::get<Index>(value));
        output.token('}');
        return true;
    }

    template<std::size_t... Index>
    static bool write_case(auto& output, const variant_type& value, std::index_sequence<Index...>)
    {
        return (write_if<Index>(output, value) || ...);
    }
};

/// Reads one complete JSON document without allocating a generic syntax tree.
template<class Codec, json_encoding Encoding = json_encoding::utf16>
[[nodiscard]] typename Codec::value_type read_json_document(std::span<const std::byte> input, json_read_options options = {})
{
    detail::check_document_size(input.size(), options.max_input_bytes);
    basic_json_reader<Encoding> cursor(input, options);
    auto result = Codec::read(cursor);
    cursor.require_end();
    return result;
}

/// Writes a compact JSON document into caller-supplied storage.
template<class Codec, json_encoding Encoding = json_encoding::utf16>
[[nodiscard]] std::span<const std::byte> write_json_document(
    std::span<std::byte> output, const typename Codec::value_type& value)
{
    basic_json_writer<Encoding> cursor(output);
    Codec::write(cursor, value);
    return cursor.written();
}
}
