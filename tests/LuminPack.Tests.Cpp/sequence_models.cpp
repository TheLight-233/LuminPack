#include "sequence.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppSequenceFixtures_x002eSequencePacket;

int main(int argc, char** argv)
try
{
    if (argc != 2)
        return 2;
    const interop_models::path directory(argv[1]);
    std::filesystem::create_directories(directory / "out");
    interop_models::exchange<packet::root_binary, packet::root_json>(directory, "sequence");

    const auto input = interop_models::load(directory / "sequence.bin");
    const auto value = luminpack::read_binary_document<packet::root_binary>(input, packet::root_binary_limits);
    const auto check_wide_nullable = [](const auto& item) {
        if constexpr (requires { item.field_WideNullable; })
            return item.field_WideNullable == std::vector<std::optional<std::int64_t>>{std::nullopt, 0x1122334455667788};
        return true;
    };
    if (!value || value->field_Tagged.size() != 2 || value->field_Packed.size() != 2 ||
        value->field_Native.size() != 2 || !value->field_Empty.empty() ||
        value->field_Tagged[0].field_Code != 20 || value->field_Tagged[1].field_Amount != 0x1122334455667788 ||
        !value->field_Packed[1].field_Active || value->field_Native[0].field_Amount != -123 ||
        value->field_SmallNullable != std::vector<std::optional<std::int32_t>>{std::nullopt, 712} ||
        !check_wide_nullable(*value) ||
        value->field_BooleanNullable != std::vector<std::optional<bool>>{std::nullopt, true})
        throw std::runtime_error("Segmented layout elements did not preserve their values and order.");
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
