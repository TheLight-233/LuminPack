#include "latin1.h"

#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
template<class T>
T load(const void* source) noexcept
{
    T value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

bool narrow_block(const char16_t* source, std::byte* destination) noexcept
{
    const auto first = load<__m512i>(source);
    const auto second = load<__m512i>(source + 32);
    const auto combined = _mm512_or_si512(first, second);
    if (_mm512_test_epi16_mask(combined, _mm512_set1_epi16(-256)) != 0)
        return false;

    // The AVX-512BW conversions preserve lane order and need no post-pack shuffle.
    const auto low = _mm512_cvtusepi16_epi8(first);
    const auto high = _mm512_cvtusepi16_epi8(second);
    std::memcpy(destination, &low, sizeof(low));
    std::memcpy(destination + 32, &high, sizeof(high));
    return true;
}

void widen_block(const std::byte* source, char16_t* destination) noexcept
{
    const auto first = _mm512_cvtepu8_epi16(load<__m256i>(source));
    const auto second = _mm512_cvtepu8_epi16(load<__m256i>(source + 32));
    std::memcpy(destination, &first, sizeof(first));
    std::memcpy(destination + 32, &second, sizeof(second));
}
}

bool narrow_latin1_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto last = source.size() - 64;
    for (std::size_t index = 0; index < last; index += 64)
    {
        if (!narrow_block(source.data() + index, destination.data() + index))
            return false;
    }
    return narrow_block(source.data() + last, destination.data() + last);
}

void widen_latin1_avx512(std::span<const std::byte> source, std::span<char16_t> destination) noexcept
{
    const auto last = source.size() - 64;
    for (std::size_t index = 0; index < last; index += 64)
        widen_block(source.data() + index, destination.data() + index);
    widen_block(source.data() + last, destination.data() + last);
}
}
