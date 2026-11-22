#include "latin1.h"

#include <algorithm>
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

bool is_ascii(__m256i value) noexcept
{
    return _mm256_testz_si256(value, _mm256_set1_epi16(-128)) != 0;
}

__m256i narrow_validated(__m256i first, __m256i second) noexcept
{
    const auto lanes = _mm256_packus_epi16(first, second);
    return _mm256_permute4x64_epi64(lanes, 0xd8);
}

void store_narrowed(std::byte* destination, __m256i first, __m256i second) noexcept
{
    const auto ordered = narrow_validated(first, second);
    std::memcpy(destination, &ordered, sizeof(ordered));
}

void store_narrowed_halves(std::byte* destination, std::size_t tail, __m256i first, __m256i last) noexcept
{
    const auto packed = narrow_validated(first, last);
    const auto low = _mm256_castsi256_si128(packed);
    const auto high = _mm256_extracti128_si256(packed, 1);
    std::memcpy(destination, &low, sizeof(low));
    std::memcpy(destination + tail, &high, sizeof(high));
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

bool try_encode_medium_ascii_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto tail = source.size() - 16;
    const auto first = load256(source.data());
    const auto last = load256(source.data() + tail);
    if (!is_ascii(_mm256_or_si256(first, last)))
        return false;
    store_narrowed_halves(destination.data(), tail, first, last);
    return true;
}

std::size_t encode_ascii_prefix_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto length = source.size();
    const auto limit = std::min(length, destination.size());
    const auto* chars = source.data();
    auto* output = destination.data();
    std::size_t index = 0;

    // Core enters the 64-character loop only with at least 128 slots left.
    for (; limit - index >= 128; index += 64)
    {
        const auto first = load256(chars + index);
        const auto second = load256(chars + index + 16);
        const auto third = load256(chars + index + 32);
        const auto fourth = load256(chars + index + 48);
        if (!is_ascii(_mm256_or_si256(_mm256_or_si256(first, second), _mm256_or_si256(third, fourth))))
            break;
        store_narrowed(output + index, first, second);
        store_narrowed(output + index + 32, third, fourth);
    }

    for (; limit - index >= 32; index += 32)
    {
        const auto first = load256(chars + index);
        const auto second = load256(chars + index + 16);
        if (!is_ascii(_mm256_or_si256(first, second)))
            break;
        store_narrowed(output + index, first, second);
    }

    if (index < length && limit == length && length - index < 32)
    {
        // The final block can overwrite only already validated bytes with the same values.
        const auto tail = length - 32;
        const auto first = load256(chars + tail);
        const auto second = load256(chars + tail + 16);
        if (is_ascii(_mm256_or_si256(first, second)))
        {
            store_narrowed(output + tail, first, second);
            index = length;
        }
    }
    return index;
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

std::int32_t write_medium_utf16_payload_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto raw_size = static_cast<std::int32_t>(source.size_bytes());
    const auto* chars = source.data();
    auto* payload = destination.data();
    if (source.size() <= 32)
    {
        const auto last = source.size() - 16;
        const auto first = load256(chars);
        const auto last_block = load256(chars + last);
        if (is_latin1(_mm256_or_si256(first, last_block)))
        {
            store_narrowed_halves(payload, last, first, last_block);
            return raw_size | 1;
        }
        std::memcpy(payload, chars, 32);
        std::memcpy(payload + last * sizeof(char16_t), chars + last, 32);
        return raw_size;
    }

    const auto last = source.size() - 32;
    const auto first = load256(chars);
    const auto first_end = load256(chars + 16);
    const auto last_start = load256(chars + last);
    const auto last_end = load256(chars + last + 16);
    if (is_latin1(_mm256_or_si256(_mm256_or_si256(first, first_end), _mm256_or_si256(last_start, last_end))))
    {
        store_narrowed(payload, first, first_end);
        store_narrowed(payload + last, last_start, last_end);
        return raw_size | 1;
    }
    std::memcpy(payload, chars, 64);
    std::memcpy(payload + last * sizeof(char16_t), chars + last, 64);
    return raw_size;
}

void widen_latin1_avx2(std::span<const std::byte> source, std::span<char16_t> destination) noexcept
{
    const auto last = source.size() - 32;
    for (std::size_t index = 0; index < last; index += 32)
        widen_block(source.data() + index, destination.data() + index);
    widen_block(source.data() + last, destination.data() + last);
}
}
