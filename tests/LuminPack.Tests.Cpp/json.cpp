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

struct manual_member
{
    using value_member = json_record_member<u"Value", &selected::value, number>;

    static bool read_if(auto& input, const auto& key, selected& value)
    {
        return value_member::read_if(input, key, value);
    }

    static void write(auto& output, const selected& value, bool& first)
    {
        value_member::write(output, value, first);
    }
};

void ordered_property_heads()
{
    constexpr auto utf8 = json_encoding::utf8;
    constexpr auto utf16 = json_encoding::utf16;
    constexpr auto label = luminpack::json_name{u"Label"};
    const row expected{choice_code::enabled, u"second", std::vector<std::int32_t>{7}, true};

    const auto with_fallback = read_json_document<json_row, utf8>(bytes(
        "{\"Kind\":256,\"Name\" : \"first\",\"Items\":[7],\"La\\u0062el\":\"second\","
        "\"Unknown\":{\"x\":[{}]},\"Flag\":true}"));
    require(with_fallback == expected, "Ordered JSON can resume after aliases, reordered names and unknown fields.");
    require(read_json_document<json_row, utf16>(bytes(
        u"{\"Kind\":256, \"Name\":\"second\",\"Items\":[7],\"Flag\":true}")) == expected,
        "Ordered UTF-16 JSON can resume after whitespace and a former name.");

    basic_json_reader<utf8> reader(bytes("{\"Label\":\"first\", \"Label\" : \"second\"}"));
    const auto object = reader.begin('{');
    require(reader.try_read_ordered_property<label>(true) && reader.read_string() == u"first",
        "Match a complete first property head.");
    require(!reader.try_read_ordered_property<label>(false) && reader.consume(','),
        "A mismatched property head leaves the comma unread.");
    require(reader.read_name().matches(label.view()), "Read the name through the ordinary parser.");
    reader.expect(':');
    require(reader.read_string() == u"second", "Read the value after a head mismatch.");
    reader.expect('}');
    reader.require_end();

    for (const auto bad : {"{\"Kind\":256\"Label\":null}", "{\"Kind\":256,}",
             "{\"Kind\":256,,\"Label\":null}", "{\"Kind\":256,\"Unknown\":{\"x\":[],}}"})
    {
        malformed([&] { (void)read_json_document<json_row, utf8>(bytes(std::string_view(bad))); },
            "Reject malformed JSON after an ordered property.");
    }
    malformed([] {
        (void)read_json_document<json_row, utf8>(bytes("{\"Kind\":256,\"Unknown\":[{}]}"), {2});
    }, "Enforce the depth limit after an ordered property.");

    const auto document8 = bytes("{\"Kind\":256,\"Label\":null}");
    const auto document16 = bytes(u"{\"Kind\":256,\"Label\":null}");
    for (std::size_t size = 0; size < document8.size(); ++size)
        malformed([&] { (void)read_json_document<json_row, utf8>(document8.first(size)); },
            "Reject every truncated UTF-8 property head and record.");
    for (std::size_t size = 0; size < document16.size(); ++size)
        malformed([&] { (void)read_json_document<json_row, utf16>(document16.first(size)); },
            "Reject every truncated UTF-16 property head and record.");

    using manual_record = json_record_body<selected, manual_member>;
    require(read_json_document<manual_record, utf8>(bytes("{\"Value\":42}")) == selected{42},
        "Read a manual JSON record member with the original contract.");

    using duplicate_names = json_record_body<row,
        json_record_member<u"Kind", &row::kind, enum_value>,
        json_record_member<u"Kind", &row::flag, boolean>>;
    const auto duplicate = read_json_document<duplicate_names, utf8>(bytes("{\"Kind\":0,\"Kind\":256}"));
    require(duplicate.kind == choice_code::enabled && !duplicate.flag,
        "Repeated names select the first member of a manual contract.");
    using conflicting_alias = json_record_body<row,
        json_record_member<u"Kind", &row::kind, enum_value, u"Flag">,
        json_record_member<u"Flag", &row::flag, boolean>>;
    const auto alias = read_json_document<conflicting_alias, utf8>(bytes("{\"Kind\":0,\"Flag\":256}"));
    require(alias.kind == choice_code::enabled && !alias.flag,
        "A manual contract's earlier alias takes precedence over a later canonical name.");

    using unicode_record = json_record_body<selected, json_record_member<u"\u754c\"\\", &selected::value, number>>;
    std::array<std::byte, 32> output{};
    const auto unicode = write_json_document<unicode_record, utf8>(output, selected{42});
    require(std::ranges::equal(unicode, bytes("{\"\xe7\x95\x8c\\\"\\\\\":42}")),
        "Write a non-ASCII escaped property through the ordinary writer.");
    require(read_json_document<unicode_record, utf8>(unicode) == selected{42},
        "Read a non-ASCII escaped property through the ordinary reader.");

    basic_json_writer<utf8> short_writer(std::span<std::byte>(output).first(1));
    try
    {
        short_writer.write_ordered_property<label>(false);
        throw std::runtime_error("A JSON property head exceeded the destination.");
    }
    catch (const std::length_error&)
    {
        require(std::ranges::equal(short_writer.written(), bytes(",")),
            "The short destination retains a successfully written separator.");
    }
}

