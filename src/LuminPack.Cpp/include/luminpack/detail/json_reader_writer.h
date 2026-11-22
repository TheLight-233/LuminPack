#pragma once

#include <array>
#include <charconv>
#include <cmath>

// Included by luminpack.h after the common binary and text declarations.
namespace luminpack
{
enum class json_encoding
{
    utf8,
    utf16
};

/// Applies a document-wide container depth limit. Zero leaves the limit unset.
struct json_read_options
{
    std::size_t max_depth = 1024;
};

template<json_encoding Encoding>
class basic_json_reader;

template<json_encoding Encoding>
using json_code_unit = std::conditional_t<Encoding == json_encoding::utf8, char, char16_t>;

namespace detail
{
std::size_t json_utf8_encoded_length(std::u16string_view name) noexcept;

template<auto Name>
inline constexpr bool plain_json_property_name = [] {
    for (const auto character : Name.view())
    {
        if (character < 0x20 || character >= 0x80 || character == u'"' || character == u'\\')
            return false;
    }
    return true;
}();

/// A static comma, quoted property name and colon in the document's encoding.
template<json_encoding Encoding, auto Name>
requires plain_json_property_name<Name>
inline constexpr auto json_property_head = [] {
    using unit = json_code_unit<Encoding>;
    constexpr auto text = Name.view();
    std::array<unit, text.size() + 4> head{};
    head[0] = static_cast<unit>(',');
    head[1] = static_cast<unit>('"');
    for (std::size_t index = 0; index < text.size(); ++index)
        head[index + 2] = static_cast<unit>(text[index]);
    head[text.size() + 2] = static_cast<unit>('"');
    head.back() = static_cast<unit>(':');
    return head;
}();
}

/// A property name borrows unescaped UTF-16 or ASCII storage, or owns a decoded name.
template<json_encoding Encoding>
class json_property_name
{
public:
    [[nodiscard]] bool matches(std::u16string_view expected) const noexcept;

    /// Hashes the decoded property name in its document encoding.
    [[nodiscard]] std::uint64_t encoded_hash() const;

    [[nodiscard]] std::size_t encoded_length() const noexcept
    {
        if (!decoded_)
            return raw_.size() / sizeof(json_code_unit<Encoding>);
        if constexpr (Encoding == json_encoding::utf16)
            return decoded_->size();
        else
            return detail::json_utf8_encoded_length(*decoded_);
    }

    template<auto Name>
    [[nodiscard]] bool matches() const noexcept
    {
        constexpr auto expected = Name.view();
        if (decoded_)
            return *decoded_ == expected;

        constexpr auto width = expected.size() * sizeof(json_code_unit<Encoding>);
        if (raw_.size() != width)
            return false;
        if constexpr (width == 0)
            return true;
        else if constexpr (Encoding == json_encoding::utf16)
            return std::memcmp(raw_.data(), expected.data(), width) == 0;
        else if constexpr (detail::plain_json_property_name<Name>)
            return std::memcmp(raw_.data(), detail::json_property_head<Encoding, Name>.data() + 2, width) == 0;
        else
            return false;
    }

private:
    friend class basic_json_reader<Encoding>;

    explicit json_property_name(std::span<const std::byte> raw) noexcept : raw_(raw) {}
    explicit json_property_name(std::u16string&& decoded) noexcept : decoded_(std::move(decoded)) {}

    std::span<const std::byte> raw_;
    std::optional<std::u16string> decoded_;
};

/// Checked JSON token reader for caller-owned UTF-8 or little-endian UTF-16 bytes.
template<json_encoding Encoding = json_encoding::utf16>
class basic_json_reader
{
public:
    using unit = json_code_unit<Encoding>;

    explicit basic_json_reader(std::span<const std::byte> input, json_read_options options = {});

    explicit basic_json_reader(std::basic_string_view<unit> input, json_read_options options = {})
        : basic_json_reader(std::as_bytes(std::span(input.data(), input.size())), options) {}

    class container
    {
    public:
        container(const container&) = delete;
        container& operator=(const container&) = delete;
        ~container() { --reader_.depth_; }

    private:
        friend class basic_json_reader;
        explicit container(basic_json_reader& reader) noexcept : reader_(reader) {}
        basic_json_reader& reader_;
    };

    /// Holds a validated local cursor for a generated run in the current object.
    class generated_int32_run
    {
    public:
        generated_int32_run(const generated_int32_run&) = delete;
        generated_int32_run& operator=(const generated_int32_run&) = delete;

