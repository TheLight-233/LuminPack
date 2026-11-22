#include "native-integer.hpp"
#include "version-native-integer.hpp"
#include "interop_models.h"

#include <iostream>
#include <limits>

namespace ordinary = luminpack_generated::root_LuminPackCppNativeIntegerFixtures_x002eNativeIntegerPacket;
namespace version = luminpack_generated::root_LuminPackCppNativeIntegerFixtures_x002eVersionNativeIntegerPacket;

namespace
{
using namespace luminpack;

constexpr auto signed_min = std::numeric_limits<std::intptr_t>::min();
constexpr auto signed_max = std::numeric_limits<std::intptr_t>::max();
constexpr auto unsigned_max = std::numeric_limits<std::uintptr_t>::max();

static_assert(std::same_as<decltype(std::declval<ordinary::root_value>()->field_Signed), std::intptr_t>);
static_assert(std::same_as<decltype(std::declval<ordinary::root_value>()->field_Unsigned), std::uintptr_t>);

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void verify_ordinary(const ordinary::root_value& value)
{
    require(value && value->field_Signed == signed_min && value->field_Unsigned == unsigned_max,
        "Retain signed and unsigned native integer limits.");
    require(value->field_SignedArray == std::vector<std::intptr_t>{signed_max, -29, 0} &&
        value->field_UnsignedList == std::vector<std::uintptr_t>{1, unsigned_max, 0},
        "Retain the native integer array and list elements.");
    require(value->field_Map && value->field_Map->size() == 1 && value->field_Map->at(-37) == unsigned_max &&
        value->field_Optional == -71, "Retain native integer dictionary keys, values, and optional values.");
    require(value->field_Memory == std::vector<std::uintptr_t>{101, unsigned_max} &&
        value->field_Sequence == std::vector<std::intptr_t>{signed_min, 103},
        "Retain native integer memory and sequence elements.");
}

void verify_version(const version::root_value& value)
{
    require(value && value->field_Signed == signed_max && value->field_Unsigned == unsigned_max &&
        value->field_Values == std::vector<std::intptr_t>{signed_min, -113, 0},
        "Read the host-width Version scalar and collection subrecords.");
}

template<class Json, json_encoding Encoding>
void exchange_json(const interop_models::path& directory, const std::string& name, auto verify)
{
    const auto original = interop_models::load(directory / name);
    const auto value = read_json_document<Json, Encoding>(original);
    verify(value);
    std::vector<std::byte> buffer(original.size());
    const auto encoded = write_json_document<Json, Encoding>(buffer, value);
    require(std::ranges::equal(encoded, original), "Write the exact managed native integer JSON.");
    interop_models::store(directory / "out" / name, encoded);
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, const std::string& name, auto verify)
{
    const auto filename = name + ".bin";
    const auto original = interop_models::load(directory / filename);
    const auto value = read_binary_document<Binary>(original);
    verify(value);
    std::vector<std::byte> buffer(original.size());
    const auto encoded = write_binary_document<Binary>(buffer, value);
    require(std::ranges::equal(encoded, original), "Write the exact managed native integer Binary.");
    interop_models::store(directory / "out" / filename, encoded);
    exchange_json<Json, json_encoding::utf8>(directory, name + ".utf8.json", verify);
    exchange_json<Json, json_encoding::utf16>(directory, name + ".utf16.json", verify);
}

void verify_json_conversion()
{
    constexpr std::string_view narrow = R"({"Signed":4294967295,"Unsigned":4294967296})";
    const auto decoded = read_json_document<ordinary::root_json, json_encoding::utf8>(std::as_bytes(std::span(narrow)));
    if constexpr (sizeof(std::intptr_t) == 4)
        require(decoded && decoded->field_Signed == -1 && decoded->field_Unsigned == 0,
            "Match the managed 64-bit JSON parse and 32-bit narrowing.");
    else
        require(decoded && decoded->field_Signed == 4294967295ll && decoded->field_Unsigned == 4294967296ull,
            "Preserve the parsed JSON values in a 64-bit native integer.");
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed native integer fixture directory.");
    const auto directory = interop_models::path(argv[1]);
    const auto width = interop_models::load(directory / "native-integer-width.txt");
    require(width.size() == 1 && std::to_integer<unsigned>(width.front()) == '0' + sizeof(std::intptr_t),
        "Use managed native integer fixtures from a matching 32-bit or 64-bit runtime.");
    std::filesystem::create_directories(directory / "out");
    exchange<ordinary::root_binary, ordinary::root_json>(directory, "native-integer", verify_ordinary);
    exchange<version::root_binary, version::root_json>(directory, "version-native-integer", verify_version);
    verify_json_conversion();
    std::cout << "Native integer Binary and JSON matched the managed fixture.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
