#include "envelope.hpp"
#include "graph-choice.hpp"
#include "graph-packet.hpp"
#include "node.hpp"
#include "rtti-wrapper.hpp"
#include "version-node.hpp"
#include "interop_models.h"

#include <iostream>

namespace envelope = luminpack_generated::root_LuminPackCppReferenceFixtures_x002eEnvelope;
namespace graph_choice = luminpack_generated::root_LuminPackCppReferenceFixtures_x002eIGraphChoice;
namespace graph_packet = luminpack_generated::root_LuminPackCppReferenceFixtures_x002eGraphPacket;
namespace node = luminpack_generated::root_LuminPackCppReferenceFixtures_x002eNode;
namespace rtti = luminpack_generated::root_LuminPackCppReferenceFixtures_x002eIWrapper;
namespace version = luminpack_generated::root_LuminPackCppReferenceFixtures_x002eVersionNode;

namespace
{
using namespace luminpack;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void reject(auto operation)
{
    try
    {
        operation();
    }
    catch (const wire_error&)
    {
        return;
    }
    throw std::runtime_error("Invalid reference data was accepted.");
}

void verify_node(const auto* value)
{
    require(value && value->field_Value == 17 && value->field_Label == u"alpha",
        "Read the reference root.");
    const auto* other = value->field_Next;
    const auto* peer = value->field_Peer;
    require(other && other->field_Value == 29 && other->field_Label == u"caf\u00e9" &&
        other->field_Next == value && other->field_Peer == peer, "Preserve a shared object and the mutual cycle.");
    require(peer && peer->field_Home == value && peer->field_Rank == -7, "Resolve a reference across concrete types.");
}

void verify_version(const auto* value)
{
    require(value && value->field_Count == 37 && value->field_Next == value &&
        value->field_Items && value->field_Items->size() == 3 &&
        (*value->field_Items)[0] == value && (*value->field_Items)[1] == nullptr && (*value->field_Items)[2] == value,
        "Preserve the Version record's early shell and its collection back references.");
}

void verify_envelope(const envelope::root_value& value)
{
    require(value && value->field_Items && value->field_Items->size() == 132 &&
        value->field_ByName && value->field_Version, "Decode the ordinary root with tracked descendants.");
    verify_node(value->field_Alternate);
    verify_version(value->field_Version);
    const auto& items = *value->field_Items;
    require(value->field_Alternate == value->field_Original && value->field_Alternate == value->field_ByName->at(u"root") &&
        items[127]->field_Value == 1127 && items[127] == items[130] && value->field_Alternate->field_Next == items[131],
        "Share identity across explicit string profiles, containers, and reference ids beyond 127.");
    require(value->field_Pick && std::get<decltype(value->field_Alternate)>(*value->field_Pick) == value->field_Alternate,
        "Share identity with a statically dispatched Union case.");
}

void verify_rtti(const rtti::root_value& value)
{
    const auto* payload = dynamic_cast<const rtti::type_LuminPackCppReferenceFixtures_x002eWrapper*>(value.get());
    require(payload != nullptr, "Select the ordinary RTTI case.");
    verify_node(payload->field_Value);
}

template<class Base>
struct graph_external final : Base
{
    graph_external() { ++live_count; }
    ~graph_external() override { --live_count; }

    std::int32_t value = 0;
    inline static int live_count = 0;
};

template<class T>
struct graph_external_binding
{
    static std::unique_ptr<T> read_binary(reader& input)
    {
        auto value = std::make_unique<T>();
        value->value = input.read<std::int32_t>() ^ 59;
        return value;
    }

    static void write_binary(writer& output, std::uint16_t tag, const T& value)
    {
        write_registered_union_tag<true>(output, tag);
        output.write(value.value ^ 59);
    }

    static std::unique_ptr<T> read_json(auto& input)
    {
        auto value = std::make_unique<T>();
        value->value = input.template read_number<std::int32_t>() ^ 71;
        return value;
    }

