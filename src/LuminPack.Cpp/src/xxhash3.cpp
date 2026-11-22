// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.
// Port of LuminPack's XxHash3Algorithm, adapted from dotnet/runtime v10.0.0.
// Based on xxHash by Yann Collet. See docs/xxhash-LICENSE.txt for both upstream licenses.
#include <luminpack/detail/xxhash3.h>

#include "cpu_isa.h"
#include "xxhash3_internal.h"

#include <bit>

#if defined(_MSC_VER)
#include <intrin.h>
#endif
#if defined(LUMINPACK_CPP_SIMD) && (defined(_M_ARM64) || (defined(__aarch64__) && defined(__ARM_NEON)))
#define LUMINPACK_XXH3_NEON
#if defined(_MSC_VER) && !defined(__clang__)
#include <arm64_neon.h>
#else
#include <arm_neon.h>
#endif
#elif defined(LUMINPACK_CPP_SIMD) && (defined(_M_X64) || defined(__x86_64__))
#define LUMINPACK_XXH3_SSE2
#include <emmintrin.h>
#endif

namespace luminpack::detail
{
namespace
{
using namespace xxhash3_internal;

std::uint32_t load32(const std::byte* source) noexcept
{
    const auto value = load_unaligned<std::uint32_t>(source);
    if constexpr (std::endian::native == std::endian::little)
        return value;
    return (value >> 24) | ((value >> 8) & 0x0000ff00) |
        ((value << 8) & 0x00ff0000) | (value << 24);
}

std::uint64_t reverse_bytes(std::uint64_t value) noexcept
{
    value = ((value & 0x00ff00ff00ff00ff) << 8) | ((value >> 8) & 0x00ff00ff00ff00ff);
    value = ((value & 0x0000ffff0000ffff) << 16) | ((value >> 16) & 0x0000ffff0000ffff);
    return (value << 32) | (value >> 32);
}

std::uint64_t load64(const std::byte* source) noexcept
{
    const auto value = load_unaligned<std::uint64_t>(source);
    if constexpr (std::endian::native == std::endian::little)
        return value;
    return reverse_bytes(value);
}

std::uint64_t multiply32(std::uint32_t left, std::uint32_t right) noexcept
{
    return static_cast<std::uint64_t>(left) * right;
}

std::uint64_t multiply_fold(std::uint64_t left, std::uint64_t right) noexcept
{
#if defined(_MSC_VER) && defined(_M_ARM64)
    return (left * right) ^ __umulh(left, right);
#elif defined(_MSC_VER) && defined(_M_X64)
    std::uint64_t high;
    const auto low = _umul128(left, right, &high);
    return low ^ high;
#elif defined(__SIZEOF_INT128__)
    const auto product = static_cast<__uint128_t>(left) * right;
    return static_cast<std::uint64_t>(product) ^ static_cast<std::uint64_t>(product >> 64);
#else
    const auto left_low = static_cast<std::uint32_t>(left);
    const auto left_high = static_cast<std::uint32_t>(left >> 32);
    const auto right_low = static_cast<std::uint32_t>(right);
    const auto right_high = static_cast<std::uint32_t>(right >> 32);
    const auto lower_low = multiply32(left_low, right_low);
    const auto higher_low = multiply32(left_high, right_low);
    const auto lower_high = multiply32(left_low, right_high);
    const auto higher_high = multiply32(left_high, right_high);
    const auto cross = (lower_low >> 32) + static_cast<std::uint32_t>(higher_low) + lower_high;
    const auto high = (higher_low >> 32) + (cross >> 32) + higher_high;
    const auto low = (cross << 32) | static_cast<std::uint32_t>(lower_low);
    return low ^ high;
#endif
}

std::uint64_t avalanche64(std::uint64_t hash) noexcept
{
    hash ^= hash >> 33;
    hash *= prime64_2;
    hash ^= hash >> 29;
    hash *= prime64_3;
    return hash ^ (hash >> 32);
}

std::uint64_t avalanche(std::uint64_t hash) noexcept
{
    hash ^= hash >> 37;
    hash *= 0x165667919e3779f9;
    return hash ^ (hash >> 32);
}

std::uint64_t hash_0_to_16(const std::byte* source, std::size_t size) noexcept
{
    if (size > 8)
    {
        const auto low = load64(source) ^ (secret_words[3] ^ secret_words[4]);
        const auto high = load64(source + size - 8) ^ (secret_words[5] ^ secret_words[6]);
        return avalanche(size + reverse_bytes(low) + high + multiply_fold(low, high));
    }
    if (size >= 4)
    {
        const auto first = static_cast<std::uint64_t>(load32(source));
        const auto last = load32(source + size - 4);
        auto hash = ((first << 32) | last) ^ (secret_words[1] ^ secret_words[2]);
        hash ^= std::rotl(hash, 49) ^ std::rotl(hash, 24);
        hash *= 0x9fb21c651e98df25;
        hash ^= (hash >> 35) + size;
        hash *= 0x9fb21c651e98df25;
        return hash ^ (hash >> 28);
    }
    if (size != 0)
    {
        const auto first = std::to_integer<std::uint32_t>(source[0]);
        const auto middle = std::to_integer<std::uint32_t>(source[size >> 1]);
        const auto last = std::to_integer<std::uint32_t>(source[size - 1]);
        const auto combined = (first << 16) | (middle << 24) | last | (static_cast<std::uint32_t>(size) << 8);
        constexpr auto secret = static_cast<std::uint32_t>(secret_words[0] ^ (secret_words[0] >> 32));
        return avalanche64(combined ^ secret);
    }
    return avalanche64(secret_words[7] ^ secret_words[8]);
}

template<std::size_t SecretOffset>
std::uint64_t mix16(const std::byte* source) noexcept
{
    return multiply_fold(load64(source) ^ secret_at(SecretOffset),
        load64(source + 8) ^ secret_at(SecretOffset + 8));
}

std::uint64_t hash_17_to_128(const std::byte* source, std::size_t size) noexcept
{
    auto hash = size * prime64_1;
    switch ((size - 1) / 32)
    {
    case 3:
        hash += mix16<96>(source + 48) + mix16<112>(source + size - 64);
        [[fallthrough]];
    case 2:
        hash += mix16<64>(source + 32) + mix16<80>(source + size - 48);
        [[fallthrough]];
    case 1:
        hash += mix16<32>(source + 16) + mix16<48>(source + size - 32);
        [[fallthrough]];
    case 0:
        hash += mix16<0>(source) + mix16<16>(source + size - 16);
        break;
    }
    return avalanche(hash);
}

std::uint64_t hash_129_to_240(const std::byte* source, std::size_t size) noexcept
{
    auto hash = size * prime64_1;
    hash += mix16<0>(source);
    hash += mix16<16>(source + 16);
    hash += mix16<32>(source + 32);
    hash += mix16<48>(source + 48);
    hash += mix16<64>(source + 64);
    hash += mix16<80>(source + 80);
    hash += mix16<96>(source + 96);
    hash += mix16<112>(source + 112);
    hash = avalanche(hash);

    switch ((size - 128) / 16)
    {
    case 7:
        hash += mix16<99>(source + 224);
        [[fallthrough]];
    case 6:
        hash += mix16<83>(source + 208);
        [[fallthrough]];
    case 5:
        hash += mix16<67>(source + 192);
        [[fallthrough]];
    case 4:
        hash += mix16<51>(source + 176);
        [[fallthrough]];
    case 3:
        hash += mix16<35>(source + 160);
        [[fallthrough]];
    case 2:
        hash += mix16<19>(source + 144);
        [[fallthrough]];
    case 1:
        hash += mix16<3>(source + 128);
        [[fallthrough]];
    case 0:
        hash += mix16<119>(source + size - 16);
        break;
    }
    return avalanche(hash);
}

struct scalar_kernel
{
    using lane = std::array<std::uint64_t, 2>;
    static constexpr std::size_t words = 2;

