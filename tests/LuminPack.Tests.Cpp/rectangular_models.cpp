#include "bounds-array.hpp"
#include "modeled-array.hpp"
#include "scalar-array.hpp"
#include "interop_models.h"

#include <iostream>

namespace bounds = luminpack_generated::root_LuminPackCppRectangularFixtures_x002eBoundsPacket;
namespace modeled = luminpack_generated::root_LuminPackCppRectangularFixtures_x002eModeledPacket;
namespace scalar = luminpack_generated::root_LuminPackCppRectangularFixtures_x002eScalarPacket;

namespace
{
using namespace luminpack;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<class Error>
void reject(auto operation, const char* message)
{
    try
    {
        operation();
    }
    catch (const Error&)
    {
        return;
    }
    throw std::runtime_error(message);
}

void verify_scalar(const scalar::root_value& value)
{
    require(value && value->field_Grid && value->field_Text && value->field_Empty &&
        value->field_Nested && !value->field_Missing, "Read the non-null, empty and null rectangular fields.");
    require(value->field_Grid->lengths == std::array<std::int32_t, 2>{2, 3} &&
        value->field_Grid->values == std::vector<std::int32_t>{17, -29, 31, 43, 61, -71},
        "Retain the rectangular dimensions and raw row-major integer payload.");
    require(value->field_Text->lengths == std::array<std::int32_t, 2>{2, 2} &&
        value->field_Text->values == std::vector<std::optional<std::u16string>>{std::nullopt, u"caf\u00e9", u"\u03a9", u""},
        "Retain string profiles and null slots inside a rectangular array.");
    require(value->field_Empty->lengths == std::array<std::int32_t, 3>{2, 0, 1} &&
        value->field_Empty->values.empty(), "Retain all dimensions of a zero-element array.");
    const auto& nested = *value->field_Nested;
    require(nested.size() == 2 && nested[0] && !nested[1] &&
        nested[0]->lengths == std::array<std::int32_t, 2>{2, 1} &&
        nested[0]->values == std::vector<std::int16_t>{7, -9}, "Decode rectangular arrays inside a list.");
}

void verify_modeled(const modeled::root_value& value)
{
    require(value && value->field_Numbers && value->field_Points, "Read both modeled element sequences.");
    require(value->field_Numbers->lengths == std::array<std::int32_t, 3>{1, 2, 2} &&
        value->field_Numbers->values == std::vector<std::optional<std::int64_t>>{17, std::nullopt, 0x1122334455667788ll, -42},
        "Decode the rectangular nullable storage block.");
    const auto& points = value->field_Points->values;
    require(value->field_Points->lengths == std::array<std::int32_t, 2>{1, 2} && points.size() == 2 &&
        points[0].field_Code == 71 && points[0].field_Size == -9 && points[0].field_Active &&
        points[1].field_Code == 81 && points[1].field_Size == 27 && !points[1].field_Active,
        "Decode the rectangular Tagged structure block.");
}

void verify_bounds(const bounds::root_value& value)
{
    require(value && value->field_Grid && value->field_Grid->lengths == std::array<std::int32_t, 2>{2, 2} &&
        value->field_Grid->lower_bounds == std::array<std::int32_t, 2>{-2, 3} &&
        value->field_Grid->values == std::vector<std::int32_t>{17, -29, 31, 43},
        "Retain nonzero CLR lower bounds in JSON.");
}

template<class Json, json_encoding Encoding>
void exchange_json(const interop_models::path& directory, const std::string& filename, auto verify)
{
    const auto original = interop_models::load(directory / filename);
    const auto value = read_json_document<Json, Encoding>(original);
    verify(value);
    std::vector<std::byte> buffer(original.size());
    const auto encoded = write_json_document<Json, Encoding>(buffer, value);
    require(std::ranges::equal(encoded, original), "Write the exact managed rectangular JSON envelope.");
    interop_models::store(directory / "out" / filename, encoded);
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, std::string name, auto verify, bool exact_binary)
{
    const auto filename = name + ".bin";
    const auto original = interop_models::load(directory / filename);
    const auto value = read_binary_document<Binary>(original);
    verify(value);
    std::vector<std::byte> buffer(original.size() * 2);
    const auto encoded = write_binary_document<Binary>(buffer, value);
    if (exact_binary)
        require(std::ranges::equal(encoded, original), "Write the exact scalar rectangular binary record.");
    verify(read_binary_document<Binary>(encoded));
    interop_models::store(directory / "out" / filename, encoded);
    exchange_json<Json, json_encoding::utf8>(directory, name + ".utf8.json", verify);
    exchange_json<Json, json_encoding::utf16>(directory, name + ".utf16.json", verify);
}

void check_validation(const interop_models::path& directory)
{
    const auto wire = interop_models::load(directory / "scalar-array.bin");
    for (std::size_t size = 0; size < wire.size(); ++size)
    {
        reader input(std::span<const std::byte>(wire).first(size));
        reject<wire_error>([&] { (void)scalar::root_binary::read(input); }, "Accepts a truncated rectangular array.");
        require(input.remaining() == size, "A failed binary record advanced the parent cursor.");
    }
    const auto changed_count = std::array{
        std::byte{0}, std::byte{2}, std::byte{0}, std::byte{0}, std::byte{0},
        std::byte{3}, std::byte{0}, std::byte{0}, std::byte{0},
        std::byte{1}, std::byte{0}, std::byte{0}, std::byte{0}};
    using shape = rectangular_array_codec<2, list_codec<scalar_codec<std::int32_t>>>;
    reject<wire_error>([&] { (void)read_binary_document<shape>(changed_count); }, "Accepts a rectangular count mismatch.");

    const auto json = interop_models::load(directory / "bounds-array.utf8.json");
    const auto bounded = read_json_document<bounds::root_json, json_encoding::utf8>(json);
    std::array<std::byte, 64> output{};
    reject<std::invalid_argument>([&] { (void)write_binary_document<bounds::root_binary>(output, bounded); },
        "Accepts nonzero lower bounds in binary.");
    for (std::string_view malformed : {
        R"({"Grid":{"Lengths":[2,2],"LowerBounds":[0,0],"Values":[1]}})",
        R"({"Grid":{"Lengths":[0,-1],"LowerBounds":[0,0],"Values":[]}})",
        R"({"Grid":{"Lengths":[1,1,1],"LowerBounds":[0,0],"Values":[1]}})",
        R"({"Grid":{"Lengths":[65536,65536],"LowerBounds":[0,0],"Values":[]}})",
        R"({"Grid":{"Lengths":[2,1],"LowerBounds":[2147483647,0],"Values":[1,2]}})"})
    {
        reject<wire_error>([&] {
            (void)read_json_document<bounds::root_json, json_encoding::utf8>(std::as_bytes(std::span(malformed)));
        }, "Accepts an invalid rectangular JSON shape.");
    }
    const std::string_view reordered =
        R"({"Grid":{"Ignored":{"z":true},"Values":[17,-29,31,43],"Lengt\u0068s":[2,2],"LowerBounds":[-2,3]}})";
    verify_bounds(read_json_document<bounds::root_json, json_encoding::utf8>(std::as_bytes(std::span(reordered))));
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed rectangular fixture directory.");
    const auto directory = interop_models::path(argv[1]);
    std::filesystem::create_directories(directory / "out");
    exchange<scalar::root_binary, scalar::root_json>(directory, "scalar-array", verify_scalar, true);
    exchange<modeled::root_binary, modeled::root_json>(directory, "modeled-array", verify_modeled, false);
    exchange_json<bounds::root_json, json_encoding::utf8>(directory, "bounds-array.utf8.json", verify_bounds);
    exchange_json<bounds::root_json, json_encoding::utf16>(directory, "bounds-array.utf16.json", verify_bounds);
    check_validation(directory);
    std::cout << "C++ verified the CLR rectangular array fixtures.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
