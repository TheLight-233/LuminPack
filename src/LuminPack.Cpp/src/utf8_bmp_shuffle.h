#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <immintrin.h>

namespace luminpack::detail::bmp
{
// Each set of three shuffles places the first 16 and last 8 bytes for eight BMP code units.
inline constexpr std::uint8_t skip = 0x80;
using shuffle_mask = std::array<std::uint8_t, 16>;
inline constexpr shuffle_mask first_lead{0, skip, skip, 1, skip, skip, 2, skip, skip, 3, skip, skip, 4, skip, skip, 5};
inline constexpr shuffle_mask first_middle{skip, 0, skip, skip, 1, skip, skip, 2, skip, skip, 3, skip, skip, 4, skip, skip};
inline constexpr shuffle_mask first_last{skip, skip, 0, skip, skip, 1, skip, skip, 2, skip, skip, 3, skip, skip, 4, skip};
inline constexpr shuffle_mask second_lead{skip, skip, 6, skip, skip, 7, skip, skip, skip, skip, skip, skip, skip, skip, skip, skip};
inline constexpr shuffle_mask second_middle{5, skip, skip, 6, skip, skip, 7, skip, skip, skip, skip, skip, skip, skip, skip, skip};
inline constexpr shuffle_mask second_last{skip, 5, skip, skip, 6, skip, skip, 7, skip, skip, skip, skip, skip, skip, skip, skip};

template<class Vector, shuffle_mask Mask>
Vector load_shuffle() noexcept
{
    alignas(sizeof(Vector)) static constexpr auto lanes = []
    {
        std::array<std::uint8_t, sizeof(Vector)> repeated{};
        for (std::size_t index = 0; index < repeated.size(); ++index)
            repeated[index] = Mask[index % Mask.size()];
        return repeated;
    }();
    Vector result;
    std::memcpy(&result, lanes.data(), sizeof(result));
    return result;
}

inline void store_lane(std::byte* destination, __m128i first, __m128i second) noexcept
{
    const auto last = _mm_cvtsi128_si64(second);
    std::memcpy(destination, &first, sizeof(first));
    std::memcpy(destination + sizeof(first), &last, sizeof(last));
}
}
