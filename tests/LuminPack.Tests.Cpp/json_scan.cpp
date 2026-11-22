#include <luminpack/luminpack.h>

#include "json_scan.h"

#include <algorithm>
#include <array>
#include <initializer_list>
#include <iostream>
#include <stdexcept>
#include <vector>

namespace
{
using luminpack::detail::json_literal_prefix;

void require(bool passed, const char* error)
{
    if (!passed)
        throw std::runtime_error(error);
}

template<class Unit>
void scan_lanes(std::initializer_list<Unit> specials)
{
    constexpr auto width = sizeof(Unit);
    constexpr bool utf16 = width == sizeof(char16_t);
    constexpr auto plain = width == 1 ? Unit{0x41} : static_cast<Unit>(0x805c);
    alignas(64) std::array<std::byte, 1 + 130 * width> storage{};
    auto unaligned = std::span(storage).subspan(1);
    for (const auto length : {0u, 1u, 7u, 8u, 15u, 16u, 17u, 31u, 32u, 33u, 63u, 64u, 65u, 129u, 130u})
    {
        const auto source = unaligned.first(length * width);
        for (std::size_t index = 0; index < length; ++index)
            std::memcpy(source.data() + index * width, &plain, width);
        require(json_literal_prefix<utf16>(source) == source.size(), "Finds a special unit in a plain window.");

        for (const auto special : specials)
            for (std::size_t index = 0; index < length; ++index)
            {
                const auto target = source.data() + index * width;
                std::memcpy(target, &special, width);
                require(json_literal_prefix<utf16>(source) == index * width, "Finds the wrong first special unit.");
                std::memcpy(target, &plain, width);
            }
    }
}

template<luminpack::json_encoding Encoding>
void text_round_trip(const std::u16string& text)
{
    std::vector<std::byte> storage(1 + text.size() * 12 + 16, std::byte{0xcc});
    auto output = std::span(storage).subspan(1);
    luminpack::basic_json_writer<Encoding> writer(output);
    writer.write_string(text);
    const auto written = writer.written();
    luminpack::basic_json_reader<Encoding> reader(written);
    require(reader.read_string() == text, "String round trip changed a literal or an escape.");
    reader.require_end();
    require(storage.front() == std::byte{0xcc} && output[written.size()] == std::byte{0xcc},
        "String write changed its canary.");
}

void validated_text()
{
    constexpr auto utf8 = luminpack::json_encoding::utf8;
    constexpr auto utf16 = luminpack::json_encoding::utf16;
    const std::u16string plain(137, u'X');
    text_round_trip<utf8>(plain);
    text_round_trip<utf16>(plain);
    const auto mixed = plain + u"\\\"\n\u80a0\u0222\U0001f600";
    text_round_trip<utf8>(mixed);
    text_round_trip<utf16>(mixed);
    text_round_trip<utf16>(std::u16string(129, u'\u805c'));

    const std::string truncated = "\"" + std::string(137, 'X');
    for (const auto* ending : {"", "\n\"", "\\z\"", "\xc3\""})
    {
        const auto bytes = truncated + ending;
        luminpack::basic_json_reader<utf8> reader(std::as_bytes(std::span(bytes)));
        try
        {
            (void)reader.read_string();
            throw std::runtime_error("Accepts an incomplete, unescaped, or invalid UTF-8 string.");
        }
        catch (const luminpack::wire_error&)
        {
        }
    }

    alignas(char16_t) std::array<char16_t, 160> overlap{};
    std::ranges::copy(mixed, overlap.begin());
    auto target = std::as_writable_bytes(std::span(overlap)).subspan(1);
    luminpack::basic_json_writer<utf16> writer(target);
    writer.write_string(std::u16string_view(overlap.data(), mixed.size()));
    luminpack::basic_json_reader<utf16> reader(writer.written());
    require(reader.read_string() == mixed, "Overlapping UTF-16 write overwrote its input.");
    reader.require_end();
}
}

int main()
{
    try
    {
        scan_lanes<std::uint8_t>({0, 0x1f, '"', '\\', 0x80, 0xff});
        scan_lanes<std::uint16_t>({0, 0x1f, '"', '\\'});
        validated_text();
        std::cout << "LuminPack C++ JSON literal scan checks passed.\n";
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
