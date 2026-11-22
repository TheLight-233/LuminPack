#include <luminpack/luminpack.h>

#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <ranges>

namespace
{
using luminpack::basic_json_reader;
using luminpack::basic_json_writer;
using luminpack::json_codec;
using luminpack::json_encoding;
using luminpack::json_record_body;
using luminpack::json_record_codec;
using luminpack::json_record_member;
using luminpack::read_json_document;
using luminpack::write_json_document;

void require(bool condition, const char* description)
{
    if (!condition)
        throw std::runtime_error(description);
}

template<class Action>
void malformed(Action action, const char* description)
{
    try
    {
        action();
    }
    catch (const luminpack::wire_error&)
    {
        return;
    }
    throw std::runtime_error(description);
}

std::string fixture(const std::filesystem::path& directory, const char* file_name)
{
    std::ifstream file(directory / file_name, std::ios::binary);
    require(file.good(), file_name);
    std::string text(std::istreambuf_iterator<char>(file), {});
    while (!text.empty() && (text.back() == '\r' || text.back() == '\n'))
        text.pop_back();
    return text;
}

template<class Unit>
std::span<const std::byte> bytes(std::basic_string_view<Unit> text)
{
    return std::as_bytes(std::span(text.data(), text.size()));
}

template<std::size_t Size, class Unit>
std::span<const std::byte> bytes(const Unit (&text)[Size])
{
    return bytes(std::basic_string_view<Unit>(text, Size - 1));
}

using number = json_codec<luminpack::scalar_codec<std::int32_t>>;
using boolean = json_codec<luminpack::boolean_codec>;
using utf8_string = luminpack::string_codec<luminpack::string_profile::utf8_length>;
using dictionary = json_codec<luminpack::dictionary_codec<luminpack::required_codec<utf8_string>,
    luminpack::scalar_codec<std::int32_t>>>;
using numeric_dictionary = json_codec<luminpack::dictionary_codec<luminpack::scalar_codec<std::int32_t>,
    luminpack::boolean_codec>>;

struct selected
{
    std::int32_t value = 0;

    bool operator==(const selected&) const = default;
};

using selection = json_record_body<selected, json_record_member<u"Value", &selected::value, number>>;
using choice = luminpack::json_union_codec<luminpack::union_case<luminpack::union_name_hash("Selected"), selection>>;
static_assert(luminpack::union_name_hash("Selected") == 4012061332u);

void dotnet_fixtures(const std::filesystem::path& directory)
{
    constexpr auto utf8 = json_encoding::utf8;
    for (const auto file : {"dictionary-string.json", "dictionary-string-pairs.json"})
    {
        const auto text = fixture(directory, file);
        const auto value = read_json_document<dictionary, utf8>(bytes(std::string_view(text)));
        require(value && value->size() == 2 && value->at(u"one") == 1 && value->at(u"a\"b") == 2,
            "Read both .NET dictionary JSON forms.");
    }

    std::array<std::byte, 512> output{};
    basic_json_writer<utf8> json(output);
    const std::array<std::pair<std::u16string_view, std::int32_t>, 2> entries{{{u"one", 1}, {u"a\"b", 2}}};
    dictionary::write_entries(json, entries);
    const auto mapping = fixture(directory, "dictionary-string.json");
    require(std::ranges::equal(json.written(), bytes(std::string_view(mapping))),
        "Write the .NET string dictionary JSON fixture.");

    const auto numeric_map = read_json_document<numeric_dictionary, utf8>(bytes("[[5,true],[-7,false]]"));
    require(numeric_map && numeric_map->size() == 2 && numeric_map->at(5) && !numeric_map->at(-7),
        "Read non-string dictionary pairs.");
    const auto replacement = read_json_document<dictionary, utf8>(bytes("{\"one\":1,\"one\":2}"));
    require(replacement && replacement->at(u"one") == 2, ".NET JSON map replacement behavior.");

    const auto union_text = fixture(directory, "union-hash.json");
    const auto tagged = read_json_document<choice, utf8>(bytes(std::string_view(union_text)));
    require(tagged && std::get<selected>(*tagged).value == 123, "Read .NET JSON Union case hash.");
    require(std::ranges::equal(write_json_document<choice, utf8>(output, tagged), bytes(std::string_view(union_text))),
        "Write .NET JSON Union envelope.");
    const auto reversed = read_json_document<choice, utf8>(
        bytes("{\"$value\":{\"Value\":123,\"skip\":[{}]}, \"ignore\": {}, \"$type\":4012061332}"));
    require(reversed == tagged, "Read a deferred Union payload before the type property.");
    require(!read_json_document<choice, utf8>(bytes("null")), "Read a null JSON Union.");
}

enum class choice_code : std::uint16_t { enabled = 256 };
using enum_value = json_codec<luminpack::enum_codec<choice_code>>;
using text_value = json_codec<utf8_string>;
using list_value = json_codec<luminpack::list_codec<luminpack::scalar_codec<std::int32_t>>>;

struct row
{
    choice_code kind = {};
    std::optional<std::u16string> text;
    std::optional<std::vector<std::int32_t>> items;
    bool flag = false;

