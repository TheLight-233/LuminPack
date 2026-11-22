#pragma once

#include <luminpack/luminpack.h>

#include <filesystem>
#include <fstream>

namespace interop_models
{
using std::filesystem::path;

inline std::vector<std::byte> load(const path& name)
{
    std::ifstream file(name, std::ios::binary | std::ios::ate);
    const auto length = file.tellg();
    if (length < 0)
        throw std::runtime_error("Missing .NET model fixture file.");
    std::vector<std::byte> data(static_cast<std::size_t>(length));
    file.seekg(0);
    file.read(reinterpret_cast<char*>(data.data()), static_cast<std::streamsize>(data.size()));
    if (!file)
        throw std::runtime_error("Could not read .NET model fixture file.");
    return data;
}

inline void store(const path& name, std::span<const std::byte> bytes)
{
    std::ofstream file(name, std::ios::binary);
    file.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    if (!file)
        throw std::runtime_error("Could not write C++ model fixture.");
}

template<class Binary, class Json>
void exchange(const path& directory, const std::string& name, bool exact_binary = false)
{
    const auto binary = load(directory / (name + ".bin"));
    const auto json = load(directory / (name + ".json"));
    auto from_binary = luminpack::read_binary_document<Binary>(binary);
    auto from_json = luminpack::read_json_document<Json>(json);
    if (!from_binary || !from_json)
        throw std::runtime_error("Unexpected null fixture model.");

    std::vector<std::byte> output(1 << 20);
    const auto written = luminpack::write_binary_document<Binary>(output, from_json);
    if (exact_binary && !std::ranges::equal(written, binary))
        throw std::runtime_error("C++ binary differs from the exact .NET fixture.");
    store(directory / "out" / (name + ".bin"), written);
    store(directory / "out" / (name + ".json"), luminpack::write_json_document<Json>(output, from_binary));
}
}
