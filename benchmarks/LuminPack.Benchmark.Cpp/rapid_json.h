#pragma once

#include "allocations.h"

#include <luminpack/luminpack.h>
#include <rapidjson/document.h>
#include <rapidjson/stringbuffer.h>
#include <rapidjson/writer.h>

namespace rapid_reference
{
inline void require(bool valid)
{
    if (!valid)
        throw std::runtime_error("Unexpected RapidJSON benchmark data.");
}

struct allocator : rapidjson::CrtAllocator
{
    void* Malloc(std::size_t size)
    {
        auto* result = rapidjson::CrtAllocator::Malloc(size);
        if (result)
            record_allocation(size);
        return result;
    }

    void* Realloc(void* address, std::size_t old, std::size_t size)
    {
        auto* result = rapidjson::CrtAllocator::Realloc(address, old, size);
        if (result)
            record_allocation(size);
        return result;
    }
};

using encoding = rapidjson::UTF16<char16_t>;
using pool = rapidjson::MemoryPoolAllocator<allocator>;
using value = rapidjson::GenericValue<encoding, pool>;
using document = rapidjson::GenericDocument<encoding, pool, allocator>;
using buffer = rapidjson::GenericStringBuffer<encoding, allocator>;
using writer = rapidjson::Writer<buffer, encoding, encoding, allocator>;

inline document parse(const char16_t* input)
{
    document result;
    result.Parse<rapidjson::kParseValidateEncodingFlag | rapidjson::kParseFullPrecisionFlag>(input);
    if (result.HasParseError())
        throw std::runtime_error("RapidJSON parse error at code unit " + std::to_string(result.GetErrorOffset()) + ".");
    return result;
}

template<class Codec>
struct binding;

template<luminpack::scalar T>
struct binding<luminpack::json_codec<luminpack::scalar_codec<T>>>
{
    using number = std::conditional_t<std::is_floating_point_v<T>, double,
        std::conditional_t<std::is_signed_v<T>, std::int64_t, std::uint64_t>>;

    static T read(const value& input)
    {
        if constexpr (std::is_floating_point_v<T>)
            require(input.IsNumber());
        else
            require(input.Is<number>());
        const auto parsed = input.Get<number>();
        if constexpr (std::is_integral_v<T>)
            require(std::in_range<T>(parsed));
        return static_cast<T>(parsed);
    }

    static void write(writer& output, T data)
    {
        if constexpr (std::is_floating_point_v<T>)
            require(output.Double(data));
        else if constexpr (std::is_signed_v<T>)
            output.Int64(data);
        else
            output.Uint64(data);
    }
};

template<>
struct binding<luminpack::json_codec<luminpack::boolean_codec>>
{
    static bool read(const value& input)
    {
        require(input.IsBool());
        return input.GetBool();
    }

    static void write(writer& output, bool data) { output.Bool(data); }
};

template<class Enum>
struct binding<luminpack::json_codec<luminpack::enum_codec<Enum>>>
{
    using underlying = std::underlying_type_t<Enum>;
    using scalar = binding<luminpack::json_codec<luminpack::scalar_codec<underlying>>>;
    static Enum read(const value& input) { return static_cast<Enum>(scalar::read(input)); }
    static void write(writer& output, Enum data) { scalar::write(output, static_cast<underlying>(data)); }
};

template<class Codec>
struct binding<luminpack::json_optional_codec<Codec>>
{
    using optional = std::optional<typename Codec::value_type>;

    static optional read(const value& input)
    {
        if (input.IsNull())
            return std::nullopt;
        return binding<Codec>::read(input);
    }

    static void write(writer& output, const optional& data)
    {
        if (data)
            binding<Codec>::write(output, *data);
        else
            output.Null();
    }
};

template<>
struct binding<luminpack::json_string_codec>
{
    static std::u16string read(const value& input)
    {
        require(input.IsString());
        return {input.GetString(), input.GetStringLength()};
    }

    static void write(writer& output, const std::u16string& data)
    {
        require(output.String(data.data(), static_cast<rapidjson::SizeType>(data.size())));
    }
};

template<luminpack::string_profile Profile>
struct binding<luminpack::json_codec<luminpack::string_codec<Profile>>>
    : binding<luminpack::json_optional_codec<luminpack::json_string_codec>> {};

template<luminpack::string_profile Profile>
struct binding<luminpack::json_codec<luminpack::required_codec<luminpack::string_codec<Profile>>>>
    : binding<luminpack::json_string_codec> {};

template<class Element>
struct binding<luminpack::json_list_codec<Element>>
{
    using list = typename luminpack::json_list_codec<Element>::value_type;

    static list read(const value& input)
    {
        if (input.IsNull())
            return std::nullopt;
        require(input.IsArray());
        list result(std::in_place);
        result->reserve(input.Size());
        for (const auto& item : input.GetArray())
            result->push_back(binding<Element>::read(item));
        return result;
    }

    static void write(writer& output, const list& data)
    {
        if (!data)
        {
            output.Null();
            return;
        }
        output.StartArray();
        for (const auto& item : *data)
            binding<Element>::write(output, item);
        output.EndArray();
    }
};

template<class Element>
struct binding<luminpack::json_codec<luminpack::list_codec<Element>>>
    : binding<luminpack::json_list_codec<luminpack::json_codec<Element>>> {};

template<class Key, class Item, class Hash, class Equal>
struct binding<luminpack::json_dictionary_codec<Key, Item, Hash, Equal>>
{
    using codec = luminpack::json_dictionary_codec<Key, Item, Hash, Equal>;
    using dictionary = typename codec::value_type;

