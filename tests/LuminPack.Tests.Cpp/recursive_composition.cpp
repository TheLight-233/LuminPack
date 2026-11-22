#include "recursive_composition.h"
#include "interop_models.h"

#include <iostream>

namespace composition = luminpack_generated::root_LuminPackCppRecursiveCompositionFixtures_x002eDocument;

namespace
{
using namespace luminpack;
using ordinary_case = composition::type_LuminPackCppRecursiveCompositionFixtures_x002eOrdinaryCase;
using tree = composition::type_LuminPackCppRecursiveCompositionFixtures_x002eTree;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void verify_tree(const std::unique_ptr<tree>& value)
{
    require(value && value->field_Text == u"caf\u00e9" && value->field_Children &&
        value->field_Children->size() == 2 && !value->field_Children->front() &&
        value->field_Children->back() && value->field_Children->back()->field_Text == u"\u503c" &&
        !value->field_Children->back()->field_Children, "Decode the recursive list and its optional records.");
}

void verify(const composition::root_value& value)
{
    require(value && value->field_Owner && value->field_Owner == value->field_Alias,
        "The tracked owner keeps its identity.");
    const auto shared = value->field_Shared;
    require(shared && shared->field_Value == 71 && shared->field_Self == shared && shared == value->field_Owner->field_Link &&
        shared == value->field_TrackedCase, "Graph references agree across independent fields and the tracked Union case.");

    const auto* ordinary = dynamic_cast<const ordinary_case*>(value->field_StaticCase);
    require(ordinary, "Read the ordinary case of the mixed Union.");
    verify_tree(value->field_Direct);
    verify_tree(value->field_Owner->field_Tree);
    verify_tree(ordinary->field_Tree);
    require(value->field_Direct != value->field_Owner->field_Tree && value->field_Direct != ordinary->field_Tree &&
        value->field_Owner->field_Tree != ordinary->field_Tree, "Ordinary recursive occurrences are independently owned.");
}

template<json_encoding Encoding>
void exchange_json(const interop_models::path& directory, const std::string& name, const composition::root_value& binary)
{
    const auto original = interop_models::load(directory / name);
    auto decoded = read_reference_json_document<composition::root_json, Encoding>(original, composition::root_json_options);
    verify(decoded.value);
    std::vector<std::byte> output(original.size());
    const auto written = write_reference_json_document<composition::root_json, Encoding>(output, binary);
    require(std::ranges::equal(written, original), "Write the exact managed mixed recursive JSON document.");
    interop_models::store(directory / "out" / name, written);
}

void check_rollback(std::span<const std::byte> bytes)
{
    reference_graph graph;
    reference_read_state references(graph);
    version_state version;
    reader truncated(bytes.first(bytes.size() - 1), composition::root_binary_limits);
    try
    {
        (void)composition::root_binary::read(truncated, version, references);
        throw std::runtime_error("Accepts a truncated mixed recursive record.");
    }
    catch (const wire_error&)
    {
        require(truncated.remaining() == bytes.size() - 1, "A failed read advances the caller's cursor.");
    }

    reader valid(bytes, composition::root_binary_limits);
    verify(composition::root_binary::read(valid, version, references));
    valid.require_end();
}

void check_nested_stack_limit(reference_document<composition::root_value>& document)
{
    std::unique_ptr<tree> child;
    for (std::size_t depth = 0; depth <= detail::recursive_record_stack_limit; ++depth)
    {
        auto parent = std::make_unique<tree>();
        if (child)
            parent->field_Children.emplace().push_back(std::move(child));
        child = std::move(parent);
    }
    document.value->field_Owner->field_Tree = std::move(child);
    std::vector<std::byte> output(1 << 17);
    try
    {
        (void)write_binary_reference_document<composition::root_binary>(output, document);
        throw std::runtime_error("The writer loses its native stack guard below the tracked parent.");
    }
    catch (const wire_error&)
    {
        return;
    }
}
}

int main(int argc, char** argv)
try
{
    static_assert(composition::root_requires_reference_graph);
    require(argc == 2, "Expected the managed recursive composition fixture directory.");
    const interop_models::path directory = argv[1];
    std::filesystem::create_directories(directory / "out");
    const auto original = interop_models::load(directory / "composition.bin");
    auto decoded = read_binary_reference_document<composition::root_binary>(original,
        composition::root_binary_limits, composition::root_binary_reference_depth);
    verify(decoded.value);
    std::vector<std::byte> output(original.size());
    const auto written = write_binary_reference_document<composition::root_binary>(output, decoded);
    require(std::ranges::equal(written, original), "Write the exact managed mixed recursive Binary document.");
    interop_models::store(directory / "out" / "composition.bin", written);
    exchange_json<json_encoding::utf16>(directory, "composition.json", decoded.value);
    exchange_json<json_encoding::utf8>(directory, "composition.utf8.json", decoded.value);
    check_rollback(original);
    check_nested_stack_limit(decoded);
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
