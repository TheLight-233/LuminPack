#include "recursive_composition.h"
#include "recursive_outgoing.h"
#include "interop_models.h"

#include <iostream>

namespace composition = luminpack_generated::root_LuminPackCppRecursiveCompositionFixtures_x002eDocument;
namespace outgoing = luminpack_generated::root_LuminPackCppRecursiveCompositionFixtures_x002eOutgoingDocument;

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

template<json_encoding Encoding, class Codec>
void exchange_json(const interop_models::path& directory, const std::string& name, const typename Codec::value_type& binary,
    json_read_options options, auto verify_value)
{
    const auto original = interop_models::load(directory / name);
    auto decoded = read_reference_json_document<Codec, Encoding>(original, options);
    verify_value(decoded.value);
    std::vector<std::byte> output(original.size());
    const auto written = write_reference_json_document<Codec, Encoding>(output, binary);
    require(std::ranges::equal(written, original), "Write the exact managed mixed recursive JSON document.");
    interop_models::store(directory / "out" / name, written);
}

template<class Codec>
void check_rollback(std::span<const std::byte> bytes, reader_limits limits, auto verify_value, bool versioned = false)
{
    reference_graph graph;
    reference_read_state references(graph);
    version_state version;
    reader truncated(bytes.first(bytes.size() - 1), limits);
    try
    {
        (void)Codec::read(truncated, version, references);
        throw std::runtime_error("Accepts a truncated mixed recursive record.");
    }
    catch (const wire_error&)
    {
        require(truncated.remaining() == bytes.size() - 1, "A failed read advances the caller's cursor.");
    }

    if (versioned)
    {
        // The UTF-16 length-profile Version marker and ordinal zero must not
        // resolve against any descriptor table retained by the failed read.
        constexpr auto ordinal = std::array{std::byte{251}, std::byte{0x52}, std::byte{0}};
        reader ordinal_cursor(ordinal);
        try
        {
            (void)read_version_header(ordinal_cursor, version, string_profile::utf16_length);
        }
        catch (const wire_error&)
        {
            require(ordinal_cursor.remaining() == ordinal.size(), "A rolled-back Version ordinal consumes input.");
        }
        require(ordinal_cursor.remaining() == ordinal.size(), "The failed record retains a Version registration.");
    }

    reader valid(bytes, limits);
    verify_value(Codec::read(valid, version, references));
    valid.require_end();
}

void verify_outgoing(const outgoing::root_value& value)
{
    require(value && value->field_Root && value->field_Root->field_Text == u"root \u03a9" &&
        value->field_Root->field_Children && value->field_Root->field_Children->size() == 2 &&
        value->field_Root->field_Children->front() && !value->field_Root->field_Children->back(),
        "Read the owning recursive Version tree.");
    const auto& root = *value->field_Root;
    require(value->field_Shared && value->field_Shared->field_Value == 53 && root.field_Leaf == value->field_Shared &&
        root.field_Children->front()->field_Leaf == value->field_Shared && value->field_Outside &&
        value->field_Outside != value->field_Shared && value->field_Outside->field_Value == -90 &&
        root.field_NestedOnly && root.field_NestedOnly->field_Number == -71,
        "Share a newly tracked value inside and outside the recursive Version member windows.");
}

void check_outgoing(const interop_models::path& directory)
{
    const auto binary = interop_models::load(directory / "outgoing.bin");
    auto decoded = read_binary_reference_document<outgoing::root_binary>(binary,
        outgoing::root_binary_limits, outgoing::root_binary_reference_depth);
    verify_outgoing(decoded.value);
    std::vector<std::byte> output(binary.size());
    const auto written = write_binary_reference_document<outgoing::root_binary>(output, decoded);
    require(std::ranges::equal(written, binary), "Write the exact managed recursive Version document with tracked leaves.");
    interop_models::store(directory / "out" / "outgoing.bin", written);
    exchange_json<json_encoding::utf16, outgoing::root_json>(directory, "outgoing.json", decoded.value,
        outgoing::root_json_options, verify_outgoing);
    exchange_json<json_encoding::utf8, outgoing::root_json>(directory, "outgoing.utf8.json", decoded.value,
        outgoing::root_json_options, verify_outgoing);
    check_rollback<outgoing::root_binary>(binary, outgoing::root_binary_limits, verify_outgoing, true);
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
    static_assert(composition::root_requires_reference_graph && outgoing::root_requires_reference_graph);
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
    exchange_json<json_encoding::utf16, composition::root_json>(directory, "composition.json", decoded.value,
        composition::root_json_options, verify);
    exchange_json<json_encoding::utf8, composition::root_json>(directory, "composition.utf8.json", decoded.value,
        composition::root_json_options, verify);
    check_rollback<composition::root_binary>(original, composition::root_binary_limits, verify);
    check_outgoing(directory);
    check_nested_stack_limit(decoded);
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