    static dictionary read(const value& input)
    {
        if (input.IsNull())
            return std::nullopt;
        dictionary result(std::in_place);
        if constexpr (codec::string_keys)
        {
            require(input.IsObject());
            result->reserve(input.MemberCount());
            for (const auto& entry : input.GetObject())
                result->insert_or_assign(binding<Key>::read(entry.name), binding<Item>::read(entry.value));
        }
        else
        {
            require(input.IsArray());
            result->reserve(input.Size());
            for (const auto& pair : input.GetArray())
            {
                require(pair.IsArray() && pair.Size() == 2);
                result->insert_or_assign(binding<Key>::read(pair[0u]), binding<Item>::read(pair[1u]));
            }
        }
        return result;
    }

    static void write(writer& output, const dictionary& data)
    {
        if (!data)
        {
            output.Null();
            return;
        }
        if constexpr (codec::string_keys)
            output.StartObject();
        else
            output.StartArray();
        for (const auto& [key, item] : *data)
        {
            if constexpr (!codec::string_keys)
                output.StartArray();
            binding<Key>::write(output, key);
            binding<Item>::write(output, item);
            if constexpr (!codec::string_keys)
                output.EndArray();
        }
        if constexpr (codec::string_keys)
            output.EndObject();
        else
            output.EndArray();
    }
};

template<class Key, class Item, class Hash, class Equal>
struct binding<luminpack::json_codec<luminpack::dictionary_codec<Key, Item, Hash, Equal>>>
    : binding<luminpack::json_dictionary_codec<luminpack::json_codec<Key>, luminpack::json_codec<Item>, Hash, Equal>> {};

template<luminpack::json_name Name, auto Field, class Codec, luminpack::json_name... Aliases>
struct binding<luminpack::json_record_member<Name, Field, Codec, Aliases...>>
{
    static bool read_if(std::u16string_view name, const value& item, auto& result)
    {
        if (name != Name.view() && ((name != Aliases.view()) && ...))
            return false;
        result.*Field = binding<Codec>::read(item);
        return true;
    }

    static void write(writer& output, const auto& data)
    {
        constexpr auto name = Name.view();
        require(output.Key(name.data(), static_cast<rapidjson::SizeType>(name.size())));
        binding<Codec>::write(output, data.*Field);
    }
};

template<class T, class... Fields>
struct binding<luminpack::json_record_body<T, Fields...>>
{
    static T read(const value& input)
    {
        require(input.IsObject());
        T result{};
        for (const auto& entry : input.GetObject())
        {
            const std::u16string_view name(entry.name.GetString(), entry.name.GetStringLength());
            static_cast<void>((binding<Fields>::read_if(name, entry.value, result) || ...));
        }
        return result;
    }

    static void write(writer& output, const T& data)
    {
        output.StartObject();
        (binding<Fields>::write(output, data), ...);
        output.EndObject();
    }
};

template<class T, class Lookup, class... Fields>
struct binding<luminpack::json_lookup_record_body<T, Lookup, Fields...>>
    : binding<luminpack::json_record_body<T, Fields...>> {};

template<class... Cases>
struct binding<luminpack::json_union_codec<Cases...>>
{
    using selection = typename luminpack::json_union_codec<Cases...>::value_type;
    using cases = std::tuple<Cases...>;

    static selection read(const value& input)
    {
        if (input.IsNull())
            return std::nullopt;
        require(input.IsObject());
        const auto tag = input.FindMember(u"$type");
        const auto payload = input.FindMember(u"$value");
        require(tag != input.MemberEnd() && tag->value.IsUint() && payload != input.MemberEnd());
        selection result;
        [&]<std::size_t... Index>(std::index_sequence<Index...>)
        {
            require((read_case<Index>(tag->value.GetUint(), payload->value, result) || ...));
        }(std::index_sequence_for<Cases...>{});
        return result;
    }

    static void write(writer& output, const selection& data)
    {
        if (!data)
        {
            output.Null();
            return;
        }
        output.StartObject();
        [&]<std::size_t... Index>(std::index_sequence<Index...>)
        {
            require((write_case<Index>(output, data) || ...));
        }(std::index_sequence_for<Cases...>{});
        output.EndObject();
    }

private:
    template<std::size_t Index>
    static bool read_case(std::uint32_t tag, const value& payload, selection& result)
    {
        using entry = std::tuple_element_t<Index, cases>;
        if (tag != entry::hash)
            return false;
        result.emplace(std::in_place_index<Index>, binding<typename entry::codec>::read(payload));
        return true;
    }

    template<std::size_t Index>
    static bool write_case(writer& output, const selection& data)
    {
        using entry = std::tuple_element_t<Index, cases>;
        if (data->index() != Index)
            return false;
        output.Key(u"$type");
        output.Uint(entry::hash);
        output.Key(u"$value");
        binding<typename entry::codec>::write(output, std::get<Index>(*data));
        return true;
    }
};

template<class Codec>
typename Codec::value_type read(const char16_t* input)
{
    return binding<Codec>::read(parse(input));
}

class reusable_writer
{
public:
    template<class Codec>
    std::u16string_view write(const typename Codec::value_type& data)
    {
        storage_.Clear();
        target_.Reset(storage_);
        binding<Codec>::write(target_, data);
        require(target_.IsComplete());
        return {storage_.GetString(), storage_.GetSize() / sizeof(char16_t)};
    }

private:
    buffer storage_;
    writer target_{storage_};
};
}
