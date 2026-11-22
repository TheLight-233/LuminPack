#include "interop_models.h"
#include "json-number-models.hpp"
#include "json-number-reject.hpp"

#include <algorithm>
#include <array>
#include <iostream>
#include <span>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace numbers = luminpack_generated::root_LuminPackPortableFixtures_x002eJsonNumberDocument;
namespace nonfinite = luminpack_generated::root_LuminPackPortableFixtures_x002eJsonNumberRejectDocument;

namespace
{
void exact(std::span<const std::byte> actual, std::span<const std::byte> expected, std::string_view operation)
{
    const auto difference = std::ranges::mismatch(actual, expected);
    if (difference.in1 != actual.end() || difference.in2 != expected.end())
        throw std::runtime_error(std::string(operation) + " differs from .NET at byte " +
            std::to_string(difference.in1 - actual.begin()) + ".");
}

void verify_numbers(const interop_models::path& directory)
{
    const auto binary = interop_models::load(directory / "numbers.bin");
    const auto utf8 = interop_models::load(directory / "numbers.utf8.json");
    const auto utf16 = interop_models::load(directory / "numbers.utf16.json");
    std::vector<std::byte> output(std::max({binary.size(), utf8.size(), utf16.size()}) + 64);

    const auto verify = [&](const auto& value) {
        exact(luminpack::write_binary_document<numbers::root_binary>(output, value), binary, "Binary");
        exact(luminpack::write_json_document<numbers::root_json, luminpack::json_encoding::utf8>(output, value),
            utf8, "UTF-8 JSON");
        exact(luminpack::write_json_document<numbers::root_json, luminpack::json_encoding::utf16>(output, value),
            utf16, "UTF-16 JSON");
    };
    verify(luminpack::read_binary_document<numbers::root_binary>(binary));
    verify(luminpack::read_json_document<numbers::root_json, luminpack::json_encoding::utf8>(utf8));
    verify(luminpack::read_json_document<numbers::root_json, luminpack::json_encoding::utf16>(utf16));
}

template<luminpack::json_encoding Encoding>
void reject_json(const auto& value)
{
    std::array<std::byte, 512> output{};
    try
    {
        (void)luminpack::write_json_document<nonfinite::root_json, Encoding>(output, value);
    }
    catch (const std::invalid_argument&)
    {
        return;
    }
    throw std::runtime_error("C++ accepted a non-finite floating-point value in JSON.");
}

void verify_nonfinite(const interop_models::path& directory)
{
    for (const char* name : {"f32-nan", "f32-positive-infinity", "f32-negative-infinity",
                            "f64-nan", "f64-positive-infinity", "f64-negative-infinity"})
    {
        const auto binary = interop_models::load(directory / (std::string(name) + ".bin"));
        const auto value = luminpack::read_binary_document<nonfinite::root_binary>(binary);
        std::array<std::byte, 64> output{};
        exact(luminpack::write_binary_document<nonfinite::root_binary>(output, value), binary, name);
        reject_json<luminpack::json_encoding::utf8>(value);
        reject_json<luminpack::json_encoding::utf16>(value);
    }
}
}

int main(int argc, char** argv)
try
{
    if (argc != 2)
        throw std::runtime_error("Expected the managed JSON number fixture directory.");
    const interop_models::path directory(argv[1]);
    verify_numbers(directory);
    verify_nonfinite(directory);
    std::cout << "C++ verified the managed Binary and both JSON number encodings.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