struct row_name_index
{
    template<json_encoding Encoding>
    static std::size_t find(const luminpack::json_property_name<Encoding>& name) noexcept
    {
        switch (name.encoded_length())
        {
        case Encoding == json_encoding::utf8 ? 6 : 3:
            if (name.template matches<luminpack::json_name{u"\u00e9\U0001f600"}>())
                return 1;
            break;
        case 4:
            if (name.template matches<luminpack::json_name{u"Kind"}>())
                return 0;
            if (name.template matches<luminpack::json_name{u"Flag"}>())
                return 3;
            break;
        case 5:
            if (name.template matches<luminpack::json_name{u"Label"}>())
                return 1;
            if (name.template matches<luminpack::json_name{u"Items"}>())
                return 2;
            break;
        }
        return 4;
    }
};

void sparse_property_lookup()
{
    using sparse_row = luminpack::json_lookup_record_body<row, row_name_index,
        json_record_member<u"Kind", &row::kind, enum_value>,
        json_record_member<u"Label", &row::text, text_value, u"\u00e9\U0001f600">,
        json_record_member<u"Items", &row::items, list_value>,
        json_record_member<u"Flag", &row::flag, boolean>>;

    const row expected{choice_code::enabled, u"match", std::vector<std::int32_t>{7}, true};
    const auto decoded8 = read_json_document<sparse_row, json_encoding::utf8>(bytes(
        "{\"Fake\":[{}], \"\\u00e9\\ud83d\\ude00\":\"match\",\"Items\":[7],\"Flag\":true,\"Ki\\u006ed\":256}"));
    require(decoded8 == expected, "UTF-8 length buckets resolve decoded aliases and check same-length names.");
    const auto decoded16 = read_json_document<sparse_row, json_encoding::utf16>(bytes(
        u"{\"Fake\":[{}], \"\u00e9\U0001f600\":\"match\",\"Items\":[7],\"Flag\":true,\"Ki\\u006ed\":256}"));
    require(decoded16 == expected, "UTF-16 length buckets count code units and can compare borrowed Unicode names.");
    malformed([] {
        (void)read_json_document<sparse_row, json_encoding::utf8>(bytes("{\"Fake\":{\"x\":[],}}"));
    }, "An exact-match miss still validates the skipped JSON value.");
}

// These 16-byte UTF-16 names collide under seed-zero XXH3.
constexpr auto first_collision = luminpack::json_name{u"\u39b9\uea42\u737b\u6782\u523a\u0996\ubc3b\uae56"};
constexpr auto second_collision = luminpack::json_name{u"\u39b8\uea42\u737b\u6782\u523a\u0996\ubc3b\uaf56"};
constexpr auto unknown_collision = luminpack::json_name{u"\u39b8\uea42\u737b\u6782\u523a\u0996\ubc3b\u2f56"};
constexpr auto collision_hash = 0x9af36ad529ccf545ull;

struct dense_name_index
{
    static std::size_t find(const luminpack::json_property_name<json_encoding::utf16>& name)
    {
        switch (name.encoded_hash())
        {
        case collision_hash:
            if (name.matches<first_collision>())
                return 0;
            if (name.matches<second_collision>())
                return 1;
            break;
        }
        return 2;
    }
};

