#include <luminpack/luminpack.h>

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <ranges>
#include <string>

namespace
{
using luminpack::reader;
using luminpack::writer;
using byte = std::byte;

void check(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<class Error, class Action>
void expect_error(Action action, const char* message)
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

std::vector<byte> fixture(const std::filesystem::path& directory, const char* name)
{
    std::ifstream file(directory / name, std::ios::binary | std::ios::ate);
    check(file.good(), name);
    const auto size = file.tellg();
    check(size >= 0, name);
    std::vector<byte> result(static_cast<std::size_t>(size));
    file.seekg(0);
    file.read(reinterpret_cast<char*>(result.data()), static_cast<std::streamsize>(size));
    check(file.good(), name);
    return result;
}

void fixed_fixtures(const std::filesystem::path& directory)
{
    auto source = fixture(directory, "int.bin");
    reader input(source);
    check(input.read<std::int32_t>() == -123456789, "Read Int32 fixture.");
    input.require_end();

    std::array<byte, 512> bytes{};
    writer output(std::span<byte>(bytes).subspan(1));
    output.write<std::int32_t>(-123456789);
    check(std::ranges::equal(output.written(), source), "Write Int32 fixture.");

    source = fixture(directory, "int-array.bin");
    input = reader(source);
    std::array<std::int32_t, 97> values{};
    const auto destination = std::span<std::int32_t>(values);
    const auto decoded = luminpack::read_array_into(input, destination);
    check(decoded && decoded->size() == values.size(), "Read Int32 array length.");
    input.require_end();

    std::array<std::int32_t, 97> expected{};
    for (std::size_t index = 0; index < expected.size(); ++index)
        expected[index] = (static_cast<std::int32_t>(index) - 32) * 17;
    check(values == expected, "Read Int32 array fixture.");
    output = writer(bytes);
    luminpack::write_array<std::int32_t>(output, expected);
    check(std::ranges::equal(output.written(), source), "Write Int32 array fixture.");

    // The byte window can be an array's own storage, including a count header.
    std::array<std::int32_t, 98> aliased{};
    auto window = std::as_writable_bytes(std::span(aliased));
    output = writer(window);
    const auto part = std::span<std::int32_t>(aliased).first(values.size());
    std::ranges::copy(expected, part.begin());
    luminpack::write_array<std::int32_t>(output, part);
    check(std::ranges::equal(output.written(), source), "Write overlapping Int32 array.");
    input = reader(output.written());
    const auto reused = luminpack::read_array_into(input, part);
    check(reused && std::ranges::equal(*reused, expected), "Read overlapping Int32 array.");

    const std::span<const byte> payload(source);
    for (std::size_t length = 0; length < source.size(); ++length)
    {
        input = reader(payload.first(length));
        values.fill(997);
        expect_error<luminpack::wire_error>([&] { (void)luminpack::read_array_into(input, destination); },
            "Accepts truncated Int32 array.");
        check(input.remaining() == length && values.front() == 997, "Publishes truncated array.");
    }
}

void text_fixtures(const std::filesystem::path& directory)
{
    const auto source = fixture(directory, "string-utf16.bin");
    std::array<char16_t, 64> characters{};
    reader input(source);
    const auto decoded = luminpack::read_utf16_into(input, characters);
    constexpr std::u16string_view expected = u"Unity \u2194 .NET 8 \U0001f600";
    check(decoded && *decoded == expected, "Read UTF-16 fixture.");
    input.require_end();

    std::array<byte, 128> bytes{};
    writer output(bytes);
    luminpack::write_utf16(output, expected);
    check(std::ranges::equal(output.written(), source), "Write UTF-16 fixture.");

    // These values contain null, empty, and a compact Latin-1 member with the default profile.
    output = writer(bytes);
    luminpack::write_utf16(output, std::nullopt);
    luminpack::write_utf16(output, std::u16string_view{});
    luminpack::write_utf16(output, u"\u0001\u00ff");
    constexpr std::array compact{
        byte{255}, byte{255}, byte{255}, byte{255}, byte{0}, byte{0}, byte{0}, byte{0},
        byte{5}, byte{0}, byte{0}, byte{0}, byte{1}, byte{255}
    };
    check(std::ranges::equal(output.written(), compact), "Write null, empty and Latin-1 text.");
    input = reader(compact);
    check(!luminpack::read_utf16_into(input, {}), "Read null text.");
    const auto empty = luminpack::read_utf16_into(input, {});
    check(empty && empty->empty(), "Read empty text.");
    check(luminpack::read_utf16_into(input, characters) == u"\u0001\u00ff", "Read Latin-1 text.");
    input.require_end();
}

void text_vector_boundaries()
{
    // One code unit past an aligned address exercises unaligned vector loads.
    alignas(32) std::array<char16_t, 258> source{};
    for (std::size_t length = 1; length <= 257; ++length)
    {
        const auto units = std::span(source).subspan(1, length);
        for (std::size_t index = 0; index < length; ++index)
            units[index] = static_cast<char16_t>((index * 17 + 111) & 255);
        const std::u16string_view current(units.data(), units.size());

        // Decode into exactly enough storage and include a canary after each destination.
        std::vector<byte> bytes(4 + length * 2 + 1, byte{204});
        std::vector<char16_t> characters(length + 1, char16_t{0xffff});
        const auto storage = std::span<byte>(bytes);
        const auto destination = std::span<char16_t>(characters).first(length);
        writer output(storage);
        for (const auto capacity : {4 + length, 4 + length * 2})
        {
            std::ranges::fill(bytes, byte{204});
            output = writer(storage.first(capacity));
            luminpack::write_utf16(output, current);
            check(output.written().size() == 4 + length && bytes[4 + length] == byte{204}, "Latin-1 writer bounds.");
            reader compact(output.written());
            check(compact.read<std::int32_t>() == static_cast<std::int32_t>(length * 2 + 1), "Latin-1 record marker.");
            check(std::ranges::equal(compact.take(length), current,
                [](byte actual, char16_t unit) { return actual == static_cast<byte>(unit); }), "Latin-1 vector lane order.");
            compact.require_end();
        }

        reader input(output.written());
        check(luminpack::read_utf16_into(input, destination) == current, "Latin-1 widening.");
        check(characters.back() == 0xffff, "Latin-1 reader bounds.");
        input.require_end();

        for (std::size_t index = 0; index < length; ++index)
        {
            const auto saved = units[index];
            units[index] = 0xd800;
            output = writer(storage.first(4 + length * 2));
            luminpack::write_utf16(output, current);
            check(output.written().size() == 4 + length * 2 && bytes.back() == byte{204}, "Raw UTF-16 writer bounds.");
            reader raw(output.written());
            check(raw.read<std::int32_t>() == static_cast<std::int32_t>(units.size_bytes()), "Raw UTF-16 record marker.");
            check(std::ranges::equal(raw.take(units.size_bytes()), std::as_bytes(units)), "Raw UTF-16 payload bytes.");
            raw.require_end();
            input = reader(output.written());
            check(luminpack::read_utf16_into(input, destination) == current, "Mixed text and unpaired surrogate preservation.");
            input.require_end();
            units[index] = saved;
        }
    }

    for (const auto length : {16, 17, 32, 33, 64, 65})
    {
        const auto units = std::span(source).subspan(1, static_cast<std::size_t>(length));
        units.back() = 0x100;
        const std::u16string_view current(units.data(), units.size());
        std::array<byte, 4 + 65> compact{};
        writer output(std::span<byte>(compact).first(4 + units.size()));
        expect_error<std::length_error>([&] { luminpack::write_utf16(output, current); },
            "Writes a raw UTF-16 record into a compact-only window.");
        check(output.written().empty() && std::ranges::all_of(compact, [](byte value) { return value == byte{}; }),
            "Publishes a failed medium UTF-16 write.");
        units.back() = u'\0';
    }

    std::array<char16_t, 40> alias{};
    constexpr std::u16string_view text = u"0123456789ABCDEF\u00ff0123456789ABCDEF\u00ff";
    std::ranges::copy(text, alias.begin());
    writer output(std::as_writable_bytes(std::span(alias)));
    luminpack::write_utf16(output, std::u16string_view(alias.data(), text.size()));
    reader input(output.written());
    check(luminpack::read_utf16_into(input, alias) == text, "Overlapping compact text.");

    std::array<byte, 20> short_bytes{};
    output = writer(short_bytes);
    expect_error<std::length_error>([&] { luminpack::write_utf16(output, u"0123456789\u0100"); },
        "Writes a wide record into too little storage.");
    check(output.written().empty() && std::ranges::all_of(short_bytes, [](byte value) { return value == byte{}; }),
        "Publishes a short UTF-16 write.");
}

void short_utf16_records()
{
    using codec = luminpack::string_codec<luminpack::string_profile::utf16_length>;
    using required = luminpack::required_codec<codec>;

    std::array<char16_t, 16> characters{};
    for (std::size_t index = 0; index < characters.size(); ++index)
        characters[index] = static_cast<char16_t>(0xff - index * 11);

    for (std::size_t length = 1; length <= characters.size(); ++length)
    {
        const std::u16string_view expected(characters.data(), length);
        const auto raw_bound = sizeof(std::int32_t) + length * sizeof(char16_t);
        const auto check_compact = [&](std::span<const byte> record)
        {
            reader input(record);
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(length * 2 + 1), "Short UTF-16 compact header.");
            const auto payload = input.take(length);
            check(std::ranges::equal(payload, expected, [](byte actual, char16_t unit) { return actual == static_cast<byte>(unit); }),
                "Short UTF-16 compact bytes.");
            input.require_end();
        };

        std::array<byte, 38> bytes;
        bytes.fill(byte{204});
        writer output(std::span<byte>(bytes).subspan(1, raw_bound));
        codec::write(output, codec::value_type(std::in_place, expected));
        check_compact(output.written());
        check(bytes.front() == byte{204} && bytes[1 + output.written().size()] == byte{204}, "Short UTF-16 write bounds.");

        std::array<char16_t, 20> alias;
        alias.fill(u'\u0100');
        std::ranges::copy(expected, alias.begin());
        output = writer(std::as_writable_bytes(std::span(alias)).first(raw_bound));
        required::write(output, std::u16string_view(alias.data(), length));
        check_compact(output.written());
    }
}

void utf16_length_collection_reads()
{
    using strings = luminpack::list_codec<luminpack::string_codec<>>;
    const strings::value_type expected = std::vector<std::optional<std::u16string>>{
        u"A\u0080\u00ffZ", u"123456\u00ff", u"1234567\u0080", u"123456789ABCDEF\u00ff",
        std::nullopt, u"", u"Raw\u0100", u"123", u"123456789ABCDEFG\u0080", u"short-tail"
    };
    std::array<byte, 256> storage{};
    writer output(std::span<byte>(storage).subspan(1));
    strings::write(output, expected);
    output.write<std::uint32_t>(0x01020304);
    reader input(output.written());
    check(strings::read(input) == expected, "Reads a compact UTF-16 list prefix and ordinary suffix.");
    check(input.read<std::uint32_t>() == 0x01020304, "UTF-16 list consumes its exact record window.");
    input.require_end();

    const strings::value_type pair = std::vector<std::optional<std::u16string>>{
        u"A\u0080\u00ffZ", u"1234567\u00ff"
    };
    output = writer(std::span<byte>(storage).subspan(1));
    strings::write(output, pair);
    const auto wire = output.written();
    strings::value_type destination(std::in_place, 1, std::u16string(u"Previous"));
    destination->reserve(32);
    const auto* reused = destination->data();
    const auto check_prefix = [&]
    {
        check(destination && destination->size() == 1 && destination->front() == pair->front(),
            "Publishes exactly the validated UTF-16 list prefix.");
        check(destination->data() == reused, "Retains existing UTF-16 list storage.");
    };

    input = reader(wire.first(wire.size() - 1));
    expect_error<luminpack::wire_error>([&] { strings::read_into(input, destination); }, "Accepts a truncated compact UTF-16 list record.");
    check(input.remaining() == wire.size() - 1, "Consumes a truncated UTF-16 list.");
    check_prefix();

    input = reader(wire, {.max_string_length = 7});
    expect_error<luminpack::wire_error>([&] { strings::read_into(input, destination); }, "Accepts a compact UTF-16 list record above its quota.");
    check(input.remaining() == wire.size(), "Consumes a rejected UTF-16 list.");
    check_prefix();

    input = reader(wire);
    strings::read_into(input, destination);
    check(destination == pair && destination->data() == reused, "Refills the vector in wire order.");
    input.require_end();

    constexpr std::size_t second_offset = sizeof(std::int32_t) * 2 + 4;
    const auto second = wire.subspan(second_offset);
    input = reader(second.first(second.size() - 1));
    expect_error<luminpack::wire_error>([&] { (void)luminpack::string_codec<>::read(input); }, "Accepts a truncated short UTF-16 string.");
    check(input.remaining() == second.size() - 1, "Consumes a truncated short UTF-16 string.");
    input = reader(second, {.max_string_length = 7});
    expect_error<luminpack::wire_error>([&] { (void)luminpack::string_codec<>::read(input); }, "Accepts a short UTF-16 string above its quota.");
    check(input.remaining() == second.size(), "Consumes a rejected short UTF-16 string.");
}

struct tagged32
{
    std::int32_t key;
    std::int32_t first;
    std::int32_t second;
};

struct tagged64
{
    std::int32_t key;
    std::int32_t first;
    std::int32_t second;
    std::uint32_t padding;
};

struct tagged_reordered
{
    std::int32_t second;
    std::int32_t key;
    std::int32_t first;
};

using luminpack::storage_model;
using luminpack::tagged_layout;
constexpr std::array wire_models{storage_model<3>{12, {0, 4, 8}}, storage_model<3>{16, {0, 4, 8}}};

template<class T>
constexpr auto layout = tagged_layout<3, 2>{
    {4, 4, 4}, {sizeof(T), {offsetof(T, key), offsetof(T, first), offsetof(T, second)}}, wire_models
};

constexpr auto padded_canonical = tagged_layout<3, 2>{
    {4, 4, 4}, layout<tagged_reordered>.local, {wire_models[1], wire_models[0]}
};

template<class T>
void check_tagged(const std::optional<std::span<T>>& decoded)
{
    check(decoded && decoded->size() == 2 &&
        (*decoded)[0].key == 1 && (*decoded)[0].first == 2 && (*decoded)[0].second == 3 &&
        (*decoded)[1].key == 4 && (*decoded)[1].first == 5 && (*decoded)[1].second == 6,
        "Read Tagged fixture fields.");
}

void tagged_fixtures(const std::filesystem::path& directory)
{
    const auto wire32 = fixture(directory, "tagged-auto-layout-32.bin");
    const auto wire64 = fixture(directory, "tagged-auto-layout-64.bin");
    std::array<tagged32, 2> values32{{{1, 2, 3}, {4, 5, 6}}};
    std::array<tagged64, 2> values64{{{1, 2, 3, 0}, {4, 5, 6, 0}}};
    std::array<tagged_reordered, 2> reordered{{{3, 1, 2}, {6, 4, 5}}};
    std::array<byte, 64> result{};

    writer output(result);
    luminpack::write_tagged_array<tagged32, layout<tagged32>>(output, values32);
    check(std::ranges::equal(output.written(), wire32), "Write native Tagged model 1.");
    output = writer(result);
    luminpack::write_tagged_array<tagged64, layout<tagged64>>(output, values64);
    check(std::ranges::equal(output.written(), wire64), "Write native Tagged model 2.");
    output = writer(result);
    luminpack::write_tagged_array<tagged_reordered, layout<tagged_reordered>>(output, reordered);
    check(std::ranges::equal(output.written(), wire32), "Write unmodeled storage in canonical form.");
    const auto mutable_bytes = std::span<byte>(result);
    result.fill(byte{255});
    output = writer(result);
    luminpack::write_tagged_array<tagged_reordered, padded_canonical>(output, reordered);
    check(output.written().size() == wire64.size() && result[4] == byte{1} &&
        result[17] == byte{0} && result[33] == byte{0}, "Canonical writer initializes padding.");

    for (const auto& source : {wire32, wire64})
    {
        reader input(source);
        values32 = {};
        check_tagged(luminpack::read_tagged_array_into<tagged32, layout<tagged32>>(input, values32));
        input.require_end();
        input = reader(source);
        values64 = {{{0, 0, 0, 77}, {0, 0, 0, 88}}};
        check_tagged(luminpack::read_tagged_array_into<tagged64, layout<tagged64>>(input, values64));
        check(values64[0].padding == (source == wire32 ? 77u : 0u), "Cross-layout read preserves local padding.");
        input.require_end();
    }

    std::array<tagged64, 3> alias{};
    auto aliased = std::as_writable_bytes(std::span(alias));
    std::ranges::copy(wire32, aliased.begin());
    reader input(aliased.first(wire32.size()));
    check_tagged(luminpack::read_tagged_array_into<tagged64, layout<tagged64>>(input, alias));
    input.require_end();

    for (std::size_t length = 0; length < wire64.size(); ++length)
    {
        input = reader(std::span<const byte>(wire64).first(length));
        values32 = {};
        expect_error<luminpack::wire_error>([&] {
            (void)luminpack::read_tagged_array_into<tagged32, layout<tagged32>>(input, values32);
        }, "Accepts truncated Tagged array.");
        check(values32[0].key == 0 && input.remaining() == length, "Publishes truncated Tagged array.");
    }

    std::ranges::copy(wire32, mutable_bytes.begin());
    result[4] = byte{3};
    input = reader(mutable_bytes.first(wire32.size()));
    expect_error<luminpack::wire_error>([&] {
        (void)luminpack::read_tagged_array_into<tagged32, layout<tagged32>>(input, values32);
    }, "Accepts unknown Tagged model.");
}

template<luminpack::string_profile Profile>
void text_list_fixture(const std::filesystem::path& directory, const char* name)
{
    using codec = luminpack::list_codec<luminpack::string_codec<Profile>>;
    const auto text = fixture(directory, name);
    const typename codec::value_type expected = std::vector<std::optional<std::u16string>>{
        std::nullopt, u"", u"a", u"\u00e9", u"\u4e2d", u"\U0001f600", std::u16string(64, u'a')
    };
    reader input(text);
    check(codec::read(input) == expected, "Read .NET VarInt List<string>.");
    input.require_end();
    std::array<byte, 256> buffer{};
    writer output(buffer);
    codec::write(output, expected);
    check(std::ranges::equal(output.written(), text), "Write .NET VarInt List<string>.");
}

enum class color : std::uint16_t { blue = 1, red = 4096 };

void collection_contracts(const std::filesystem::path& directory)
{
    using numbers = luminpack::list_codec<luminpack::scalar_codec<std::int32_t>>;
    using colors = luminpack::list_codec<luminpack::enum_codec<color>>;
    using bools = luminpack::list_codec<luminpack::boolean_codec>;
    using utf8_key = luminpack::required_codec<luminpack::string_codec<luminpack::string_profile::utf8_length>>;
    using dictionary = luminpack::dictionary_codec<utf8_key, luminpack::scalar_codec<std::int32_t>>;
    using float_map = luminpack::dictionary_codec<luminpack::scalar_codec<float>, luminpack::scalar_codec<std::int32_t>>;

    const auto integer_bytes = fixture(directory, "int-array.bin");
    reader input(integer_bytes);
    auto value = numbers::read(input);
    check(value && value->size() == 97 && (*value)[32] == 0, "Read raw scalar List.");
    auto* original_storage = value->data();
    input = reader(integer_bytes);
    numbers::read_into(input, value);
    check(value->data() == original_storage, "Raw scalar List reuses capacity.");
    std::array<byte, 512> buffer{};
    writer output(buffer);
    numbers::write(output, value);
    check(std::ranges::equal(output.written(), integer_bytes), "Write raw scalar List.");

    output = writer(buffer);
    const colors::value_type expected_colors = std::vector{color::blue, color::red, static_cast<color>(0xffee)};
    colors::write(output, expected_colors);
    input = reader(output.written());
    check(colors::read(input) == expected_colors && output.written().size() == 10, "Read unnamed enum in raw List.");
    input.require_end();

    output = writer(buffer);
    bools::write(output, std::vector<bool>{true, false, true});
    input = reader(output.written());
    check(bools::read(input) == bools::value_type(std::vector<bool>{true, false, true}), "Read Boolean List.");
    input.require_end();
    buffer[sizeof(std::int32_t) + 1] = byte{2};
    input = reader(output.written());
    expect_error<luminpack::wire_error>([&] { (void)bools::read(input); }, "Accepts invalid Boolean.");

    const auto dictionary_bytes = fixture(directory, "dictionary.bin");
    input = reader(dictionary_bytes);
    const auto map = dictionary::read(input);
    check(map && map->size() == 2 && map->at(u"one") == 1 && map->at(u"\u4e2d\u6587") == 2,
        "Read .NET Dictionary<string,int>.");
    input.require_end();
    const std::array<std::pair<std::u16string_view, std::int32_t>, 2> entries{{{u"one", 1}, {u"\u4e2d\u6587", 2}}};
    output = writer(buffer);
    dictionary::write_entries(output, entries);
    check(std::ranges::equal(output.written(), dictionary_bytes), "Write .NET Dictionary<string,int>.");

    output = writer(buffer);
    output.write<std::int32_t>(2);
    output.write(std::bit_cast<float>(0x7fc00000u));
    output.write<std::int32_t>(1);
    output.write(std::bit_cast<float>(0x7fc00001u));
    output.write<std::int32_t>(2);
    input = reader(output.written());
    expect_error<luminpack::wire_error>([&] { (void)float_map::read(input); }, "Accepts duplicate .NET NaN keys.");

    output = writer(buffer);
    dictionary::write(output, std::nullopt);
    dictionary::write(output, dictionary::dictionary_type{});
    input = reader(output.written());
    check(!dictionary::read(input), "Read null dictionary.");
    const auto empty = dictionary::read(input);
    check(empty && empty->empty(), "Read empty dictionary.");
    input.require_end();

    input = reader(integer_bytes, {.max_collection_length = 96});
    expect_error<luminpack::wire_error>([&] { numbers::read_into(input, value); }, "Disregards the collection quota.");
    check(value->data() == original_storage && input.remaining() == integer_bytes.size(), "Mutates a quota-rejected list.");
}

void string_profiles(const std::filesystem::path& directory)
{
    using luminpack::string_profile;
    const auto utf8 = fixture(directory, "string-utf8.bin");
    constexpr auto expected = u"Unity \u2194 .NET 8 \U0001f600";
    reader input(utf8);
    check(luminpack::read_string(input, string_profile::utf8_length) == expected, "Read .NET UTF-8.");
    input.require_end();
    std::array<byte, 256> buffer{};
    writer output(buffer);
    luminpack::write_string(output, string_profile::utf8_length, expected);
    check(std::ranges::equal(output.written(), utf8), "Write .NET UTF-8.");
    text_list_fixture<string_profile::utf8_varint>(directory, "string-varint-utf8.bin");
    text_list_fixture<string_profile::utf16_varint>(directory, "string-varint-utf16.bin");

    using strings = luminpack::list_codec<luminpack::string_codec<string_profile::utf8_length>>;
    const auto raw_list = fixture(directory, "string-list.bin");
    const strings::value_type values = std::vector<std::optional<std::u16string>>{
        u"alpha", u"\u4e2d\u6587", u"\U0001f600", u""
    };
    input = reader(raw_list);
    check(strings::read(input) == values, "Read .NET List<string>.");
    input.require_end();
    output = writer(buffer);
    strings::write(output, values);
    check(std::ranges::equal(output.written(), raw_list), "Write .NET List<string>.");

    for (const auto profile : {string_profile::utf8_token, string_profile::utf16_token})
    {
        output = writer(buffer);
        luminpack::write_string(output, profile, std::nullopt);
        luminpack::write_string(output, profile, u"");
        luminpack::write_string(output, profile, expected);
        input = reader(output.written());
        const auto null_as_empty = luminpack::read_string(input, profile);
        check(null_as_empty && null_as_empty->empty(), "Token null is represented as empty.");
        check(luminpack::read_string(input, profile) == u"", "Read an empty token.");
        std::array<char16_t, 32> destination{};
        check(luminpack::read_string_into(input, profile, destination) == expected, "Read into an existing token buffer.");
        input.require_end();
    }

    constexpr std::array malformed_utf8_token{byte{0xe2}, byte{0x82}, byte{0x40}, byte{0}};
    input = reader(malformed_utf8_token);
    check(luminpack::read_string(input, string_profile::utf8_token) == u"\ufffd@", "UTF-8 token replacement.");
    constexpr std::array malformed_utf16_token{byte{0}, byte{0xd8}, byte{64}, byte{0}, byte{0}, byte{0}};
    input = reader(malformed_utf16_token);
    check(luminpack::read_string(input, string_profile::utf16_token) == u"\ufffd@", "UTF-16 token replacement.");

    constexpr std::array overlong{byte{0x80}, byte{0}};
    input = reader(overlong);
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_string(input, string_profile::utf16_varint); },
        "Accepts a non-canonical variable header.");
    constexpr std::array wrong_count{byte{3}, byte{3}, byte{0xc3}, byte{0xa9}};
    input = reader(wrong_count);
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_string(input, string_profile::utf8_varint); },
        "Accepts a mismatched UTF-8 character count.");
    check(input.remaining() == wrong_count.size(), "Consumes an invalid string.");

    input = reader(utf8, {.max_string_length = 16});
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_string(input, string_profile::utf8_length); },
        "Disregards the string quota.");
}

