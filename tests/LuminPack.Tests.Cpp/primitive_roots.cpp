#include "interop_models.h"
#include "array.hpp"
#include "bools.hpp"
#include "derived.hpp"
#include "dict.hpp"
#include "float.hpp"
#include "list.hpp"
#include "nested.hpp"
#include "scalar.hpp"

namespace
{
template<class Binary, class Json>
void check(const std::filesystem::path& directory, const std::string& name)
{
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto value = luminpack::read_binary_document<Binary>(binary);
    std::vector<std::byte> output(4096);
    auto exchange = [&]<luminpack::json_encoding Encoding>(const char* suffix) {
        const auto json = interop_models::load(directory / (name + suffix));
        if (!std::ranges::equal(luminpack::write_json_document<Json, Encoding>(output, value), json))
            throw std::runtime_error("Unexpected primitive root JSON for " + name);
        const auto decoded = luminpack::read_json_document<Json, Encoding>(json);
        if (!std::ranges::equal(luminpack::write_binary_document<Binary>(output, decoded), binary))
            throw std::runtime_error("Unexpected primitive root Binary for " + name);
    };
    exchange.template operator()<luminpack::json_encoding::utf8>(".utf8.json");
    exchange.template operator()<luminpack::json_encoding::utf16>(".utf16.json");
}
}

int main(int argc, char** argv)
{
    if (argc != 2) return 2;
    const std::filesystem::path directory(argv[1]);
    namespace scalar_root = luminpack_generated::root_System_x002eInt32;
    namespace float_root = luminpack_generated::root_float;
    namespace bools_root = luminpack_generated::root_bool_x005b_x005d;
    namespace nested_root = luminpack_generated::root_System_x002eCollections_x002eGeneric_x002eDictionary_x003cint_x002cSystem_x002eCollections_x002eGeneric_x002eList_x003cfloat_x003e_x003e;
    namespace array_root = luminpack_generated::root_int_x003f_x005b_x005d;
    namespace list_root = luminpack_generated::root_System_x002eCollections_x002eGeneric_x002eList_x003cfloat_x003f_x003e;
    namespace dict_root = luminpack_generated::root_System_x002eCollections_x002eGeneric_x002eDictionary_x003cint_x002cint_x003f_x003e;
    namespace derived_root = luminpack_generated::root_LuminPackPortableFixtures_x002ePrimitiveList;
    check<scalar_root::root_binary, scalar_root::root_json>(directory, "scalar");
    check<float_root::root_binary, float_root::root_json>(directory, "float");
    check<bools_root::root_binary, bools_root::root_json>(directory, "bools");
    check<nested_root::root_binary, nested_root::root_json>(directory, "nested");
    check<array_root::root_binary, array_root::root_json>(directory, "array");
    check<array_root::root_binary, array_root::root_json>(directory, "empty");
    check<array_root::root_binary, array_root::root_json>(directory, "null");
    check<list_root::root_binary, list_root::root_json>(directory, "list");
    check<dict_root::root_binary, dict_root::root_json>(directory, "dict");
    check<derived_root::root_binary, derived_root::root_json>(directory, "derived");
    try
    {
        (void)derived_root::read_root_binary(interop_models::load(directory / "derived-null.bin"));
        return 1;
    }
    catch (const luminpack::wire_error&) {}
    try
    {
        (void)luminpack::read_json_document<derived_root::root_json, luminpack::json_encoding::utf8>(
            interop_models::load(directory / "derived-null.json"));
        return 1;
    }
    catch (const luminpack::wire_error&) {}
}
