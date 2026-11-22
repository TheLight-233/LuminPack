// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.
// Port of LuminPack's XxHash3Algorithm, adapted from dotnet/runtime v10.0.0.
// Based on xxHash by Yann Collet. See docs/xxhash-LICENSE.txt for both upstream licenses.
#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <span>
#include <utility>

namespace luminpack::detail::xxhash3_internal
{
inline constexpr auto prime32_1 = std::uint32_t{0x9e3779b1};
inline constexpr auto prime32_2 = std::uint32_t{0x85ebca77};
inline constexpr auto prime32_3 = std::uint32_t{0xc2b2ae3d};
inline constexpr auto prime64_1 = std::uint64_t{0x9e3779b185ebca87};
inline constexpr auto prime64_2 = std::uint64_t{0xc2b2ae3d27d4eb4f};
inline constexpr auto prime64_3 = std::uint64_t{0x165667b19e3779f9};
inline constexpr auto prime64_4 = std::uint64_t{0x85ebca77c2b2ae63};
inline constexpr auto prime64_5 = std::uint64_t{0x27d4eb2f165667c5};

inline constexpr std::array<std::uint64_t, 24> secret_words{
    0xbe4ba423396cfeb8, 0x1cad21f72c81017c, 0xdb979083e96dd4de, 0x1f67b3b7a4a44072,
    0x78e5c0cc4ee679cb, 0x2172ffcc7dd05a82, 0x8e2443f7744608b8, 0x4c263a81e69035e0,
    0xcb00c391bb52283c, 0xa32e531b8b65d088, 0x4ef90da297486471, 0xd8acdea946ef1938,
    0x3f349ce33f76faa8, 0x1d4f0bc7c7bbdcf9, 0x3159b4cd4be0518a, 0x647378d9c97e9fc8,
    0xc3ebd33483acc5ea, 0xeb6313faffa081c5, 0x49daf0b751dd0d17, 0x9e68d429265516d3,
    0xfca1477d58be162b, 0xce31d07ad1b8f88f, 0x280416958f3acb45, 0x7e404bbbcafbd7af
};

inline constexpr auto long_secret = []
{
    std::array<std::byte, secret_words.size() * sizeof(std::uint64_t)> result{};
    for (std::size_t index = 0; index < result.size(); ++index)
        result[index] = static_cast<std::byte>(
            (secret_words[index / 8] >> ((index % 8) * 8)) & 0xff);
    return result;
}();

constexpr std::uint64_t secret_at(std::size_t offset) noexcept
{
    std::uint64_t value = 0;
    for (std::size_t index = 0; index < sizeof(value); ++index)
        value |= std::to_integer<std::uint64_t>(long_secret[offset + index]) << (index * 8);
    return value;
}

template<class T>
T load_unaligned(const std::byte* source) noexcept
{
    T value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

using accumulators = std::array<std::uint64_t, 8>;

template<class Kernel, std::size_t... Index>
accumulators accumulate_long(
    std::span<const std::byte> source, std::index_sequence<Index...>) noexcept
{
    constexpr std::size_t stripe_length = 64;
    constexpr std::size_t secret_consume_rate = 8;
    constexpr std::size_t stripes_per_block = (long_secret.size() - stripe_length) / secret_consume_rate;
    constexpr std::size_t block_length = stripe_length * stripes_per_block;
    constexpr std::size_t last_secret_offset = long_secret.size() - stripe_length - 7;
    constexpr std::size_t scramble_secret_offset = long_secret.size() - stripe_length;
    constexpr std::size_t width = sizeof(typename Kernel::lane);

    accumulators initial{
        prime32_3, prime64_1, prime64_2, prime64_3, prime64_4, prime32_2, prime64_5, prime32_1
    };
    typename Kernel::lane lanes[]{Kernel::load(initial.data() + Index * Kernel::words)...};

    const auto accumulate_stripe = [&](const std::byte* input, const std::byte* secret)
    {
        ((lanes[Index] = Kernel::accumulate(lanes[Index], input + Index * width, secret + Index * width)), ...);
    };

    // Leave the final byte for the overlapping stripe even when the input is block-aligned.
    const auto block_count = (source.size() - 1) / block_length;
    const auto* input = source.data();
    for (std::size_t block = 0; block < block_count; ++block)
    {
        for (std::size_t stripe = 0; stripe < stripes_per_block; ++stripe, input += stripe_length)
            accumulate_stripe(input, long_secret.data() + stripe * secret_consume_rate);
        const auto* secret = long_secret.data() + scramble_secret_offset;
        ((lanes[Index] = Kernel::scramble(lanes[Index], secret + Index * width)), ...);
    }

    const auto tail_stripes = (source.size() - 1 - block_count * block_length) / stripe_length;
    for (std::size_t stripe = 0; stripe < tail_stripes; ++stripe, input += stripe_length)
        accumulate_stripe(input, long_secret.data() + stripe * secret_consume_rate);
    accumulate_stripe(source.data() + source.size() - stripe_length, long_secret.data() + last_secret_offset);

    accumulators result;
    (Kernel::store(result.data() + Index * Kernel::words, lanes[Index]), ...);
    return result;
}

template<class Kernel>
accumulators accumulate_long(std::span<const std::byte> source) noexcept
{
    static_assert(sizeof(typename Kernel::lane) == Kernel::words * sizeof(std::uint64_t));
    return accumulate_long<Kernel>(source, std::make_index_sequence<accumulators{}.size() / Kernel::words>{});
}

#if defined(LUMINPACK_CPP_AVX2)
accumulators accumulate_avx2(std::span<const std::byte> source) noexcept;
#endif
}
