#include "simple-benchmark.hpp"
#include "serialize-benchmark.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>

namespace simple = luminpack_generated::root_LuminPackBenchmark_x002eSimpleClass;
namespace save = luminpack_generated::root_LuminPackBenchmark_x002eCharacterSaveData;

namespace
{
using transform = simple::type_LuminPackBenchmark_x002eTransform;
constexpr bool dense_transform = sizeof(transform) == 12 &&
    offsetof(transform, field_x) == 0 && offsetof(transform, field_y) == 4 && offsetof(transform, field_z) == 8;
static_assert(luminpack::detail::contiguous_codec<simple::type_LuminPackBenchmark_x002eTransform_binary> == dense_transform);

using std::filesystem::path;

std::vector<std::byte> load(const path& name)
{
    std::ifstream file(name, std::ios::binary | std::ios::ate);
    const auto length = file.tellg();
    if (length < 0)
        throw std::runtime_error("Missing .NET benchmark fixture file.");
    std::vector<std::byte> data(static_cast<std::size_t>(length));
    file.seekg(0);
    file.read(reinterpret_cast<char*>(data.data()), static_cast<std::streamsize>(data.size()));
    if (!file)
        throw std::runtime_error("Could not read .NET benchmark fixture file.");
    return data;
}

void store(const path& name, std::span<const std::byte> bytes)
{
    std::ofstream file(name, std::ios::binary);
    file.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    if (!file)
        throw std::runtime_error("Could not write C++ benchmark fixture.");
}

template<class Binary, class Json>
void exchange(const path& directory, const std::string& name)
{
    const auto binary = load(directory / (name + ".bin"));
    const auto json = load(directory / (name + ".json"));
    auto from_binary = luminpack::read_binary_document<Binary>(binary);
    auto from_json = luminpack::read_json_document<Json>(json);
    if (!from_binary || !from_json)
        throw std::runtime_error("Unexpected null benchmark model.");

    std::vector<std::byte> output(1 << 20);
    store(directory / "out" / (name + ".bin"), luminpack::write_binary_document<Binary>(output, from_json));
    store(directory / "out" / (name + ".json"), luminpack::write_json_document<Json>(output, from_binary));
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the benchmark model documents.");
        const path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        exchange<simple::root_binary, simple::root_json>(directory, "simple");
        exchange<save::root_binary, save::root_json>(directory, "serialize");
        std::cout << "Exchanged the generated SimpleClass and CharacterSaveData models.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
