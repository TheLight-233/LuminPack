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
    for (std::size_t length = 1; length <= 257; ++length)
    {
        std::u16string current(length, u'\0');
        for (std::size_t index = 0; index < length; ++index)
            current[index] = static_cast<char16_t>((index * 17 + 111) & 255);

        // Decode into exactly enough storage and include a canary after each destination.
        std::vector<byte> bytes(4 + length * 2 + 1, byte{204});
        std::vector<char16_t> characters(length + 1, char16_t{0xffff});
        const auto storage = std::span<byte>(bytes);
        const auto destination = std::span<char16_t>(characters).first(length);
        writer output(storage.first(4 + length));
        luminpack::write_utf16(output, current);
        check(output.written().size() == 4 + length && bytes[4 + length] == byte{204}, "Latin-1 writer bounds.");
        reader input(output.written());
        check(input.read<std::int32_t>() == static_cast<std::int32_t>(length * 2 + 1), "Latin-1 record marker.");
        const auto payload = input.take(length);
        for (std::size_t index = 0; index < length; ++index)
            check(payload[index] == static_cast<byte>(current[index]), "Latin-1 vector lane order.");

        output = writer(storage.first(4 + length * 2));
        luminpack::write_utf16(output, current);
        input = reader(output.written());
        check(output.written().size() == 4 + length, "Latin-1 writer selects compact form.");
        check(luminpack::read_utf16_into(input, destination) == current, "Latin-1 widening.");
        check(characters.back() == 0xffff, "Latin-1 reader bounds.");
        input.require_end();

        for (std::size_t index = 0; index < length; ++index)
        {
            const auto saved = current[index];
            current[index] = 0xd800;
            output = writer(storage.first(4 + length * 2));
            luminpack::write_utf16(output, current);
            check(output.written().size() == 4 + length * 2 && bytes.back() == byte{204}, "Raw UTF-16 writer bounds.");
            input = reader(output.written());
            check(luminpack::read_utf16_into(input, destination) == current, "Mixed text and unpaired surrogate preservation.");
            input.require_end();
            current[index] = saved;
        }
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
        tagged_fixtures(directory);
        object_and_input_checks();
        std::cout << "LuminPack C++ wire checks passed.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