    bool operator==(const row&) const = default;
};

using json_row = json_record_codec<row,
    json_record_member<u"Kind", &row::kind, enum_value>,
    json_record_member<u"Label", &row::text, text_value, u"Name">,
    json_record_member<u"Items", &row::items, list_value>,
    json_record_member<u"Flag", &row::flag, boolean>>;

void profiles_and_strings()
{
    constexpr auto utf8 = json_encoding::utf8;
    constexpr auto utf16 = json_encoding::utf16;
    std::array<std::byte, 512> result{};
    const row sample{choice_code::enabled, u"\u00e9\U0001f600\n", std::vector<std::int32_t>{-1, 8, 200}, true};

    const auto written8 = write_json_document<json_row, utf8>(result, sample);
    constexpr auto expected8 = "{\"Kind\":256,\"Label\":\"\xc3\xa9\xf0\x9f\x98\x80\\n\",\"Items\":[-1,8,200],\"Flag\":true}";
    require(std::ranges::equal(written8, bytes(std::string_view(expected8))), "UTF-8 JSON string and enum output.");
    require(read_json_document<json_row, utf8>(written8) == sample, "UTF-8 JSON DTO round trip.");

    const auto unaligned = std::span<std::byte>(result).subspan(1);
    const auto written16 = write_json_document<json_row>(unaligned, sample);
    constexpr auto expected16 = u"{\"Kind\":256,\"Label\":\"\u00e9\U0001f600\\n\",\"Items\":[-1,8,200],\"Flag\":true}";
    require(std::ranges::equal(written16, bytes(std::u16string_view(expected16))), "UTF-16 JSON string and enum output.");
    require(read_json_document<json_row>(written16) == sample, "Unaligned UTF-16 JSON DTO round trip.");

    const auto backwards = read_json_document<json_row>(
        bytes(u"{ \"Flag\" : true, \"Items\": [-1,8,200], \"Na\\u006de\":\"\u00e9\\ud83d\\ude00\\n\", \"Kind\":256,"
              u"\"ignored\":{\"numbers\":[0,-1.25e2,true,null,{\"inner\":[]}]} }"));
    require(backwards == sample, "JSON escaping, aliases, unknown fields and unordered fields.");

    const auto sparse = read_json_document<json_row, utf8>(bytes("{\"Label\":null}"));
    require(sparse && sparse->kind == choice_code{} && !sparse->text && !sparse->items && !sparse->flag,
        "Missing JSON properties retain defaults.");

    const std::u16string controls{u'\0', u'\b', u'\f', u'\r', u'\t', u'"', u'\\'};
    const auto quoted = write_json_document<luminpack::json_string_codec, utf8>(result, controls);
    require(std::ranges::equal(quoted, bytes("\"\\u0000\\u0008\\u000c\\r\\t\\\"\\\\\"")),
        ".NET uses two-character escapes only for its short escape set.");
    require(read_json_document<luminpack::json_string_codec, utf8>(quoted) == controls, "Round trip escaped controls.");

    const auto character = write_json_document<json_codec<luminpack::scalar_codec<char16_t>>, utf16>(result, u'\u754c');
    require(std::ranges::equal(character, bytes(u"\"\u754c\"")), "C# char writes as a JSON string.");
    require(read_json_document<json_codec<luminpack::scalar_codec<char16_t>>>(character) == u'\u754c',
        "Read a UTF-16 C# char.");
}

struct coordinate
{
    std::int32_t x;
    std::int32_t y;

