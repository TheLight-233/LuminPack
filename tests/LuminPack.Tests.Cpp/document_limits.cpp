#include <luminpack/luminpack.h>

#include <array>
#include <cstddef>
#include <cstdint>
#include <span>
#include <stdexcept>
#include <string_view>

namespace
{
using namespace luminpack;

void require(bool condition)
{
    if (!condition)
        throw std::runtime_error("Unexpected LuminPack document limit behavior.");
}

template<class Action>
void expect_wire_error(Action action)
{
    try
    {
        action();
    }
    catch (const wire_error&)
    {
        return;
    }
    throw std::runtime_error("Expected LuminPack to reject the document.");
}

int codec_reads = 0;

struct checked_scalar : scalar_codec<std::int32_t>
{
    static std::int32_t read(reader& input)
    {
        ++codec_reads;
        return scalar_codec<std::int32_t>::read(input);
    }
};

struct checked_json_scalar : json_codec<scalar_codec<std::int32_t>>
{
    static std::int32_t read(auto& input)
    {
        ++codec_reads;
        return json_codec<scalar_codec<std::int32_t>>::read(input);
    }
};

template<json_encoding Encoding, class Char>
void verify_json_limit(std::basic_string_view<Char> document)
{
    const auto input = std::as_bytes(std::span{document});
    require(read_json_document<checked_json_scalar, Encoding>(input, {1024, input.size()}) == 42);
    const auto previous_reads = codec_reads;
    expect_wire_error([&] {
        (void)read_json_document<checked_json_scalar, Encoding>(input, {1024, input.size() - 1});
    });
    require(codec_reads == previous_reads);
    require(read_json_document<checked_json_scalar, Encoding>(input, {1024}) == 42);

    basic_json_reader<Encoding> cursor(input, {1024, 0});
    require(checked_json_scalar::read(cursor) == 42);
    cursor.require_end();
}
}

int main()
{
    constexpr std::array<std::int32_t, 2> values{42, 43};
    const auto input = std::as_bytes(std::span{values});
    const auto document = input.first(sizeof(std::int32_t));
    static_assert(noexcept(reader(input, reader_limits{})));
    require(read_binary_document<checked_scalar>(document, {10, 10, document.size()}) == 42);
    const auto previous_reads = codec_reads;
    expect_wire_error([&] { (void)read_binary_document<checked_scalar>(input, {10, 10, document.size()}); });
    expect_wire_error([&] { (void)read_binary_document<checked_scalar>(document, {10, 10, 0}); });
    require(codec_reads == previous_reads);
    require(read_binary_document<checked_scalar>(document, {10, 10}) == 42);

    reader cursor(input, {10, 10, 0});
    require(checked_scalar::read(cursor) == 42 && checked_scalar::read(cursor) == 43);
    cursor.require_end();

    // Trailing space is valid JSON and is part of the encoded document length.
    verify_json_limit<json_encoding::utf8>(std::string_view("42 "));
    verify_json_limit<json_encoding::utf16>(std::u16string_view(u"42 "));
}
