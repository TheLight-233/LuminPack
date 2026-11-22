// Adapted from LuminPack's XjbFloat32, XjbFloat64 and LuminPackJsonFloatFormatter.
// Copyright 2026 xjb714 and contributors. Licensed under Apache-2.0.
// The upstream license is included as LICENSE-xjb.txt.
#include <stdint.h>
#include "xjb-tables.h"

static char output[32];
static const char digit_pairs[] =
    "0001020304050607080910111213141516171819202122232425262728293031323334353637383940414243444546474849"
    "5051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899";

typedef struct {
    uint64_t high;
    uint64_t low;
} product;

typedef struct {
    uint64_t digits;
    int exponent;
} decimal;

static product multiply(uint64_t left, uint64_t right)
{
    const uint64_t left_low = (uint32_t)left;
    const uint64_t left_high = left >> 32;
    const uint64_t right_low = (uint32_t)right;
    const uint64_t right_high = right >> 32;
    const uint64_t p00 = left_low * right_low;
    const uint64_t p01 = left_low * right_high;
    const uint64_t p10 = left_high * right_low;
    const uint64_t p11 = left_high * right_high;
    // Stop LLVM folding the product into a compiler-runtime 128-bit multiply call.
    const volatile uint64_t middle = (p00 >> 32) + (uint32_t)p01 + (uint32_t)p10;
    const uint64_t carry = middle;
    return (product){p11 + (p01 >> 32) + (p10 >> 32) + (carry >> 32), (carry << 32) | (uint32_t)p00};
}

static decimal normalize(uint64_t digits, int exponent)
{
    while (digits % 10 == 0) {
        digits /= 10;
        exponent++;
    }
    return (decimal){digits, exponent};
}

static decimal shortest_float32(uint32_t significand, int exponent)
{
    const uint64_t decimal_mask = (UINT64_C(1) << 36) - 1;
    const int q = exponent == 0 ? -149 : exponent - 150;
    if (significand == 0) {
        const int k = (q * 1233 - 512) >> 12;
        const int h = q + ((k * -1701 - 1701) >> 9);
        const uint64_t pow10 = float32_pow10[45 + k];
        const uint64_t high = pow10 >> (4 - h);
        const uint64_t ten = (high >> 36) * 10;
        const uint64_t dot = high & decimal_mask;
        const uint64_t half = pow10 >> (28 - h);
        const uint64_t offset = (UINT64_C(1) << 34) - 7 + (dot >> 32);
        uint64_t one = (dot * 5 + offset) >> 35;
        if ((half >> 1) > dot)
            one = 0;
        if (q == 31 - 150 || q == 214 - 150 || q == 217 - 150)
            one++;
        if (half > decimal_mask - dot)
            one = 10;
        return normalize(ten + one, k);
    }

    const uint64_t c = exponent == 0 ? significand : significand | 0x800000;
    const uint32_t shift = float32_h37[exponent];
    const int k = (q * 1233) >> 12;
    const uint64_t pow10 = float32_pow10[45 + k];
    const uint64_t high = multiply(c << shift, pow10).high;
    const uint64_t half = (pow10 >> (65 - shift)) + ((significand + 1) & 1);
    const uint64_t shorter = ((high + half) >> 36) * 10;
    const uint64_t longer = (high * 5 + (UINT64_C(1) << 34) - 7 + ((high >> 32) & 15)) >> 35;
    return normalize(((high - half) >> 36) < ((high + half) >> 36) ? shorter : longer, k);
}

static decimal shortest_float64(uint64_t significand, int exponent)
{
    const int q = exponent == 0 ? -1074 : exponent - 1075;
    if (significand == 0) {
        const int k = (q * 315653 - 131072) >> 20;
        const int h = q + ((k * -217707 - 217707) >> 16);
        const uint64_t pow10 = float64_pow10[584 - 2 * k];
        const uint64_t half = pow10 >> -h;
        const uint64_t dot = pow10 << (53 + h);
        const uint64_t ten = (pow10 >> (11 - h)) * 10;
        uint64_t one = ((dot >> (53 + h)) * 5 + (UINT64_C(1) << (9 - h))) >> (10 - h);
        const uint64_t tail = (dot >> 54) * 5;
        if ((tail & 511) > (half >> 55) * 5)
            one = (tail >> 9) + 1;
        if (dot == (UINT64_C(1) << 62))
            one = 2;
        if ((half >> 1) > dot)
            one = 0;
        if (half > UINT64_MAX - dot)
            one = 10;
        return normalize(ten + one, k);
    }

    const uint64_t c = exponent == 0 ? significand : significand | (UINT64_C(1) << 52);
    const int k = ((exponent - 1075) * 78913) >> 18;
    const int h = q + (((-1 - k) * 217707) >> 16);
    const uint64_t shifted = c << (h + 7);
    const int p = 584 - 2 * k;
    const uint64_t pow10_high = float64_pow10[p];
    const uint64_t pow10_low = float64_pow10[p + 1];
    const product a = multiply(shifted, pow10_high);
    const product b = multiply(shifted, pow10_low);
    const uint64_t low = a.low + b.high;
    const uint64_t high = a.high + (low < a.low);
    const uint64_t dot = (low >> 6) | (high << 58);
    const uint64_t half = (pow10_high >> -h) + ((significand + 1) & 1);
    const uint64_t ten = (high >> 6) * 10;
    const product ones = multiply(dot, 10);
    const uint64_t addend = dot == (UINT64_C(1) << 62) ? 0 : (UINT64_C(1) << 63) + 6;
    uint64_t one = ones.high + (ones.low + addend < ones.low);
    if (dot < half)
        one = 0;
    if (UINT64_MAX - dot < half)
        one = 10;
    return normalize(ten + one, k);
}

