#pragma once

#include "binary_model.h"

#include <msgpack.hpp>

#include <bit>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <string>

namespace msgpack
{
MSGPACK_API_VERSION_NAMESPACE(MSGPACK_DEFAULT_API_NS)
{
namespace adaptor
{
template<binary_model::record T>
struct pack<T>
{
    template<class Stream>
    msgpack::packer<Stream>& operator()(msgpack::packer<Stream>& output, const T& data) const
    {
        return ylt::reflection::visit_members(data, [&](const auto&... fields) -> msgpack::packer<Stream>&
        {
            output.pack_array(sizeof...(fields));
            (output.pack(fields), ...);
            return output;
        });
    }
};

template<binary_model::record T>
struct convert<T>
{
    const msgpack::object& operator()(const msgpack::object& input, T& data) const
    {
        ylt::reflection::visit_members(data, [&](auto&... fields)
        {
            if (input.type != msgpack::type::ARRAY || input.via.array.size != sizeof...(fields))
                throw msgpack::type_error();
            auto* item = input.via.array.ptr;
            ((item++)->convert(fields), ...);
        });
        return input;
    }
};

template<class T> requires std::is_enum_v<T>
struct pack<T>
{
    template<class Stream>
    msgpack::packer<Stream>& operator()(msgpack::packer<Stream>& output, T data) const
    {
        return output.pack(static_cast<std::underlying_type_t<T>>(data));
    }
};

template<class T> requires std::is_enum_v<T>
struct convert<T>
{
    const msgpack::object& operator()(const msgpack::object& input, T& data) const
    {
        data = static_cast<T>(input.as<std::underlying_type_t<T>>());
        return input;
    }
};

// MessagePack text uses UTF-8. Preserve the fixtures' UTF-16 code units in a BIN payload.
static_assert(std::endian::native == std::endian::little);

template<>
struct pack<std::u16string>
{
    template<class Stream>
    msgpack::packer<Stream>& operator()(msgpack::packer<Stream>& output, const std::u16string& data) const
    {
        if (data.size() > UINT32_MAX / sizeof(char16_t))
            throw std::length_error("MessagePack UTF-16 payload is too large.");
        const auto bytes = static_cast<std::uint32_t>(data.size() * sizeof(char16_t));
        output.pack_bin(bytes);
        return output.pack_bin_body(reinterpret_cast<const char*>(data.data()), bytes);
    }
};

template<>
struct convert<std::u16string>
{
    const msgpack::object& operator()(const msgpack::object& input, std::u16string& data) const
    {
        if (input.type != msgpack::type::BIN || input.via.bin.size % sizeof(char16_t) != 0)
            throw msgpack::type_error();
        data.resize(input.via.bin.size / sizeof(char16_t));
        if (!data.empty())
            std::memcpy(data.data(), input.via.bin.ptr, input.via.bin.size);
        return input;
    }
};
}
}
}

namespace msgpack_reference
{
struct output_buffer
{
    std::span<char> output;
    std::size_t size = 0;

    void write(const char* data, std::size_t count)
    {
        if (count > output.size() - size)
            throw std::length_error("MessagePack output buffer is too small.");
        std::memcpy(output.data() + size, data, count);
        size += count;
    }
};

template<class T>
std::size_t write(std::span<char> output, const T& model)
{
    output_buffer buffer{output};
    msgpack::pack(buffer, model);
    return buffer.size;
}

// BIN spans can refer to the existing input while the owning model is being materialized.
inline bool reference_payload(msgpack::type::object_type type, std::size_t, void*)
{
    return type == msgpack::type::BIN;
}

template<class T>
T read(std::span<const char> input)
{
    std::size_t consumed = 0;
    const auto object = msgpack::unpack(input.data(), input.size(), consumed, reference_payload);
    if (consumed != input.size())
        throw std::runtime_error("MessagePack did not read the complete document.");
    T result{};
    object.get().convert(result);
    return result;
}
}
