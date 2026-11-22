#pragma once

#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <type_traits>

namespace luminpack::detail
{
inline constexpr auto json_digit_pairs = [] {
    std::array<std::uint16_t, 100> table{};
    for (unsigned value = 0; value < table.size(); ++value)
        table[value] = static_cast<std::uint16_t>(('0' + value / 10) | (('0' + value % 10) << 8));
    return table;
}();

struct json_digit_quad_table
{
    std::uint32_t values[10000]{};

    constexpr json_digit_quad_table() noexcept
    {
        for (unsigned high = 0; high < 100; ++high)
        {
            const auto prefix = static_cast<std::uint32_t>(json_digit_pairs[high]);
            for (unsigned low = 0; low < 100; ++low)
                values[high * 100 + low] = prefix | (static_cast<std::uint32_t>(json_digit_pairs[low]) << 16);
        }
    }
};

extern const json_digit_quad_table json_digit_quads;

inline constexpr auto json_digit_limits = [] {
    std::array<std::uint64_t, 20> powers{};
    powers[0] = 1;
    for (std::size_t i = 1; i < powers.size(); ++i)
        powers[i] = powers[i - 1] * 10;
    return powers;
}();

[[nodiscard]] inline int json_digit_count(std::uint64_t value) noexcept
{
    if (value < 100)
        return value < 10 ? 1 : 2;
    const auto log2 = static_cast<int>(std::bit_width(value)) - 1;
    const auto estimate = ((log2 * 1233) >> 12) + 1;
    return estimate + (estimate < 20 && value >= json_digit_limits[estimate]);
}

template<bool Wide>
inline constexpr std::size_t json_number_unit_width = Wide ? 2 : 1;

template<bool Wide>
inline void store_json_digit(std::byte* output, unsigned value) noexcept
{
    output[0] = static_cast<std::byte>(value);
    if constexpr (Wide)
        output[1] = std::byte{};
}

template<bool Wide>
inline void store_json_digit_pair(std::byte* output, unsigned value) noexcept
{
    const auto pair = json_digit_pairs[value];
    if constexpr (Wide)
    {
        const std::uint32_t widened = (static_cast<std::uint32_t>(pair) | (static_cast<std::uint32_t>(pair) << 8))
            & 0x00ff00ffu;
        std::memcpy(output, &widened, sizeof(widened));
    }
    else
        std::memcpy(output, &pair, sizeof(pair));
}

template<bool Wide>
inline void store_json_digit_quad(std::byte* output, unsigned value) noexcept
{
    const auto quad = json_digit_quads.values[value];
    if constexpr (Wide)
    {
        std::uint64_t widened = quad;
        widened = (widened | (widened << 16)) & 0x0000ffff0000ffffull;
        widened = (widened | (widened << 8)) & 0x00ff00ff00ff00ffull;
        std::memcpy(output, &widened, sizeof(widened));
    }
    else
        std::memcpy(output, &quad, sizeof(quad));
}

/// Writes a validated coefficient in pairs or complete packed quads.
template<bool Wide, class U>
inline void write_json_digits(std::byte* output, U value, int digits, bool use_quads) noexcept
{
    constexpr auto width = json_number_unit_width<Wide>;
    auto* next = output + digits * width;
    if (use_quads)
    {
        while (value >= 10000u)
        {
            const auto quotient = value / 10000u;
            next -= 4 * width;
            store_json_digit_quad<Wide>(next, static_cast<unsigned>(value - quotient * 10000u));
            value = quotient;
        }
        if (value >= 1000u)
        {
            store_json_digit_quad<Wide>(output, static_cast<unsigned>(value));
            return;
        }
    }
    while (value >= 100u)
    {
        const auto quotient = value / 100u;
        next -= 2 * width;
        store_json_digit_pair<Wide>(next, static_cast<unsigned>(value - quotient * 100u));
        value = quotient;
    }
    if (value < 10u)
        store_json_digit<Wide>(output, static_cast<unsigned>('0' + value));
    else
        store_json_digit_pair<Wide>(output, static_cast<unsigned>(value));
}

template<bool Wide, class T>
[[nodiscard]] inline std::size_t format_json_integer(std::byte* output, T value) noexcept
{
    using unsigned_value = std::make_unsigned_t<T>;
    using chunk_type = std::conditional_t<(sizeof(T) <= 4), std::uint32_t, std::uint64_t>;
    const auto bits = static_cast<unsigned_value>(value);
    std::size_t prefix = 0;
    auto magnitude = static_cast<chunk_type>(bits);
    if constexpr (std::is_signed_v<T>)
        if (value < 0)
        {
            store_json_digit<Wide>(output, '-');
            output += json_number_unit_width<Wide>;
            prefix = 1;
            magnitude = static_cast<unsigned_value>(unsigned_value{0} - bits);
        }

    const auto digits = json_digit_count(magnitude);
    write_json_digits<Wide>(output, magnitude, digits, true);
    return prefix + static_cast<std::size_t>(digits);
}

/// The callers validate finiteness and supply a complete 24/32-code-unit window.
std::size_t format_json_float_utf8(std::byte* output, float value) noexcept;
std::size_t format_json_float_utf8(std::byte* output, double value) noexcept;
std::size_t format_json_float_utf16(std::byte* output, float value) noexcept;
std::size_t format_json_float_utf16(std::byte* output, double value) noexcept;
}