void short_utf8_records(const std::filesystem::path& directory)
{
    constexpr auto profile = luminpack::string_profile::utf8_length;
    using strings = luminpack::list_codec<luminpack::string_codec<profile>>;
    const auto write_fixture = [&](const char* name, std::size_t first, std::size_t last)
    {
        const auto source = fixture(directory, name);
        strings::value_type expected(std::in_place);
        for (auto length = first; length <= last; ++length)
            expected->emplace_back(std::in_place, length, u'x');
        std::vector<byte> fixture_output(source.size(), byte{204});
        writer output(fixture_output);
        strings::write(output, expected);
        check(std::ranges::equal(output.written(), source), name);
    };
    write_fixture("short-ascii.bin", 4, 16);
    write_fixture("medium-ascii.bin", 17, 32);

    constexpr std::size_t header_size = 8;
    for (std::size_t length = 3; length <= 33; ++length)
    {
        alignas(32) std::array<char16_t, 34> characters{};
        const auto units = std::span(characters).subspan(1, length);
        constexpr std::array pattern{u'\0', u'"', u'\\', u'\n', u'\x7f', u'~', u'x', u'Z', u';', u'@', u'}'};
        for (std::size_t index = 0; index < length; ++index)
            units[index] = pattern[index % pattern.size()];
        const auto text = std::u16string_view(units.data(), length);
        const auto ascii_byte = [](byte actual, char16_t unit) { return actual == static_cast<byte>(unit); };
        const auto check_ascii = [&](std::span<const byte> record)
        {
            reader input(record);
            const auto count = static_cast<std::int32_t>(length);
            check(input.read<std::int32_t>() == count && input.read<std::int32_t>() == count, "Short ASCII length headers.");
            check(std::ranges::equal(input.take(length), text, ascii_byte), "Short ASCII payload bytes.");
            input.require_end();
        };

        std::array<byte, 44> storage;
        storage.fill(byte{204});
        const auto window = std::span(storage).subspan(1, header_size + length);
        writer output(window);
        luminpack::write_string(output, profile, text);
        check_ascii(output.written());
        check(storage.front() == byte{204} && storage[1 + window.size()] == byte{204}, "Short ASCII write bounds.");

        std::array<char16_t, 48> alias{};
        std::ranges::copy(text, alias.begin() + 1);
        output = writer(std::as_writable_bytes(std::span(alias)).subspan(1, window.size()));
        luminpack::write_string(output, profile, std::u16string_view(alias.data() + 1, length));
        check_ascii(output.written());

        const auto verify_failure = [&](auto error)
        {
            storage.fill(byte{204});
            output = writer(window);
            using Error = typename decltype(error)::type;
            expect_error<Error>([&] { luminpack::write_string(output, profile, text); }, "Accepts an invalid short UTF-8 write.");
            check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
                "Publishes a failed short UTF-8 write.");
        };
        const auto fallback = std::span(storage).subspan(1, window.size() + 1);
        for (std::size_t index = 0; index < length; ++index)
        {
            const auto original = units[index];
            units[index] = 0x80;
            verify_failure(std::type_identity<std::length_error>{});
            output = writer(fallback);
            luminpack::write_string(output, profile, text);
            reader input(output.written());
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(length + 1) &&
                input.read<std::int32_t>() == static_cast<std::int32_t>(length), "Non-ASCII short UTF-8 length headers.");
            check(std::ranges::equal(input.take(index), text.substr(0, index), ascii_byte) &&
                input.read<std::uint8_t>() == 0xc2 && input.read<std::uint8_t>() == 0x80 &&
                std::ranges::equal(input.take(length - index - 1), text.substr(index + 1), ascii_byte), "Non-ASCII short UTF-8 payload bytes.");
            input.require_end();
            units[index] = original;
        }

        units.back() = 0xd800;
        verify_failure(std::type_identity<std::invalid_argument>{});
    }
}

