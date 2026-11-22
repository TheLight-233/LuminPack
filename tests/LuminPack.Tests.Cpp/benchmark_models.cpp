#include "simple-benchmark.hpp"
#include "serialize-benchmark.hpp"
#include "interop_models.h"

#include <iostream>

namespace simple = luminpack_generated::root_LuminPackBenchmark_x002eSimpleClass;
namespace save = luminpack_generated::root_LuminPackBenchmark_x002eCharacterSaveData;

namespace
{
using transform = simple::type_LuminPackBenchmark_x002eTransform;
constexpr bool dense_transform = sizeof(transform) == 12 &&
    offsetof(transform, field_x) == 0 && offsetof(transform, field_y) == 4 && offsetof(transform, field_z) == 8;
static_assert(luminpack::detail::contiguous_codec<simple::type_LuminPackBenchmark_x002eTransform_binary> == dense_transform);

}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the benchmark model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<simple::root_binary, simple::root_json>(directory, "simple");
        interop_models::exchange<save::root_binary, save::root_json>(directory, "serialize");
        std::cout << "Exchanged the generated SimpleClass and CharacterSaveData models.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
