#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <immintrin.h>

namespace luminpack::detail::mixed_latin1
{
struct shuffle_tables
{
    std::array<std::array<std::uint8_t, 16>, 256> controls;
    std::array<std::uint8_t, 256> widths;
};

alignas(16) inline constexpr auto shuffles = []
{
    shuffle_tables tables{};
    for (std::size_t mask = 0; mask < tables.controls.size(); ++mask)
    {
        std::array<std::uint8_t, 16> indices{};
        std::size_t count = 0;
        for (std::size_t index = 0; index < 8; ++index)
        {
            indices[count++] = static_cast<std::uint8_t>(index * 2);
            if ((mask & (std::size_t{1} << index)) != 0)
                indices[count++] = static_cast<std::uint8_t>(index * 2 + 1);
        }
        // Adding one byte per set bit produces Core's exact 8 + PopCount(mask) width.
        tables.widths[mask] = static_cast<std::uint8_t>(count);
        for (std::size_t index = 0; index < 8; ++index)
        {
            tables.controls[mask][index] = indices[index];
            tables.controls[mask][index + 8] = indices[count - 8 + index];
        }
    }
    return tables;
}();

inline __m128i load_shuffle(std::uint8_t mask) noexcept
{
    __m128i result;
    std::memcpy(&result, shuffles.controls[mask].data(), sizeof(result));
    return result;
}

// The first and last eight selected bytes cover the lane at its exact width.
inline std::size_t store_lane(std::byte* destination, __m128i packed, std::uint8_t mask) noexcept
{
    const auto width = static_cast<std::size_t>(shuffles.widths[mask]);
    const auto first = _mm_cvtsi128_si64(packed);
    const auto last = _mm_cvtsi128_si64(_mm_unpackhi_epi64(packed, packed));
    std::memcpy(destination, &first, sizeof(first));
    std::memcpy(destination + width - sizeof(last), &last, sizeof(last));
    return width;
}
}