void long_utf8_ascii_runs()
{
    constexpr auto profile = luminpack::string_profile::utf8_length;
    constexpr std::size_t header_size = 8;
    constexpr std::array<std::size_t, 18> lengths{32, 33, 63, 64, 65, 95, 96, 97, 127, 128, 129, 159, 160, 161, 191, 192, 193, 257};

    for (const auto length : lengths)
    {
        alignas(32) std::array<char16_t, 258> characters{};
        const auto units = std::span(characters).subspan(1, length);
        const auto text = std::u16string_view(units.data(), units.size());
        std::vector<byte> ascii(length);
        for (std::size_t index = 0; index < length; ++index)
        {
            units[index] = static_cast<char16_t>((index * 17 + index / 8) & 0x7f);
            ascii[index] = static_cast<byte>(units[index]);
        }

        const auto check_record = [&](std::span<const byte> record, std::span<const byte> payload)
        {
            reader input(record);
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(payload.size()), "Long UTF-8 byte count.");
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(length), "Long UTF-8 code-unit count.");
            check(std::ranges::equal(input.take(payload.size()), payload), "Long UTF-8 exact payload.");
            input.require_end();
        };

        std::array<byte, header_size + 257 + 3> storage;
        const auto verify_write = [&](std::span<const byte> payload)
        {
            storage.fill(byte{204});
            const auto window = std::span(storage).subspan(1, header_size + payload.size());
            writer output(window);
            luminpack::write_string(output, profile, text);
            check_record(output.written(), payload);
            check(storage.front() == byte{204} && storage[1 + window.size()] == byte{204}, "Long UTF-8 write bounds.");
        };
        verify_write(ascii);

        const std::array<std::size_t, 12> positions{1, 31, 32, 63, 64, 95, 96, 127, 128, 191, 192, length - 1};
        for (const auto index : positions)
        {
            if (index >= length)
                continue;
            const auto original = units[index];
            units[index] = 0x80;
            auto mixed = ascii;
            mixed[index] = byte{0xc2};
            mixed.insert(mixed.begin() + static_cast<std::ptrdiff_t>(index + 1), byte{0x80});
            verify_write(mixed);
            units[index] = original;
        }

        std::array<char16_t, 258> alias{};
        std::ranges::copy(text, alias.begin() + 1);
        writer output(std::as_writable_bytes(std::span(alias)).subspan(1, header_size + length));
        luminpack::write_string(output, profile, std::u16string_view(alias.data() + 1, length));
        check_record(output.written(), ascii);

        storage.fill(byte{204});
        output = writer(std::span(storage).first(header_size + length - 1));
        expect_error<std::length_error>([&] { luminpack::write_string(output, profile, text); }, "Writes a short UTF-8 window.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes a short long-ASCII write.");

        units.back() = 0xd800;
        output = writer(storage);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(output, profile, text); }, "Writes an invalid UTF-8 tail.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes an invalid long-ASCII write.");
    }
}

