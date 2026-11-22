#pragma once

#include <algorithm>
#include <cmath>
#include <functional>
#include <ranges>
#include <string>
#include <unordered_map>
#include <utility>

#include "utf16_short.h"

// Included by luminpack.h after the cursor and raw-storage declarations.
namespace luminpack
{
/// Closed binary codec for a raw numeric or character value.
template<scalar T>
struct scalar_codec
{
    using value_type = T;
    static constexpr std::size_t minimum_size = sizeof(T);

    static T read(reader& input) { return input.read<T>(); }
    static void write(writer& output, T value) { output.write(value); }
};

/// A Boolean uses one checked wire byte per value.
struct boolean_codec
{
    using value_type = bool;
    static constexpr std::size_t minimum_size = 1;

    static bool read(reader& input) { return input.read_bool(); }
    static void write(writer& output, bool value) { output.write_bool(value); }
};

/// Declared C# enum values use their underlying width, including unnamed values.
template<class T>
requires std::is_enum_v<T> && scalar<std::underlying_type_t<T>>
struct enum_codec
{
    using value_type = T;
    using underlying_type = std::underlying_type_t<T>;
    static constexpr std::size_t minimum_size = sizeof(underlying_type);

    static T read(reader& input) { return static_cast<T>(input.read<underlying_type>()); }
    static void write(writer& output, T value) { output.write(static_cast<underlying_type>(value)); }
};

/// A string codec bound to one of the six built-in profiles.
template<string_profile Profile = string_profile::utf16_length>
struct string_codec
{
    using value_type = std::optional<std::u16string>;
    static constexpr std::size_t minimum_size =
        Profile == string_profile::utf8_length ? 8 : Profile == string_profile::utf16_length ? 4 :
        Profile == string_profile::utf8_varint || Profile == string_profile::utf16_token ? 2 : 1;

    static value_type read(reader& input) { return read_string(input, Profile); }

    static void write(writer& output, const value_type& value)
    {
        const auto text = value ? std::optional<std::u16string_view>(*value) : std::nullopt;
        if constexpr (Profile == string_profile::utf16_length)
            detail::write_utf16_length(output, text);
        else
            write_string(output, Profile, text);
    }
};

/// A non-null collection member, such as a string key in Dictionary<string,T>.
template<class Nullable>
struct required_codec;

template<string_profile Profile>
struct required_codec<string_codec<Profile>>
{
    using value_type = std::u16string;
    static constexpr auto minimum_size = string_codec<Profile>::minimum_size;

    static value_type read(reader& input)
    {
        auto value = read_string(input, Profile);
        if (!value)
            throw wire_error("Null LuminPack string in a required member.");
        return std::move(*value);
    }

    static void write(writer& output, std::u16string_view value)
    {
        if constexpr (Profile == string_profile::utf16_length)
            detail::write_utf16_length(output, value);
        else
            write_string(output, Profile, value);
    }
};

/// Transfers a native result after the generated codec proves the complete object representation.
/// Restrict raw members to representations that admit every bit pattern.
template<class T>
struct raw_struct_codec
{
    static_assert(std::is_trivially_copyable_v<T> && std::is_standard_layout_v<T>);
    using value_type = T;
    static constexpr std::size_t minimum_size = sizeof(T);

    static T read(reader& input)
    {
        const auto source = input.take(sizeof(T));
        T value;
        std::memcpy(&value, source.data(), sizeof(T));
        return value;
    }

    static void write(writer& output, const T& value)
    {
        const auto target = output.reserve(sizeof(T));
        std::memmove(target.data(), &value, sizeof(T));
    }
};

namespace detail
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
inline constexpr auto utf16_length_header_size = sizeof(std::int32_t);

inline std::size_t short_latin1_length(std::span<const std::byte> available) noexcept
{
    if (available.size() < utf16_length_header_size)
        return 0;
    std::int32_t header;
    std::memcpy(&header, available.data(), sizeof(header));
    return header >= 9 && header <= 33 && (header & 1) != 0 ? static_cast<std::size_t>(header >> 1) : 0;
}

std::size_t read_short_utf16_length_prefix(reader& input, std::size_t count, std::size_t first_length,
    std::vector<std::optional<std::u16string>>& destination);
#endif

template<class Codec>
inline constexpr bool contiguous_codec = false;

template<class T>
inline constexpr bool contiguous_codec<raw_struct_codec<T>> = true;

template<scalar T>
inline constexpr bool contiguous_codec<scalar_codec<T>> = true;

template<class T>
requires std::is_enum_v<T> && scalar<std::underlying_type_t<T>>
inline constexpr bool contiguous_codec<enum_codec<T>> = true;

template<class T>
bool is_null_key(const T&) noexcept
{
    return false;
}

template<class T>
bool is_null_key(const std::optional<T>& value) noexcept
{
    return !value;
}

/// Match .NET dictionary equality for floating-point NaNs and signed zero.
template<class T>
struct key_equal
{
    bool operator()(const T& left, const T& right) const
    {
        if constexpr (std::floating_point<T>)
            return left == right || (std::isnan(left) && std::isnan(right));
        else
            return std::equal_to<T>{}(left, right);
    }
};

template<class T>
struct key_hash
{
    std::size_t operator()(const T& value) const
    {
        if constexpr (std::floating_point<T>)
        {
            if (std::isnan(value))
                return std::hash<T>{}(std::numeric_limits<T>::quiet_NaN());
        }
        return std::hash<T>{}(value);
    }
};
}