        /// Reads a complete property head and bounded decimal Int32, leaving this field unread on a miss.
        template<auto Name, bool First>
        [[nodiscard]] bool read(std::int32_t& value) noexcept
        {
            if constexpr (!detail::plain_json_property_name<Name>)
                return false;
            else
            {
                constexpr const auto& head = detail::json_property_head<Encoding, Name>;
                constexpr std::size_t skip = First ? 1 : 0;
                constexpr auto width = (head.size() - skip) * sizeof(unit);
                constexpr auto step = sizeof(unit);
                if (remaining_.size() < step || width > remaining_.size() - step ||
                    std::memcmp(remaining_.data(), head.data() + skip, width) != 0)
                    return false;

                const auto last = remaining_.size() - step;
                auto offset = width;
                auto code = code_at(offset);
                const bool negative = code == '-';
                if (negative)
                {
                    offset += step;
                    if (offset > last)
                        return false;
                    code = code_at(offset);
                }

                const auto first_digit = code - '0';
                if (first_digit > 9)
                    return false;
                std::uint64_t magnitude = first_digit;
                int digits = 1;
                offset += step;
                while (offset <= last)
                {
                    code = code_at(offset);
                    const auto digit = code - '0';
                    if (digit > 9)
                    {
                        if (code != ' ' && code != '\t' && code != '\r' && code != '\n' &&
                            code != ',' && code != ']' && code != '}')
                            return false;
                        break;
                    }
                    if (first_digit == 0 || digits == 10)
                        return false;
                    magnitude = magnitude * 10 + digit;
                    offset += step;
                    ++digits;
                }
                if (magnitude > (negative ? 2147483648ull : 2147483647ull))
                    return false;

                const auto signed_magnitude = static_cast<std::int64_t>(magnitude);
                value = static_cast<std::int32_t>(negative ? -signed_magnitude : signed_magnitude);
                remaining_ = remaining_.subspan(offset);
                return true;
            }
        }

        /// Publishes the matched prefix before the ordinary object reader resumes.
        void complete()
        {
            const auto consumed = initial_size_ - remaining_.size();
            if (consumed == 0)
                return;
            if (reader_.input_.size() - reader_.position_ != initial_size_)
                throw wire_error("The JSON reader moved during a generated Int32 run.");
            reader_.position_ += consumed;
        }

    private:
        friend class basic_json_reader;

        explicit generated_int32_run(basic_json_reader& reader) noexcept
            : reader_(reader), remaining_(reader.input_.subspan(reader.position_)), initial_size_(remaining_.size()) {}

        [[nodiscard]] std::uint32_t code_at(std::size_t offset) const noexcept
        {
            if constexpr (Encoding == json_encoding::utf8)
                return std::to_integer<unsigned char>(remaining_[offset]);
            else
            {
                char16_t code;
                std::memcpy(&code, remaining_.data() + offset, sizeof(code));
                return code;
            }
        }

        basic_json_reader& reader_;
        std::span<const std::byte> remaining_;
        std::size_t initial_size_;
    };

    [[nodiscard]] generated_int32_run begin_generated_int32_run() noexcept
    {
        return generated_int32_run(*this);
    }

    /// Enters an object or array, checking its delimiter and the depth limit.
    [[nodiscard]] container begin(char opening);
    [[nodiscard]] char peek();
    [[nodiscard]] bool consume(char token);
    void expect(char token);
    [[nodiscard]] bool consume_null();
    [[nodiscard]] bool read_bool();
    [[nodiscard]] std::u16string read_string();
    [[nodiscard]] json_property_name<Encoding> read_name();
    void skip_value();
    [[nodiscard]] basic_json_reader take_value();
    void require_end();

    /// Matches the complete expected property head within an open object.
    /// The object codec supplies whether a comma is required. A mismatch leaves the cursor unchanged.
    template<auto Name>
    [[nodiscard]] bool try_read_ordered_property(bool first) noexcept
    {
        if constexpr (!detail::plain_json_property_name<Name>)
            return false;
        else
        {
            constexpr const auto& head = detail::json_property_head<Encoding, Name>;
            const auto skip = first ? std::size_t{1} : std::size_t{0};
            const auto width = (head.size() - skip) * sizeof(unit);
            if (width > input_.size() - position_ ||
                std::memcmp(input_.data() + position_, head.data() + skip, width) != 0)
                return false;
            position_ += width;
            return true;
        }
    }

