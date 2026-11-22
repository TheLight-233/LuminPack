#include "default.hpp"
#include "pinned-root.hpp"
#include "utf8-length.hpp"
#include "utf8-token.hpp"
#include "utf8-varint.hpp"
#include "utf16-length.hpp"
#include "utf16-token.hpp"
#include "utf16-varint.hpp"
#include "interop_models.h"

#include <iostream>

namespace defaults = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument;
namespace pinned = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eDeclaredRecord_binary_utf8_length;
namespace utf8_length = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument_binary_utf8_length;
namespace utf8_token = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument_binary_utf8_token;
namespace utf8_varint = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument_binary_utf8_varint;
namespace utf16_length = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument_binary_utf16_length;
namespace utf16_token = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument_binary_utf16_token;
namespace utf16_varint = luminpack_generated::root_LuminPackCppBinaryProfileFixtures_x002eProfileDocument_binary_utf16_varint;

namespace
{
void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void verify(const auto& value)
{
    require(value && value->field_Text == u"h\u00e9llo \u03a9 \U0001f600" && value->field_FixedField == u"member \u03a9",
        "The profile did not preserve inherited or explicitly scoped text.");
    require(value->field_Items == std::vector<std::optional<std::u16string>>{u"single", u"", u"caf\u00e9", u"\U0001f600"},
        "The profile did not preserve the list elements.");
    require(value->field_Declared && value->field_Declared->field_Text == u"d\u00e9claration value" &&
        value->field_Declared->field_MemberOverride == u"override \u03a9", "A declared profile or member override was lost.");
    require(value->field_Version && value->field_Version->field_Text == u"version \u03a9" &&
        value->field_Version->field_Code == 73, "The nested Version payload changed.");
}

template<class Json, luminpack::json_encoding Encoding>
void exchange_json(const interop_models::path& directory, const std::string& name, const typename Json::value_type& value)
{
    constexpr auto suffix = Encoding == luminpack::json_encoding::utf8 ? ".utf8.json" : ".utf16.json";
    const auto managed = interop_models::load(directory / (std::string("expected") + suffix));
    verify(luminpack::read_json_document<Json, Encoding>(managed));
    std::array<std::byte, 4096> storage;
    const auto written = luminpack::write_json_document<Json, Encoding>(storage, value);
    require(std::ranges::equal(written, managed), "Binary profile selection changed the JSON document.");
    interop_models::store(directory / "out" / (name + suffix), written);
}

template<class Binary, class Json, luminpack::json_encoding JsonEncoding>
void exchange_profile(const interop_models::path& directory, const std::string& name)
{
    static_assert(JsonEncoding == defaults::root_json_encoding);
    const auto managed = interop_models::load(directory / (name + ".bin"));
    auto value = luminpack::read_binary_document<Binary>(managed);
    verify(value);
    std::array<std::byte, 4096> storage;
    const auto written = luminpack::write_binary_document<Binary>(storage, value);
    require(std::ranges::equal(written, managed), "C++ Binary differs from the managed operation-profile fixture.");
    interop_models::store(directory / "out" / (name + ".bin"), written);
    exchange_json<Json, luminpack::json_encoding::utf8>(directory, name, value);
    exchange_json<Json, luminpack::json_encoding::utf16>(directory, name, value);
}

void exchange_pinned_root(const interop_models::path& directory)
{
    const auto managed = interop_models::load(directory / "pinned-root.bin");
    const auto value = luminpack::read_binary_document<pinned::root_binary>(managed);
    require(value && value->field_Text == u"d\u00e9claration value" && value->field_MemberOverride == u"override \u03a9",
        "The root's explicit declaration did not take precedence.");
    std::array<std::byte, 4096> storage;
    const auto written = luminpack::write_binary_document<pinned::root_binary>(storage, value);
    require(std::ranges::equal(written, managed), "The Binary profile changed the declared root.");
    interop_models::store(directory / "out" / "pinned-root.bin", written);
}
}

int main(int argc, char** argv)
try
{
    if (argc != 2)
        return 2;
    const interop_models::path directory(argv[1]);
    std::filesystem::create_directories(directory / "out");
    exchange_profile<defaults::root_binary, defaults::root_json, defaults::root_json_encoding>(directory, "default");
    exchange_profile<utf8_length::root_binary, utf8_length::root_json, utf8_length::root_json_encoding>(directory, "utf8-length");
    exchange_profile<utf8_token::root_binary, utf8_token::root_json, utf8_token::root_json_encoding>(directory, "utf8-token");
    exchange_profile<utf16_length::root_binary, utf16_length::root_json, utf16_length::root_json_encoding>(directory, "utf16-length");
    exchange_profile<utf16_token::root_binary, utf16_token::root_json, utf16_token::root_json_encoding>(directory, "utf16-token");
    exchange_profile<utf8_varint::root_binary, utf8_varint::root_json, utf8_varint::root_json_encoding>(directory, "utf8-varint");
    exchange_profile<utf16_varint::root_binary, utf16_varint::root_json, utf16_varint::root_json_encoding>(directory, "utf16-varint");
    exchange_pinned_root(directory);
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
