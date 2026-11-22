#include <luminpack/luminpack.h>

#include "latin1.h"
#include "unicode.h"

namespace luminpack
{
namespace
{
constexpr auto end_of_input = std::uint32_t{0x110000};

bool digit(std::uint32_t value) noexcept { return value >= '0' && value <= '9'; }
bool white(std::uint32_t value) noexcept { return value == ' ' || value == '\n' || value == '\r' || value == '\t'; }
bool escaped(std::uint32_t value) noexcept { return value < 0x20 || value == '"' || value == '\\'; }

char escape_letter(char16_t value) noexcept
{
    switch (value)
    {
    case u'"': return '"';
    case u'\\': return '\\';
    case u'\n': return 'n';
    case u'\r': return 'r';
    case u'\t': return 't';
    default: return 'u';
    }
}

int hex_value(std::uint32_t value) noexcept
{
    if (value >= '0' && value <= '9')
        return static_cast<int>(value - '0');
    if (value >= 'a' && value <= 'f')
        return static_cast<int>(value - 'a' + 10);
    if (value >= 'A' && value <= 'F')
        return static_cast<int>(value - 'A' + 10);
    return -1;
}

template<json_encoding Encoding>
std::uint32_t load_json_unit(const std::byte* source) noexcept
{
    if constexpr (Encoding == json_encoding::utf8)
        return std::to_integer<unsigned char>(*source);
    else
    {
        char16_t value;
        std::memcpy(&value, source, sizeof(value));
        return value;
    }
}

template<json_encoding Encoding>
void store_json_unit(std::byte*& target, std::uint32_t value) noexcept
{
    if constexpr (Encoding == json_encoding::utf8)
        *target = static_cast<std::byte>(value);
    else
    {
        const auto unit = static_cast<char16_t>(value);
        std::memcpy(target, &unit, sizeof(unit));
    }
    target += sizeof(json_code_unit<Encoding>);
}

void store_utf8_scalar(std::byte*& target, std::uint32_t value) noexcept
{
    if (value < 0x80)
        *target++ = static_cast<std::byte>(value);
    else if (value < 0x800)
    {
        *target++ = static_cast<std::byte>(0xc0 | (value >> 6));
        *target++ = static_cast<std::byte>(0x80 | (value & 0x3f));
    }
    else
    {
        if (value < 0x10000)
            *target++ = static_cast<std::byte>(0xe0 | (value >> 12));
        else
        {
            *target++ = static_cast<std::byte>(0xf0 | (value >> 18));
            *target++ = static_cast<std::byte>(0x80 | ((value >> 12) & 0x3f));
        }
        *target++ = static_cast<std::byte>(0x80 | ((value >> 6) & 0x3f));
        *target++ = static_cast<std::byte>(0x80 | (value & 0x3f));
    }
}

template<json_encoding Encoding>
void store_escape(std::byte*& target, char16_t value) noexcept
{
    store_json_unit<Encoding>(target, '\\');
    const auto letter = escape_letter(value);
    store_json_unit<Encoding>(target, letter);
    if (letter == 'u')
    {
        for (int shift = 12; shift >= 0; shift -= 4)
        {
            constexpr std::string_view hex = "0123456789abcdef";
            store_json_unit<Encoding>(target, hex[(value >> shift) & 0xf]);
        }
    }
}
}

template<json_encoding Encoding>
bool json_property_name<Encoding>::matches(std::u16string_view expected) const noexcept
{
    if (decoded_)
        return *decoded_ == expected;
    using unit = json_code_unit<Encoding>;
    if (raw_.size() / sizeof(unit) != expected.size())
        return false;
    if constexpr (Encoding == json_encoding::utf16)
        return raw_.empty() || std::memcmp(raw_.data(), expected.data(), raw_.size()) == 0;
    else
    {
        for (std::size_t index = 0; index < expected.size(); ++index)
        {
            if (std::to_integer<unsigned char>(raw_[index]) != expected[index])
                return false;
        }
        return true;
    }
}

std::size_t detail::json_utf8_encoded_length(std::u16string_view name) noexcept
{
    std::size_t length = 0;
    for (std::size_t index = 0; index < name.size();)
    {
        const auto code = detail::read_utf16_scalar(name, index, true);
        length += code < 0x80 ? 1 : code < 0x800 ? 2 : code < 0x10000 ? 3 : 4;
    }
    return length;
}

template<json_encoding Encoding>
basic_json_reader<Encoding>::basic_json_reader(std::span<const std::byte> input, json_read_options options)
    : input_(input), max_depth_(options.max_depth)
{
    if (input.size() % sizeof(unit) != 0)
        throw wire_error("Incomplete UTF-16 JSON code unit.");
}

template<json_encoding Encoding>
std::uint32_t basic_json_reader<Encoding>::at(std::size_t offset) const noexcept
{
    if (offset == input_.size())
        return end_of_input;
    return load_json_unit<Encoding>(input_.data() + offset);
}

template<json_encoding Encoding>
std::uint32_t basic_json_reader<Encoding>::next_unit()
{
    if (position_ == input_.size())
        throw wire_error("Truncated LuminPack JSON token.");
    const auto value = at(position_);
    position_ += sizeof(unit);
    return value;
}

template<json_encoding Encoding>
void basic_json_reader<Encoding>::skip_whitespace() noexcept
{
    while (white(at(position_)))
        position_ += sizeof(unit);
}

template<json_encoding Encoding>
char basic_json_reader<Encoding>::peek()
{
    skip_whitespace();
    const auto value = at(position_);
    return value < 0x80 ? static_cast<char>(value) : '\0';
}

template<json_encoding Encoding>
bool basic_json_reader<Encoding>::consume(char token)
{
    skip_whitespace();
    if (at(position_) != static_cast<unsigned char>(token))
        return false;
    position_ += sizeof(unit);
    return true;
}

template<json_encoding Encoding>
void basic_json_reader<Encoding>::expect(char token)
{
    if (!consume(token))
        throw wire_error("Unexpected delimiter in LuminPack JSON.");
}

template<json_encoding Encoding>
bool basic_json_reader<Encoding>::consume_literal(std::string_view literal)
{
    skip_whitespace();
    if (at(position_) != static_cast<unsigned char>(literal.front()))
        return false;
    auto offset = position_;
    for (const auto character : literal)
    {
        if (at(offset) != static_cast<unsigned char>(character))
            throw wire_error("Invalid literal in LuminPack JSON.");
        offset += sizeof(unit);
    }
    position_ = offset;
    return true;
}

template<json_encoding Encoding>
bool basic_json_reader<Encoding>::consume_null()
{
    return consume_literal("null");
}

template<json_encoding Encoding>
bool basic_json_reader<Encoding>::read_bool()
{
    if (consume_literal("true"))
        return true;
    if (consume_literal("false"))
        return false;
    throw wire_error("Expected a Boolean in LuminPack JSON.");
}

template<json_encoding Encoding>
std::span<const std::byte> basic_json_reader<Encoding>::number_token()
{
    skip_whitespace();
    const auto start = position_;
    auto offset = start;
    if (at(offset) == '-')
        offset += sizeof(unit);
    if (!digit(at(offset)))
        throw wire_error("Expected a number in LuminPack JSON.");
    if (at(offset) == '0')
        offset += sizeof(unit);
    else
    {
        do
            offset += sizeof(unit);
        while (digit(at(offset)));
    }
    if (at(offset) == '.')
    {
        offset += sizeof(unit);
        if (!digit(at(offset)))
            throw wire_error("Invalid fraction in LuminPack JSON.");
        do
            offset += sizeof(unit);
        while (digit(at(offset)));
    }
    if (at(offset) == 'e' || at(offset) == 'E')
    {
        offset += sizeof(unit);
        if (at(offset) == '+' || at(offset) == '-')
            offset += sizeof(unit);
        if (!digit(at(offset)))
            throw wire_error("Invalid exponent in LuminPack JSON.");
        do
            offset += sizeof(unit);
        while (digit(at(offset)));
    }
    const auto end = at(offset);
    if (end != end_of_input && end != ',' && end != ']' && end != '}' && !white(end))
        throw wire_error("Invalid numeric token boundary in LuminPack JSON.");
    position_ = offset;
    return input_.subspan(start, offset - start);
}

template<json_encoding Encoding>
char16_t basic_json_reader<Encoding>::read_hex_escape()
{
    unsigned value = 0;
    for (unsigned index = 0; index < 4; ++index)
    {
        const auto digit_value = hex_value(next_unit());
        if (digit_value < 0)
            throw wire_error("Invalid Unicode escape in LuminPack JSON.");
        value = (value << 4) | static_cast<unsigned>(digit_value);
    }
    return static_cast<char16_t>(value);
}

template<json_encoding Encoding>
template<class Put>
void basic_json_reader<Encoding>::read_text(Put&& put)
{
    expect('"');
    for (;;)
    {
        auto value = next_unit();
        if (value == '"')
            return;
        if (value < 0x20)
            throw wire_error("Unescaped control code unit in LuminPack JSON.");
        if (value == '\\')
        {
            switch (next_unit())
            {
            case '"': value = '"'; break;
            case '\\': value = '\\'; break;
            case '/': value = '/'; break;
            case 'b': value = '\b'; break;
            case 'f': value = '\f'; break;
            case 'n': value = '\n'; break;
            case 'r': value = '\r'; break;
            case 't': value = '\t'; break;
            case 'u':
            {
                const auto escaped_value = read_hex_escape();
                if (detail::high_surrogate(escaped_value))
                {
                    if (next_unit() != '\\' || next_unit() != 'u')
                        throw wire_error("Missing low-surrogate escape in LuminPack JSON.");
                    const auto low = read_hex_escape();
                    if (!detail::low_surrogate(low))
                        throw wire_error("Invalid low-surrogate escape in LuminPack JSON.");
                    put(escaped_value);
                    put(low);
                    continue;
                }
                if (detail::low_surrogate(escaped_value))
                    throw wire_error("Unexpected low-surrogate escape in LuminPack JSON.");
                value = escaped_value;
                break;
            }
            default:
                throw wire_error("Invalid escape in LuminPack JSON.");
            }
        }
        else if constexpr (Encoding == json_encoding::utf8)
        {
            if (value >= 0x80)
            {
                --position_;
                value = detail::read_utf8_scalar(input_, position_, false);
                if (value > 0xffff)
                {
                    value -= 0x10000;
                    put(static_cast<char16_t>(0xd800 | (value >> 10)));
                    value = 0xdc00 | (value & 0x3ff);
                }
            }
        }
        put(static_cast<char16_t>(value));
    }
}

template<json_encoding Encoding>
std::u16string basic_json_reader<Encoding>::read_string()
{
    std::u16string text;
    read_text([&](char16_t value) { text.push_back(value); });
    return text;
}

template<json_encoding Encoding>
json_property_name<Encoding> basic_json_reader<Encoding>::read_name()
{
    skip_whitespace();
    if (at(position_) != '"')
        throw wire_error("Expected a property name in LuminPack JSON.");
    const auto start = position_ + sizeof(unit);
    auto offset = start;
    auto value = at(offset);
    while (value >= 0x20 && value != end_of_input && value != '"' && value != '\\' &&
        (Encoding == json_encoding::utf16 || value < 0x80))
    {
        offset += sizeof(unit);
        value = at(offset);
    }
    if (value == '"')
    {
        position_ = offset + sizeof(unit);
        return json_property_name<Encoding>(input_.subspan(start, offset - start));
    }
    return json_property_name<Encoding>(read_string());
}

template<json_encoding Encoding>
void basic_json_reader<Encoding>::check_depth(std::size_t depth) const
{
    if (max_depth_ != 0 && depth > max_depth_)
        throw wire_error("LuminPack JSON exceeds the configured depth.");
}

template<json_encoding Encoding>
typename basic_json_reader<Encoding>::container basic_json_reader<Encoding>::begin(char opening)
{
    check_depth(depth_ + 1);
    expect(opening);
    ++depth_;
    return container(*this);
}

template<json_encoding Encoding>
void basic_json_reader<Encoding>::skip_value()
{
    // Use stack storage for ordinary lookahead, and grow separately for a deep unknown value.
    std::array<char, 32> small;
    std::vector<char> large;
    std::size_t size = 0;
    const auto push = [&](char closer) {
        check_depth(depth_ + size + 1);
        if (size < small.size())
            small[size] = closer;
        else
            large.push_back(closer);
        ++size;
    };
    const auto pop = [&] {
        if (size > small.size())
            large.pop_back();
        --size;
    };

    for (;;)
    {
        const auto token = peek();
        if (token == '{' || token == '[')
        {
            const auto closer = token == '{' ? '}' : ']';
            expect(token);
            push(closer);
            if (!consume(closer))
            {
                if (closer == '}')
                {
                    read_text([](char16_t) {});
                    expect(':');
                }
                continue;
            }
            pop();
        }
        else if (token == '"')
            read_text([](char16_t) {});
        else if (token == 'n')
            (void)consume_null();
        else if (token == 't' || token == 'f')
            (void)read_bool();
        else
            (void)number_token();

        while (size != 0)
        {
            const auto closer = size <= small.size() ? small[size - 1] : large.back();
            if (consume(closer))
            {
                pop();
                continue;
            }
            expect(',');
            if (closer == '}')
            {
                read_text([](char16_t) {});
                expect(':');
            }
            break;
        }
        if (size == 0)
            return;
    }
}

template<json_encoding Encoding>
basic_json_reader<Encoding> basic_json_reader<Encoding>::take_value()
{
    skip_whitespace();
    const auto start = position_;
    skip_value();
    basic_json_reader value(input_.subspan(start, position_ - start), {max_depth_});
    value.depth_ = depth_;
    return value;
}

template<json_encoding Encoding>
void basic_json_reader<Encoding>::require_end()
{
    skip_whitespace();
    if (position_ != input_.size())
        throw wire_error("Trailing bytes after a LuminPack JSON document.");
}

template<json_encoding Encoding>
void basic_json_writer<Encoding>::token(char value)
{
    auto* target = output_.reserve(sizeof(unit)).data();
    store_json_unit<Encoding>(target, static_cast<unsigned char>(value));
}

template<json_encoding Encoding>
void basic_json_writer<Encoding>::ascii(std::string_view value)
{
    const auto width = detail::checked_output_width(value.size(), sizeof(unit), output_.remaining());
    auto* target = output_.reserve(width).data();
    if constexpr (Encoding == json_encoding::utf8)
    {
        if (width != 0)
            std::memcpy(target, value.data(), width);
    }
    else
    {
        for (const auto character : value)
            store_json_unit<Encoding>(target, static_cast<unsigned char>(character));
    }
}

template<json_encoding Encoding>
void basic_json_writer<Encoding>::write_string(std::u16string_view value)
{
    constexpr auto quote_bytes = sizeof(unit);
    if (output_.remaining() < 2 * quote_bytes)
        detail::short_output();
    auto room = (output_.remaining() - 2 * quote_bytes) / sizeof(unit);
    const auto initial_room = room;
    bool plain = true;
    for (std::size_t index = 0; index < value.size();)
    {
        const auto character = value[index];
        std::size_t width = 1;
        if (escaped(character))
        {
            plain = false;
            width = escape_letter(character) == 'u' ? 6 : 2;
            ++index;
        }
        else if constexpr (Encoding == json_encoding::utf8)
        {
            const auto code = detail::read_utf16_scalar(value, index, true);
            width = code < 0x80 ? 1 : code < 0x800 ? 2 : code < 0x10000 ? 3 : 4;
            plain = plain && width == 1;
        }
        else
            ++index;
        if (width > room)
            detail::short_output();
        room -= width;
    }

    const auto payload_width = (initial_room - room) * sizeof(unit);
    writer next = output_;
    auto record = next.reserve(payload_width + 2 * quote_bytes);
    auto* target = record.data() + quote_bytes;
    const auto content = std::as_bytes(std::span(value.data(), value.size()));
    if (plain && Encoding == json_encoding::utf16)
    {
        if (!content.empty())
            std::memmove(target, content.data(), content.size());
    }
    else
    {
        const auto payload = record.subspan(quote_bytes, payload_width);
        std::u16string scratch;
        if (detail::overlaps(content, payload))
        {
            scratch = value;
            value = scratch;
        }
        if (plain)
        {
            const auto source = std::span(value.data(), value.size());
            (void)detail::narrow_latin1(source, payload);
        }
        else
        {
            for (std::size_t index = 0; index < value.size();)
            {
                const auto character = value[index];
                if (escaped(character))
                {
                    store_escape<Encoding>(target, character);
                    ++index;
                }
                else if constexpr (Encoding == json_encoding::utf8)
                    store_utf8_scalar(target, detail::read_utf16_scalar(value, index, true));
                else
                {
                    store_json_unit<Encoding>(target, character);
                    ++index;
                }
            }
        }
    }
    auto* head = record.data();
    auto* tail = record.data() + record.size() - quote_bytes;
    store_json_unit<Encoding>(head, '"');
    store_json_unit<Encoding>(tail, '"');
    output_ = next;
}

template class json_property_name<json_encoding::utf8>;
template class json_property_name<json_encoding::utf16>;
template class basic_json_reader<json_encoding::utf8>;
template class basic_json_reader<json_encoding::utf16>;
template class basic_json_writer<json_encoding::utf8>;
template class basic_json_writer<json_encoding::utf16>;
}
