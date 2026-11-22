#include "nullable.hpp"
#include "nullable-version.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppFixtures_x002eCppNullablePacket;
namespace version = luminpack_generated::root_LuminPackCppFixtures_x002eCppNullableVersion;

namespace
{
void check_alternate_layout()
{
    using namespace luminpack;
    using wide = scalar_codec<std::int64_t>;
    constexpr nullable_storage_layout model64{16, 0, 8};
    constexpr nullable_storage_layout model32{12, 0, 4};
    using list = nullable_list_codec<wide, model64, model32>;

    std::array<std::byte, 64> buffer{};
    writer output(buffer);
    output.write<std::int32_t>(2);
    output.write<std::uint8_t>(2);
    nullable_storage_codec<wide, model32>::write(output, 0x1122334455667788ll);
    nullable_storage_codec<wide, model32>::write(output, std::nullopt);

    reader input(output.written());
    list::value_type values(std::in_place, 4, 19);
    list::read_into(input, values);
    input.require_end();
    if (values->size() != 2 || (*values)[0] != 0x1122334455667788ll || (*values)[1])
        throw std::runtime_error("Cannot decode the 32-bit nullable storage model.");

    output = writer(buffer);
    list::write(output, values);
    if (output.written().size() != 37 || output.written()[4] != std::byte{1})
        throw std::runtime_error("Tagged nullable write did not use its canonical model.");
}

void check_invalid_flag()
{
    using namespace luminpack;
    using list = nullable_list_codec<boolean_codec, nullable_storage_layout{2, 0, 1}>;
    constexpr std::array malformed{std::byte{1}, std::byte{}, std::byte{}, std::byte{}, std::byte{}, std::byte{2}};
    reader input(malformed);
    list::value_type values(std::in_place, 1, true);
    try
    {
        list::read_into(input, values);
    }
    catch (const wire_error&)
    {
        if (input.remaining() == malformed.size() && values->size() == 1 && values->front() == true)
            return;
    }
    throw std::runtime_error("Invalid nullable Booleans must fail before publishing the cursor or collection.");
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the nullable model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<packet::root_binary, packet::root_json>(directory, "nullable");
        interop_models::exchange<version::root_binary, version::root_json>(directory, "nullable-version", true);
        check_alternate_layout();
        check_invalid_flag();
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
