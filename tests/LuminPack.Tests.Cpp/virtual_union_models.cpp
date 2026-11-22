#include "large-document.hpp"
#include "large-graph.hpp"
#include "large-graph-document.hpp"
#include "large-interface.hpp"
#include "interop_models.h"

#include <iostream>

namespace ordinary = luminpack_generated::root_LuminPackCppVirtualUnionFixtures_x002eILarge;
namespace graph = luminpack_generated::root_LuminPackCppVirtualUnionFixtures_x002eLargeGraph;
namespace document = luminpack_generated::root_LuminPackCppVirtualUnionFixtures_x002eLargeDocument;
namespace mixed = luminpack_generated::root_LuminPackCppVirtualUnionFixtures_x002eGraphDocument;

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

using registry = mixed::type_LuminPackCppVirtualUnionFixtures_x002eLargeGraph_binary::registry;
struct application_case final : registry::base_type
{
    std::int32_t value = 0;
};

struct application_binding
{
    static std::unique_ptr<application_case> read_binary(reader& input)
    {
        auto result = std::make_unique<application_case>();
        result->value = input.read<std::int32_t>() ^ 23;
        return result;
    }

    static void write_binary(writer& output, std::uint16_t tag, const application_case& value)
    {
        write_registered_union_tag<registry::wide_tags>(output, tag);
        output.write(value.value ^ 23);
    }

    static std::unique_ptr<application_case> read_json(auto& input)
    {
        auto result = std::make_unique<application_case>();
        result->value = input.template read_number<std::int32_t>() ^ 73;
        return result;
    }

    static void write_json(auto& output, std::uint16_t tag, const application_case& value)
    {
        output.token('{');
        output.template write_property<json_name{u"$type"}>();
        output.write_number(tag);
        output.token(',');
        output.template write_property<json_name{u"$value"}>();
        output.write_number(value.value ^ 73);
        output.token('}');
    }
};

template<class Binary, class Json, bool Graph = false>
void exchange(const interop_models::path& directory, const std::string& name, auto verify)
{
    const auto binary = interop_models::load(directory / (name + ".bin"));
    const auto utf8 = interop_models::load(directory / (name + ".utf8.json"));
    const auto utf16 = interop_models::load(directory / (name + ".utf16.json"));
    const auto read_json = []<json_encoding Encoding>(std::span<const std::byte> input)
    {
        if constexpr (Graph)
            return read_reference_json_document<Json, Encoding>(input);
        else
            return read_json_document<Json, Encoding>(input);
    };
    const auto read_binary = [](std::span<const std::byte> input)
    {
        if constexpr (Graph)
            return read_binary_reference_document<Binary>(input);
        else
            return read_binary_document<Binary>(input);
    };
    const auto value = [](const auto& result) -> decltype(auto)
    {
        if constexpr (Graph)
            return (result.value);
        else
            return (result);
    };

    const auto from_binary = read_binary(binary);
    const auto from_utf8 = read_json.template operator()<json_encoding::utf8>(utf8);
    const auto from_utf16 = read_json.template operator()<json_encoding::utf16>(utf16);
    verify(value(from_binary));
    verify(value(from_utf8));
    verify(value(from_utf16));

    std::vector<std::byte> buffer(1 << 16);
    std::span<const std::byte> written;
    if constexpr (Graph)
        written = write_binary_reference_document<Binary>(buffer, value(from_utf8));
    else
        written = write_binary_document<Binary>(buffer, value(from_utf8));
    require(std::ranges::equal(written, binary), "Write the exact managed virtual Union binary document.");
    interop_models::store(directory / "out" / (name + ".bin"), written);

    const auto write_json = [&]<json_encoding Encoding>(std::span<const std::byte> source, std::string_view suffix)
    {
        if constexpr (Graph)
            written = write_reference_json_document<Json, Encoding>(buffer, value(from_binary));
        else
            written = write_json_document<Json, Encoding>(buffer, value(from_binary));
        require(std::ranges::equal(written, source), "Write the exact managed virtual Union JSON document.");
        interop_models::store(directory / "out" / (name + std::string(suffix)), written);
    };
    write_json.template operator()<json_encoding::utf8>(utf8, ".utf8.json");
    write_json.template operator()<json_encoding::utf16>(utf16, ".utf16.json");
}

void check_all_cases(const document::root_value& value)
{
    require(value && value->field_Items && value->field_Items->size() == 34 &&
        value->field_Default && value->field_Alternate, "Decode every declared case and both effective profiles.");
    const auto& items = *value->field_Items;
    const auto* first = dynamic_cast<const document::type_LuminPackCppVirtualUnionFixtures_x002eCase01*>(items[0].get());
    const auto* last = dynamic_cast<const document::type_LuminPackCppVirtualUnionFixtures_x002eCase32*>(items[31].get());
    require(first && first->field_Number == 7 && last && last->field_Number == 224,
        "Resolve the first and last ordinary case hashes.");
    const auto* closed_number = items[32].get();
    const auto* closed_text = items[33].get();
    require(closed_number && closed_text && typeid(*closed_number) != typeid(*closed_text),
        "Resolve the closed generic instantiations independently.");
}

