#include "envelope-variant.hpp"
#include "envelope-rtti.hpp"
#include "choice-variant.hpp"
#include "choice-rtti.hpp"
#include "graph-rtti.hpp"
#include "interop_models.h"

#include <iostream>

namespace variant = luminpack_generated::root_LuminPackCppVersionUnionFixtures_x002eEnvelope;
namespace rtti = luminpack_generated::root_LuminPackCppVersionUnionFixtures_x002eEnvelope_binary_utf16_length;
namespace variant_choice = luminpack_generated::root_LuminPackCppVersionUnionFixtures_x002eIChoice;
namespace rtti_choice = luminpack_generated::root_LuminPackCppVersionUnionFixtures_x002eIChoice_binary_utf16_length;
namespace graph = luminpack_generated::root_LuminPackCppVersionUnionFixtures_x002eGraphEnvelope;

namespace
{
void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void match(const interop_models::path& destination, std::span<const std::byte> actual,
    std::span<const std::byte> expected)
{
    require(std::ranges::equal(actual, expected), "A generated Version Union differs from the managed bytes.");
    interop_models::store(destination, actual);
}

template<class Json, luminpack::json_encoding Encoding, bool Tracked>
void exchange_json(const interop_models::path& directory, const interop_models::path& destination, const char* name)
{
    std::array<std::byte, 4096> buffer;
    const auto filename = std::string(name) + (Encoding == luminpack::json_encoding::utf8 ? ".utf8.json" : ".utf16.json");
    const auto bytes = interop_models::load(directory / filename);
    if constexpr (Tracked)
    {
        const auto document = luminpack::read_reference_json_document<Json, Encoding>(bytes);
        require(document.value && document.value->field_Cases && document.value->field_Cases->size() == 3 &&
            (*document.value->field_Cases)[1] == (*document.value->field_Cases)[2], "JSON graph identity changed.");
        match(destination / filename, luminpack::write_reference_json_document<Json, Encoding>(buffer, document.value), bytes);
    }
    else
    {
        const auto value = luminpack::read_json_document<Json, Encoding>(bytes);
        match(destination / filename, luminpack::write_json_document<Json, Encoding>(buffer, value), bytes);
    }
}

template<class Binary, class Json, bool Tracked = false>
void exchange(const interop_models::path& directory, const interop_models::path& destination, const char* name)
{
    const auto filename = std::string(name) + ".bin";
    const auto bytes = interop_models::load(directory / filename);
    std::array<std::byte, 4096> buffer;
    if constexpr (Tracked)
    {
        const auto document = luminpack::read_binary_reference_document<Binary>(bytes);
        require(document.value && document.value->field_Cases && document.value->field_Cases->size() == 3 &&
            (*document.value->field_Cases)[1] == (*document.value->field_Cases)[2], "Binary graph identity changed.");
        match(destination / filename, luminpack::write_binary_reference_document<Binary>(buffer, document.value), bytes);
    }
    else
    {
        const auto value = luminpack::read_binary_document<Binary>(bytes);
        match(destination / filename, luminpack::write_binary_document<Binary>(buffer, value), bytes);
    }
    exchange_json<Json, luminpack::json_encoding::utf8, Tracked>(directory, destination, name);
    exchange_json<Json, luminpack::json_encoding::utf16, Tracked>(directory, destination, name);
}

template<class Binary, class Json>
void check_null(const interop_models::path& directory, const interop_models::path& destination)
{
    const auto typed = interop_models::load(directory / "null.bin");
    const auto null = luminpack::read_binary_document<Binary>(typed);
    require(!null, "A known Version discriminator with a null body produced an object.");
    std::array<std::byte, 64> buffer;
    const auto canonical = luminpack::write_binary_document<Binary>(buffer, null);
    require(std::ranges::equal(canonical, std::array<std::byte, 4>{}), "Binary did not write the canonical null marker.");
    interop_models::store(destination / "null.bin", canonical);

    const auto utf8 = interop_models::load(directory / "null.utf8.json");
    const auto utf16 = interop_models::load(directory / "null.utf16.json");
    require(!luminpack::read_json_document<Json, luminpack::json_encoding::utf8>(utf8), "Typed null became a UTF-8 object.");
    const auto null_json = luminpack::read_json_document<Json>(utf16);
    require(!null_json, "Typed null became a UTF-16 object.");
    interop_models::store(destination / "null.utf16.json", luminpack::write_json_document<Json>(buffer, null_json));

    std::array unknown{std::byte{0x12}, std::byte{0x34}, std::byte{0x56}, std::byte{0x78}, std::byte{255}};
    try
    {
        (void)luminpack::read_binary_document<Binary>(unknown);
    }
    catch (const luminpack::wire_error&)
    {
        return;
    }
    throw std::runtime_error("An unknown discriminator was treated as a known Version null.");
}
}

int main(int argc, char** argv)
try
{
    if (argc != 2)
        return 2;
    const interop_models::path directory(argv[1]);
    const auto variant_path = directory / "out" / "variant";
    const auto rtti_path = directory / "out" / "rtti";
    std::filesystem::create_directories(variant_path);
    std::filesystem::create_directories(rtti_path);
    exchange<variant::root_binary, variant::root_json>(directory, variant_path, "envelope");
    exchange<rtti::root_binary, rtti::root_json>(directory, rtti_path, "envelope");
    exchange<graph::root_binary, graph::root_json, true>(directory, rtti_path, "graph");
    check_null<variant_choice::root_binary, variant_choice::root_json>(directory, variant_path);
    check_null<rtti_choice::root_binary, rtti_choice::root_json>(directory, rtti_path);
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
