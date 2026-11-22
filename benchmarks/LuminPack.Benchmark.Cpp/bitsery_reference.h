#pragma once

#include "binary_model.h"

#include <bitsery/adapter/buffer.h>
#include <bitsery/bitsery.h>
#include <bitsery/ext/std_map.h>
#include <bitsery/ext/std_optional.h>
#include <bitsery/ext/std_variant.h>
#include <bitsery/traits/string.h>
#include <bitsery/traits/vector.h>

#include <optional>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <vector>

namespace bitsery_reference
{
constexpr std::size_t maximum_items = 1u << 20;

struct transfer
{
    template<class Archive, class T>
    void operator()(Archive& archive, T& data) const
    {
        using value = std::remove_const_t<T>;
        if constexpr (binary_model::record<value>)
        {
            ylt::reflection::visit_members(data, [&](auto&... fields) { ((*this)(archive, fields), ...); });
        }
        else if constexpr (std::is_same_v<value, bool>)
            archive.boolValue(data);
        else
        {
            static_assert(std::is_arithmetic_v<value> || std::is_enum_v<value>);
            archive.template value<sizeof(T)>(data);
        }
    }

    template<class Archive>
    void operator()(Archive& archive, const std::u16string& data) const
    {
        archive.template text<sizeof(char16_t)>(data, maximum_items);
    }

    template<class Archive>
    void operator()(Archive& archive, std::u16string& data) const
    {
        archive.template text<sizeof(char16_t)>(data, maximum_items);
    }

    template<class Archive, class T>
    void operator()(Archive& archive, std::optional<T>& data) const
    {
        archive.ext(data, bitsery::ext::StdOptional{}, *this);
    }

    template<class Archive, class T>
    void operator()(Archive& archive, const std::optional<T>& data) const
    {
        archive.ext(data, bitsery::ext::StdOptional{}, *this);
    }

    template<class Archive, class... T>
    void operator()(Archive& archive, std::variant<T...>& data) const
    {
        archive.ext(data, bitsery::ext::StdVariant{*this});
    }

    template<class Archive, class... T>
    void operator()(Archive& archive, const std::variant<T...>& data) const
    {
        archive.ext(data, bitsery::ext::StdVariant{*this});
    }

    template<class Archive, class T>
    void operator()(Archive& archive, std::vector<T>& data) const { vector(archive, data); }

    template<class Archive, class T>
    void operator()(Archive& archive, const std::vector<T>& data) const { vector(archive, data); }

    template<class Archive, class K, class V, class H, class E>
    void operator()(Archive& archive, std::unordered_map<K, V, H, E>& data) const { map(archive, data); }

    template<class Archive, class K, class V, class H, class E>
    void operator()(Archive& archive, const std::unordered_map<K, V, H, E>& data) const { map(archive, data); }

private:
    template<class Archive, class Vector>
    void vector(Archive& archive, Vector& data) const
    {
        using element = typename Vector::value_type;
        if constexpr ((std::is_arithmetic_v<element> || std::is_enum_v<element>) && !std::is_same_v<element, bool>)
            archive.template container<sizeof(element)>(data, maximum_items);
        else
            archive.container(data, maximum_items, *this);
    }

    template<class Archive, class Map>
    void map(Archive& archive, Map& data) const
    {
        archive.ext(data, bitsery::ext::StdMap{maximum_items}, [this](auto& target, auto& key, auto& item)
        {
            (*this)(target, key);
            (*this)(target, item);
        });
    }
};

template<class T>
std::size_t write(std::vector<char>& output, const T& model)
{
    bitsery::Serializer<bitsery::OutputBufferAdapter<std::vector<char>>> archive(output);
    transfer{}(archive, model);
    archive.adapter().flush();
    return archive.adapter().writtenBytesCount();
}

template<class T>
T read(const std::vector<char>& input)
{
    using adapter = bitsery::InputBufferAdapter<std::vector<char>>;
    bitsery::Deserializer<adapter> archive(input.begin(), input.size());
    T result{};
    transfer{}(archive, result);
    if (archive.adapter().error() != bitsery::ReaderError::NoError || !archive.adapter().isCompletedSuccessfully())
        throw std::runtime_error("Bitsery did not read the complete document.");
    return result;
}
}
