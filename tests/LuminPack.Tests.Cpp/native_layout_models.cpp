#include "layout-native-padded.hpp"
#include "interop_models.h"

#include <iostream>

namespace native = luminpack_generated::root_LuminPackCppLayoutFixtures_x002eLayoutNativePaddedPacket;

namespace
{
static_assert(native::type_LuminPackCppLayoutFixtures_x002eLayoutNativePadded_binary::minimum_size == 16);
static_assert(native::type_LuminPackCppLayoutFixtures_x002eLayoutTaggedPadded_binary::minimum_size == 8);

void check_padding(const interop_models::path& directory)
{
    const auto original = interop_models::load(directory / "layout-native-padded.bin");
    luminpack::reader input(original);
    if (luminpack::read_object_header(input) != 3 || input.read_collection_count(16) != 2)
        throw std::runtime_error("Expected the Native padded fixture and its two items.");
    const auto items = input.take(32);
    const auto scalar = input.take(16);
    if (input.read_collection_count(8) != 1)
        throw std::runtime_error("Expected a headerless Tagged element after its count.");
    const auto tagged = input.take(8);
    input.require_end();
    if (items[13] != std::byte{0xA1} || items[29] != std::byte{0xB2} ||
        scalar[13] != std::byte{0xC3} || tagged[5] != std::byte{0xD4})
        throw std::runtime_error("Expected the managed writer to include the seeded native padding bytes.");

    const auto decoded = luminpack::read_binary_document<native::root_binary>(original);
    std::vector<std::byte> output(original.size());
    const auto written = luminpack::write_binary_document<native::root_binary>(output, decoded);
    if (!std::ranges::equal(written, original))
        throw std::runtime_error("C++ did not retain the Native or single-model Tagged padding.");
    interop_models::store(directory / "out" / "layout-native-padded-preserved.bin", written);
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the native layout model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<native::root_binary, native::root_json>(directory, "layout-native-padded");
        check_padding(directory);
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
