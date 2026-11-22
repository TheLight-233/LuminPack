#include "direct_closed.h"
#include "nested_closed.h"
#include "interop_models.h"

#include <iostream>

namespace direct = luminpack_generated::root_LuminPackCppClosedGenericFixtures_x002eBox_x003cstring_x003e;
namespace nested = luminpack_generated::root_LuminPackCppClosedGenericFixtures_x002eCell_x003cint_x003e;

namespace
{
using namespace luminpack;
using nested_binary = list_codec<nested::root_binary>;
using nested_json = json_list_codec<nested::root_json>;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void verify_direct(const direct::root_value& value)
{
    require(value && value->field_Value == u"caf\u00e9 \u03a9" && value->field_Items &&
        *value->field_Items == std::vector<std::optional<std::u16string>>{std::nullopt, u"\u503c"} &&
        value->field_ByKey && value->field_ByKey->size() == 1 && value->field_ByKey->at(71) == u"item",
        "Read the direct closed generic and its list and dictionary slots.");
}

void verify_nested(const nested_binary::value_type& value)
{
    require(value && value->size() == 3 && (*value)[0] && !(*value)[1] && (*value)[2] &&
        (*value)[0]->field_Value == -71 && (*value)[0]->field_Index == 13 &&
        (*value)[2]->field_Value == 97 && (*value)[2]->field_Index == -31,
        "Read two closed Version values with a shared schema and a null list element.");
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, const std::string& name, auto verify)
{
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto json = interop_models::load(directory / (name + ".json"));
    const auto utf8 = interop_models::load(directory / (name + ".utf8.json"));
    const auto from_binary = read_binary_document<Binary>(binary);
    const auto from_json = read_json_document<Json>(json);
    const auto from_utf8 = read_json_document<Json, json_encoding::utf8>(utf8);
    verify(from_binary);
    verify(from_json);
    verify(from_utf8);

    std::vector<std::byte> buffer(std::max({binary.size(), json.size(), utf8.size()}));
    auto bytes = write_binary_document<Binary>(buffer, from_json);
    require(std::ranges::equal(bytes, binary), "Write the exact managed closed generic Binary.");
    interop_models::store(directory / "out" / (name + ".bin"), bytes);
    bytes = write_json_document<Json>(buffer, from_binary);
    require(std::ranges::equal(bytes, json), "Write the exact managed closed generic UTF-16 JSON.");
    interop_models::store(directory / "out" / (name + ".json"), bytes);
    bytes = write_json_document<Json, json_encoding::utf8>(buffer, from_binary);
    require(std::ranges::equal(bytes, utf8), "Write the exact managed closed generic UTF-8 JSON.");
    interop_models::store(directory / "out" / (name + ".utf8.json"), bytes);
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed closed generic fixture directory.");
    const interop_models::path directory = argv[1];
    std::filesystem::create_directories(directory / "out");
    exchange<direct::root_binary, direct::root_json>(directory, "direct", verify_direct);
    exchange<nested_binary, nested_json>(directory, "nested", verify_nested);
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
