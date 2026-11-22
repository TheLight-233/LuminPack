#include "simple-benchmark.hpp"
#include "serialize-benchmark.hpp"
#include "interop_models.h"

#include <iostream>

namespace simple = luminpack_generated::root_LuminPackBenchmark_x002eSimpleClass;
namespace save = luminpack_generated::root_LuminPackBenchmark_x002eCharacterSaveData;

namespace
{
using base = save::type_LuminPackBenchmark_x002eEventLogBase_base;
using combat = save::type_LuminPackBenchmark_x002eCombatEventLog;
using binary = save::type_LuminPackBenchmark_x002eEventLogBase_binary;
using json = save::type_LuminPackBenchmark_x002eEventLogBase_json;
static_assert(std::same_as<binary::value_type, std::unique_ptr<base>> && std::derived_from<combat, base>);
static_assert(std::same_as<binary::value_type, json::value_type>);

void verify_exact_case_type()
{
    struct unlisted : combat {};
    std::array<std::byte, 1024> buffer{};
    auto known = std::make_unique<combat>();
    known->field_Timestamp = 47;
    const binary::value_type value = std::move(known);
    const auto binary_bytes = luminpack::write_binary_document<binary>(buffer, value);
    luminpack::reader header(binary_bytes);
    if (header.read<std::uint32_t>() != luminpack::union_name_hash("LuminPackBenchmark.CombatEventLog"))
        throw std::runtime_error("The Binary Union hash must match the canonical case name.");
    const auto binary_result = luminpack::read_binary_document<binary>(binary_bytes);
    if (!binary_result || typeid(*binary_result) != typeid(combat) ||
        static_cast<const combat&>(*binary_result).field_Timestamp != 47)
        throw std::runtime_error("The Binary case switch did not restore the concrete DTO.");

    const auto json_bytes = luminpack::write_json_document<json>(buffer, value);
    const auto json_result = luminpack::read_json_document<json>(json_bytes);
    if (!json_result || typeid(*json_result) != typeid(combat) ||
        static_cast<const combat&>(*json_result).field_Timestamp != 47)
        throw std::runtime_error("The JSON case switch did not restore the concrete DTO.");

    const binary::value_type unknown = std::make_unique<unlisted>();
    luminpack::writer output(buffer);
    try
    {
        binary::write(output, unknown);
    }
    catch (const std::invalid_argument&)
    {
        if (output.written().empty())
            return;
    }
    throw std::runtime_error("The Binary writer must reject an unlisted subtype before writing its hash.");
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::runtime_error("Expected a directory containing the RTTI benchmark model documents.");
        const interop_models::path directory(argv[1]);
        std::filesystem::create_directories(directory / "out");
        interop_models::exchange<simple::root_binary, simple::root_json>(directory, "simple");
        interop_models::exchange<save::root_binary, save::root_json>(directory, "serialize");
        verify_exact_case_type();
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