    static void write_json(auto& output, std::uint16_t tag, const T& value)
    {
        output.token('{');
        output.template write_property<json_name{u"$type"}>();
        output.write_number(tag);
        output.token(',');
        output.template write_property<json_name{u"$value"}>();
        output.write_number(value.value ^ 71);
        output.token('}');
    }
};

void verify_graph_root(graph_choice::root_value value)
{
    const auto* tracked = dynamic_cast<const graph_choice::type_LuminPackCppReferenceFixtures_x002eGraphNode*>(value);
    require(tracked && tracked->field_Id == 73, "Decode the direct tracked RTTI case.");
    const auto* peer = dynamic_cast<const graph_choice::type_LuminPackCppReferenceFixtures_x002eGraphLeaf*>(tracked->field_Next);
    require(peer && peer->field_Value == -81 && peer->field_Link == tracked,
        "Preserve a cycle through an ordinary RTTI case.");
}

template<class Case>
void verify_graph_version(const auto* value)
{
    const auto* tracked = dynamic_cast<const Case*>(value);
    require(tracked && tracked->field_Id == 79 && tracked->field_Next == value,
        "Preserve the tracked Version case and its RTTI back reference.");
}

using packet_registry = graph_packet::type_LuminPackCppReferenceFixtures_x002eIGraphChoice_binary::registry;
using packet_external = graph_external<packet_registry::base_type>;

void verify_graph_packet(const graph_packet::root_value& value)
{
    require(value && value->field_Direct && value->field_Items && value->field_Items->size() == 6,
        "Read the record containing a graph RTTI Union.");
    const auto* tracked = value->field_Direct;
    const auto& items = *value->field_Items;
    require(tracked->field_Id == 73 && value->field_ThroughUnion == tracked && items[0] == tracked && items[5] == tracked,
        "Share the same concrete shell across direct members, RTTI slots and list elements.");
    const auto* child = dynamic_cast<const graph_packet::type_LuminPackCppReferenceFixtures_x002eGraphLeaf*>(tracked->field_Next);
    const auto* leaf = dynamic_cast<const graph_packet::type_LuminPackCppReferenceFixtures_x002eGraphLeaf*>(items[1]);
    require(child && leaf && child != leaf && child->field_Link == tracked && leaf->field_Link == tracked &&
        leaf->field_Value == -81 && !items[2], "Own ordinary RTTI cases independently and preserve their graph edges.");
    verify_graph_version<graph_packet::type_LuminPackCppReferenceFixtures_x002eGraphVersion>(items[3]);
    const auto* external = dynamic_cast<const packet_external*>(items[4]);
    require(external && external->value == 97, "Resolve the wide app-owned case through the graph RTTI registry.");
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, const std::string& name, auto verify,
    reader_limits limits, std::size_t max_depth)
{
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto utf8 = interop_models::load(directory / (name + ".utf8.json"));
    const auto utf16 = interop_models::load(directory / (name + ".utf16.json"));
    auto from_binary = read_binary_reference_document<Binary>(binary, limits, max_depth);
    auto from_utf8 = read_reference_json_document<Json, json_encoding::utf8>(utf8);
    auto from_utf16 = read_reference_json_document<Json, json_encoding::utf16>(utf16);
    verify(from_binary.value);
    verify(from_utf8.value);
    verify(from_utf16.value);

    std::vector<std::byte> buffer(1 << 18);
    const auto out = directory / "out";
    const auto encoded = write_binary_reference_document<Binary>(buffer, from_utf8.value);
    require(std::ranges::equal(encoded, binary), "Write the exact binary reference graph.");
    interop_models::store(out / (name + ".bin"), encoded);
    const auto encoded8 = write_reference_json_document<Json, json_encoding::utf8>(buffer, from_binary.value);
    require(std::ranges::equal(encoded8, utf8), "Write the exact UTF-8 JSON reference graph.");
    interop_models::store(out / (name + ".utf8.json"), encoded8);
    const auto encoded16 = write_reference_json_document<Json, json_encoding::utf16>(buffer, from_binary.value);
    require(std::ranges::equal(encoded16, utf16), "Write the exact UTF-16 JSON reference graph.");
    interop_models::store(out / (name + ".utf16.json"), encoded16);
}

void invalid_references(const interop_models::path& directory)
{
    const auto binary = interop_models::load(directory / "node.bin");
    for (std::size_t size = 0; size < binary.size(); ++size)
        reject([&] { (void)read_binary_reference_document<node::root_binary>(std::span(binary).first(size)); });

    constexpr std::array known_invalid{std::byte{250}, std::byte{0}};
    reject([&] { (void)read_binary_reference_document<node::root_binary>(known_invalid); });
    reject([&] { (void)read_binary_reference_document<node::root_binary>(binary, {}, 1); });

    for (std::string_view json : {R"({"$ref":0})", R"({"$id":0,"Peer":{"$ref":0}})", R"({"$id":0,"Next":{"$id":0}})"})
    {
        const auto bytes = std::as_bytes(std::span(json));
        reject([&] { (void)read_reference_json_document<node::root_json, json_encoding::utf8>(bytes); });
    }
}

void version_aliases()
{
    constexpr std::string_view json =
        R"({"PreviousCount":37,"\u0024id":71000,"Items":[{"$ref":71000},null,{"$ref":71000}],"Next":{"\u0024ref":71000}})";
    const auto result = read_reference_json_document<version::root_json, json_encoding::utf8>(std::as_bytes(std::span(json)));
    verify_version(result.value);
}

void deferred_rtti_reference(const interop_models::path& directory)
{
    const auto utf8 = interop_models::load(directory / "rtti-wrapper.utf8.json");
    const std::string_view text(reinterpret_cast<const char*>(utf8.data()), utf8.size());
    const auto separator = text.find(",\"$value\":");
    require(separator != std::string_view::npos, "Find the generated RTTI envelope.");
    auto reordered = std::string("{") + std::string(text.substr(separator + 1, text.size() - separator - 2)) + "," +
        std::string(text.substr(1, separator - 1)) + "}";
    const auto input = std::as_bytes(std::span(reordered));
    const auto result = read_reference_json_document<rtti::root_json, json_encoding::utf8>(input);
    verify_rtti(result.value);
}

void reuse_shells(const interop_models::path& directory)
{
    using object = std::remove_pointer_t<node::root_value>;
    const auto binary = interop_models::load(directory / "node.bin");
    reference_graph binary_graph;
    auto* existing = binary_graph.make<object>();
    auto* value = existing;
    reference_read_state binary_references(binary_graph);
    version_state version;
    reader truncated(std::span<const std::byte>(binary).first(binary.size() - 1));
    reject([&] { node::root_binary::read_into(truncated, value, version, binary_references); });
    require(truncated.remaining() == binary.size() - 1 && value == existing && value->field_Next == nullptr,
        "Roll back a failed tracked read before retrying with the same shell and state.");
    reader binary_cursor(binary);
    node::root_binary::read_into(binary_cursor, value, version, binary_references);
    binary_cursor.require_end();
    require(value == existing, "Reuse the supplied Binary shell.");
    verify_node(value);

    const auto utf8 = interop_models::load(directory / "node.utf8.json");
    reference_graph json_graph;
    auto* json_existing = json_graph.make<object>();
    auto* json_value = json_existing;
    reference_read_state json_references(json_graph);
    reference_json_reader<json_encoding::utf8> json_cursor(utf8, {}, json_references);
    node::root_json::read_into(json_cursor, json_value);
    json_cursor.require_end();
    require(json_value == json_existing, "Reuse the supplied JSON shell.");
    verify_node(json_value);
}

void reuse_rtti_shell(const interop_models::path& directory)
{
    using tracked = graph_choice::type_LuminPackCppReferenceFixtures_x002eGraphNode;
    const auto binary = interop_models::load(directory / "tracked-case.bin");
    reference_graph graph;
    auto* shell = graph.make<tracked>();
    graph_choice::root_value value = shell;
    reference_read_state references(graph);
    version_state version;
    reader input(binary);
    graph_choice::root_binary::read_into(input, value, version, references);
    input.require_end();
    require(value == shell, "Reuse the existing exact tracked RTTI shell.");
    verify_graph_root(value);
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed reference graph fixture directory.");
    const auto directory = interop_models::path(argv[1]);
    std::filesystem::create_directories(directory / "out");
    static_assert(node::root_requires_reference_graph && version::root_requires_reference_graph &&
        envelope::root_requires_reference_graph && rtti::root_requires_reference_graph &&
        graph_choice::root_requires_reference_graph && graph_packet::root_requires_reference_graph);

    exchange<node::root_binary, node::root_json>(directory, "node",
        [](const auto* value) { verify_node(value); }, node::root_binary_limits, node::root_binary_reference_depth);
    exchange<node::root_binary, node::root_json>(directory, "null-node",
        [](const auto* value) { require(value == nullptr, "Read a null reference root."); },
        node::root_binary_limits, node::root_binary_reference_depth);
    exchange<version::root_binary, version::root_json>(directory, "version-node",
        [](const auto* value) { verify_version(value); }, version::root_binary_limits, version::root_binary_reference_depth);
    exchange<envelope::root_binary, envelope::root_json>(directory, "envelope",
        verify_envelope, envelope::root_binary_limits, envelope::root_binary_reference_depth);
    exchange<rtti::root_binary, rtti::root_json>(directory, "rtti-wrapper",
        verify_rtti, rtti::root_binary_limits, rtti::root_binary_reference_depth);
    exchange<graph_choice::root_binary, graph_choice::root_json>(directory, "tracked-case",
        verify_graph_root, graph_choice::root_binary_limits, graph_choice::root_binary_reference_depth);
    exchange<graph_choice::root_binary, graph_choice::root_json>(directory, "tracked-version-case",
        [](const auto* value) { verify_graph_version<graph_choice::type_LuminPackCppReferenceFixtures_x002eGraphVersion>(value); },
        graph_choice::root_binary_limits, graph_choice::root_binary_reference_depth);
    exchange<graph_choice::root_binary, graph_choice::root_json>(directory, "null-tracked-union",
        [](const auto* value) { require(!value, "Read a null graph RTTI Union."); },
        graph_choice::root_binary_limits, graph_choice::root_binary_reference_depth);
    packet_registry::register_case<packet_external, graph_external_binding<packet_external>>(600);
    exchange<graph_packet::root_binary, graph_packet::root_json>(directory, "tracked-union-packet",
        verify_graph_packet, graph_packet::root_binary_limits, graph_packet::root_binary_reference_depth);
    require(packet_external::live_count == 0, "Free registered cases when their document graphs are destroyed.");
    invalid_references(directory);
    version_aliases();
    reuse_shells(directory);
    deferred_rtti_reference(directory);
    reuse_rtti_shell(directory);

    std::cout << "C++ verified binary and JSON reference graph fixtures.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