void dense_property_lookup()
{
    const auto hash_utf8 = [](std::string_view quoted) {
        basic_json_reader<json_encoding::utf8> reader(quoted);
        const auto hash = reader.read_name().encoded_hash();
        reader.require_end();
        return hash;
    };
    require(hash_utf8("\"\"") == 0x2d06800538d394c2ull &&
        hash_utf8("\"DisplayName\"") == 0xdbfb57cbde200beaull &&
        hash_utf8("\"\xe5\x90\x8d\xe7\xa7\xb0\xf0\x9f\x9a\x80\"") == 0x1843da27ff5a8226ull &&
        hash_utf8("\"\\u540d\\u79f0\\ud83d\\ude80\"") == 0x1843da27ff5a8226ull,
        "UTF-8 JSON property hashes match the managed vectors before and after decoding.");
    const auto long_name = std::string(299, 'a');
    constexpr auto long_name_hash = 0xc1d17581d8eb7e21ull;
    require(hash_utf8("\"a" + long_name + "\"") == long_name_hash &&
        hash_utf8("\"\\u0061" + long_name + "\"") == long_name_hash,
        "Long literal and escaped UTF-8 names match the managed XXH3 hash.");

    using dense_row = luminpack::json_lookup_record_body<row, dense_name_index,
        json_record_member<u"Kind", &row::kind, enum_value, first_collision>,
        json_record_member<u"Flag", &row::flag, boolean, second_collision>>;
    for (const auto name : {first_collision.view(), second_collision.view(), unknown_collision.view()})
    {
        std::u16string quoted = u"\"";
        quoted.append(name).append(u"\"");
        basic_json_reader<json_encoding::utf16> reader{std::u16string_view(quoted)};
        require(reader.read_name().encoded_hash() == collision_hash, "The UTF-16 collision vectors share one hash.");
    }
    std::u16string document = u"{\"";
    document.append(second_collision.view()).append(u"\":true,\"")
        .append(unknown_collision.view()).append(u"\":{\"ignored\":[{}]},\"")
        .append(first_collision.view()).append(u"\":256}");
    const auto value = read_json_document<dense_row>(bytes(std::u16string_view(document)));
    require(value.kind == choice_code::enabled && value.flag, "Dense JSON lookup compares every colliding name exactly.");
}

template<json_encoding Encoding>
void generated_int32_prefix(std::span<const std::byte> document)
{
    basic_json_reader<Encoding> reader(document);
    const auto object = reader.begin('{');
    auto run = reader.begin_generated_int32_run();
    std::int32_t first = 0;
    std::int32_t second = 0;
    std::int32_t third = -1;
    require(run.template read<luminpack::json_name{u"A"}, true>(first) &&
        run.template read<luminpack::json_name{u"B"}, false>(second),
        "Match consecutive generated Int32 fields at both signed bounds.");
    require(first == std::numeric_limits<std::int32_t>::min() && second == std::numeric_limits<std::int32_t>::max(),
        "Decode the exact generated Int32 values.");
    require(!run.template read<luminpack::json_name{u"C"}, false>(third) && third == -1,
        "Leave a property with value whitespace untouched for the ordinary reader.");
    require(reader.peek() == '"', "The generated Int32 window remains private until publication.");
    run.complete();
    reader.expect(',');
    require(reader.read_name().matches(u"C"), "Publication advances to the first unconsumed property.");
    reader.expect(':');
    require(reader.template read_number<std::int32_t>() == 3, "The ordinary reader consumes the complete unmatched value.");
    reader.expect('}');
    reader.require_end();
}

void ordered_int32_runs()
{
    constexpr auto utf8 = json_encoding::utf8;
    generated_int32_prefix<utf8>(bytes("{\"A\":-2147483648,\"B\":2147483647,\"C\": 3}"));

    const auto document16 = bytes(u"{\"A\":-2147483648,\"B\":2147483647,\"C\": 3}");
    std::array<std::byte, 128> buffer{};
    auto unaligned = std::span(buffer).subspan(1, document16.size());
    std::ranges::copy(document16, unaligned.begin());
    generated_int32_prefix<json_encoding::utf16>(unaligned);

    for (const auto bad : {"{\"A", "{\"A\":", "{\"A\":-", "{\"A\":01}", "{\"A\":2147483648}", "{\"A\":-2147483649}",
             "{\"A\":1.0}", "{\"A\":1e2}", "{\"A\":1+2}", "{\"A\":1234567890123456}"})
    {
        basic_json_reader<utf8> reader{std::string_view(bad)};
        const auto object = reader.begin('{');
        auto run = reader.begin_generated_int32_run();
        std::int32_t value = -1;
        require(!run.read<luminpack::json_name{u"A"}, true>(value) && value == -1,
            "A malformed or unsupported integer never changes the destination on the generated path.");
        run.complete();
        require(reader.peek() == '"', "A generated Int32 miss publishes no bytes.");
    }
}

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
        ordered_property_heads();
        sparse_property_lookup();
        dense_property_lookup();
        ordered_int32_runs();
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
