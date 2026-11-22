#include "direct_recursive.h"
#include "mutual_recursive.h"
#include "version_recursive.h"
#include "interop_models.h"

#include <iostream>

namespace direct = luminpack_generated::root_LuminPackCppRecursiveFixtures_x002eNode;
namespace version = luminpack_generated::root_LuminPackCppRecursiveFixtures_x002eVersionNode;
namespace mutual = luminpack_generated::root_LuminPackCppRecursiveFixtures_x002eEnvelope;

namespace
{
using namespace luminpack;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void reject(auto action, const char* message)
{
    try
    {
        action();
    }
    catch (const wire_error&)
    {
        return;
    }
    throw std::runtime_error(message);
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, const std::string& name, reader_limits binary_limits,
    json_read_options json_options, auto verify)
{
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto utf16 = interop_models::load(directory / (name + ".json"));
    const auto utf8 = interop_models::load(directory / (name + ".utf8.json"));
    const auto native_binary = read_binary_document<Binary>(binary, binary_limits);
    const auto native_utf16 = read_json_document<Json, json_encoding::utf16>(utf16, json_options);
    const auto native_utf8 = read_json_document<Json, json_encoding::utf8>(utf8, json_options);
    verify(native_binary);
    verify(native_utf16);
    verify(native_utf8);

    std::vector<std::byte> buffer(1 << 16);
    const auto rewritten_binary = write_binary_document<Binary>(buffer, native_utf8);
    require(std::ranges::equal(rewritten_binary, binary), "Rewrite the managed recursive Binary document exactly.");
    interop_models::store(directory / "out" / (name + ".bin"), rewritten_binary);

    const auto rewrite_json = [&]<json_encoding Encoding>(std::span<const std::byte> expected, std::string_view suffix)
    {
        const auto rewritten = write_json_document<Json, Encoding>(buffer, native_binary);
        require(std::ranges::equal(rewritten, expected), "Rewrite the managed recursive JSON document exactly.");
        interop_models::store(directory / "out" / (name + std::string(suffix)), rewritten);
    };
    rewrite_json.template operator()<json_encoding::utf16>(utf16, ".json");
    rewrite_json.template operator()<json_encoding::utf8>(utf8, ".utf8.json");
}

void verify_direct(const direct::root_value& value)
{
    require(value && value->field_Value == -19 && value->field_Next && value->field_Next->field_Value == 203,
        "Decode the ordinary recursive child.");
    const auto& list = value->field_Children;
    const auto& map = value->field_ByKey;
    require(list && list->size() == 2 && !list->front() && list->back() && list->back()->field_Value == 71 &&
        map && map->size() == 1 && map->at(-31) && map->at(-31)->field_Value == 37,
        "Decode optional list and dictionary entries within the recursive tree.");
}

void verify_version(const version::root_value& value)
{
    require(value && value->field_Value == -89 && value->field_Next && value->field_Next->field_Value == 97 &&
        value->field_Next->field_Next && value->field_Next->field_Next->field_Value == 101 &&
        !value->field_Next->field_Next->field_Next, "Decode each nested Version record and the last null.");
}

void verify_mutual(const mutual::root_value& value)
{
    require(value && value->field_Entry && value->field_Entry->field_Children &&
        value->field_Entry->field_Children->size() == 1, "Decode the acyclic root of the mutually recursive pair.");
    const auto& right = value->field_Entry->field_Children->at(31);
    require(right && right->field_Links && right->field_Links->size() == 2 &&
        right->field_Links->front() && !right->field_Links->front()->field_Children && !right->field_Links->back(),
        "Decode the mutually recursive collection entries and their null markers.");
}

void verify_depth(const interop_models::path& directory)
{
    const auto allowed = interop_models::load(directory / "default-depth.bin");
    const auto long_default = interop_models::load(directory / "default-deep.bin");
    const auto long_version = interop_models::load(directory / "version-deep.bin");
    reader default_cursor(long_default, direct::root_binary_limits);
    reject([&] { (void)direct::root_binary::read(default_cursor); },
        "Reject a recursive Default input above its declared depth.");
    require(default_cursor.remaining() == long_default.size(), "A failed recursive read retains its caller's Binary cursor.");
    reject([&] {
        (void)read_binary_document<version::root_binary>(std::span(long_version).first(long_version.size() - 1),
            version::root_binary_limits);
    }, "Reject a truncated recursive Version body.");
    auto accepted = read_binary_document<direct::root_binary>(allowed, direct::root_binary_limits);
    std::size_t count = 0;
    for (auto* node = accepted.get(); node; node = node->field_Next.get())
        ++count;
    require(count == 4, "A failed decode leaves the exact recursive depth available for a later operation.");

    auto accepted_version = read_binary_document<version::root_binary>(long_version, version::root_binary_limits);
    count = 0;
    for (auto* node = accepted_version.get(); node; node = node->field_Next.get())
        ++count;
    require(count == 5, "A managed Version chain keeps all five records in a single document.");
}

void verify_native_stack_guard()
{
    direct::root_value chain;
    for (std::size_t index = 0; index <= detail::recursive_record_stack_limit; ++index)
    {
        auto parent = std::make_unique<direct::root_value::element_type>();
        parent->field_Next = std::move(chain);
        chain = std::move(parent);
    }

    std::vector<std::byte> buffer(1 << 17);
    reject([&] { (void)write_binary_document<direct::root_binary>(buffer, chain); },
        "The recursive Binary writer must check the native stack limit.");
    reject([&] { (void)write_json_document<direct::root_json>(buffer, chain); },
        "The recursive JSON writer must check the native stack limit.");
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed recursive fixture directory.");
    const auto directory = interop_models::path(argv[1]);
    std::filesystem::create_directories(directory / "out");
    static_assert(!direct::root_requires_reference_graph && !version::root_requires_reference_graph &&
        !mutual::root_requires_reference_graph);
    static_assert(std::same_as<mutual::root_value, std::optional<mutual::root_value::value_type>>);
    exchange<direct::root_binary, direct::root_json>(directory, "direct", direct::root_binary_limits,
        direct::root_json_options, verify_direct);
    exchange<version::root_binary, version::root_json>(directory, "version", version::root_binary_limits,
        version::root_json_options, verify_version);
    exchange<mutual::root_binary, mutual::root_json>(directory, "mutual", mutual::root_binary_limits,
        mutual::root_json_options, verify_mutual);
    verify_depth(directory);
    verify_native_stack_guard();
    std::cout << "C++ verified the managed recursive documents.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
