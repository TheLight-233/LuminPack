#include "utf8_ascii_read.h"

#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
void store_wide_ascii_256(char16_t* target, __m128i source) noexcept
{
    const auto wide = _mm256_cvtepu8_epi16(source);
    std::memcpy(target, &wide, sizeof(wide));
}

void store_wide_ascii_32(char16_t* target, __m256i source) noexcept
{
    store_wide_ascii_256(target, _mm256_castsi256_si128(source));
    store_wide_ascii_256(target + 16, _mm256_extracti128_si256(source, 1));
}
}

void widen_medium_ascii_avx2(char16_t* target, std::size_t length, __m128i first, __m128i last) noexcept
{
    store_wide_ascii_256(target, first);
    store_wide_ascii_256(target + length - 16, last);
}

bool try_widen_long_ascii_avx2(std::span<const std::byte> source, std::span<char16_t> destination) noexcept
{
    auto* target = destination.data();
    const auto* bytes = source.data();
    const auto length = source.size();
    std::size_t index = 0;
    for (; length - index >= 64; index += 64)
    {
        const auto first = load_ascii_word<__m256i>(bytes + index);
        const auto second = load_ascii_word<__m256i>(bytes + index + 32);
        if (_mm256_movemask_epi8(_mm256_or_si256(first, second)) != 0)
            return false;
        store_wide_ascii_32(target + index, first);
        store_wide_ascii_32(target + index + 32, second);
    }
    for (; length - index >= 32; index += 32)
    {
        const auto block = load_ascii_word<__m256i>(bytes + index);
        if (_mm256_movemask_epi8(block) != 0)
            return false;
        store_wide_ascii_32(target + index, block);
    }

    if (index < length)
    {
        const auto tail = length - 32;
        const auto block = load_ascii_word<__m256i>(bytes + tail);
        if (_mm256_movemask_epi8(block) != 0)
            return false;
        store_wide_ascii_32(target + tail, block);
    }
    return true;
}
}
