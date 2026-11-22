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
    for (const auto length : {1u, 2u, 3u, 4u, 7u, 8u, 15u, 16u, 17u, 31u, 32u, 33u, 63u, 64u, 65u, 96u, 129u})
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

        std::array<char16_t, 144> overlap{};
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
        std::cout << "LuminPack C++ UTF-8 Length ASCII reads passed.\n";
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