void homogeneous_two_byte_utf8_runs()
{
    constexpr auto profile = luminpack::string_profile::utf8_length;
    constexpr std::size_t header_size = 8;
    constexpr std::array<std::size_t, 13> lengths{31, 32, 33, 39, 40, 47, 48, 63, 64, 65, 79, 96, 129};

    for (const auto length : lengths)
    {
        alignas(64) std::array<char16_t, 130> characters{};
        const auto units = std::span(characters).subspan(1, length);
        const auto text = std::u16string_view(units.data(), units.size());
        units.front() = 0x80;
        units.back() = 0x7ff;
        for (std::size_t index = 1; index + 1 < length; ++index)
            units[index] = static_cast<char16_t>(0x80 + (index * 67) % 0x780);

        std::vector<byte> payload;
        for (const auto unit : text)
        {
            payload.push_back(static_cast<byte>(0xc0 | unit / 64));
            payload.push_back(static_cast<byte>(0x80 | unit % 64));
        }

        std::array<byte, header_size + 2 * 129 + 3> storage;
        const auto check_record = [&](std::span<const byte> record, std::span<const byte> expected)
        {
            reader input(record);
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(expected.size()), "Homogeneous UTF-8 byte count.");
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(length), "Homogeneous UTF-8 code-unit count.");
            check(std::ranges::equal(input.take(expected.size()), expected), "Homogeneous UTF-8 exact payload.");
            input.require_end();
        };
        const auto verify_write = [&](std::span<const byte> expected)
        {
            storage.fill(byte{204});
            const auto window = std::span(storage).subspan(1, header_size + expected.size());
            writer output(window);
            luminpack::write_string(output, profile, text);
            check_record(output.written(), expected);
            check(storage.front() == byte{204} && storage[1 + window.size()] == byte{204}, "Homogeneous UTF-8 write bounds.");
        };
        verify_write(payload);

        const std::array<std::size_t, 11> positions{7, 8, 15, 16, 31, 32, 47, 48, 63, 64, length - 1};
        for (const auto index : positions)
        {
            if (index >= length)
                continue;
            const auto original = units[index];
            units[index] = 0x800;
            auto mixed = payload;
            mixed[index * 2] = byte{0xe0};
            mixed[index * 2 + 1] = byte{0xa0};
            mixed.insert(mixed.begin() + static_cast<std::ptrdiff_t>(index * 2 + 2), byte{0x80});
            verify_write(mixed);
            units[index] = original;
        }

        // A supplementary scalar consumes two UTF-16 units and produces four bytes.
        units[length - 2] = 0xd83d;
        units[length - 1] = 0xde00;
        auto supplementary = payload;
        std::ranges::copy(std::array{byte{0xf0}, byte{0x9f}, byte{0x98}, byte{0x80}}, supplementary.end() - 4);
        verify_write(supplementary);

        std::array<char16_t, 140> alias{};
        std::ranges::copy(text, alias.begin() + 1);
        writer output(std::as_writable_bytes(std::span(alias)).subspan(1, header_size + supplementary.size()));
        luminpack::write_string(output, profile, std::u16string_view(alias.data() + 1, length));
        check_record(output.written(), supplementary);

        storage.fill(byte{204});
        output = writer(std::span(storage).first(header_size + supplementary.size() - 1));
        expect_error<std::length_error>([&] { luminpack::write_string(output, profile, text); }, "Writes a short homogeneous UTF-8 window.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes a short homogeneous UTF-8 write.");

        units.back() = 0xd800;
        output = writer(storage);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(output, profile, text); }, "Writes an invalid homogeneous UTF-8 tail.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes an invalid homogeneous UTF-8 write.");
    }
}