/// Codec for one-dimensional arrays and List<T> with the supplied element wire contract.
template<class Element>
struct list_codec
{
    using element_type = typename Element::value_type;
    using value_type = std::optional<std::vector<element_type>>;
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);

    template<class... Context>
    [[nodiscard]] static value_type read(reader& input, Context&... context)
    {
        value_type result;
        read_into(input, result, context...);
        return result;
    }

    /// Reuses the vector allocation. Element-wise decoding can publish a prefix on error.
    template<class... Context>
    static void read_into(reader& input, value_type& destination, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        const auto count = next.read_collection_count(Element::minimum_size);
        if (!count)
        {
            destination.reset();
            input = next;
            return;
        }

        auto& items = destination ? *destination : destination.emplace();
        if constexpr (detail::contiguous_codec<Element>)
        {
            auto payload = next.take(*count * sizeof(element_type));
            std::vector<std::byte> scratch;
            if (*count != items.size() && detail::overlaps(payload, std::as_bytes(std::span(items))))
            {
                scratch.assign(payload.begin(), payload.end());
                payload = scratch;
            }
            items.resize(*count);
            if (!payload.empty())
                std::memmove(items.data(), payload.data(), payload.size());
        }
        else
        {
            items.clear();
            items.reserve(std::min(*count, std::size_t{128}));
            std::size_t index = 0;
#if defined(LUMINPACK_CPP_INLINE_SSE2)
            if constexpr (std::same_as<Element, string_codec<string_profile::utf16_length>>)
            {
                if (*count != 0)
                {
                    const auto length = detail::short_latin1_length(next.unread());
                    if (length != 0)
                        index = detail::read_short_utf16_length_prefix(next, *count, length, items);
                }
            }
#endif
            for (; index < *count; ++index)
                items.emplace_back(detail::read_codec<Element>(next, context...));
        }
        input = next;
        transaction.commit();
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if (!value)
        {
            output.write<std::int32_t>(-1);
            return;
        }
        write_elements(output, *value, context...);
    }

    template<class... Context>
    static void write_elements(writer& output, const std::vector<element_type>& value, Context&... context)
    {
        writer next = output;
        const auto count = detail::collection_count(value.size());
        auto transaction = detail::version_transaction(context...);
        if constexpr (detail::contiguous_codec<Element>)
        {
            const auto source = std::as_bytes(std::span(value));
            if (next.remaining() < minimum_size || source.size() > next.remaining() - minimum_size)
                detail::short_output();
            const auto record = next.reserve(minimum_size + source.size());
            if (!source.empty())
                std::memmove(record.data() + minimum_size, source.data(), source.size());
            detail::store_count(record, count);
        }
        else
        {
            next.write(count);
            for (const auto& item : value)
                detail::write_codec<Element>(next, item, context...);
        }
        output = next;
        transaction.commit();
    }
};

/// Dictionary<TKey,TValue> uses a count followed by key/value pairs in wire order.
template<class Key, class Value, class Hash = detail::key_hash<typename Key::value_type>,
    class Equal = detail::key_equal<typename Key::value_type>>
struct dictionary_codec
{
    using key_type = typename Key::value_type;
    using mapped_type = typename Value::value_type;
    using dictionary_type = std::unordered_map<key_type, mapped_type, Hash, Equal>;
    using value_type = std::optional<dictionary_type>;
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);
    static constexpr std::size_t pair_minimum_size = Key::minimum_size + Value::minimum_size;

    template<class... Context>
    [[nodiscard]] static value_type read(reader& input, Context&... context)
    {
        value_type result;
        read_into(input, result, context...);
        return result;
    }

    /// Reuses the dictionary's existing buckets and validates null and duplicate keys.
    template<class... Context>
    static void read_into(reader& input, value_type& destination, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        const auto count = next.read_collection_count(pair_minimum_size);
        if (!count)
        {
            destination.reset();
            input = next;
            return;
        }

        auto& entries = destination ? *destination : destination.emplace();
        entries.clear();
        if (static_cast<float>(entries.bucket_count()) * entries.max_load_factor() < std::min(*count, std::size_t{128}))
            entries.reserve(std::min(*count, std::size_t{128}));
        for (std::size_t index = 0; index < *count; ++index)
        {
            auto key = detail::read_codec<Key>(next, context...);
            if (detail::is_null_key(key))
                throw wire_error("Null LuminPack dictionary key.");
            auto value = detail::read_codec<Value>(next, context...);
            if (!entries.try_emplace(std::move(key), std::move(value)).second)
                throw wire_error("Duplicate LuminPack dictionary key.");
        }
        input = next;
        transaction.commit();
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if (value)
            write_entries(output, *value, context...);
        else
            output.write<std::int32_t>(-1);
    }

    /// Writes existing unique entries in traversal order, including a caller-defined order.
    template<class Entries, class... Context>
    requires std::ranges::sized_range<const Entries>
    static void write_entries(writer& output, const Entries& entries, Context&... context)
    {
        writer next = output;
        auto transaction = detail::version_transaction(context...);
        next.write(detail::collection_count(std::ranges::size(entries)));
        for (const auto& [key, value] : entries)
        {
            if (detail::is_null_key(key))
                throw std::invalid_argument("Null LuminPack dictionary key.");
            detail::write_codec<Key>(next, key, context...);
            detail::write_codec<Value>(next, value, context...);
        }
        output = next;
        transaction.commit();
    }
};
}
