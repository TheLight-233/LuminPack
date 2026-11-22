#pragma once

#include "binary_model.h"

#include <cereal/archives/binary.hpp>
#include <cereal/types/optional.hpp>
#include <cereal/types/string.hpp>
#include <cereal/types/unordered_map.hpp>
#include <cereal/types/variant.hpp>
#include <cereal/types/vector.hpp>

#include <cstring>
#include <span>
#include <stdexcept>

namespace cereal
{
template<class Archive, binary_model::record T>
void serialize(Archive& archive, T& model)
{
    ylt::reflection::visit_members(model, [&](auto&... fields) { archive(fields...); });
}
}

namespace cereal_reference
{
class output_buffer : public std::streambuf
{
public:
    explicit output_buffer(std::span<char> output) : output_(output) {}

    std::size_t size() const { return size_; }

protected:
    std::streamsize xsputn(const char* data, std::streamsize count) override
    {
        const auto bytes = static_cast<std::size_t>(count);
        if (bytes > output_.size() - size_)
            return 0;
        std::memcpy(output_.data() + size_, data, bytes);
        size_ += bytes;
        return count;
    }

private:
    std::span<char> output_;
    std::size_t size_ = 0;
};

class input_buffer : public std::streambuf
{
public:
    explicit input_buffer(std::span<const char> input) : input_(input) {}

    bool empty() const { return input_.empty(); }

protected:
    std::streamsize xsgetn(char* data, std::streamsize count) override
    {
        const auto bytes = static_cast<std::size_t>(count);
        if (bytes > input_.size())
            return 0;
        std::memcpy(data, input_.data(), bytes);
        input_ = input_.subspan(bytes);
        return count;
    }

private:
    std::span<const char> input_;
};

template<class T>
std::size_t write(std::span<char> output, const T& model)
{
    output_buffer buffer(output);
    std::ostream stream(&buffer);
    cereal::BinaryOutputArchive archive(stream);
    archive(model);
    return buffer.size();
}

template<class T>
T read(std::span<const char> input)
{
    input_buffer buffer(input);
    std::istream stream(&buffer);
    cereal::BinaryInputArchive archive(stream);
    T result{};
    archive(result);
    if (!buffer.empty())
        throw std::runtime_error("cereal did not read the complete document.");
    return result;
}
}