    bool operator==(const coordinate&) const = default;
};

constexpr auto read_coordinate = [](auto& input) {
    const auto container = input.begin('[');
    const auto x = input.template read_number<std::int32_t>();
    input.expect(',');
    const auto y = input.template read_number<std::int32_t>();
    input.expect(']');
    return coordinate{x, y};
};

constexpr auto write_coordinate = [](auto& output, const coordinate& value) {
    output.token('[');
    output.write_number(value.x);
    output.token(',');
    output.write_number(value.y);
    output.token(']');
};

using coordinate_codec = luminpack::json_custom_codec<coordinate, read_coordinate, write_coordinate>;

void custom_json()
{
    const coordinate expected{-45, 72};
    std::array<std::byte, 64> buffer{};
    const auto written = write_json_document<coordinate_codec>(buffer, expected);
    require(std::ranges::equal(written, bytes(u"[-45,72]")), "Write a Custom UTF-16 JSON representation.");
    require(read_json_document<coordinate_codec>(written) == expected, "Read a Custom UTF-16 JSON representation.");
}

void validation()
{
    constexpr auto utf8 = json_encoding::utf8;
    using floating = json_codec<luminpack::scalar_codec<double>>;
    for (const auto bad : {"01", "1.", "1e+", "1e999", "NaN", "true", "2 false", "-"})
    {
        malformed([&] { (void)read_json_document<floating, utf8>(bytes(std::string_view(bad))); },
            "Reject an invalid or overflowing JSON number.");
    }
    malformed([&] { (void)read_json_document<number, utf8>(bytes("2147483648")); },
        "Reject an out-of-range JSON Int32.");
    malformed([&] { (void)read_json_document<luminpack::json_string_codec, utf8>(bytes("\"\xc0\x80\"")); },
        "Reject an overlong UTF-8 encoding.");
    malformed([&] { (void)read_json_document<luminpack::json_string_codec, utf8>(bytes("\"\\ud800x\"")); },
        "Reject unpaired escaped UTF-16 surrogates.");
    malformed([&] { (void)read_json_document<json_row, utf8>(bytes("{\"Items\":[1,]}")); },
        "Reject trailing JSON array comma.");
    malformed([&] { (void)read_json_document<json_row, utf8>(bytes("{\"Unknown\":{\"x\":[],}}")); },
        "Validate grammar in a skipped JSON property.");
    malformed([&] { (void)read_json_document<json_row, utf8>(bytes("{\"Items\":[1]}"), {1}); },
        "Enforce the depth limit for known properties.");
    malformed([&] { (void)read_json_document<json_row, utf8>(bytes("{\"Unknown\":[1]}"), {1}); },
        "Enforce the depth limit for unknown properties.");
    malformed([&] { (void)read_json_document<choice, utf8>(bytes("{\"$type\":1,\"$value\":{}}")); },
        "Reject an unrecognized JSON Union hash.");
    malformed([&] {
        (void)read_json_document<choice, utf8>(
            bytes("{\"$type\":4012061332,\"$value\":{\"Value\":123},\"$type\":4012061332}"));
    }, "Reject repeated JSON Union discriminators.");

    std::string deep = "{\"Unknown\":";
    deep.append(60, '[');
    deep.append("0");
    deep.append(60, ']');
    deep.append("}");
    require(read_json_document<json_row, utf8>(bytes(std::string_view(deep)), {0}).has_value(),
        "Iterative skip can handle deep unknown structures when the configured limit is disabled.");

    std::array<std::byte, 16> output{};
    basic_json_writer<utf8> writer(output);
    try
    {
        floating::write(writer, std::numeric_limits<double>::infinity());
        throw std::runtime_error("JSON accepted a non-finite value.");
    }
    catch (const std::invalid_argument&)
    {
        require(writer.written().empty(), "Non-finite JSON write commits no bytes.");
    }
}
}

int main(int argc, char** argv)
{
    try
    {
        require(argc == 2, "Expected .NET fixture directory.");
        dotnet_fixtures(argv[1]);
        profiles_and_strings();
        validation();
        custom_json();
        std::cout << "LuminPack C++ JSON wire checks passed.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
