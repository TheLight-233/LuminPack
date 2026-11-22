// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.
// Port of LuminPack's XxHash3Algorithm, adapted from dotnet/runtime v10.0.0.
// Based on xxHash by Yann Collet. See docs/xxhash-LICENSE.txt for both upstream licenses.
#include "xxhash3_internal.h"

#include <bit>
#include <immintrin.h>

namespace luminpack::detail::xxhash3_internal
{
namespace
{
struct avx2_kernel
{
    using lane = __m256i;
    static constexpr std::size_t words = 4;

    static lane load(const std::uint64_t* source) noexcept
    {
        return load_unaligned<lane>(reinterpret_cast<const std::byte*>(source));
    }

    static void store(std::uint64_t* destination, lane value) noexcept
    {
        std::memcpy(destination, &value, sizeof(value));
    }

    static lane accumulate(lane value, const std::byte* source, const std::byte* secret) noexcept
    {
        const auto input = load_unaligned<lane>(source);
        const auto key = _mm256_xor_si256(input, load_unaligned<lane>(secret));
        const auto swapped = _mm256_shuffle_epi32(input, _MM_SHUFFLE(1, 0, 3, 2));
        const auto high = _mm256_shuffle_epi32(key, _MM_SHUFFLE(0, 3, 0, 1));
        const auto product = _mm256_mul_epu32(key, high);
        return _mm256_add_epi64(_mm256_add_epi64(value, swapped), product);
    }

    static lane scramble(lane value, const std::byte* secret) noexcept
    {
        const auto shifted = _mm256_xor_si256(value, _mm256_srli_epi64(value, 47));
        const auto key = _mm256_xor_si256(shifted, load_unaligned<lane>(secret));
        const auto prime = _mm256_set1_epi32(std::bit_cast<std::int32_t>(prime32_1));
        const auto low_product = _mm256_mul_epu32(key, prime);
        const auto high_product = _mm256_mul_epu32(_mm256_srli_epi64(key, 32), prime);
        return _mm256_add_epi64(low_product, _mm256_slli_epi64(high_product, 32));
    }
};
}

accumulators accumulate_avx2(std::span<const std::byte> source) noexcept
{
    return accumulate_long<avx2_kernel>(source);
}
}
