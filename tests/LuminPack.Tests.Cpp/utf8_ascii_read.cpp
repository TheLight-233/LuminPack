#include <luminpack/luminpack.h>

#include <algorithm>
#include <array>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <vector>

namespace
{
constexpr auto profile = luminpack::string_profile::utf8_length;
constexpr auto header_size = 2 * sizeof(std::int32_t);

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<class Error = luminpack::wire_error>
void expect_error(auto&& operation)
{
    try
    {
        operation();
    }
    catch (const Error&)
    {
        return;
    }
    throw std::runtime_error("Invalid UTF-8 Length operation succeeded.");
}

void managed_records(const std::filesystem::path& path, std::size_t first, std::size_t last)
{
    std::ifstream stream(path, std::ios::binary);
    require(stream.is_open(), "Cannot open .NET ASCII fixture.");
    const std::vector<char> bytes((std::istreambuf_iterator<char>(stream)), {});
    luminpack::reader reader(std::as_bytes(std::span(bytes)));
    using codec = luminpack::list_codec<luminpack::string_codec<profile>>;
    const auto result = codec::read(reader);
    require(result && result->size() == last - first + 1, "Invalid managed fixture count.");
    for (std::size_t length = first; length <= last; ++length)
        require((*result)[length - first] == std::u16string(length, u'x'), "Incorrect managed ASCII fixture.");
    reader.require_end();
}

void check_ascii_records()
{
    constexpr auto canary = u'\u007f';
    for (const auto length : {1u, 2u, 3u, 4u, 7u, 8u, 15u, 16u, 17u, 31u, 32u, 33u, 63u, 64u, 65u, 96u, 127u, 128u, 129u, 130u, 255u, 256u, 257u, 513u})
    {
        std::u16string expected;
        for (std::size_t index = 0; index < length; ++index)
            expected.push_back(static_cast<char16_t>((index * 29 + length * 11) & 0x7f));

        std::vector<std::byte> storage(header_size + length + 2, std::byte{0xcc});
        const auto record = std::span(storage).subspan(1, header_size + length);
        luminpack::writer writer(record);
        luminpack::write_string(writer, profile, expected);
        require(writer.written().size() == record.size(), "Incorrect UTF-8 Length record size.");

        luminpack::reader owning(record);
        require(luminpack::read_string(owning, profile) == expected, "Incorrect owning ASCII result.");
        owning.require_end();

        std::vector<char16_t> output(length + 2, canary);
        luminpack::reader buffer(record);
        require(luminpack::read_string_into(buffer, profile, std::span(output).subspan(1, length)) == expected,
            "Incorrect ASCII result in caller storage.");
        buffer.require_end();
        require(output.front() == canary && output.back() == canary, "ASCII read wrote outside its destination.");

        std::vector<char16_t> overlap(length + 16);
        const auto aliased = std::as_writable_bytes(std::span(overlap)).subspan(1, record.size());
        std::ranges::copy(record, aliased.begin());
        luminpack::reader overlapping(aliased);
        require(luminpack::read_string_into(overlapping, profile, std::span(overlap).subspan(3, length)) == expected,
            "Overlapping ASCII read overwrote input before using it.");
        overlapping.require_end();

        luminpack::reader short_output(record);
        std::ranges::fill(output, canary);
        expect_error<std::length_error>([&] {
            (void)luminpack::read_string_into(short_output, profile, std::span(output).first(length - 1));
        });
        require(short_output.remaining() == record.size(), "Short output advanced the input cursor.");
        require(std::ranges::all_of(output, [](char16_t value) { return value == canary; }),
            "Short output changed caller storage.");

        luminpack::reader limited(record, {1000, length - 1});
        expect_error([&] { (void)luminpack::read_string(limited, profile); });
        require(limited.remaining() == record.size(), "Exceeded string quota advanced the input cursor.");

        const auto truncated = record.first(record.size() - 1);
        luminpack::reader short_input(truncated);
        expect_error([&] { (void)luminpack::read_string(short_input, profile); });
        require(short_input.remaining() == truncated.size(), "Truncation advanced the input cursor.");

        auto payload = record.subspan(header_size);
        for (std::size_t index = 0; index < length; ++index)
        {
            const auto saved = payload[index];
            payload[index] = std::byte{0xff};
            luminpack::reader incorrect(record);
            expect_error([&] { (void)luminpack::read_string(incorrect, profile); });
            require(incorrect.remaining() == record.size(), "Malformed UTF-8 advanced the owning reader.");
            luminpack::reader incorrect_into(record);
            expect_error([&] { (void)luminpack::read_string_into(incorrect_into, profile, output); });
            require(incorrect_into.remaining() == record.size(), "Malformed UTF-8 advanced the borrowed reader.");
            require(std::ranges::all_of(output, [](char16_t value) { return value == canary; }),
                "Malformed UTF-8 changed caller storage.");
            payload[index] = saved;
        }
        require(storage.front() == std::byte{0xcc} && storage.back() == std::byte{0xcc}, "Unexpected record mutation.");
    }
}

void check_variable_ascii_groups()
{
    constexpr auto variable_profile = luminpack::string_profile::utf8_varint;
    constexpr auto lengths = std::array{31u, 32u, 33u, 63u, 64u, 65u, 127u, 128u, 129u, 259u};
    alignas(32) std::array<std::byte, 32 + lengths.back() + 4> storage;
    for (const auto length : lengths)
    {
        const std::u16string expected(length, u'x');
        for (std::size_t offset = 0; offset < 32; ++offset)
        {
            storage.fill(std::byte{0xff});
            luminpack::writer output(std::span<std::byte>(storage).subspan(offset));
            luminpack::write_string(output, variable_profile, expected);
            const auto record = output.written();
            luminpack::reader input(record);
            require(luminpack::read_string(input, variable_profile) == expected, "Incorrect variable-length ASCII result.");
            input.require_end();

            const auto payload_offset = offset + record.size() - length;
            for (const auto index : {0u, length / 2, length - 1})
            {
                auto& byte = storage[payload_offset + index];
                byte = std::byte{0xff};
                input = luminpack::reader(record);
                expect_error([&] { (void)luminpack::read_string(input, variable_profile); });
                require(input.remaining() == record.size(), "Malformed variable-length UTF-8 advanced the reader.");
                byte = std::byte{'x'};
            }
        }
    }
}

void check_asymmetric_width_writes()
{
    constexpr auto canary = std::byte{0xcc};
    constexpr std::size_t length = 259;
    std::u16string text(length, u'a');

    for (const auto index : {1u, 7u, 8u, 15u, 16u, 31u, 32u, 63u, 64u, 65u, 127u, 128u, 256u})
    {
        text[index] = u'\u00ff';
        text[index + 1] = u'\u0800';

        std::vector<std::byte> expected(header_size + length + 3, std::byte{'a'});
        luminpack::writer header(expected);
        header.write(static_cast<std::int32_t>(length + 3));
        header.write(static_cast<std::int32_t>(length));
        const std::array multibyte{std::byte{0xc3}, std::byte{0xbf}, std::byte{0xe0}, std::byte{0xa0}, std::byte{0x80}};
        std::ranges::copy(multibyte, expected.begin() + header_size + index);

        std::vector<std::byte> storage(expected.size() + 2, canary);
        const auto target = std::span(storage).subspan(1, expected.size());
        luminpack::writer writer(target);
        luminpack::write_string(writer, profile, text);
        require(std::ranges::equal(writer.written(), expected), "Mixed UTF-8 write changed the wire bytes.");
        require(storage.front() == canary && storage.back() == canary, "UTF-8 write exceeded the checked destination.");

        auto aliased = text;
        auto overlapping_target = std::as_writable_bytes(std::span(aliased)).subspan(3, target.size());
        luminpack::writer overlapping(overlapping_target);
        luminpack::write_string(overlapping, profile, aliased);
        require(std::ranges::equal(overlapping.written(), expected), "Overlapping UTF-8 write changed the wire bytes.");

        std::ranges::fill(storage, canary);
        luminpack::writer short_output(target.first(target.size() - 1));
        expect_error<std::length_error>([&] { luminpack::write_string(short_output, profile, text); });
        require(short_output.written().empty(), "Short UTF-8 write published the cursor.");
        require(std::ranges::all_of(storage, [](std::byte value) { return value == canary; }),
            "Short UTF-8 write changed the destination.");

        text[index] = text[index + 1] = u'a';
        text[index] = static_cast<char16_t>(0xd800);
        luminpack::writer malformed(target);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(malformed, profile, text); });
        require(malformed.written().empty(), "Malformed UTF-8 write published the cursor.");
        require(std::ranges::all_of(storage, [](std::byte value) { return value == canary; }),
            "Malformed UTF-8 write changed the destination.");
        text[index] = u'a';
    }
}