    static lane load(const std::uint64_t* source) noexcept
    {
        return {source[0], source[1]};
    }

    static void store(std::uint64_t* destination, lane value) noexcept
    {
        destination[0] = value[0];
        destination[1] = value[1];
    }

    static lane accumulate(lane value, const std::byte* source, const std::byte* secret) noexcept
    {
        const auto first = load64(source);
        const auto second = load64(source + 8);
        const auto first_key = first ^ load64(secret);
        const auto second_key = second ^ load64(secret + 8);
        value[0] += second + multiply32(static_cast<std::uint32_t>(first_key), static_cast<std::uint32_t>(first_key >> 32));
        value[1] += first + multiply32(static_cast<std::uint32_t>(second_key), static_cast<std::uint32_t>(second_key >> 32));
        return value;
    }

    static lane scramble(lane value, const std::byte* secret) noexcept
    {
        return {
            (value[0] ^ (value[0] >> 47) ^ load64(secret)) * prime32_1,
            (value[1] ^ (value[1] >> 47) ^ load64(secret + 8)) * prime32_1
        };
    }
};

#if defined(LUMINPACK_XXH3_SSE2)
struct simd_kernel
{
    using lane = __m128i;
    static constexpr std::size_t words = 2;

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
        const auto key = _mm_xor_si128(input, load_unaligned<lane>(secret));
        const auto swapped = _mm_shuffle_epi32(input, _MM_SHUFFLE(1, 0, 3, 2));
        const auto high = _mm_shuffle_epi32(key, _MM_SHUFFLE(0, 3, 0, 1));
        const auto product = _mm_mul_epu32(key, high);
        return _mm_add_epi64(_mm_add_epi64(value, swapped), product);
    }

