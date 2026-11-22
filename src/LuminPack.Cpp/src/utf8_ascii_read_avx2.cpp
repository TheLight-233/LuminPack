#include "utf8_ascii_read.h"
#include "ascii_bytes.h"

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

bool is_ascii_bytes_avx2(std::span<const std::byte> source) noexcept
{
    // MinGW GCC can outline a vector-returning loader with an under-aligned return slot.
    return ascii_vector_groups<sizeof(__m256i)>(source,
        []<std::size_t Vectors>(const std::byte* first, const std::byte* second) noexcept {
            const auto a = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(first));
            const auto b = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(second));
            auto bytes = _mm256_or_si256(a, b);
            if constexpr (Vectors == 2)
            {
                const auto c = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(first + 32));
                const auto d = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(second + 32));
                bytes = _mm256_or_si256(bytes, _mm256_or_si256(c, d));
            }
            return _mm256_movemask_epi8(bytes) == 0;
        });
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
        const auto first = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(bytes + index));
        const auto second = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(bytes + index + 32));
        if (_mm256_movemask_epi8(_mm256_or_si256(first, second)) != 0)
            return false;
        store_wide_ascii_32(target + index, first);
        store_wide_ascii_32(target + index + 32, second);
    }
    for (; length - index >= 32; index += 32)
    {
        const auto block = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(bytes + index));
        if (_mm256_movemask_epi8(block) != 0)
            return false;
        store_wide_ascii_32(target + index, block);
    }

    if (index < length)
    {
        const auto tail = length - 32;
        const auto block = _mm256_loadu_si256(reinterpret_cast<const __m256i*>(bytes + tail));
        if (_mm256_movemask_epi8(block) != 0)
            return false;
        store_wide_ascii_32(target + tail, block);
    }
    return true;
}
}
