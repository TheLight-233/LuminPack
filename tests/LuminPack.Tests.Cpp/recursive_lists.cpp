#include "interop_models.h"
#include "list.hpp"
#include "tracked.hpp"
#include "tree.hpp"

namespace list = luminpack_generated::root_LuminPackPortableFixtures_x002eSelfList;
namespace tracked = luminpack_generated::root_LuminPackPortableFixtures_x002eSharedList;
namespace tree = luminpack_generated::root_LuminPackPortableFixtures_x002eUnionTree;

template<class Binary, class Json>
void exchange(const std::filesystem::path& directory, const std::string& name)
{
    using namespace luminpack;
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto json = interop_models::load(directory / (name + ".json"));
    std::vector<std::byte> buffer(4096);
    auto value = read_binary_document<Binary>(binary);
    if (!std::ranges::equal(write_json_document<Json, json_encoding::utf8>(buffer, value), json))
        throw std::runtime_error("Unexpected recursive JSON for " + name);
    auto from_json = read_json_document<Json, json_encoding::utf8>(json);
    if (!std::ranges::equal(write_binary_document<Binary>(buffer, from_json), binary))
        throw std::runtime_error("Unexpected recursive Binary for " + name);
}

int main(int argc, char** argv)
{
    using namespace luminpack;
    if (argc != 2) return 2;
    const std::filesystem::path directory(argv[1]);
    exchange<list::root_binary, list::root_json>(directory, "list");
    exchange<tree::root_binary, tree::root_json>(directory, "tree");
    const auto binary = interop_models::load(directory / "tracked.bin");
    const auto json = interop_models::load(directory / "tracked.json");
    auto wide = tracked::read_root_binary(interop_models::load(directory / "wide.bin"));
    if (!wide.value || wide.value->items.size() != 255) return 1;
    try
    {
        (void)tracked::read_root_binary(interop_models::load(directory / "invalid.bin"));
        return 1;
    }
    catch (const wire_error&) {}
    auto value = tracked::read_root_binary(binary);
    auto from_json = read_reference_json_document<tracked::root_json, json_encoding::utf8>(json);
    for (auto* node : { value.value, from_json.value })
        if (!node || node->items.size() != 2 || node->items[0] != node || node->items[1]) return 1;
    std::vector<std::byte> buffer(4096);
    if (!std::ranges::equal(write_binary_reference_document<tracked::root_binary>(buffer, from_json.value), binary))
        return 1;
    return !std::ranges::equal(write_reference_json_document<tracked::root_json, json_encoding::utf8>(buffer, value.value), json);
}