    static lane scramble(lane value, const std::byte* secret) noexcept
    {
        const auto shifted = _mm_xor_si128(value, _mm_srli_epi64(value, 47));
        const auto key = _mm_xor_si128(shifted, load_unaligned<lane>(secret));
        const auto prime = _mm_set1_epi32(std::bit_cast<std::int32_t>(prime32_1));
        const auto low_product = _mm_mul_epu32(key, prime);
        const auto high_product = _mm_mul_epu32(_mm_srli_epi64(key, 32), prime);
        return _mm_add_epi64(low_product, _mm_slli_epi64(high_product, 32));
    }
};
#elif defined(LUMINPACK_XXH3_NEON)
struct simd_kernel
{
    using lane = uint64x2_t;
    static constexpr std::size_t words = 2;

    static lane load(const std::uint64_t* source) noexcept
    {
        return vld1q_u64(source);
    }

    static void store(std::uint64_t* destination, lane value) noexcept
    {
        vst1q_u64(destination, value);
    }

    static lane accumulate(lane value, const std::byte* source, const std::byte* secret) noexcept
    {
        const auto input = load_unaligned<lane>(source);
        const auto key = vreinterpretq_u32_u64(veorq_u64(input, load_unaligned<lane>(secret)));
        const auto swapped = vextq_u64(input, input, 1);
        const auto low = vget_low_u32(vuzp1q_u32(key, key));
        const auto high = vget_low_u32(vuzp2q_u32(key, key));
        const auto product = vmull_u32(low, high);
        return vaddq_u64(vaddq_u64(value, swapped), product);
    }

    static lane scramble(lane value, const std::byte* secret) noexcept
    {
        const auto shifted = veorq_u64(value, vshrq_n_u64(value, 47));
        const auto key = veorq_u64(shifted, load_unaligned<lane>(secret));
        const auto low_product = vmull_n_u32(vmovn_u64(key), prime32_1);
        const auto high_product = vmull_n_u32(vshrn_n_u64(key, 32), prime32_1);
        return vaddq_u64(low_product, vshlq_n_u64(high_product, 32));
    }
};
#endif

accumulators select_accumulators(std::span<const std::byte> source) noexcept
{
    if constexpr (sizeof(void*) == 8 && std::endian::native == std::endian::little)
    {
#if defined(LUMINPACK_CPP_AVX2)
        if (has_avx2())
            return accumulate_avx2(source);
#endif
#if defined(LUMINPACK_XXH3_SSE2) || defined(LUMINPACK_XXH3_NEON)
        return accumulate_long<simd_kernel>(source);
#endif
    }
    return accumulate_long<scalar_kernel>(source);
}

std::uint64_t hash_long(std::span<const std::byte> source) noexcept
{
    const auto values = select_accumulators(source);
    auto hash = source.size() * prime64_1;
    for (std::size_t index = 0; index < values.size(); index += 2)
    {
        constexpr std::size_t merge_secret_offset = 11;
        const auto* secret = long_secret.data() + merge_secret_offset + index * 8;
        hash += multiply_fold(values[index] ^ load64(secret), values[index + 1] ^ load64(secret + 8));
    }
    return avalanche(hash);
}
}

std::uint64_t xxhash3(std::span<const std::byte> source) noexcept
{
    if (source.size() <= 16)
        return hash_0_to_16(source.data(), source.size());
    if (source.size() <= 128)
        return hash_17_to_128(source.data(), source.size());
    if (source.size() <= 240)
        return hash_129_to_240(source.data(), source.size());
    return hash_long(source);
}
}