void check_surrogate_width_writes()
{
    constexpr auto canary = std::byte{0xcc};
    constexpr std::size_t length = 81;
    constexpr std::size_t payload_size = length + 3;
    std::u16string text(length, u'a');
    text.front() = u'\u00e9';

    for (std::size_t index = 1; index + 1 < length; ++index)
    {
        text[index] = static_cast<char16_t>(0xd83d);
        text[index + 1] = static_cast<char16_t>(0xde42);
        std::vector<std::byte> expected(header_size + payload_size, std::byte{'a'});
        luminpack::writer header(expected);
        header.write(static_cast<std::int32_t>(payload_size));
        header.write(static_cast<std::int32_t>(length));
        expected[header_size] = std::byte{0xc3};
        expected[header_size + 1] = std::byte{0xa9};
        const std::array supplementary{std::byte{0xf0}, std::byte{0x9f}, std::byte{0x99}, std::byte{0x82}};
        std::ranges::copy(supplementary, expected.begin() + header_size + index + 1);

        std::vector<std::byte> storage(expected.size() + 2, canary);
        const auto target = std::span(storage).subspan(1, expected.size());
        luminpack::writer writer(target);
        luminpack::write_string(writer, profile, text);
        require(std::ranges::equal(writer.written(), expected), "UTF-8 surrogate pair changed the wire bytes.");
        require(storage.front() == canary && storage.back() == canary, "UTF-8 surrogate pair exceeded the output window.");

        std::ranges::fill(storage, canary);
        text[index] = u'a';
        luminpack::writer invalid(target);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(invalid, profile, text); });
        require(invalid.written().empty(), "Standalone low surrogate published the writer cursor.");
        require(std::ranges::all_of(storage, [](std::byte value) { return value == canary; }),
            "Standalone low surrogate changed the output window.");
        text[index + 1] = u'a';
    }
}