void homogeneous_bmp_utf8_runs()
{
    constexpr auto profile = luminpack::string_profile::utf8_length;
    constexpr std::size_t header_size = 8;
    constexpr std::array<std::size_t, 15> lengths{31, 32, 33, 39, 40, 47, 48, 63, 64, 65, 71, 72, 96, 129, 193};
    constexpr std::array<char16_t, 7> units_table{0x0800, 0x4e2d, 0x97a9, 0xd7ff, 0xe000, 0xfffd, 0xffff};
    constexpr std::array<std::array<byte, 3>, 7> byte_table{{
        {byte{0xe0}, byte{0xa0}, byte{0x80}}, {byte{0xe4}, byte{0xb8}, byte{0xad}}, {byte{0xe9}, byte{0x9e}, byte{0xa9}},
        {byte{0xed}, byte{0x9f}, byte{0xbf}}, {byte{0xee}, byte{0x80}, byte{0x80}}, {byte{0xef}, byte{0xbf}, byte{0xbd}},
        {byte{0xef}, byte{0xbf}, byte{0xbf}}
    }};

    for (const auto length : lengths)
    {
        alignas(64) std::array<char16_t, 194> characters{};
        const auto units = std::span(characters).subspan(1, length);
        const auto text = std::u16string_view(units.data(), units.size());
        std::vector<byte> payload;
        for (std::size_t index = 0; index < length; ++index)
        {
            const auto entry = index % units_table.size();
            units[index] = units_table[entry];
            payload.insert(payload.end(), byte_table[entry].begin(), byte_table[entry].end());
        }

        std::array<byte, header_size + 3 * 193 + 2> storage;
        const auto check_record = [&](std::span<const byte> record, std::span<const byte> expected)
        {
            reader input(record);
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(expected.size()), "BMP UTF-8 byte count.");
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(length), "BMP UTF-8 code-unit count.");
            check(std::ranges::equal(input.take(expected.size()), expected), "BMP UTF-8 exact payload.");
            input.require_end();
        };
        const auto verify_write = [&](std::span<const byte> expected)
        {
            storage.fill(byte{204});
            const auto window = std::span(storage).subspan(1, header_size + expected.size());
            writer output(window);
            luminpack::write_string(output, profile, text);
            check_record(output.written(), expected);
            check(storage.front() == byte{204} && storage[1 + window.size()] == byte{204}, "BMP UTF-8 write bounds.");
        };
        verify_write(payload);

        const std::array<std::size_t, 11> positions{7, 8, 15, 16, 31, 32, 47, 48, 63, 64, length - 1};
        for (const auto index : positions)
        {
            if (index >= length)
                continue;
            const auto original = units[index];
            units[index] = 0x7ff;
            auto mixed = payload;
            mixed[index * 3] = byte{0xdf};
            mixed[index * 3 + 1] = byte{0xbf};
            mixed.erase(mixed.begin() + static_cast<std::ptrdiff_t>(index * 3 + 2));
            verify_write(mixed);
            units[index] = original;
        }

        if (length == lengths.back())
        {
            auto separated = payload;
            const std::array<std::size_t, 3> delimiters{98, 65, 32};
            for (const auto index : delimiters)
            {
                units[index] = u'|';
                const auto at = separated.begin() + static_cast<std::ptrdiff_t>(index * 3);
                *at = byte{'|'};
                separated.erase(at + 1, at + 3);
            }
            verify_write(separated);
            for (const auto index : delimiters)
                units[index] = units_table[index % units_table.size()];
        }

        units[length - 2] = 0xd83d;
        units[length - 1] = 0xde00;
        auto supplementary = payload;
        supplementary.resize(payload.size() - 6);
        const std::array emoji{byte{0xf0}, byte{0x9f}, byte{0x98}, byte{0x80}};
        supplementary.insert(supplementary.end(), emoji.begin(), emoji.end());
        verify_write(supplementary);

        std::array<char16_t, 300> alias{};
        std::ranges::copy(text, alias.begin() + 1);
        writer output(std::as_writable_bytes(std::span(alias)).subspan(1, header_size + supplementary.size()));
        luminpack::write_string(output, profile, std::u16string_view(alias.data() + 1, length));
        check_record(output.written(), supplementary);

        storage.fill(byte{204});
        output = writer(std::span(storage).first(header_size + supplementary.size() - 1));
        expect_error<std::length_error>([&] { luminpack::write_string(output, profile, text); }, "Writes a short BMP UTF-8 window.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes a short BMP UTF-8 write.");

        units.back() = 0xd800;
        output = writer(storage);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(output, profile, text); }, "Writes an invalid BMP UTF-8 tail.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes an invalid BMP UTF-8 write.");
    }
}

