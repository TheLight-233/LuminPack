#include <luminpack/detail/json_number_format.h>
#include <luminpack/detail/xjb_tables.h>
#include <cmath>
#include <limits>

#if defined(_MSC_VER) && (defined(_M_X64) || defined(_M_ARM64))
#include <intrin.h>
#endif

namespace luminpack::detail
{
constinit const json_digit_quad_table json_digit_quads{};

namespace
{
struct uint128_product
{
    std::uint64_t high;
    std::uint64_t low;
};

[[nodiscard]] inline uint128_product multiply_words(std::uint64_t left, std::uint64_t right) noexcept
{
#if defined(__SIZEOF_INT128__)
    __extension__ using uint128 = unsigned __int128;
    const auto result = static_cast<uint128>(left) * right;
    return {static_cast<std::uint64_t>(result >> 64), static_cast<std::uint64_t>(result)};
#elif defined(_MSC_VER) && defined(_M_X64)
    std::uint64_t high;
    const auto low = _umul128(left, right, &high);
    return {high, low};
#elif defined(_MSC_VER) && defined(_M_ARM64)
    return {__umulh(left, right), left * right};
#else
    const std::uint64_t left_low = static_cast<std::uint32_t>(left);
    const std::uint64_t right_low = static_cast<std::uint32_t>(right);
    const auto left_high = left >> 32;
    const auto right_high = right >> 32;
    const auto low_low = left_low * right_low;
    const auto low_high = left_low * right_high;
    const auto high_low = left_high * right_low;
    const auto high_high = left_high * right_high;
    const auto middle = (low_low >> 32) + static_cast<std::uint32_t>(low_high) + static_cast<std::uint32_t>(high_low);
    return {high_high + (low_high >> 32) + (high_low >> 32) + (middle >> 32),
        (middle << 32) | static_cast<std::uint32_t>(low_low)};
#endif
}

struct decimal
{
    std::uint64_t digits;
    int exponent;
};

[[nodiscard]] inline decimal normalize_decimal(std::uint64_t digits, int exponent) noexcept
{
    while (digits % 10 == 0)
    {
        digits /= 10;
        ++exponent;
    }
    return {digits, exponent};
}

// Port of LuminPack Core's binary32 and binary64 XJB cores.
// Upstream xjb714/xjb at 80cc89574a8f8457ffbf951afa2fd27c2459bd4a, Apache-2.0.
[[nodiscard]] decimal float_decimal(std::uint32_t bits) noexcept
{
    constexpr int bit = 36;
    constexpr auto fraction_mask = (std::uint64_t{1} << bit) - 1;
    const auto sig = bits & ((1u << 23) - 1);
    const auto exp = bits >> 23;
    const auto c = exp == 0 ? sig : sig | (1u << 23);
    const int q = exp == 0 ? -149 : static_cast<int>(exp) - 150;

    if (sig == 0)
    {
        // Powers of two have an asymmetric lower interval.
        const int k = (q * 1233 - 512) >> 12;
        const int h = q + ((k * -1701 - 1701) >> 9);
        const auto pow10 = float32_pow10[45 + k];
        const auto sig_high = pow10 >> (4 - h);
        const auto ten = (sig_high >> bit) * 10;
        const auto dot_one = sig_high & fraction_mask;
        const auto half_ulp = pow10 >> (64 - bit - h);
        const auto offset_num = (std::uint64_t{1} << (bit - 2)) - 7 + (dot_one >> (bit - 4));
        auto one = (dot_one * 5 + offset_num) >> (bit - 1);
        one = (half_ulp >> 1) > dot_one ? 0 : one;
        if (q == 31 - 150 || q == 214 - 150 || q == 217 - 150)
            ++one;
        one = half_ulp > fraction_mask - dot_one ? 10 : one;
        return normalize_decimal(ten + one, k);
    }

    const int h37 = float32_h37[exp];
    const int k = (q * 1233) >> 12;
    const auto pow10 = float32_pow10[45 + k];
    const auto high = multiply_words(static_cast<std::uint64_t>(c) << h37, pow10).high;
    const auto half_ulp = (pow10 >> (65 - h37)) + ((sig + 1u) & 1u);
    const auto shorter = ((high + half_ulp) >> bit) * 10;
    const auto longer = (high * 5 + (std::uint64_t{1} << (bit - 2)) - 7 + ((high >> 32) & 15u)) >> (bit - 1);
    const auto digits = ((high - half_ulp) >> bit) < ((high + half_ulp) >> bit) ? shorter : longer;
    return normalize_decimal(digits, k);
}

[[nodiscard]] decimal double_decimal(std::uint64_t bits) noexcept
{
    constexpr int offset = 6;
    constexpr int pow10_bias = 584;
    constexpr auto max_word = std::numeric_limits<std::uint64_t>::max();
    const auto sig = bits & ((std::uint64_t{1} << 52) - 1);
    const int exp = static_cast<int>(bits >> 52);
    const auto c = exp == 0 ? sig : sig | (std::uint64_t{1} << 52);
    const int q = exp == 0 ? -1074 : exp - 1075;

    if (sig == 0)
    {
        const int k = (q * 315653 - 131072) >> 20;
        const int h = q + ((k * -217707 - 217707) >> 16);
        const auto pow10 = float64_pow10[pow10_bias - k * 2];
        const auto half_ulp = pow10 >> -h;
        const auto dot_one = pow10 << (53 + h);
        const auto ten = (pow10 >> (11 - h)) * 10;
        auto one = ((dot_one >> (53 + h)) * 5 + (std::uint64_t{1} << (9 - h))) >> (10 - h);
        const auto round = (dot_one >> 54) * 5;
        if ((round & 511u) > ((half_ulp >> 55) * 5))
            one = (round >> 9) + 1;
        if (dot_one == (std::uint64_t{1} << 62))
            one = 2;
        one = (half_ulp >> 1) > dot_one ? 0 : one;
        one = half_ulp > max_word - dot_one ? 10 : one;
        return normalize_decimal(ten + one, k);
    }

    const int k = ((exp - 1075) * 78913) >> 18;
    const int h = q + (((-1 - k) * 217707) >> 16);
    const auto cb = c << (h + 1 + offset);
    const int p = pow10_bias - k * 2;
    const auto pow10_high = float64_pow10[p];
    const auto pow10_low = float64_pow10[p + 1];
    const auto high_product = multiply_words(cb, pow10_high);
    const auto low_product = multiply_words(cb, pow10_low);
    const auto low = high_product.low + low_product.high;
    const auto high = high_product.high + (low < high_product.low);
    const auto dot_one = (low >> offset) | (high << (64 - offset));
    const auto half_ulp = (pow10_high >> -h) + ((bits + 1u) & 1u);
    const auto ten = (high >> offset) * 10;
    const auto digit_product = multiply_words(dot_one, 10);
    const auto addend = dot_one == (std::uint64_t{1} << 62) ? 0 : (std::uint64_t{1} << 63) + 6;
    auto one = digit_product.high + (digit_product.low + addend < digit_product.low);
    one = dot_one < half_ulp ? 0 : one;
    one = max_word - dot_one < half_ulp ? 10 : one;
    return normalize_decimal(ten + one, k);
}

template<bool Wide>
void write_zeroes(std::byte* output, int count) noexcept
{
    if constexpr (Wide)
    {
        for (int i = 0; i < count; ++i)
            store_json_digit<true>(output + i * 2, '0');
    }
    else
        std::memset(output, '0', static_cast<std::size_t>(count));
}

template<bool Wide>
[[nodiscard]] int emit_fixed(std::byte* output, decimal value, int digits) noexcept
{
    constexpr auto width = json_number_unit_width<Wide>;
    const bool use_quads = Wide && digits >= 10;
    if (value.exponent >= 0)
    {
        write_json_digits<Wide>(output, value.digits, digits, use_quads);
        write_zeroes<Wide>(output + digits * width, value.exponent);
        return digits + value.exponent;
    }

    const int fraction = -value.exponent;
    if (fraction >= digits)
    {
        store_json_digit<Wide>(output, '0');
        store_json_digit<Wide>(output + width, '.');
        const int zeros = fraction - digits;
        write_zeroes<Wide>(output + 2 * width, zeros);
        write_json_digits<Wide>(output + (zeros + 2) * width, value.digits, digits, use_quads);
        return fraction + 2;
    }

    const int head = digits - fraction;
    if (fraction <= 2)
    {
        const auto scale = fraction == 1 ? 10u : 100u;
        const auto whole = value.digits / scale;
        const auto tail = static_cast<unsigned>(value.digits - whole * scale);
        write_json_digits<Wide>(output, whole, head, Wide && head >= 10);
        store_json_digit<Wide>(output + head * width, '.');
        if (fraction == 1)
            store_json_digit<Wide>(output + (head + 1) * width, '0' + tail);
        else
            store_json_digit_pair<Wide>(output + (head + 1) * width, tail);
    }
    else
    {
        write_json_digits<Wide>(output, value.digits, digits, use_quads);
        std::memmove(output + (head + 1) * width, output + head * width, fraction * width);
        store_json_digit<Wide>(output + head * width, '.');
    }
    return digits + 1;
}

template<bool Wide>
[[nodiscard]] int emit_scientific(std::byte* output, decimal value, int digits) noexcept
{
    constexpr auto width = json_number_unit_width<Wide>;
    write_json_digits<Wide>(output + width, value.digits, digits, digits >= (Wide ? 10 : 8));
    store_json_digit<Wide>(output, std::to_integer<unsigned>(output[width]));
    int position = 1;
    if (digits != 1)
    {
        store_json_digit<Wide>(output + width, '.');
        position = digits + 1;
    }

    int exponent = value.exponent + digits - 1;
    store_json_digit<Wide>(output + position++ * width, 'E');
    store_json_digit<Wide>(output + position++ * width, exponent < 0 ? '-' : '+');
    if (exponent < 0)
        exponent = -exponent;
    if (exponent >= 100)
        store_json_digit<Wide>(output + position++ * width, static_cast<unsigned>('0' + exponent / 100));
    store_json_digit_pair<Wide>(output + position * width, static_cast<unsigned>(exponent % 100));
    return position + 2;
}

template<bool Wide, class T>
[[nodiscard]] int write_short_decimal(std::byte* output, double magnitude) noexcept
{
    constexpr bool single = std::is_same_v<T, float>;
    if constexpr (!single)
    {
        const double prefilter = magnitude * 1000.0;
        if (prefilter != static_cast<double>(static_cast<std::uint64_t>(prefilter)))
            return 0;
    }

    // These two widths include shorter decimals once their trailing zeroes are removed.
    for (int decimals : {2, 4})
    {
        const auto scale = decimals == 2 ? 100.0 : 10000.0;
        const auto scaled = static_cast<std::uint64_t>(magnitude * scale + 0.5);
        if (scaled == 0)
            continue;
        const auto candidate = single ? static_cast<double>(scaled) * (decimals == 2 ? 0.01 : 0.0001)
            : static_cast<double>(scaled) / scale;
        if (static_cast<double>(static_cast<T>(candidate)) != magnitude)
            continue;
        const auto short_value = normalize_decimal(scaled, -decimals);
        const int digits = json_digit_count(short_value.digits);
        return digits <= (single ? 6 : 15) ? emit_fixed<Wide>(output, short_value, digits) : 0;
    }
    return 0;
}

template<bool Wide, class T>
[[nodiscard]] std::size_t format_float(std::byte* output, T value) noexcept
{
    constexpr bool single = std::is_same_v<T, float>;
    constexpr int fraction_bits = single ? 23 : 52;
    constexpr int legacy_low = single ? 95 : 991;
    constexpr int fraction_cap = single ? 9 : 17;
    constexpr double integral_limit = single ? 16777216.0 : 9007199254740992.0;
    constexpr auto width = json_number_unit_width<Wide>;
    using uint = std::conditional_t<single, std::uint32_t, std::uint64_t>;
    constexpr auto sign_mask = uint{1} << (sizeof(uint) * 8 - 1);
    const auto bits = std::bit_cast<uint>(value);
    const auto abs_bits = bits & (sign_mask - 1);
    const auto exponent = static_cast<int>(abs_bits >> fraction_bits);
    const auto negative = (bits & sign_mask) != 0;
    if (negative)
    {
        store_json_digit<Wide>(output, '-');
        output += width;
    }
    const std::size_t sign = negative;
    if (abs_bits == 0)
    {
        store_json_digit<Wide>(output, '0');
        return sign + 1;
    }

    const bool legacy = exponent >= legacy_low && exponent <= legacy_low + 64;
    const double magnitude = static_cast<double>(std::bit_cast<T>(abs_bits));
    if (legacy && magnitude < integral_limit)
    {
        const auto integral = static_cast<std::uint64_t>(magnitude);
        if (static_cast<double>(integral) == magnitude)
        {
            const auto digits = json_digit_count(integral);
            write_json_digits<Wide>(output, integral, digits, Wide && digits >= 10);
            return sign + static_cast<std::size_t>(digits);
        }
    }

    if (legacy && magnitude <= 2147483648.0)
    {
        if (const auto written = write_short_decimal<Wide, T>(output, magnitude); written != 0)
            return sign + static_cast<std::size_t>(written);
    }

    const auto shortest = [&] {
        if constexpr (single)
            return float_decimal(abs_bits);
        else
            return double_decimal(abs_bits);
    }();
    const int digits = json_digit_count(shortest.digits);
    const int leading_exponent = shortest.exponent + digits - 1;
    const bool fixed = (legacy && shortest.exponent >= -fraction_cap)
        || (leading_exponent >= -4 && leading_exponent < fraction_cap);
    return sign + static_cast<std::size_t>(fixed ? emit_fixed<Wide>(output, shortest, digits)
        : emit_scientific<Wide>(output, shortest, digits));
}
}

std::size_t format_json_float_utf8(std::byte* output, float value) noexcept { return format_float<false>(output, value); }
std::size_t format_json_float_utf8(std::byte* output, double value) noexcept { return format_float<false>(output, value); }
std::size_t format_json_float_utf16(std::byte* output, float value) noexcept { return format_float<true>(output, value); }
std::size_t format_json_float_utf16(std::byte* output, double value) noexcept { return format_float<true>(output, value); }
}
