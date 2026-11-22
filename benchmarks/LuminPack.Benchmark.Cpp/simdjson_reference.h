#pragma once

#include "json_dom.h"

#include <simdjson.h>

namespace simdjson_reference
{
struct dom
{
    using node = simdjson::dom::element;

    static bool is_null(node input) { return input.is_null(); }
    static bool boolean(node input) { return input.get_bool().value(); }
    static std::string_view string(node input) { return input.get_string().value(); }
    static node at(node input, std::size_t index) { return input.at(index).value(); }
    static node member(node input, std::string_view key) { return input[key].value(); }

    template<class T>
    static T number(node input) { return input.get<T>().value(); }

    static std::size_t array_size(node input) { return input.get_array().value().size(); }
    static std::size_t object_size(node input) { return input.get_object().value().size(); }

    static void items(node input, const auto& visit)
    {
        const auto array = input.get_array().value();
        for (auto value : array)
            visit(value);
    }

    static void members(node input, const auto& visit)
    {
        const auto object = input.get_object().value();
        for (auto entry : object)
            visit(entry.key, entry.value);
    }
};

class reusable_reader
{
public:
    explicit reusable_reader(std::string_view input) : input_(input) {}

    template<class Codec>
    typename Codec::value_type read()
    {
        return json_dom::decode<Codec>::template read<dom>(parser_.parse(input_).value());
    }

private:
    simdjson::padded_string input_;
    simdjson::dom::parser parser_;
};
}