void mixed_latin1_utf8_masks()
{
    constexpr auto profile = luminpack::string_profile::utf8_length;
    constexpr std::size_t header_size = 8;
    constexpr std::size_t length = 128;
    constexpr std::array suffix{char16_t{0x100}, char16_t{0x4e2d}, char16_t{0xd83d}, char16_t{0xde00}, u'?'};
    constexpr std::array suffix_bytes{
        byte{0xc4}, byte{0x80}, byte{0xe4}, byte{0xb8}, byte{0xad},
        byte{0xf0}, byte{0x9f}, byte{0x98}, byte{0x80}, byte{'?'}
    };

    for (unsigned mask = 0; mask < 256; ++mask)
    {
        alignas(64) std::array<char16_t, length + suffix.size() + 1> characters{};
        const auto units = std::span(characters).subspan(1, length);
        for (std::size_t index = 0; index < length; ++index)
        {
            const auto high_bit = ((mask >> (index % 8)) & 1u) * 128;
            units[index] = static_cast<char16_t>(high_bit + ((index * 19 + mask * 31) & 0x7f));
        }
        // Lead with Latin-1 and reject the homogeneous sample even for mask 255.
        units[0] = 0xff;
        units[1] = u'\0';
        const auto text = std::u16string_view(units.data(), length);

        std::vector<byte> expected;
        for (const auto unit : text)
        {
            if (unit < 128)
                expected.push_back(static_cast<byte>(unit));
            else
            {
                expected.push_back(static_cast<byte>(192 + unit / 64));
                expected.push_back(static_cast<byte>(128 + unit % 64));
            }
        }

        const auto check_record = [](std::span<const byte> record, std::span<const byte> payload, std::size_t unit_count)
        {
            reader input(record);
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(payload.size()), "Mixed Latin-1 UTF-8 byte count.");
            check(input.read<std::int32_t>() == static_cast<std::int32_t>(unit_count), "Mixed Latin-1 UTF-8 code-unit count.");
            check(std::ranges::equal(input.take(payload.size()), payload), "Mixed Latin-1 UTF-8 exact payload.");
            input.require_end();
        };
        std::array<byte, header_size + length * 2 + suffix_bytes.size() + 2> storage;
        storage.fill(byte{204});
        const auto window = std::span(storage).subspan(1, header_size + expected.size());
        writer output(window);
        luminpack::write_string(output, profile, text);
        check_record(output.written(), expected, text.size());
        check(storage.front() == byte{204} && storage[1 + window.size()] == byte{204}, "Mixed Latin-1 UTF-8 write bounds.");

        std::array<char16_t, length + 8> alias{};
        std::ranges::copy(text, alias.begin() + 1);
        output = writer(std::as_writable_bytes(std::span(alias)).subspan(1, window.size()));
        luminpack::write_string(output, profile, std::u16string_view(alias.data() + 1, length));
        check_record(output.written(), expected, text.size());

        std::ranges::copy(suffix, characters.begin() + 1 + length);
        const auto with_suffix = std::u16string_view(units.data(), length + suffix.size());
        expected.insert(expected.end(), suffix_bytes.begin(), suffix_bytes.end());
        output = writer(std::span(storage).subspan(1, header_size + expected.size()));
        luminpack::write_string(output, profile, with_suffix);
        check_record(output.written(), expected, with_suffix.size());

        storage.fill(byte{204});
        output = writer(std::span(storage).first(header_size + expected.size() - 1));
        expect_error<std::length_error>([&] { luminpack::write_string(output, profile, with_suffix); }, "Writes a short mixed Latin-1 UTF-8 window.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes a short mixed Latin-1 UTF-8 write.");

        characters.back() = 0xd800;
        output = writer(storage);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(output, profile, with_suffix); }, "Writes an invalid mixed Latin-1 UTF-8 tail.");
        check(output.written().empty() && std::ranges::all_of(storage, [](byte value) { return value == byte{204}; }),
            "Publishes an invalid mixed Latin-1 UTF-8 write.");
    }
}

