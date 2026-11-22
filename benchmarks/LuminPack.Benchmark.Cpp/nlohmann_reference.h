#pragma once

#include "json_dom.h"

#include <nlohmann/json.hpp>

namespace nlohmann_reference
{
using json = nlohmann::json;

struct dom
{
    using node = const json&;

    static bool is_null(node input) { return input.is_null(); }
    static bool boolean(node input) { return input.get<bool>(); }
    static std::string_view string(node input) { return input.get_ref<const std::string&>(); }
    static node at(node input, std::size_t index) { return input.at(index); }
    static node member(node input, std::string_view key) { return input.at(key); }

    template<class T>
    static T number(node input) { return input.get<T>(); }

    static std::size_t array_size(node input) { return input.get_ref<const json::array_t&>().size(); }
    static std::size_t object_size(node input) { return input.get_ref<const json::object_t&>().size(); }

    static void items(node input, const auto& visit)
    {
        for (const auto& value : input.get_ref<const json::array_t&>())
            visit(value);
    }

    static void members(node input, const auto& visit)
    {
        for (const auto& [key, value] : input.get_ref<const json::object_t&>())
            visit(key, value);
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
    std::string write(const typename Codec::value_type& data)
    {
        rapid_reference::binding<Codec>::write(*this, data);
        return root_.dump();
    }

    void StartObject() { path_.push_back({&add(json::object()), {}}); }
    void StartArray() { path_.push_back({&add(json::array()), {}}); }
    void EndObject() { path_.pop_back(); }
    void EndArray() { path_.pop_back(); }
    void Null() { add(nullptr); }
    void Bool(bool value) { add(value); }
    void Int64(std::int64_t value) { add(value); }
    void Uint64(std::uint64_t value) { add(value); }
    void Uint(unsigned value) { add(value); }
    bool Double(double value) { add(value); return true; }

    bool String(const char16_t* data, rapidjson::SizeType size)
    {
        add(json_dom::to_utf8({data, size}));
        return true;
    }

    bool Key(const char16_t* data) { return Key(data, std::char_traits<char16_t>::length(data)); }

    bool Key(const char16_t* data, std::size_t size)
    {
        path_.back().key = json_dom::to_utf8({data, size});
        return true;
    }

    template<luminpack::json_name Name>
    void property() { path_.back().key = json_dom::property_name<Name>(); }

private:
    json& add(json value)
    {
        if (path_.empty())
            return root_ = std::move(value);
        auto& parent = path_.back();
        if (parent.value->is_array())
            return parent.value->emplace_back(std::move(value));
        return (*parent.value)[parent.key] = std::move(value);
    }

    struct frame
    {
        json* value;
        std::string key;
    };

    json root_;
    std::vector<frame> path_;
};
}
