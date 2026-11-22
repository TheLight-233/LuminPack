#include <wasm_simd128.h>

int find_utf16_zero(const unsigned char *source, unsigned int width)
{
    v128_t zero = wasm_i16x8_splat(0);
    for (unsigned int offset = 0; offset + 16 <= width; offset += 16) {
        v128_t units = wasm_v128_load(source + offset);
        unsigned int matches = wasm_i16x8_bitmask(wasm_i16x8_eq(units, zero));
        if (matches != 0)
            return (int)(offset + 2 * __builtin_ctz(matches));
    }
    return -1;
}

int has_utf16_surrogate(const unsigned char *source, unsigned int width)
{
    v128_t surrogate = wasm_i16x8_splat(0x1b);
    for (unsigned int offset = 0; offset + 16 <= width; offset += 16) {
        v128_t units = wasm_v128_load(source + offset);
        v128_t high = wasm_u16x8_shr(units, 11);
        if (wasm_v128_any_true(wasm_i16x8_eq(high, surrogate)))
            return 1;
    }
    return 0;
}

int is_ascii(const unsigned char *source, unsigned int width)
{
    for (unsigned int offset = 0; offset + 16 <= width; offset += 16) {
        if (wasm_i8x16_bitmask(wasm_v128_load(source + offset)) != 0)
            return 0;
    }
    return 1;
}