struct point
{
    std::int32_t x;
    std::optional<std::u16string> note;
    bool operator==(const point&) const = default;
};

struct custom_pair
{
    std::uint32_t first;
    char16_t second;
    bool operator==(const custom_pair&) const = default;
};

custom_pair read_custom(reader& input)
{
    const auto second = input.read<char16_t>();
    return {input.read<std::uint32_t>(), second};
}

void write_custom(writer& output, const custom_pair& value)
{
    output.write(value.second);
    output.write(value.first);
}

void record_and_union_codecs()
{
    using x_member = luminpack::record_member<&point::x, luminpack::scalar_codec<std::int32_t>>;
    using note_member = luminpack::record_member<&point::note, luminpack::string_codec<>>;
    using positional = luminpack::record_codec<point, x_member, note_member>;
    using payload = luminpack::record_payload_codec<point, x_member, note_member>;
    using custom = luminpack::custom_codec<custom_pair, read_custom, write_custom, 6>;
    constexpr auto point_hash = luminpack::union_name_hash("Demo.Point");
    constexpr auto custom_hash = luminpack::union_name_hash("Demo.Custom");
    using tagged = luminpack::union_codec<luminpack::union_case<point_hash, payload>,
        luminpack::union_case<custom_hash, custom>>;

    const point expected{42, u"abc"};
    const custom_pair pair{0xfedcba98u, u'\u03b1'};
    std::array<byte, 64> bytes{};
    writer output(bytes);
    positional::write(output, expected);
    tagged::write(output, tagged::variant_type(expected));
    tagged::write(output, tagged::variant_type(pair));
    tagged::write(output, std::nullopt);
    reader input(output.written());
    check(positional::read(input) == expected, "Read a concrete positional DTO.");
    const auto with_tag = input.unread();
    auto first = tagged::read(input);
    check(first && std::get<point>(*first) == expected, "Read a statically bound Union case.");
    reader tag_only(with_tag);
    check(tag_only.read<std::uint32_t>() == point_hash && tag_only.read<std::int32_t>() == 42,
        "Union payload omits the positional member count.");
    auto second = tagged::read(input);
    check(second && std::get<custom_pair>(*second) == pair, "Dispatch a static custom codec.");
    check(!tagged::read(input), "Read the four-byte null Union marker.");
    input.require_end();
    bytes.front() = byte{1};
    input = reader(output.written());
    expect_error<luminpack::wire_error>([&] { (void)positional::read(input); }, "Accepts a mismatched positional schema.");

    output = writer(bytes);
    output.write<std::uint32_t>(0xffffffffu);
    input = reader(output.written());
    expect_error<luminpack::wire_error>([&] { (void)tagged::read(input); }, "Accepts an unknown registered Union case.");
}

struct version_one
{
    std::int32_t id = -1;
    std::optional<std::u16string> name;
    std::int64_t removed = -1;
};

struct version_two
{
    std::int32_t added = -1;
    std::optional<std::u16string> display;
    std::int32_t id = -1;
};

using luminpack::version_codec;
using luminpack::version_member;
using luminpack::string_profile;
constexpr auto v_id = 0x040bf85f36e8b900ull;
constexpr auto v_name = 0xfe0bfeab0fe07306ull;
constexpr auto v_removed = 0x080bf53970a26e7bull;
constexpr auto v_added = 0x040bf85fb35c3185ull;
using v1 = version_codec<version_one, string_profile::utf16_length,
    version_member<&version_one::id, luminpack::scalar_codec<std::int32_t>, v_id>,
    version_member<&version_one::name, luminpack::string_codec<>, v_name>,
    version_member<&version_one::removed, luminpack::scalar_codec<std::int64_t>, v_removed>>;
using v2 = version_codec<version_two, string_profile::utf16_length,
    version_member<&version_two::added, luminpack::scalar_codec<std::int32_t>, v_added>,
    version_member<&version_two::display, luminpack::string_codec<>, v_name, luminpack::union_name_hash("DisplayName")>,
    version_member<&version_two::id, luminpack::scalar_codec<std::int32_t>, v_id>>;
using version_list = luminpack::list_codec<v1>;

struct incompatible_version
{
    float id;
};

using conflicting = version_codec<incompatible_version, string_profile::utf16_length,
    version_member<&incompatible_version::id, luminpack::scalar_codec<float>, v_id ^ (1ull << 32)>>;

struct version_scopes
{
    version_list::value_type first;
    version_list::value_type second;
};

using scope_codec = version_codec<version_scopes, string_profile::utf16_length,
    version_member<&version_scopes::first, version_list, 0xff753e67ee3d49e1ull>,
    version_member<&version_scopes::second, version_list, 0xff753e67f486b7fdull>>;