void check_heterogeneous_utf8_tail()
{
    constexpr auto canary = std::byte{0xcc};
    constexpr auto chunk = std::u16string_view(u"Long ASCII runabcdefgh\u00e9\u07ff\u4e2d\u6587\U0001f642tail");
    constexpr auto encoded = std::string_view("Long ASCII runabcdefgh\xc3\xa9\xdf\xbf\xe4\xb8\xad\xe6\x96\x87\xf0\x9f\x99\x82tail");
    for (const auto length : {63u, 64u, 65u, 127u, 128u, 129u})
    {
        std::u16string text(64, u'\u4e2d');
        std::string payload;
        for (int index = 0; index < 64; ++index)
            payload.append("\xe4\xb8\xad");
        while (64 + length - text.size() >= chunk.size())
        {
            text.append(chunk);
            payload.append(encoded);
        }
        payload.append(64 + length - text.size(), 'x');
        text.resize(64 + length, u'x');

        std::vector<std::byte> expected(header_size + payload.size());
        luminpack::writer expected_writer(expected);
        expected_writer.write(static_cast<std::int32_t>(payload.size()));
        expected_writer.write(static_cast<std::int32_t>(text.size()));
        std::memcpy(expected.data() + header_size, payload.data(), payload.size());

        std::vector<std::byte> storage(expected.size() + 2, canary);
        const auto target = std::span(storage).subspan(1, expected.size());
        luminpack::writer writer(target);
        luminpack::write_string(writer, profile, text);
        require(std::ranges::equal(writer.written(), expected), "Heterogeneous UTF-8 tail changed the wire bytes.");
        require(storage.front() == canary && storage.back() == canary, "UTF-8 tail exceeded the output window.");

        auto aliased = text;
        aliased.resize(text.size() + expected.size());
        const auto overlapping = std::as_writable_bytes(std::span(aliased)).subspan(3, expected.size());
        luminpack::writer overlap_writer(overlapping);
        luminpack::write_string(overlap_writer, profile, std::u16string_view(aliased.data(), text.size()));
        require(std::ranges::equal(overlap_writer.written(), expected), "Overlapping UTF-8 tail changed the wire bytes.");

        std::ranges::fill(storage, canary);
        luminpack::writer short_writer(target.first(target.size() - 1));
        expect_error<std::length_error>([&] { luminpack::write_string(short_writer, profile, text); });
        require(short_writer.written().empty(), "Short UTF-8 tail published the cursor.");

        text.back() = static_cast<char16_t>(0xd800);
        luminpack::writer malformed(target);
        expect_error<std::invalid_argument>([&] { luminpack::write_string(malformed, profile, text); });
        require(malformed.written().empty(), "Invalid UTF-8 tail published the cursor.");
        require(std::ranges::all_of(storage, [=](std::byte value) { return value == canary; }),
            "Failed UTF-8 tail changed the destination.");
    }
}
}

int main(int argc, char** argv)
{
    if (argc != 2)
        return 2;
    try
    {
        managed_records(std::filesystem::path(argv[1]) / "short-ascii.bin", 4, 16);
        managed_records(std::filesystem::path(argv[1]) / "medium-ascii.bin", 17, 32);
        check_ascii_records();
        check_variable_ascii_groups();
        check_asymmetric_width_writes();
        check_surrogate_width_writes();
        check_heterogeneous_utf8_tail();
        std::cout << "LuminPack C++ UTF-8 Length ASCII reads passed.\n";
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
