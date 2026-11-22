#include "latin1.h"

#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
__m256i load256(const void* source) noexcept
{
    __m256i value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

bool is_latin1(__m256i value) noexcept
{
    return _mm256_testz_si256(value, _mm256_set1_epi16(-256)) != 0;
}

void store_narrowed(std::byte* destination, __m256i first, __m256i second) noexcept
{
    const auto lanes = _mm256_packus_epi16(first, second);
    const auto ordered = _mm256_permute4x64_epi64(lanes, 0xd8);
    std::memcpy(destination, &ordered, sizeof(ordered));
}

bool narrow_block(const char16_t* source, std::byte* destination) noexcept
{
    const auto first = load256(source);
    const auto second = load256(source + 16);
    if (!is_latin1(_mm256_or_si256(first, second)))
        return false;
    store_narrowed(destination, first, second);
    return true;
}

void widen_block(const std::byte* source, char16_t* destination) noexcept
{
    const auto bytes = load256(source);
    const auto first = _mm256_cvtepu8_epi16(_mm256_castsi256_si128(bytes));
    const auto second = _mm256_cvtepu8_epi16(_mm256_extracti128_si256(bytes, 1));
    std::memcpy(destination, &first, sizeof(first));
    std::memcpy(destination + 16, &second, sizeof(second));
}
}

bool narrow_latin1_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    // Preserve the four-vector classification group used by Core's long Latin-1 path.
    std::size_t index = 0;
    for (; source.size() - index >= 64; index += 64)
    {
        const auto* from = source.data() + index;
        const auto first = load256(from);
        const auto second = load256(from + 16);
        const auto third = load256(from + 32);
        const auto fourth = load256(from + 48);
        if (!is_latin1(_mm256_or_si256(_mm256_or_si256(first, second), _mm256_or_si256(third, fourth))))
            return false;
        store_narrowed(destination.data() + index, first, second);
        store_narrowed(destination.data() + index + 32, third, fourth);
    }

    if (index == source.size())
        return true;
    const auto last = source.size() - 32;
    for (; index < last; index += 32)
    {
        if (!narrow_block(source.data() + index, destination.data() + index))
            return false;
    }
    return narrow_block(source.data() + last, destination.data() + last);
}

void widen_latin1_avx2(std::span<const std::byte> source, std::span<char16_t> destination) noexcept
{
    const auto last = source.size() - 32;
    for (std::size_t index = 0; index < last; index += 32)
        widen_block(source.data() + index, destination.data() + index);
    widen_block(source.data() + last, destination.data() + last);
}
}
