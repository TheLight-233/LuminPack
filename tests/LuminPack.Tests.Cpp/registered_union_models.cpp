#include "narrow.hpp"
#include "wide.hpp"

#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>

namespace
{
namespace narrow = luminpack_generated::root_LuminPackCppRegisteredUnionFixtures_x002eINarrow;
namespace wide = luminpack_generated::root_LuminPackCppRegisteredUnionFixtures_x002eIWide;
using byte = std::byte;

void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<class Error, class Action>
void reject(Action action, const char* message)
{
    try
    {
        action();
    }
    catch (const Error&)
    {
        return;
    }
    throw std::runtime_error(message);
}

std::vector<char> read_file(const std::filesystem::path& path)
{
    std::ifstream input(path, std::ios::binary);
    if (!input)
        throw std::runtime_error("Cannot open fixture " + path.string());
    return {std::istreambuf_iterator<char>(input), {}};
}

void write_file(const std::filesystem::path& path, std::span<const byte> bytes)
{
    std::ofstream output(path, std::ios::binary);
    output.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    check(output.good(), "Writes the native file for managed verification.");
}

template<class Root>
struct app_case : Root
{
    std::int32_t number{};
};

template<class Case, bool Wide>
struct payload_binding
{
    static std::unique_ptr<Case> read_binary(luminpack::reader& input)
    {
        auto result = std::make_unique<Case>();
        result->number = input.read<std::int32_t>() ^ 85;
        return result;
    }

    static void write_binary(luminpack::writer& output, std::uint16_t tag, const Case& value)
    {
        luminpack::write_registered_union_tag<Wide>(output, tag);
        output.write(static_cast<std::int32_t>(value.number ^ 85));
    }

    template<luminpack::json_encoding Encoding>
    static std::unique_ptr<Case> read_json(luminpack::basic_json_reader<Encoding>& input)
    {
        auto result = std::make_unique<Case>();
        result->number = input.template read_number<std::int32_t>() ^ 137;
        return result;
    }