    template<class T>
    requires scalar<T> && (!std::same_as<T, char16_t>)
    [[nodiscard]] T read_number()
    {
        const auto token = number_token();
        if constexpr (Encoding == json_encoding::utf8)
            return parse_number<T>(std::string_view(reinterpret_cast<const char*>(token.data()), token.size()));
        else
        {
            std::array<char, 128> buffer;
            std::string extra;
            const auto length = token.size() / sizeof(unit);
            const auto target = length <= buffer.size() ? std::span(buffer).first(length) :
                std::span<char>(extra.append(length, '\0').data(), length);
            for (std::size_t index = 0; index < length; ++index)
                target[index] = static_cast<char>(std::to_integer<unsigned char>(token[index * sizeof(unit)]));
            return parse_number<T>(std::string_view(target.data(), target.size()));
        }
    }

private:
    template<class T>
    static T parse_number(std::string_view token)
    {
        const auto* end = token.data() + token.size();
        T value;
        const auto result = std::from_chars(token.data(), end, value);
        if (result.ec != std::errc{} || result.ptr != end)
            throw wire_error("LuminPack JSON number is out of range or has the wrong type.");
        if constexpr (std::floating_point<T>)
        {
            if (!std::isfinite(value))
                throw wire_error("Non-finite LuminPack JSON number.");
        }
        return value;
    }

    [[nodiscard]] std::uint32_t at(std::size_t offset) const noexcept;
    [[nodiscard]] std::uint32_t next_unit();
    void skip_whitespace() noexcept;
    [[nodiscard]] bool consume_literal(std::string_view literal);
    [[nodiscard]] std::span<const std::byte> number_token();
    [[nodiscard]] char16_t read_hex_escape();
    template<class Put>
    void read_text(Put&& put);
    template<class Put>
    void read_text_content(Put&& put);
    void check_depth(std::size_t depth) const;

    std::span<const std::byte> input_;
    std::size_t position_ = 0;
    std::size_t depth_ = 0;
    std::size_t max_depth_;
};

/// JSON writer targeting caller-owned UTF-8 or little-endian UTF-16 byte storage.
template<json_encoding Encoding = json_encoding::utf16>
class basic_json_writer
{
public:
    using unit = json_code_unit<Encoding>;

    explicit basic_json_writer(std::span<std::byte> output) noexcept : output_(output) {}

    [[nodiscard]] std::span<const std::byte> written() const noexcept { return output_.written(); }
    void token(char value);
    void write_null() { ascii("null"); }
    void write_bool(bool value) { ascii(value ? "true" : "false"); }
    void write_string(std::u16string_view value);

    /// Writes a generated property name and colon with one checked copy when the name is plain ASCII.
    template<auto Name>
    void write_property()
    {
        write_ordered_property<Name>(true);
    }

    /// Writes the property head including its separator unless this is the first property.
    template<auto Name>
    void write_ordered_property(bool first)
    {
        if constexpr (detail::plain_json_property_name<Name>)
        {
            constexpr auto width = detail::json_property_head<Encoding, Name>.size() * sizeof(unit);
            if (first)
                write_property_head<Name, 1>();
            else if (width <= output_.remaining())
                write_property_head<Name, 0>();
            else
            {
                token(',');
                write_property_head<Name, 1>();
            }
        }
        else
        {
            if (!first)
                token(',');
            write_string(Name.view());
            token(':');
        }
    }

    template<class T>
    requires scalar<T> && (!std::same_as<T, char16_t>)
    void write_number(T value)
    {
        if constexpr (std::floating_point<T>)
        {
            if (!std::isfinite(value))
                throw std::invalid_argument("Non-finite LuminPack JSON number.");
        }
        std::array<char, 64> buffer;
        const auto result = std::to_chars(buffer.data(), buffer.data() + buffer.size(), value);
        if (result.ec != std::errc{})
            throw std::invalid_argument("Cannot format LuminPack JSON number.");
        ascii(std::string_view(buffer.data(), result.ptr));
    }

private:
    template<auto Name, std::size_t Skip>
    void write_property_head()
    {
        constexpr const auto& head = detail::json_property_head<Encoding, Name>;
        constexpr auto width = (head.size() - Skip) * sizeof(unit);
        auto target = output_.reserve(width);
        std::memcpy(target.data(), head.data() + Skip, width);
    }

    void ascii(std::string_view value);
    writer output_;
};
}
