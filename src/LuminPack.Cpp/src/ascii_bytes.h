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
template<class Load, class Merge, class Test>
bool ascii_vector_groups(std::span<const std::byte> source, Load load, Merge merge, Test is_ascii) noexcept
{
    constexpr auto width = sizeof(decltype(load(source.data())));
    const auto* front = source.data();
    const auto* end = front + source.size();
    if (source.size() <= 2 * width)
        return is_ascii(merge(load(front), load(end - width)));

    const auto pair = [&](const std::byte* start) noexcept { return merge(load(start), load(start + width)); };
    constexpr auto group_width = 4 * width;
    if (source.size() > group_width)
    {
        const auto group = [&](const std::byte* start) noexcept { return merge(pair(start), pair(start + 2 * width)); };
        if (!is_ascii(group(front)))
            return false;

        // The first group covers the bytes before the next aligned group.
        const auto misalignment = reinterpret_cast<std::uintptr_t>(front) & (width - 1);
        const auto last = source.size() - group_width;
        for (auto offset = group_width - misalignment; offset < last; offset += group_width)
            if (!is_ascii(group(front + offset)))
                return false;
        front += last;
    }

    // Both ends hold two complete vectors. Overlap covers the short tail.
    return is_ascii(merge(pair(front), pair(end - 2 * width)));
}
}
