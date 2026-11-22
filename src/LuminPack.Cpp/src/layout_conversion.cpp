#include "layout_conversion_kernels.h"
#include "cpu_isa.h"

#if defined(LUMINPACK_CPP_SIMD) && (defined(_M_ARM64) || (defined(__aarch64__) && defined(__ARM_NEON)))
#define LUMINPACK_CPP_LAYOUT_NEON
#if defined(_MSC_VER) && !defined(__clang__)
#include <arm64_neon.h>
#else
#include <arm_neon.h>
#endif
#endif

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_LAYOUT_NEON)
namespace
{
void store_layout_neon(std::uint8_t* destination, uint8x16_t value, uint8x16_t valid, bool preserve_padding) noexcept
{
    if (preserve_padding)
        value = vbslq_u8(valid, value, vld1q_u8(destination));
    vst1q_u8(destination, value);
}

std::size_t group_layout_neon(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_grouped_plan<16, 4>& plan, bool preserve_padding) noexcept
{
    const auto& window = plan.window;
    const auto blocks = window.blocks(count);
    const auto zero = vdupq_n_u8(0);
    for (std::size_t block = 0; block < blocks; ++block)
    {
        const auto* input = reinterpret_cast<const std::uint8_t*>(source + block * window.source_step);
        auto* output = reinterpret_cast<std::uint8_t*>(destination + block * window.destination_step);
        const uint8x16x4_t table{{
            vld1q_u8(input), window.input_vectors > 1 ? vld1q_u8(input + 16) : zero,
            window.input_vectors > 2 ? vld1q_u8(input + 32) : zero,
            window.input_vectors > 3 ? vld1q_u8(input + 48) : zero
        }};
        for (std::size_t index = 0; index < window.output_vectors; ++index)
            store_layout_neon(output + index * 16, vqtbl4q_u8(table, vld1q_u8(plan.bytes[index].data())),
                vld1q_u8(plan.valid[index].data()), preserve_padding);
    }
    return blocks * window.elements;
}

std::size_t shuffle_layout_neon(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept
{
    const auto& window = plan.window;
    const auto blocks = window.blocks(count);
    const auto mask0 = vld1q_u8(plan.masks[0].data());
    const auto mask1 = vld1q_u8(plan.masks[1].data());
    const auto mask2 = vld1q_u8(plan.masks[2].data());
    const auto mask3 = vld1q_u8(plan.masks[3].data());
    const auto valid0 = vld1q_u8(plan.valid[0].data());
    const auto valid1 = vld1q_u8(plan.valid[1].data());
    for (std::size_t block = 0; block < blocks; ++block)
    {
        const auto* input = reinterpret_cast<const std::uint8_t*>(source + block * window.source_step);
        auto* output = reinterpret_cast<std::uint8_t*>(destination + block * window.destination_step);
        const auto low = vld1q_u8(input);
        const auto high = window.input_vectors == 2 ? vld1q_u8(input + 16) : vdupq_n_u8(0);
        auto first = vqtbl1q_u8(low, mask0);
        if (window.input_vectors == 2)
            first = vorrq_u8(first, vqtbl1q_u8(high, mask1));
        store_layout_neon(output, first, valid0, preserve_padding);
        if (window.output_vectors == 2)
        {
            auto second = vqtbl1q_u8(low, mask2);
            if (window.input_vectors == 2)
                second = vorrq_u8(second, vqtbl1q_u8(high, mask3));
            store_layout_neon(output + 16, second, valid1, preserve_padding);
        }
    }
    return blocks * window.elements;
}
}
#endif

std::size_t convert_layout_prefix(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_conversion_plan& plan, bool preserve_padding) noexcept
{
#if defined(LUMINPACK_CPP_SSSE3)
    if (!has_ssse3())
        return 0;

#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
    const bool supported_grouped512 = plan.avx512.window.elements != 0 && has_avx512f();
    const bool vbmi = supported_grouped512 && has_avx512vbmi();
    const bool use_grouped512 = supported_grouped512 && (vbmi || plan.avx512.dword_eligible);
    if (use_grouped512 && count >= plan.avx512.window.reserve)
        return vbmi ? group_layout_avx512vbmi(source, destination, count, plan.avx512, preserve_padding)
            : group_layout_avx512f(source, destination, count, plan.avx512, preserve_padding);
#else
    constexpr bool use_grouped512 = false;
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (!use_grouped512 && plan.avx2.window.elements != 0 && count >= plan.avx2.window.reserve && has_avx2())
        return group_layout_avx2(source, destination, count, plan.avx2, preserve_padding);
#else
    static_cast<void>(use_grouped512);
#endif

    const auto& window = plan.shuffle.window;
    const auto blocks = window.blocks(count);
    std::size_t done = 0;
#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
    if (blocks >= 4 && has_avx512bw())
        done = shuffle_layout_avx512bw(source, destination, blocks, plan.shuffle, preserve_padding);
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (blocks - done >= 2 && has_avx2())
        done += shuffle_layout_avx2(source + done * window.source_step, destination + done * window.destination_step,
            blocks - done, plan.shuffle, preserve_padding);
#endif
    if (done < blocks)
        shuffle_layout_ssse3(source + done * window.source_step, destination + done * window.destination_step,
            blocks - done, plan.shuffle, preserve_padding);
    return blocks * window.elements;
#elif defined(LUMINPACK_CPP_LAYOUT_NEON)
    if (plan.arm64.window.elements != 0 && count >= plan.arm64.window.reserve)
        return group_layout_neon(source, destination, count, plan.arm64, preserve_padding);
    return shuffle_layout_neon(source, destination, count, plan.shuffle, preserve_padding);
#else
    static_cast<void>(source);
    static_cast<void>(destination);
    static_cast<void>(count);
    static_cast<void>(plan);
    static_cast<void>(preserve_padding);
    return 0;
#endif
}
}
