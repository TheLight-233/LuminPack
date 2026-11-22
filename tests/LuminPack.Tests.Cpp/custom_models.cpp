#include "custom_bindings.h"
#include "custom-binary-only.hpp"
#include "custom-ordinary.hpp"
#include "custom-version.hpp"
#include "custom.hpp"
#include "interop_models.h"

#include <iostream>

namespace shifted = luminpack_generated::root_LuminPackCppCustomFixtures_x002eShifted;
namespace binary_only = luminpack_generated::root_LuminPackCppCustomFixtures_x002eBinaryOnly;
namespace ordinary = luminpack_generated::root_LuminPackCppCustomFixtures_x002eOrdinary;
namespace versioned = luminpack_generated::root_LuminPackCppCustomFixtures_x002eVersioned;

namespace
{
using namespace luminpack;

void require(bool condition, const char* description)
{
    if (!condition)
        throw std::runtime_error(description);
}

void reject(auto operation)
{
    try
    {
        operation();
    }
    catch (const wire_error&)
    {
        return;
    }
    throw std::runtime_error("An unsupported Custom JSON operation or invalid bounded field succeeded.");
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, const std::string& name, auto verify)
{
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto json = interop_models::load(directory / (name + ".json"));
    const auto from_binary = read_binary_document<Binary>(binary);
    const auto from_json = read_json_document<Json>(json);
    verify(from_binary);
    verify(from_json);

    std::array<std::byte, 4096> output{};
    const auto encoded = write_binary_document<Binary>(output, from_json);
    require(std::ranges::equal(encoded, binary), "The C++ binary differs from the exact .NET Custom fixture.");
    interop_models::store(directory / "out" / (name + ".bin"), encoded);
    const auto encoded_json = write_json_document<Json>(output, from_binary);
    require(std::ranges::equal(encoded_json, json), "The C++ JSON differs from the exact .NET Custom fixture.");
    interop_models::store(directory / "out" / (name + ".json"), encoded_json);
}
}

int main(int argc, char** argv)
{
    try
    {
        require(argc == 2, "Expected the Custom fixture directory.");
        const auto directory = interop_models::path(argv[1]);
        std::filesystem::create_directories(directory / "out");

        exchange<shifted::root_binary, shifted::root_json>(directory, "custom", [](const auto& value) {
            require(value == 42, "Read the root Custom value.");
        });
        exchange<shifted::root_binary, shifted::root_json>(directory, "custom-null", [](const auto& value) {
            require(!value, "Read the application-owned Custom null sentinel.");
        });
        exchange<ordinary::root_binary, ordinary::root_json>(directory, "custom-ordinary", [](const auto& value) {
            require(value && value->field_Data == 42 && !value->field_Absent && value->field_Values &&
                value->field_Values->size() == 2 && !value->field_Values->at(0) &&
                value->field_Values->at(1) == 42 && value->field_Marker == 13,
                "Read a positional record and list of Custom values.");
        });
        const auto verify_version = [](const auto& value) {
            require(value && value->field_Data == 42 && value->field_Marker == 7, "Read a Version Custom subrecord.");
        };
        exchange<versioned::root_binary, versioned::root_json>(directory, "custom-version", verify_version);
        constexpr auto alias = std::u16string_view{uR"({"Previous":163,"Marker":7})"};
        verify_version(read_json_document<versioned::root_json>(std::as_bytes(std::span(alias))));

        const auto opaque = interop_models::load(directory / "custom-binary-only.bin");
        const auto from_binary = read_binary_document<binary_only::root_binary>(opaque);
        require(from_binary == 29, "Read a Binary-only Custom root.");
        std::array<std::byte, 4096> output{};
        const auto binary = write_binary_document<binary_only::root_binary>(output, from_binary);
        require(std::ranges::equal(binary, opaque), "Write the exact Binary-only Custom payload.");
        interop_models::store(directory / "out/custom-binary-only.bin", binary);

        reject([&] { (void)write_json_document<shifted::root_json, json_encoding::utf8>(output, 42); });
        reject([&] { (void)write_json_document<binary_only::root_json>(output, from_binary); });

        auto truncated = interop_models::load(directory / "custom-version.bin");
        reader cursor(truncated);
        version_state state;
        (void)read_version_header(cursor, state, string_profile::utf16_length);
        const auto offset = truncated.size() - cursor.remaining();
        require(cursor.read<std::uint32_t>() == sizeof(std::int64_t), "The Version Custom member has its own bounded payload.");
        truncated[offset] = static_cast<std::byte>(sizeof(std::int64_t) - 1);
        reject([&] { (void)read_binary_document<versioned::root_binary>(truncated); });

        std::cout << "C++ verified the generated Custom Binary and JSON contracts.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