void version_contracts(const std::filesystem::path& directory)
{
    const auto complete = fixture(directory, "version-complete.bin");
    const auto evolved = fixture(directory, "version-evolved.bin");
    const auto sequence = fixture(directory, "version-short-collection.bin");
    const auto scopes = fixture(directory, "version-scopes.bin");

    reader input(complete);
    const auto renamed = v2::read(input);
    check(renamed && renamed->id == 13 && renamed->display == u"player" && renamed->added == 0,
        "Map the .NET original Version schema to the evolved DTO.");
    input.require_end();
    input = reader(complete);
    expect_error<luminpack::wire_error>([&] { (void)conflicting::read(input); },
        "Accepts a known Version name with a different type contract.");

    input = reader(evolved);
    const auto older = v1::read(input);
    check(older && older->id == 19 && older->name == u"renamed" && older->removed == 0,
        "Map the .NET evolved Version schema to the original DTO.");
    input.require_end();

    std::array<byte, 512> bytes{};
    writer output(bytes);
    const version_one original{13, u"player", 17};
    const version_one second{29, u"next", 31};
    v1::write(output, original);
    check(std::ranges::equal(output.written(), complete), "Write .NET Version full header and fields.");
    output = writer(bytes);
    v2::write(output, version_two{23, u"renamed", 19});
    check(std::ranges::equal(output.written(), evolved), "Write .NET evolved Version schema.");

    luminpack::version_state state;
    input = reader(sequence);
    const auto items = version_list::read(input, state);
    check(items && items->size() == 2 && (*items)[1] && (*items)[1]->id == 29,
        "Read a Version list with a short second header.");
    input.require_end();
    state.reset();
    output = writer(bytes);
    const version_list::value_type values = std::vector<v1::value_type>{original, second};
    version_list::write(output, values, state);
    check(std::ranges::equal(output.written(), sequence), "Write .NET short-record schema ordinal.");
    check(luminpack::read_binary_document<version_list>(sequence)->size() == 2,
        "Document API shares a registry between Revision-4 and Revision-5 elements.");
    check(std::ranges::equal(luminpack::write_binary_document<version_list>(bytes, values), sequence),
        "Document API writes a shared Version ordinal.");

    output = writer(bytes);
    state.reset();
    scope_codec::write(output, version_scopes{values, values}, state);
    check(std::ranges::equal(output.written(), scopes), "Write .NET nested Version registration scopes.");
    input = reader(scopes);
    state.reset();
    const auto scoped = scope_codec::read(input, state);
    check(scoped && scoped->first && scoped->second && (*scoped->first)[0]->id == 13 &&
        (*scoped->second)[1]->id == 29, "Read .NET nested Version registration scopes.");
    input.require_end();

    constexpr std::array child_ordinal{byte{251}, byte{0x52}, byte{1}};
    input = reader(child_ordinal);
    expect_error<luminpack::wire_error>([&] { (void)v1::read(input, state); },
        "Retains a schema from a closed member scope.");

    auto bad_schema = complete;
    bad_schema[3] ^= byte{1};
    input = reader(bad_schema);
    state.reset();
    expect_error<luminpack::wire_error>([&] { (void)v1::read(input, state); }, "Accepts a wrong Version schema fingerprint.");
    input = reader(std::span<const byte>(complete).first(complete.size() - 1));
    expect_error<luminpack::wire_error>([&] { (void)v1::read(input, state); }, "Accepts a truncated Version body.");
    constexpr std::array first_ordinal{byte{251}, byte{0x52}, byte{0}};
    input = reader(first_ordinal);
    expect_error<luminpack::wire_error>([&] {
        (void)luminpack::read_version_header(input, state, string_profile::utf16_length);
    }, "Keeps a schema from a failed Version body.");

    // Cross the small-table boundary and the single-byte ordinal boundary.
    std::array<byte, 4096> many{};
    output = writer(many);
    for (std::uint32_t index = 0; index < 130; ++index)
    {
        const std::array fields{luminpack::version_descriptor(index, 0, 0)};
        luminpack::write_version_header(output, state, string_profile::utf16_length, fields);
    }
    const std::array last{luminpack::version_descriptor(129, 0, 0)};
    luminpack::write_version_header(output, state, string_profile::utf16_length, last);
    constexpr std::array last_ordinal{byte{251}, byte{0x52}, byte{0x81}, byte{1}};
    check(std::ranges::equal(output.written().last(4), last_ordinal), "Write a canonical two-byte Version ordinal.");
    state.reset();
    input = reader(output.written());
    for (std::uint32_t index = 0; index < 130; ++index)
        check(luminpack::read_version_header(input, state, string_profile::utf16_length)->front() == index,
            "Read a distinct Version descriptor.");
    check(luminpack::read_version_header(input, state, string_profile::utf16_length)->front() == 129,
        "Resolve a canonical two-byte Version ordinal.");
    input.require_end();

    state.reset();
    output = writer(many);
    const auto register_schema = [&](std::uint32_t key)
    {
        const auto start = output.written().size();
        const std::array fields{luminpack::version_descriptor(key, 0, 0)};
        luminpack::write_version_header(output, state, string_profile::utf16_length, fields);
        return output.written().subspan(start);
    };
    for (std::uint32_t key = 0; key < 4; ++key)
        (void)register_schema(key);
    {
        luminpack::version_state::scope outer(state);
        for (std::uint32_t key = 4; key < 12; ++key)
            (void)register_schema(key);
        {
            luminpack::version_state::scope inner(state);
            for (std::uint32_t key = 12; key < 40; ++key)
                (void)register_schema(key);
        }
        constexpr std::array retained{byte{251}, byte{0x52}, byte{4}};
        check(std::ranges::equal(register_schema(4), retained), "Retain a probe chain across Version scope growth.");
        check(register_schema(12)[1] == byte{0x42}, "Discard the indexed Version schema in the inner scope.");
    }
    check(register_schema(4)[1] == byte{0x42}, "Return to a sequential table after the outer Version scope.");
    for (std::uint32_t key = 5; key < 11; ++key)
        (void)register_schema(key);
    constexpr std::array reindexed{byte{251}, byte{0x52}, byte{7}};
    check(std::ranges::equal(register_schema(7), reindexed), "Rebuild the Version index from the retained prefix.");
}

void object_and_input_checks()
{
    // The same direct calls that a generated positional codec will use.
    std::array<byte, 16> bytes{};
    writer output(bytes);
    luminpack::write_object_header(output, 2);
    output.write<std::int32_t>(42);
    luminpack::write_utf16(output, u"wire");
    luminpack::write_object_header(output, std::nullopt);
    constexpr std::array expected{
        byte{2}, byte{42}, byte{0}, byte{0}, byte{0}, byte{9}, byte{0}, byte{0}, byte{0},
        byte{119}, byte{105}, byte{114}, byte{101}, byte{255}
    };
    check(std::ranges::equal(output.written(), expected), "Write positional object.");
    reader input(expected);
    check(luminpack::read_object_header(input) == 2 && input.read<std::int32_t>() == 42, "Read positional object.");
    std::array<char16_t, 4> text{};
    check(luminpack::read_utf16_into(input, text) == u"wire", "Read positional object member.");
    expect_error<luminpack::wire_error>([&] { input.require_end(); }, "Accepts trailing input.");
    check(!luminpack::read_object_header(input), "Read null positional object.");
    input.require_end();

    const std::array invalid{byte{254}, byte{255}, byte{255}, byte{255}};
    input = reader(invalid);
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_object_header(input); }, "Accepts invalid object marker.");
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_utf16_into(input, text); }, "Accepts invalid string length.");
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_array_into<std::int32_t>(input, {}); },
        "Accepts invalid array length.");
    check(input.remaining() == invalid.size(), "Consumes input after a framing error.");

    const std::array short_text{byte{9}, byte{0}, byte{0}, byte{0}, byte{102}};
    input = reader(short_text);
    expect_error<luminpack::wire_error>([&] { (void)luminpack::read_utf16_into(input, text); }, "Accepts truncated Latin-1 input.");

    output = writer(bytes);
    luminpack::write_array<std::int32_t>(output, std::nullopt);
    luminpack::write_array<std::int32_t>(output, std::span<const std::int32_t>{});
    luminpack::write_tagged_array<tagged32, layout<tagged32>>(output, std::nullopt);
    luminpack::write_tagged_array<tagged32, layout<tagged32>>(output, std::span<const tagged32>{});
    input = reader(output.written());
    check(!luminpack::read_array_into<std::int32_t>(input, {}), "Read null raw array.");
    const auto empty = luminpack::read_array_into<std::int32_t>(input, {});
    check(empty && empty->empty(), "Read empty raw array.");
    check(!luminpack::read_tagged_array_into<tagged32, layout<tagged32>>(input, {}), "Read null Tagged array.");
    const auto tagged_empty = luminpack::read_tagged_array_into<tagged32, layout<tagged32>>(input, {});
    check(tagged_empty && tagged_empty->empty(), "Read empty Tagged array without layout tag.");
    input.require_end();
}
}

int main(int argc, char** argv)
{
    try
    {
        check(argc == 2, "Expected the .NET wire fixture directory.");
        const std::filesystem::path directory(argv[1]);
        fixed_fixtures(directory);
        text_fixtures(directory);
        text_vector_boundaries();
        short_utf16_records();
        utf16_length_collection_reads();
        tagged_fixtures(directory);
        object_and_input_checks();
        collection_contracts(directory);
        string_profiles(directory);
        short_utf8_records(directory);
        long_utf8_ascii_runs();
        homogeneous_two_byte_utf8_runs();
        homogeneous_bmp_utf8_runs();
        mixed_latin1_utf8_masks();
        record_and_union_codecs();
        version_contracts(directory);
        std::cout << "LuminPack C++ wire checks passed.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