    template<luminpack::json_encoding Encoding>
    static void write_json(luminpack::basic_json_writer<Encoding>& output, std::uint16_t tag, const Case& value)
    {
        output.token('{');
        output.template write_property<luminpack::json_name{u"$type"}>();
        output.write_number(tag);
        output.token(',');
        output.template write_property<luminpack::json_name{u"$value"}>();
        output.write_number(static_cast<std::int32_t>(value.number ^ 137));
        output.token('}');
    }
};

template<class Json, luminpack::json_encoding Encoding, class Value>
void exchange_json(const std::filesystem::path& source, const std::filesystem::path& destination,
    const auto& verify_value)
{
    const auto fixture = read_file(source);
    const auto bytes = std::as_bytes(std::span(fixture));
    const Value decoded = luminpack::read_json_document<Json, Encoding>(bytes);
    verify_value(decoded);
    std::array<byte, 96> storage{};
    const auto encoded = luminpack::write_json_document<Json, Encoding>(std::span(storage).subspan(1), decoded);
    check(std::ranges::equal(encoded, bytes), "Native and managed JSON Union bytes agree.");
    write_file(destination, encoded);
}

template<class Binary, class Json, class Generated, bool Wide>
void exchange(const std::filesystem::path& directory, const std::filesystem::path& out,
    const char* name, std::uint16_t tag)
{
    using registry = typename Binary::registry;
    static_assert(registry::wide_tags == Wide);
    using value_type = typename Binary::value_type;
    using runtime_case = app_case<typename registry::base_type>;
    using binding = payload_binding<runtime_case, Wide>;
    struct other_case : runtime_case { };

    const auto fixture = read_file(directory / (std::string(name) + ".bin"));
    const auto bytes = std::as_bytes(std::span(fixture));
    const auto expected = std::to_array<byte>({byte{255}, byte{255}, byte{255}, byte{255}, byte{100}, byte{127}, byte{0}, byte{0}, byte{0}});
    const auto wide_expected = std::to_array<byte>({byte{255}, byte{255}, byte{255}, byte{255}, byte{250}, byte{88}, byte{2},
        byte{127}, byte{0}, byte{0}, byte{0}});
    const std::span<const byte> registered_bytes = Wide ? std::span<const byte>(wide_expected) : std::span<const byte>(expected);
    check(std::ranges::equal(bytes, registered_bytes), "Managed escape, numeric tag and app-owned Binary payload.");

    luminpack::reader unregistered(bytes);
    reject<luminpack::wire_error>([&] { (void)Binary::read(unregistered); }, "Decodes a case before it is registered.");
    check(unregistered.remaining() == bytes.size(), "Consumes an unknown registered case.");
    reject<std::invalid_argument>([] { registry::template register_case<runtime_case, binding>(10); }, "Accepts a reserved case tag.");
    reject<std::invalid_argument>([] { registry::template register_case<runtime_case, binding>(65536); }, "Accepts an out-of-range tag.");
    registry::template register_case<runtime_case, binding>(tag);
    reject<std::invalid_argument>([&] {
        registry::template register_case<other_case, payload_binding<other_case, Wide>>(tag);
    }, "Accepts a duplicate registered tag.");
    registry::template register_case<other_case, payload_binding<other_case, Wide>>(Wide ? 249 : tag + 1);
    if constexpr (Wide)
    {
        auto value = std::make_unique<other_case>();
        value->number = 42;
        const value_type compact_case = std::move(value);
        std::array<byte, 16> storage{};
        luminpack::writer output(storage);
        Binary::write(output, compact_case);
        const auto compact = std::to_array<byte>({byte{255}, byte{255}, byte{255}, byte{255}, byte{249}, byte{127}, byte{0}, byte{0}, byte{0}});
        check(std::ranges::equal(output.written(), compact), "A wide Union keeps the compact form below the marker.");
        const auto decoded = luminpack::read_binary_document<Binary>(compact);
        check(dynamic_cast<const other_case*>(decoded.get()) != nullptr, "A wide Union reads a compact registered tag.");
    }

    for (std::size_t length = 0; length < bytes.size(); ++length)
    {
        luminpack::reader input(bytes.first(length));
        reject<luminpack::wire_error>([&] { (void)Binary::read(input); }, "Accepts a truncated registered case.");
        check(input.remaining() == length, "Publishes a truncated registered case.");
    }

    const auto verify_case = [](const value_type& result)
    {
        const auto* value = dynamic_cast<const runtime_case*>(result.get());
        check(value != nullptr && typeid(*value) == typeid(runtime_case) && value->number == 42,
            "Reads the runtime-registered case and its app-owned payload.");
    };
    const auto verify_generated = [](const value_type& result)
    {
        const auto* value = dynamic_cast<const Generated*>(result.get());
        check(value != nullptr && value->field_Value == 42, "Keeps the generated case dispatch.");
    };
    const auto binary_exchange = [&](const char* fixture_name, const auto& verify)
    {
        const std::string filename = std::string(fixture_name) + ".bin";
        const auto original = read_file(directory / filename);
        const auto wire = std::as_bytes(std::span(original));
        const auto decoded = luminpack::read_binary_document<Binary>(wire);
        verify(decoded);
        std::array<byte, 48> buffer{};
        const auto encoded = luminpack::write_binary_document<Binary>(std::span(buffer).subspan(1), decoded);
        check(std::ranges::equal(encoded, wire), "Native and managed Binary Union bytes agree.");
        write_file(out / filename, encoded);
    };
    const auto json_exchange = [&](const char* fixture_name, const auto& verify)
    {
        const std::string utf16 = std::string(fixture_name) + ".json";
        const std::string utf8 = std::string(fixture_name) + ".utf8.json";
        exchange_json<Json, luminpack::json_encoding::utf16, value_type>(directory / utf16, out / utf16, verify);
        exchange_json<Json, luminpack::json_encoding::utf8, value_type>(directory / utf8, out / utf8, verify);
    };
    binary_exchange(name, verify_case);
    json_exchange(name, verify_case);
    const auto generated_name = std::string("static-") + name;
    binary_exchange(generated_name.c_str(), verify_generated);
    json_exchange(generated_name.c_str(), verify_generated);

    const auto deferred = std::string("{\"$value\":163,\"$type\":") + std::to_string(tag) + "}";
    verify_case(luminpack::read_json_document<Json, luminpack::json_encoding::utf8>(std::as_bytes(std::span(deferred))));
}
}

int main(int argc, char** argv)
{
    try
    {
        check(argc == 2, "Expected the managed fixture directory.");
        const std::filesystem::path directory(argv[1]);
        const auto out = directory / "out";
        std::filesystem::create_directories(out);
        exchange<narrow::root_binary, narrow::root_json, narrow::type_LuminPackCppRegisteredUnionFixtures_x002eGeneratedCase, false>(
            directory, out, "narrow", 100);
        exchange<wide::root_binary, wide::root_json, wide::type_LuminPackCppRegisteredUnionFixtures_x002eGeneratedCase, true>(
            directory, out, "wide", 600);
        std::cout << "Registered Binary and JSON Union fixtures passed.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
