#include "latin1.h"
#include "ascii_utf16.h"

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

void store_narrowed(std::byte* destination, __m512i value) noexcept
{
    const auto bytes = _mm512_cvtusepi16_epi8(value);
    std::memcpy(destination, &bytes, sizeof(bytes));
}

bool narrow_block(const char16_t* source, std::byte* destination) noexcept
{
    const auto first = load<__m512i>(source);
    const auto second = load<__m512i>(source + 32);
    const auto combined = _mm512_or_si512(first, second);
    if (_mm512_test_epi16_mask(combined, _mm512_set1_epi16(-256)) != 0)
        return false;

    // The AVX-512BW conversions preserve lane order and need no post-pack shuffle.
    store_narrowed(destination, first);
    store_narrowed(destination + 32, second);
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

std::size_t first_non_ascii_utf16_avx512(std::span<const char16_t> source) noexcept
{
    return first_non_ascii_utf16_vectors<32>(source, [](const char16_t* chars) noexcept {
        const auto invalid = _mm512_test_epi16_mask(load<__m512i>(chars), _mm512_set1_epi16(-128));
        return static_cast<std::size_t>(std::countr_zero(static_cast<std::uint32_t>(invalid)));
    });
}

std::size_t encode_ascii_prefix_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto narrow32 = [](const char16_t* chars, std::byte* output) noexcept {
        const auto first = load<__m512i>(chars);
        if (_mm512_test_epi16_mask(first, _mm512_set1_epi16(-128)) != 0)
            return false;
        store_narrowed(output, first);
        return true;
    };
    const auto narrow64 = [](const char16_t* chars, std::byte* output) noexcept {
        const auto first = load<__m512i>(chars);
        const auto second = load<__m512i>(chars + 32);
        if (_mm512_test_epi16_mask(_mm512_or_si512(first, second), _mm512_set1_epi16(-128)) != 0)
            return false;
        store_narrowed(output, first);
        store_narrowed(output + 32, second);
        return true;
    };
    return encode_ascii_prefix_blocks(source, destination, narrow32, narrow64);
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
