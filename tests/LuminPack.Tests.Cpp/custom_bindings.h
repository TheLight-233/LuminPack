#pragma once

#include <luminpack/luminpack.h>

namespace custom_fixture
{
using shifted = std::optional<std::int32_t>;
inline constexpr auto null_number = std::numeric_limits<std::int64_t>::min();

inline shifted decode_shifted(std::int64_t encoded, std::int32_t mask)
{
    if (encoded == null_number)
        return std::nullopt;
    if (encoded < std::numeric_limits<std::int32_t>::min() || encoded > std::numeric_limits<std::int32_t>::max())
        throw luminpack::wire_error("The Custom value is outside the Int32 range.");
    return static_cast<std::int32_t>(encoded) ^ mask;
}

inline constexpr auto read_shifted = [](luminpack::reader& input) {
    return decode_shifted(input.read<std::int64_t>(), 85);
};

inline constexpr auto write_shifted = [](luminpack::writer& output, const shifted& value) {
    output.write<std::int64_t>(value ? *value ^ 85 : null_number);
};

inline constexpr auto read_shifted_json = [](auto& input) {
    return decode_shifted(input.template read_number<std::int64_t>(), 137);
};

inline constexpr auto write_shifted_json = [](auto& output, const shifted& value) {
    output.write_number(value ? *value ^ 137 : null_number);
};

inline constexpr auto read_binary_only = [](luminpack::reader& input) {
    return static_cast<std::uint8_t>(input.read<std::uint8_t>() ^ 85);
};

inline constexpr auto write_binary_only = [](luminpack::writer& output, std::uint8_t value) {
    output.write<std::uint8_t>(static_cast<std::uint8_t>(value ^ 85));
};
}

template<>
struct luminpack::custom_binding<u"LuminPackCppCustomFixtures.Shifted">
{
    using binary = luminpack::custom_codec<custom_fixture::shifted,
        custom_fixture::read_shifted, custom_fixture::write_shifted, sizeof(std::int64_t)>;
    using json = luminpack::json_custom_codec<custom_fixture::shifted,
        custom_fixture::read_shifted_json, custom_fixture::write_shifted_json>;
};

template<>
struct luminpack::custom_binding<u"LuminPackCppCustomFixtures.BinaryOnly">
{
    using binary = luminpack::custom_codec<std::uint8_t,
        custom_fixture::read_binary_only, custom_fixture::write_binary_only, sizeof(std::uint8_t)>;
};