// Each decimal coefficient fits in 20 digits, and each final token in 32 bytes.
static unsigned write_coefficient(char *end, uint64_t digits)
{
    char *position = end;
    while (digits >= 100) {
        const uint64_t quotient = digits / 100;
        const unsigned pair = (unsigned)(digits - quotient * 100) * 2;
        *--position = digit_pairs[pair + 1];
        *--position = digit_pairs[pair];
        digits = quotient;
    }
    if (digits >= 10)
        *--position = digit_pairs[digits * 2 + 1];
    *--position = digits < 10 ? '0' + (char)digits : digit_pairs[digits * 2];
    return (unsigned)(end - position);
}

static unsigned emit(decimal value, int negative, int legacy, int single)
{
    char coefficient[20];
    const unsigned length = write_coefficient(coefficient + sizeof(coefficient), value.digits);
    const char *digits = coefficient + sizeof(coefficient) - length;
    const int point = (int)length + value.exponent;
    const int leading = point - 1;
    unsigned written = 0;
    if (negative)
        output[written++] = '-';

    const int fixed = (legacy && value.exponent >= -(single ? 9 : 17)) ||
        (leading >= -4 && leading < (single ? 9 : 17));
    if (fixed) {
        if (point <= 0) {
            output[written++] = '0';
            output[written++] = '.';
            for (int i = 0; i < -point; i++)
                output[written++] = '0';
        }
        for (unsigned i = 0; i < length; i++) {
            if ((int)i == point && i != 0)
                output[written++] = '.';
            output[written++] = digits[i];
        }
        for (int i = (int)length; i < point; i++)
            output[written++] = '0';
    } else {
        output[written++] = digits[0];
        if (length > 1) {
            output[written++] = '.';
            for (unsigned i = 1; i < length; i++)
                output[written++] = digits[i];
        }
        output[written++] = 'E';
        output[written++] = leading < 0 ? '-' : '+';
        unsigned magnitude = (unsigned)(leading < 0 ? -leading : leading);
        if (magnitude >= 100) {
            output[written++] = '0' + (char)(magnitude / 100);
            magnitude %= 100;
        }
        output[written++] = digit_pairs[magnitude * 2];
        output[written++] = digit_pairs[magnitude * 2 + 1];
    }
    return written;
}

static unsigned format_short_decimal(double magnitude, int negative, int single)
{
    if (magnitude > 2147483648.0)
        return 0;
    if (!single) {
        const double prefilter = magnitude * 1000.0;
        if (prefilter != (double)(uint64_t)prefilter)
            return 0;
    }
    for (int decimals = 2; decimals <= 4; decimals += 2) {
        const double power = decimals == 2 ? 100.0 : 10000.0;
        const uint64_t scaled = (uint64_t)(magnitude * power + 0.5);
        if (scaled == 0)
            continue;
        const double quotient = single ? (double)scaled * (decimals == 2 ? 0.01 : 0.0001) : (double)scaled / power;
        if ((single ? (double)(float)quotient : quotient) != magnitude)
            continue;
        const decimal candidate = normalize(scaled, -decimals);
        if (candidate.digits >= (single ? UINT64_C(1000000) : UINT64_C(1000000000000000)))
            return 0;
        return emit(candidate, negative, 1, single);
    }
    return 0;
}

unsigned format_float32(float value)
{
    const union { float number; uint32_t bits; } raw = {value};
    const int exponent = (int)((raw.bits >> 23) & 255);
    if (exponent == 255)
        return 0;
    const int negative = (int)(raw.bits >> 31);
    const uint32_t significand = raw.bits & 0x7fffff;
    if ((raw.bits & 0x7fffffff) == 0)
        return emit((decimal){0, 0}, negative, 1, 1);
    const int legacy = exponent >= 95 && exponent <= 159;
    if (legacy) {
        const double magnitude = negative ? -(double)value : (double)value;
        const unsigned length = format_short_decimal(magnitude, negative, 1);
        if (length != 0)
            return length;
    }
    return emit(shortest_float32(significand, exponent), negative, legacy, 1);
}

unsigned format_float64(double value)
{
    const union { double number; uint64_t bits; } raw = {value};
    const int exponent = (int)((raw.bits >> 52) & 2047);
    if (exponent == 2047)
        return 0;
    const int negative = (int)(raw.bits >> 63);
    const uint64_t significand = raw.bits & ((UINT64_C(1) << 52) - 1);
    if ((raw.bits & (UINT64_MAX >> 1)) == 0)
        return emit((decimal){0, 0}, negative, 1, 0);
    const int legacy = exponent >= 991 && exponent <= 1055;
    if (legacy) {
        const double magnitude = negative ? -value : value;
        const unsigned length = format_short_decimal(magnitude, negative, 0);
        if (length != 0)
            return length;
    }
    return emit(shortest_float64(significand, exponent), negative, legacy, 0);
}

const char *json_number_buffer(void)
{
    return output;
}
