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

/// A property name borrows unescaped UTF-16 or ASCII storage, or owns a decoded name.
template<json_encoding Encoding>
class json_property_name
{
public:
    [[nodiscard]] bool matches(std::u16string_view expected) const noexcept;

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
        constexpr auto name = Name.view();
        constexpr auto plain = [] {
            for (const auto character : Name.view())
            {
                if (character < 0x20 || character >= 0x80 || character == u'"' || character == u'\\')
                    return false;
            }
            return true;
        }();
        if constexpr (plain)
        {
            static constexpr auto head = [] {
                constexpr auto text = Name.view();
                std::array<unit, text.size() + 3> result{};
                result.front() = static_cast<unit>('"');
                for (std::size_t index = 0; index < text.size(); ++index)
                    result[index + 1] = static_cast<unit>(text[index]);
                result[text.size() + 1] = static_cast<unit>('"');
                result.back() = static_cast<unit>(':');
                return result;
            }();
            auto target = output_.reserve(head.size() * sizeof(unit));
            std::memcpy(target.data(), head.data(), target.size());
        }
        else
        {
            write_string(name);
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
    void ascii(std::string_view value);
    writer output_;
};
}