void check_tracked(const graph::root_value value)
{
    const auto* tracked = dynamic_cast<const graph::type_LuminPackCppVirtualUnionFixtures_x002eTracked*>(value);
    require(tracked && tracked->field_Count == 89 && tracked->field_Next == value, "Resolve a virtual Version self-cycle.");
}

void check_mixed(const mixed::root_value& value)
{
    require(value && value->field_Anchor && value->field_Anchor->field_Count == 89 &&
        value->field_Anchor->field_Next == value->field_Anchor && value->field_Items && value->field_Items->size() == 7,
        "Read the tracked and ordinary cases from the mixed graph's virtual Union.");
    const auto& items = *value->field_Items;
    const auto* saved = dynamic_cast<const mixed::type_LuminPackCppVirtualUnionFixtures_x002eCase32*>(items[1]);
    const auto* external = dynamic_cast<const application_case*>(items[4]);
    require(value->field_Anchor == value->field_ThroughUnion && items[0] == value->field_Anchor &&
        items[6] == value->field_Anchor && saved && saved->field_Number == 141 && !items[3] &&
        external && external->value == 113, "Retain shared identities and the wide registered case.");
}

void check_inherited_slot(const interop_models::path& directory)
{
    struct subclass final : ordinary::type_LuminPackCppVirtualUnionFixtures_x002eCase32 {};
    auto derived = std::make_unique<subclass>();
    derived->field_Number = 141;
    ordinary::root_value value = std::move(derived);
    std::array<std::byte, 128> buffer{};
    require(std::ranges::equal(write_binary_document<ordinary::root_binary>(buffer, value),
        interop_models::load(directory / "inherited-case.bin")), "Invoke the inherited virtual Binary slot.");
    require(std::ranges::equal(write_json_document<ordinary::root_json, json_encoding::utf8>(buffer, value),
        interop_models::load(directory / "inherited-case.utf8.json")), "Invoke the inherited virtual JSON slot.");

    using base = ordinary::type_LuminPackCppVirtualUnionFixtures_x002eILarge_base;
    struct unregistered final : base {};
    value = std::make_unique<unregistered>();
    reject<std::invalid_argument>([&] { (void)write_binary_document<ordinary::root_binary>(buffer, value); },
        "The root's default virtual slot accepted an unknown Binary case.");
    reject<std::invalid_argument>([&] { (void)write_json_document<ordinary::root_json>(buffer, value); },
        "The root's default virtual slot accepted an unknown JSON case.");
}

void check_rejected_reads()
{
    std::array<std::byte, sizeof(std::uint32_t)> binary{};
    writer output(binary);
    output.write(std::uint32_t{0xfffffffe});
    for (std::size_t size = 0; size <= binary.size(); ++size)
    {
        reject<wire_error>([&] { (void)read_binary_document<ordinary::root_binary>(std::span(binary).first(size)); },
            "A virtual Union accepted a truncated or unknown Binary case hash.");
    }

    constexpr std::string_view json = R"({"$type":4294967294,"$value":{}})";
    reject<wire_error>([&] {
        (void)read_json_document<ordinary::root_json>(std::as_bytes(std::span(json)));
    }, "A virtual Union accepted an unknown JSON case hash.");
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed virtual Union fixture directory.");
    const auto directory = interop_models::path(argv[1]);
    std::filesystem::create_directories(directory / "out");
    static_assert(!ordinary::root_requires_reference_graph && !document::root_requires_reference_graph &&
        graph::root_requires_reference_graph && mixed::root_requires_reference_graph);
    static_assert(registry::wide_tags);
    registry::register_case<application_case, application_binding>(612);
    for (const auto* name : {"generic-int", "generic-string", "inherited-case"})
        exchange<ordinary::root_binary, ordinary::root_json>(directory, name,
            [](const auto& value) { require(value != nullptr, "Read a declared virtual case."); });
    exchange<ordinary::root_binary, ordinary::root_json>(directory, "null-case",
        [](const auto& value) { require(value == nullptr, "Read the null virtual Union."); });
    exchange<document::root_binary, document::root_json>(directory, "all-cases", check_all_cases);
    exchange<graph::root_binary, graph::root_json, true>(directory, "direct-graph", check_tracked);
    exchange<mixed::root_binary, mixed::root_json, true>(directory, "mixed-graph", check_mixed);
    check_inherited_slot(directory);
    check_rejected_reads();
    std::cout << "C++ verified all large managed virtual Union fixtures.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
