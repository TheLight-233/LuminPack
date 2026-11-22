#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

namespace luminpack::detail
{
bool is_ascii_bytes(std::span<const std::byte> source) noexcept;

#if defined(LUMINPACK_CPP_AVX2)
bool is_ascii_bytes_avx2(std::span<const std::byte> source) noexcept;
#endif

// Mirror Core's ASCII validation groups. The caller proves at least one vector is readable.
// Each check takes the two addresses and returns a scalar, keeping vectors in the ISA kernel.
template<std::size_t Width, class Test>
bool ascii_vector_groups(std::span<const std::byte> source, Test is_ascii) noexcept
{
    const auto* front = source.data();
    const auto* end = front + source.size();
    if (source.size() <= 2 * Width)
        return is_ascii.template operator()<1>(front, end - Width);

    constexpr auto group_width = 4 * Width;
    if (source.size() > group_width)
    {
        const auto group = [&](const std::byte* start) noexcept {
            return is_ascii.template operator()<2>(start, start + 2 * Width);
        };
        if (!group(front))
            return false;

        // The first group covers the bytes before the next aligned group.
        const auto misalignment = reinterpret_cast<std::uintptr_t>(front) & (Width - 1);
        const auto last = source.size() - group_width;
        for (auto offset = group_width - misalignment; offset < last; offset += group_width)
            if (!group(front + offset))
                return false;
        front += last;
    }

    // Both ends hold two complete vectors. Overlap covers the short tail.
    return is_ascii.template operator()<2>(front, end - 2 * Width);
}
}
