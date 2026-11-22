#pragma once

#include "json_dom.h"

#include <jsoncons/json.hpp>

namespace jsoncons_reference
{
using json = jsoncons::json;

struct dom
{
    using node = const json&;

    static bool is_null(node input) { return input.is_null(); }
    static bool boolean(node input) { return input.as<bool>(); }
    static std::string_view string(node input) { return input.as_string_view(); }
    static node at(node input, std::size_t index) { return input.at(index); }
    static node member(node input, std::string_view key) { return input.at(key); }

    template<class T>
    static T number(node input) { return input.as<T>(); }

    static std::size_t array_size(node input)
    {
        rapid_reference::require(input.is_array());
        return input.size();
    }

    static std::size_t object_size(node input)
    {
        rapid_reference::require(input.is_object());
        return input.size();
    }

    static void items(node input, const auto& visit)
    {
        for (const auto& value : input.array_range())
            visit(value);
    }

    static void members(node input, const auto& visit)
    {
        for (const auto& entry : input.object_range())
            visit(entry.key(), entry.value());
    }
};

template<class Codec>
typename Codec::value_type read(std::string_view input)
{
    return json_dom::decode<Codec>::template read<dom>(json::parse(input));
}

class reusable_writer
{
public:
    template<class Codec>
    std::string_view write(const typename Codec::value_type& data)
    {
        storage_.clear();
        target_.reset();
        rapid_reference::binding<Codec>::write(*this, data);
        target_.flush();
        return storage_;
    }

    void StartObject() { target_.begin_object(); }
    void StartArray() { target_.begin_array(); }
    void EndObject() { target_.end_object(); }
    void EndArray() { target_.end_array(); }
    void Null() { target_.null_value(); }
    void Bool(bool value) { target_.bool_value(value); }
    void Int64(std::int64_t value) { target_.int64_value(value); }
    void Uint64(std::uint64_t value) { target_.uint64_value(value); }
    void Uint(unsigned value) { target_.uint64_value(value); }
    bool Double(double value) { return target_.double_value(value); }

    bool String(const char16_t* data, rapidjson::SizeType size) { return target_.string_value(json_dom::to_utf8({data, size})); }

    bool Key(const char16_t* data) { return target_.key(json_dom::to_utf8(data)); }
    bool Key(const char16_t* data, std::size_t size) { return target_.key(json_dom::to_utf8({data, size})); }

    template<luminpack::json_name Name>
    void property() { target_.key(json_dom::property_name<Name>()); }

private:
    std::string storage_;
    jsoncons::compact_json_string_encoder target_{storage_};
};
}
