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

    constexpr auto utf16 = json_encoding::utf16;
    require(read_json_document<choice, utf8>(
        bytes(R"({"$typeof":12,"\u0024value":{"Value":123},"$valuE":0,"$ty\u0070e":4012061332})")) == tagged,
        "Match the complete decoded UTF-8 Union names.");
    require(read_json_document<choice, utf16>(
        bytes(uR"({"$typeof":12,"\u0024value":{"Value":123},"$valuE":0,"$ty\u0070e":4012061332})")) == tagged,
        "Match the complete decoded UTF-16 Union names.");
    require(std::ranges::equal(write_json_document<choice, utf16>(output, tagged),
        bytes(uR"({"$type":4012061332,"$value":{"Value":123}})")),
        "Write the exact UTF-16 envelope and unsigned Union hash.");
    malformed([] {
        (void)read_json_document<choice, utf16>(
            bytes(uR"({"$type":4012061332,"\u0024type":4012061332,"$value":{"Value":123}})"));
    }, "Reject an escaped duplicate Union discriminator.");
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

void unicode_property_heads()
{
    using luminpack::detail::json_property_head;
    static constexpr auto name = luminpack::json_name{u"A\u00e9\u754c\U00010400"};
    constexpr auto& head8 = json_property_head<json_encoding::utf8, name, '{'>;
    constexpr auto& head16 = json_property_head<json_encoding::utf16, name, '{'>;
    constexpr std::array<unsigned char, 14> expected8{
        '{', '"', 'A', 0xc3, 0xa9, 0xe7, 0x95, 0x8c, 0xf0, 0x90, 0x90, 0x80, '"', ':'};
    static_assert(std::ranges::equal(head8, expected8, {}, [](char byte) { return static_cast<unsigned char>(byte); }));
    static_assert(std::u16string_view(head16.data(), head16.size()) == u"{\"A\u00e9\u754c\U00010400\":");

    basic_json_reader<json_encoding::utf8> reader8(bytes("{\"A\xc3\xa9\xe7\x95\x8c\xf0\x90\x90\x80\":7}"));
    const auto scope8 = reader8.begin('{');
    require(reader8.try_read_ordered_property<name>(true) && reader8.read_number<std::int32_t>() == 7,
        "Match a complete generated UTF-8 property head containing multibyte code points.");
    reader8.expect('}');
    reader8.require_end();

    basic_json_reader<json_encoding::utf16> reader16(bytes(u"{\"A\u00e9\u754c\U00010400\":-7}"));
    const auto scope16 = reader16.begin('{');
    auto run = reader16.begin_generated_int32_run();
    std::int32_t value{};
    require(run.read<name, true>(value) && value == -7, "Use the UTF-16 Unicode head in a generated Int32 run.");
    run.complete();
    reader16.expect('}');
    reader16.require_end();

    using unicode = json_record_body<selected, json_record_member<name, &selected::value, number>>;
    require(read_json_document<unicode, json_encoding::utf8>(
        bytes("{\"A\\u00e9\\u754c\\ud801\\udc00\":7}")) == selected{7},
        "Accept the decoded spelling after an ordered Unicode name mismatch.");

    const auto check_short_write = []<json_encoding Encoding>(std::span<const std::byte> prefix) {
        std::array<std::byte, 32> buffer{};
        basic_json_writer<Encoding> writer(std::span<std::byte>(buffer).first(prefix.size()));
        try
        {
            writer.template write_opening_property<name>();
        }
        catch (const std::length_error&)
        {
            require(std::ranges::equal(writer.written(), prefix), "Preserve the quoted Unicode name before a short colon.");
            return;
        }
        throw std::runtime_error("A Unicode property head exceeded the destination.");
    };
    check_short_write.template operator()<json_encoding::utf8>(bytes("{\"A\xc3\xa9\xe7\x95\x8c\xf0\x90\x90\x80\""));
    check_short_write.template operator()<json_encoding::utf16>(bytes(u"{\"A\u00e9\u754c\U00010400\""));
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

void json_characters()
{
    using character = json_codec<luminpack::scalar_codec<char16_t>>;
    constexpr auto utf8 = json_encoding::utf8;
    constexpr auto utf16 = json_encoding::utf16;
    std::array<std::byte, 32> output{};
    const auto check_escapes = [&]<json_encoding Encoding>(const auto& backspace, const auto& form_feed, const auto& lone_surrogate) {
        require(std::ranges::equal(write_json_document<character, Encoding>(output, u'\b'), bytes(backspace)) &&
            read_json_document<character, Encoding>(bytes(backspace)) == u'\b', "A C# char uses the short backspace escape.");
        require(std::ranges::equal(write_json_document<character, Encoding>(output, u'\f'), bytes(form_feed)) &&
            read_json_document<character, Encoding>(bytes(form_feed)) == u'\f', "A C# char uses the short form-feed escape.");
        require(std::ranges::equal(write_json_document<character, Encoding>(output, char16_t{0xd800}),
            bytes(lone_surrogate)), "Write the managed escape for an isolated UTF-16 code unit.");
        malformed([&] { (void)read_json_document<character, Encoding>(bytes(lone_surrogate)); },
            "The managed char reader rejects an unpaired surrogate escape.");
    };
    check_escapes.template operator()<utf8>("\"\\b\"", "\"\\f\"", "\"\\ud800\"");
    check_escapes.template operator()<utf16>(u"\"\\b\"", u"\"\\f\"", u"\"\\ud800\"");

    require(read_json_document<character, utf8>(bytes("\"\"")) == u'\0' &&
        read_json_document<character, utf16>(bytes(u"\"\"")) == u'\0', "An empty char string reads as NUL.");
    require(read_json_document<character, utf8>(bytes("\"\\ud83d\\ude00\"")) == char16_t{0xd83d} &&
        read_json_document<character, utf16>(bytes(u"\"\U0001f600\"")) == char16_t{0xd83d},
        "A valid supplementary code point returns its first UTF-16 code unit.");
    require(read_json_document<character, utf16>(bytes(u"\"foo\"")) == u'f',
        "The managed UTF-16 reader returns the first code unit.");
    malformed([&] { (void)read_json_document<character, utf8>(bytes("\"foo\"")); },
        "The managed UTF-8 reader checks the two-unit conversion destination.");

    output.fill(std::byte{0xa4});
    luminpack::basic_json_writer<utf8> bounded{std::span(output).first<7>()};
    try
    {
        bounded.write_character(char16_t{0xd800});
        throw std::runtime_error("Check the entire char escape before writing.");
    }
    catch (const std::length_error&)
    {
        require(bounded.written().empty() && std::ranges::all_of(output, [](auto byte) { return byte == std::byte{0xa4}; }),
            "A short character buffer retains its original bytes and cursor.");
    }
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

    using registered = luminpack::detail::registered_custom_json_codec<coordinate, coordinate_codec>;
    require(read_json_document<registered>(written) == expected, "The registered Custom codec owns the complete JSON value.");
    malformed([] {
        (void)read_json_document<registered, json_encoding::utf8>(bytes("[-45,72]"));
    }, "The automatic Custom registration applies only to UTF-16 JSON.");
    malformed([] {
        (void)read_json_document<luminpack::detail::registered_custom_json_codec<coordinate>>(bytes(u"[-45,72]"));
    }, "A Binary-only Custom contract has no registered JSON reader.");
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

template<json_encoding Encoding, class Ordinary, class Generated>
void compare_generated_opening(const typename Ordinary::value_type& value, std::span<const std::byte> expected)
{
    const auto write = [&]<class Codec>(std::type_identity<Codec>, std::span<std::byte> destination) {
        basic_json_writer<Encoding> output(destination);
        try
        {
            Codec::write(output, value);
        }
        catch (const std::length_error&)
        {
            return std::pair{false, output.written()};
        }
        return std::pair{true, output.written()};
    };
    std::array<std::byte, 96> manual{};
    std::array<std::byte, 96> generated{};
    for (std::size_t capacity = 0; capacity <= expected.size(); ++capacity)
    {
        const auto prior = write(std::type_identity<Ordinary>{}, std::span(manual).first(capacity));
        const auto current = write(std::type_identity<Generated>{}, std::span(generated).subspan(1, capacity));
        require(prior.first == current.first && std::ranges::equal(prior.second, current.second),
            "Generated opening heads preserve the original bytes and published prefix at every capacity.");
        if (capacity == expected.size())
            require(current.first && std::ranges::equal(current.second, expected),
                "Generated opening heads preserve the complete JSON document.");
    }
}

struct nested_selection
{
    selected item;
    bool enabled;
};

void generated_opening_heads()
{
    constexpr auto utf8 = json_encoding::utf8;
    constexpr auto utf16 = json_encoding::utf16;
    using generated_selection = luminpack::json_lookup_record_body<selected, void,
        json_record_member<u"Value", &selected::value, number>>;
    using manual_nested = json_record_body<nested_selection,
        json_record_member<u"Nested", &nested_selection::item, selection>,
        json_record_member<u"Enabled", &nested_selection::enabled, boolean>>;
    using generated_nested = luminpack::json_lookup_record_body<nested_selection, void,
        json_record_member<u"Nested", &nested_selection::item, generated_selection>,
        json_record_member<u"Enabled", &nested_selection::enabled, boolean>>;
    const nested_selection nested{{42}, true};
    compare_generated_opening<utf8, manual_nested, generated_nested>(
        nested, bytes("{\"Nested\":{\"Value\":42},\"Enabled\":true}"));
    compare_generated_opening<utf16, manual_nested, generated_nested>(
        nested, bytes(u"{\"Nested\":{\"Value\":42},\"Enabled\":true}"));

    using escaped_member = json_record_member<u"\u754c\"", &selected::value, number>;
    using manual_escaped = json_record_body<selected, escaped_member>;
    using generated_escaped = luminpack::json_lookup_record_body<selected, void, escaped_member>;
    compare_generated_opening<utf8, manual_escaped, generated_escaped>(
        selected{42}, bytes("{\"\xe7\x95\x8c\\\"\":42}"));
    compare_generated_opening<utf16, manual_escaped, generated_escaped>(
        selected{42}, bytes(u"{\"\u754c\\\"\":42}"));

    using manual_empty = json_record_body<selected>;
    using generated_empty = luminpack::json_lookup_record_body<selected, void>;
    compare_generated_opening<utf8, manual_empty, generated_empty>(selected{}, bytes("{}"));
    compare_generated_opening<utf16, manual_empty, generated_empty>(selected{}, bytes(u"{}"));

    using manual_nullable = luminpack::json_optional_codec<selection>;
    using generated_nullable = luminpack::json_optional_codec<generated_selection>;
    compare_generated_opening<utf8, manual_nullable, generated_nullable>(std::nullopt, bytes("null"));
    compare_generated_opening<utf16, manual_nullable, generated_nullable>(std::nullopt, bytes(u"null"));

    malformed([] {
        (void)read_json_document<generated_nested, utf8>(bytes("{\"Nested\":{\"Value\":42},,\"Enabled\":true}"));
    }, "Generated objects still reject duplicate property separators.");
}

struct json_graph_node;

struct json_graph_node_codec
{
    using value_type = json_graph_node*;
    static value_type read(auto& input);
    static void read_into(auto& input, value_type& value);
    static void write(auto& output, const value_type& value);
};

struct json_graph_node
{
    std::int32_t value = 99;
    json_graph_node* next = nullptr;
    std::optional<std::vector<json_graph_node*>> children;
};

struct json_graph_node_lookup
{
    static std::size_t find(const auto& name) noexcept
    {
        if (name.matches(u"Value")) return 0;
        if (name.matches(u"Next")) return 1;
        if (name.matches(u"Children")) return 2;
        if (name.matches(u"$id")) return 4;
        if (name.matches(u"$ref")) return 5;
        return 3;
    }
};

using json_graph_node_impl = luminpack::tracked_json_codec<json_graph_node, json_graph_node_lookup,
    json_record_member<u"Value", &json_graph_node::value, number>,
    json_record_member<u"Next", &json_graph_node::next, json_graph_node_codec>,
    json_record_member<u"Children", &json_graph_node::children, luminpack::json_list_codec<json_graph_node_codec>>>;

json_graph_node* json_graph_node_codec::read(auto& input)
{
    return json_graph_node_impl::read(input);
}

void json_graph_node_codec::read_into(auto& input, value_type& value)
{
    json_graph_node_impl::read_into(input, value);
}

void json_graph_node_codec::write(auto& output, const value_type& value)
{
    json_graph_node_impl::write(output, value);
}

struct other_graph_node {};

struct other_graph_node_lookup
{
    static std::size_t find(const auto& name) noexcept
    {
        if (name.matches(u"$id")) return 1;
        if (name.matches(u"$ref")) return 2;
        return 0;
    }
};

using other_graph_node_codec = luminpack::tracked_json_codec<other_graph_node, other_graph_node_lookup>;
constexpr auto graph_node_hash = luminpack::union_name_hash("GraphNode");
using graph_json_union = luminpack::json_union_codec<luminpack::union_case<graph_node_hash, json_graph_node_codec>>;

struct json_graph_envelope
{
    json_graph_node* anchor = nullptr;
    graph_json_union::value_type choice;
    other_graph_node* other = nullptr;
};

using json_graph_envelope_codec = json_record_body<json_graph_envelope,
    json_record_member<u"Anchor", &json_graph_envelope::anchor, json_graph_node_codec>,
    json_record_member<u"Choice", &json_graph_envelope::choice, graph_json_union>,
    json_record_member<u"Other", &json_graph_envelope::other, other_graph_node_codec>>;

struct generated_integer_row
{
    std::int32_t signed32 = std::numeric_limits<std::int32_t>::min();
    std::uint32_t unsigned32 = std::numeric_limits<std::uint32_t>::max();
    std::int64_t signed64 = std::numeric_limits<std::int64_t>::min();
    std::uint64_t unsigned64 = std::numeric_limits<std::uint64_t>::max();

    bool operator==(const generated_integer_row&) const = default;
};

template<auto Field>
using integer_field_codec = json_codec<luminpack::scalar_codec<
    std::remove_cvref_t<decltype(std::declval<generated_integer_row>().*Field)>>>;

template<bool Fused, luminpack::json_name Name, auto Field, luminpack::json_name... FormerNames>
using integer_field = std::conditional_t<Fused,
    luminpack::detail::generated_json_integer_member<Name, Field, integer_field_codec<Field>, FormerNames...>,
    json_record_member<Name, Field, integer_field_codec<Field>, FormerNames...>>;

template<bool Fused>
using generated_integer_body = luminpack::json_lookup_record_body<generated_integer_row, void,
    integer_field<Fused, u"Abcd", &generated_integer_row::signed32>,
    integer_field<Fused, u"Sequence", &generated_integer_row::unsigned32>,
    integer_field<Fused, u"Delta", &generated_integer_row::signed64>,
    integer_field<Fused, u"Abcdefghijkl", &generated_integer_row::unsigned64, u"OldTotal">>;

void generated_integer_writes()
{
    constexpr auto utf8 = json_encoding::utf8;
    constexpr auto utf16 = json_encoding::utf16;
    const generated_integer_row value;
    const auto verify = []<json_encoding Encoding>(std::span<const std::byte> wire, const generated_integer_row& value) {
        for (std::size_t capacity = 0; capacity <= wire.size(); ++capacity)
        {
            constexpr auto canary = std::byte{0xa5};
            std::vector<std::byte> plain(wire.size() + 16, canary);
            auto generated = plain;
            const auto write = [&]<bool Fused>(std::integral_constant<bool, Fused>, auto& buffer) {
                basic_json_writer<Encoding> output(std::span<std::byte>(buffer).subspan(1, capacity));
                try
                {
                    generated_integer_body<Fused>::write(output, value);
                    require(capacity == wire.size(), "Reject a short generated integer document.");
                }
                catch (const std::length_error&)
                {
                    require(capacity < wire.size(), "Accept an exact-sized generated integer document.");
                }
                return output.written();
            };
            const auto ordinary = write(std::false_type{}, plain);
            const auto fused = write(std::true_type{}, generated);
            require(ordinary.size() == fused.size() && plain == generated,
                "Preserve separate integer writer bytes at each capacity.");
            if (capacity == wire.size())
                require(std::ranges::equal(fused, wire), "Write exact signed and unsigned integer text.");
        }
        require(read_json_document<generated_integer_body<true>, Encoding>(wire) == value,
            "Keep the generated integer fields in normal property lookup.");
    };
    verify.template operator()<utf8>(bytes(
        R"({"Abcd":-2147483648,"Sequence":4294967295,"Delta":-9223372036854775808,"Abcdefghijkl":18446744073709551615})"), value);
    verify.template operator()<utf16>(bytes(
        uR"({"Abcd":-2147483648,"Sequence":4294967295,"Delta":-9223372036854775808,"Abcdefghijkl":18446744073709551615})"), value);
    verify.template operator()<utf8>(bytes(R"({"Abcd":-1,"Sequence":2,"Delta":-3,"Abcdefghijkl":4})"),
        generated_integer_row{-1, 2, -3, 4});
    require(read_json_document<generated_integer_body<true>, utf8>(
        bytes(R"({"OldTotal":103})")).unsigned64 == 103, "Keep the former name of a generated integer property.");
}

void utf16_string_writes()
{
    constexpr auto utf16 = json_encoding::utf16;
    const auto check = [](std::u16string_view value, std::u16string_view escaped_content) {
        std::u16string expected(1, u'"');
        expected.append(escaped_content);
        expected.push_back(u'"');
        const auto wire = bytes(std::u16string_view(expected));
        constexpr auto canary = std::byte{0xa5};
        std::vector<std::byte> storage(wire.size() + 2, canary);
        auto destination = std::span(storage).subspan(1, wire.size());
        basic_json_writer<utf16> output(destination);
        output.write_string(value);
        require(std::ranges::equal(output.written(), wire), "Write exact UTF-16 string bytes into an unaligned span.");
        require(storage.front() == canary && storage.back() == canary, "Keep the UTF-16 string output bounded.");
        basic_json_reader<utf16> input(output.written());
        require(input.read_string() == value, "Round-trip UTF-16 string literals and escape runs.");

        std::ranges::fill(storage, canary);
        basic_json_writer<utf16> short_output(destination.first(wire.size() - 1));
        try
        {
            short_output.write_string(value);
            throw std::runtime_error("Reject a short UTF-16 string output span.");
        }
        catch (const std::length_error&)
        {
            require(short_output.written().empty() && std::ranges::all_of(storage, [canary](auto byte) { return byte == canary; }),
                "Keep UTF-16 string output atomic when even the last code unit does not fit.");
        }

        for (const auto source_first : {false, true})
        {
            const std::size_t start = source_first ? 0 : 2;
            const std::size_t target = source_first ? 2 : 0;
            std::vector<char16_t> overlapping(expected.size() + 2);
            std::ranges::copy(value, overlapping.data() + start);
            basic_json_writer<utf16> overlap_output(std::as_writable_bytes(std::span(overlapping).subspan(target)));
            overlap_output.write_string(std::u16string_view(overlapping.data() + start, value.size()));
            require(std::ranges::equal(overlap_output.written(), wire), "Preserve overlapping UTF-16 string input.");
        }
    };

    for (const auto length : {0, 1, 15, 16, 31, 32, 511, 512})
    {
        const std::u16string value(length, u'\u754c');
        check(value, value);
    }
    check(u"\u00e9\r\t\U00010400\f\b", u"\u00e9\\r\\t\U00010400\\u000c\\u0008");

    const std::u16string prefix(33, u'a');
    const std::u16string literal(520, u'\u754c');
    const std::u16string short_run(16, u'x');
    for (const auto tail_length : {31, 32})
    {
        const std::u16string boundary_prefix(511 - tail_length, u'a');
        const std::u16string boundary_tail(tail_length, u'\u754c');
        check(boundary_prefix + u'\t' + boundary_tail, boundary_prefix + uR"(\t)" + boundary_tail);
    }
    const std::u16string cluster{u'\\', u'\0', u'"'};
    const auto sparse_prefix = prefix + u"\n\r" + literal;
    const auto escaped_prefix = prefix + uR"(\n\r)" + literal;
    check(sparse_prefix, escaped_prefix);
    check(sparse_prefix + cluster + short_run + u"\t\f\b",
        escaped_prefix + uR"(\\\u0000\")" + short_run + uR"(\t\u000c\u0008)");
    check(prefix + u"\n\r\t" + literal + u'"', prefix + uR"(\n\r\t)" + literal + uR"(\")");
    const std::u16string dense(40, u'\t');
    std::u16string escaped_dense;
    for (std::size_t index = 0; index < dense.size(); ++index)
        escaped_dense.append(uR"(\t)");
    check(sparse_prefix + dense, escaped_prefix + escaped_dense);
}

void tracked_json_graphs()
{
    using luminpack::read_reference_json_document;
    using luminpack::write_reference_json_document;
    constexpr auto utf8 = json_encoding::utf8;
    json_graph_node root{};
    json_graph_node child{};
    root.value = 1;
    root.next = &root;
    root.children = std::vector<json_graph_node*>{&child, &child};
    child.value = 2;
    child.next = &root;

    const auto verify = [](const json_graph_node* value) {
        require(value && value->value == 1 && value->next == value, "Resolve an early tracked object shell.");
        require(value->children && value->children->size() == 2 &&
            value->children->at(0) == value->children->at(1), "Preserve aliases through a JSON list.");
        const auto* leaf = value->children->front();
        require(leaf && leaf->value == 2 && leaf->next == value && !leaf->children, "Resolve a reference to an enclosing object.");
    };
    std::array<std::byte, 512> output{};
    const auto utf8_written = write_reference_json_document<json_graph_node_codec, utf8>(output, &root);
    require(std::ranges::equal(utf8_written, bytes("{\"$id\":0,\"Value\":1,\"Next\":{\"$ref\":0},\"Children\":["
        "{\"$id\":1,\"Value\":2,\"Next\":{\"$ref\":0},\"Children\":null},{\"$ref\":1}]}")),
        "Write canonical JSON reference metadata before fields.");
    verify(read_reference_json_document<json_graph_node_codec, utf8>(utf8_written).value);
    const auto utf16_written = write_reference_json_document<json_graph_node_codec>(std::span(output).subspan(1), &root);
    require(std::ranges::equal(utf16_written, bytes(u"{\"$id\":0,\"Value\":1,\"Next\":{\"$ref\":0},\"Children\":["
        u"{\"$id\":1,\"Value\":2,\"Next\":{\"$ref\":0},\"Children\":null},{\"$ref\":1}]}")),
        "Write canonical reference metadata in unaligned UTF-16.");
    verify(read_reference_json_document<json_graph_node_codec>(utf16_written).value);

    const auto sparse = read_reference_json_document<json_graph_node_codec>(bytes(
        u"{\"Value\":null,\"$\\u0069d\":4294967295,\"Next\":{\"\\u0024ref\":4294967295,\"Ignored\":[1,2]}}"));
    require(sparse.value && sparse.value->next == sparse.value && sparse.value->value == 0,
        "Read an out-of-order identifier and a default-null scalar.");
    const auto aliases = read_reference_json_document<json_graph_node_codec, utf8>(bytes(
        "{\"$id\":0,\"$id\":7,\"Children\":[{\"$ref\":0},{\"$ref\":7}]}"));
    require(aliases.value && aliases.value->children && aliases.value->children->at(0) == aliases.value &&
        aliases.value->children->at(1) == aliases.value, "Distinct identifiers can name the same early shell.");
    const auto anonymous = read_reference_json_document<json_graph_node_codec, utf8>(bytes("{}"));
    require(anonymous.value && anonymous.value->value == 0, "Missing metadata still publishes a fully staged object.");
    require(!read_reference_json_document<json_graph_node_codec, utf8>(bytes("null")).value,
        "A null reference root owns no object.");

    const auto text = "{\"Anchor\":{\"$id\":0},\"Choice\":{\"$value\":{\"$ref\":0},\"$type\":" +
        std::to_string(graph_node_hash) + "}}";
    const auto envelope = read_reference_json_document<json_graph_envelope_codec, utf8>(bytes(std::string_view(text)));
    require(envelope.value.anchor && envelope.value.choice &&
        std::get<0>(*envelope.value.choice) == envelope.value.anchor,
        "A deferred Union payload shares the enclosing reference table.");
    json_graph_envelope original;
    original.anchor = &root;
    original.choice.emplace(std::in_place_index<0>, &root);
    const auto envelope_written = write_reference_json_document<json_graph_envelope_codec, utf8>(output, original);
    const auto restored = read_reference_json_document<json_graph_envelope_codec, utf8>(envelope_written);
    require(restored.value.anchor && restored.value.choice &&
        std::get<0>(*restored.value.choice) == restored.value.anchor,
        "An ordinary record writes the same reference ID through a nested Union.");

    malformed([] {
        (void)read_reference_json_document<json_graph_node_codec, utf8>(bytes("{\"Next\":{\"$ref\":1},\"$id\":1}"));
    }, "Reject a reference before its identifier is registered.");
    malformed([] {
        (void)read_reference_json_document<json_graph_node_codec, utf8>(bytes("{\"$id\":0,\"$id\":0}"));
    }, "Reject a duplicate reference identifier.");
    malformed([] {
        (void)read_reference_json_document<json_graph_envelope_codec, utf8>(
            bytes("{\"Anchor\":{\"$id\":0},\"Other\":{\"$ref\":0}}"));
    }, "Reject a reference to a differently typed shell.");
    malformed([] {
        (void)read_reference_json_document<json_graph_node_codec, utf8>(bytes("{\"$ref\":0}"));
    }, "Each reference JSON document starts with independent state.");

    luminpack::reference_graph graph;
    luminpack::reference_read_state state(graph);
    malformed([&] {
        luminpack::reference_json_reader<utf8> failed(bytes("{\"$id\":0,\"Value\":true}"), {}, state);
        (void)json_graph_node_codec::read(failed);
    }, "Reject a malformed member after early registration.");
    luminpack::reference_json_reader<utf8> valid(bytes("{\"$id\":0,\"Next\":{\"$ref\":0}}"), {}, state);
    const auto* recovered = json_graph_node_codec::read(valid);
    require(recovered && recovered->next == recovered, "A failed read removes its provisional reference registrations.");

    json_graph_node existing{};
    json_graph_node existing_child{};
    existing.next = &existing_child;
    auto* target = &existing;
    luminpack::reference_json_reader<utf8> reuse(
        bytes("{\"$id\":1,\"Value\":10,\"Next\":{\"$id\":2,\"Value\":20}}"), {}, state);
    json_graph_node_codec::read_into(reuse, target);
    require(target == &existing && target->value == 10 && target->next &&
        target->next != &existing_child && target->next->value == 20 && existing_child.value == 99,
        "Reuse the supplied outer shell while staging new child objects.");

    luminpack::reference_json_reader<utf8> new_shell(bytes("{\"$id\":3,\"Value\":30}"), {}, state);
    json_graph_node_codec::read_into(new_shell, target);
    require(target != &existing && target->value == 30 && existing.value == 10,
        "An existing ID keeps its shell when a later read supplies that same object.");

    luminpack::reference_graph failed_graph;
    luminpack::reference_read_state failed_state(failed_graph);
    json_graph_node retained_child{};
    json_graph_node retained_root{};
    retained_root.next = &retained_child;
    auto* retained = &retained_root;
    malformed([&] {
        luminpack::reference_json_reader<utf8> failed(bytes(
            "{\"$id\":0,\"Next\":{\"$id\":1,\"Next\":{\"$id\":2,\"Value\":7}},\"Value\":"), {}, failed_state);
        json_graph_node_codec::read_into(failed, retained);
    }, "Reject a truncated parent after its child and grandchild are decoded.");
    require(retained == &retained_root && retained->next == &retained_child && retained_child.next == nullptr,
        "Rolling back a failed parent cannot leave new references in its caller-owned child.");
}
}

int main(int argc, char** argv)
{
    try
    {
        require(argc == 2, "Expected .NET fixture directory.");
        dotnet_fixtures(argv[1]);
        profiles_and_strings();
        json_characters();
        ordered_property_heads();
        sparse_property_lookup();
        unicode_property_heads();
        dense_property_lookup();
        ordered_int32_runs();
        validation();
        custom_json();
        generated_opening_heads();
        generated_integer_writes();
        utf16_string_writes();
        tracked_json_graphs();
        std::cout << "LuminPack C++ JSON wire checks passed.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
