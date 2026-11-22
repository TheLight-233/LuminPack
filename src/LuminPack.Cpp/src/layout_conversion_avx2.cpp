#include "layout_conversion_kernels.h"

#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
template<class Vector>
Vector load(const void* source) noexcept
{
    Vector result;
    std::memcpy(&result, source, sizeof(result));
    return result;
}

template<class Vector>
void store(void* destination, Vector value) noexcept
{
    std::memcpy(destination, &value, sizeof(value));
}

void store_narrow(void* destination, __m128i value, __m128i valid, bool preserve_padding) noexcept
{
    if (preserve_padding)
        value = _mm_or_si128(value, _mm_andnot_si128(valid, load<__m128i>(destination)));
    store(destination, value);
}

struct bank_masks
{
    __m256i values[4];
};

__m256i shuffle_banks(__m256i low, __m256i swap_low, __m256i high, __m256i swap_high,
    const bank_masks& masks, unsigned banks) noexcept
{
    auto value = _mm256_shuffle_epi8(low, masks.values[0]);
    if ((banks & 2) != 0)
        value = _mm256_or_si256(value, _mm256_shuffle_epi8(swap_low, masks.values[1]));
    if ((banks & 4) != 0)
        value = _mm256_or_si256(value, _mm256_shuffle_epi8(high, masks.values[2]));
    if ((banks & 8) != 0)
        value = _mm256_or_si256(value, _mm256_shuffle_epi8(swap_high, masks.values[3]));
    return value;
}
}

std::size_t shuffle_layout_avx2(const std::byte* source, std::byte* destination, std::size_t blocks,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept
{
    const auto window = plan.window;
    const auto mask0 = _mm256_broadcastsi128_si256(load<__m128i>(plan.masks[0].data()));
    const auto mask1 = _mm256_broadcastsi128_si256(load<__m128i>(plan.masks[1].data()));
    const auto mask2 = _mm256_broadcastsi128_si256(load<__m128i>(plan.masks[2].data()));
    const auto mask3 = _mm256_broadcastsi128_si256(load<__m128i>(plan.masks[3].data()));
    const auto valid0 = load<__m128i>(plan.valid[0].data());
    const auto valid1 = load<__m128i>(plan.valid[1].data());
    const auto done = blocks - blocks % 2;
    for (std::size_t block = 0; block < done; block += 2)
    {
        const auto* input = source + block * window.source_step;
        auto* output = destination + block * window.destination_step;
        const auto low = _mm256_set_m128i(load<__m128i>(input + window.source_step), load<__m128i>(input));
        const auto high = window.input_vectors == 2 ? _mm256_set_m128i(
            load<__m128i>(input + window.source_step + 16), load<__m128i>(input + 16)) : _mm256_setzero_si256();
        auto first = _mm256_shuffle_epi8(low, mask0);
        if (window.input_vectors == 2)
            first = _mm256_or_si256(first, _mm256_shuffle_epi8(high, mask1));
        auto second = _mm256_setzero_si256();
        if (window.output_vectors == 2)
        {
            second = _mm256_shuffle_epi8(low, mask2);
            if (window.input_vectors == 2)
                second = _mm256_or_si256(second, _mm256_shuffle_epi8(high, mask3));
        }
        store_narrow(output, _mm256_castsi256_si128(first), valid0, preserve_padding);
        if (window.output_vectors == 2)
            store_narrow(output + 16, _mm256_castsi256_si128(second), valid1, preserve_padding);
        store_narrow(output + window.destination_step, _mm256_extracti128_si256(first, 1), valid0, preserve_padding);
        if (window.output_vectors == 2)
            store_narrow(output + window.destination_step + 16, _mm256_extracti128_si256(second, 1), valid1, preserve_padding);
    }
    return done;
}

std::size_t group_layout_avx2(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_avx2_plan& plan, bool preserve_padding) noexcept
{
    const auto window = plan.window;
    const auto blocks = window.blocks(count);
    if (plan.dword_eligible)
    {
        const __m256i indices[]{load<__m256i>(plan.dwords[0].data()), load<__m256i>(plan.dwords[1].data())};
        const __m256i selectors[]{load<__m256i>(plan.select[0].data()), load<__m256i>(plan.select[1].data())};
        const __m256i valid[]{load<__m256i>(plan.valid[0].data()), load<__m256i>(plan.valid[1].data())};
        const bool has_padding = plan.has_padding;
        for (std::size_t block = 0; block < blocks; ++block)
        {
            const auto* input = source + block * window.source_step;
            auto* output = destination + block * window.destination_step;
            const auto low = load<__m256i>(input);
            const auto high = window.input_vectors == 2 ? load<__m256i>(input + 32) : _mm256_setzero_si256();
            for (std::size_t index = 0; index < window.output_vectors; ++index)
            {
                auto value = _mm256_permutevar8x32_epi32(low, indices[index]);
                if (window.input_vectors == 2)
                    value = _mm256_blendv_epi8(value, _mm256_permutevar8x32_epi32(high, indices[index]), selectors[index]);
                if (preserve_padding)
                    value = _mm256_blendv_epi8(load<__m256i>(output + index * 32), value, valid[index]);
                else if (has_padding)
                    value = _mm256_and_si256(value, valid[index]);
                store(output + index * 32, value);
            }
        }
    }
    else
    {
        const auto bank_selectors = plan.banks;
        const auto banks = bank_selectors[0] | bank_selectors[1];
        const __m256i valid[]{load<__m256i>(plan.valid[0].data()), load<__m256i>(plan.valid[1].data())};
        bank_masks masks[2];
        for (std::size_t vector = 0; vector < window.output_vectors; ++vector)
            for (std::size_t bank = 0; bank < 4; ++bank)
                masks[vector].values[bank] = load<__m256i>(plan.masks[vector][bank].data());
        for (std::size_t block = 0; block < blocks; ++block)
        {
            const auto* input = source + block * window.source_step;
            auto* output = destination + block * window.destination_step;
            const auto low = load<__m256i>(input);
            const auto high = window.input_vectors == 2 ? load<__m256i>(input + 32) : _mm256_setzero_si256();
            const auto swap_low = (banks & 2) != 0 ? _mm256_permute2x128_si256(low, low, 1) : _mm256_setzero_si256();
            const auto swap_high = (banks & 8) != 0 ? _mm256_permute2x128_si256(high, high, 1) : _mm256_setzero_si256();
            for (std::size_t index = 0; index < window.output_vectors; ++index)
            {
                auto value = shuffle_banks(low, swap_low, high, swap_high, masks[index], bank_selectors[index]);
                if (preserve_padding)
                    value = _mm256_or_si256(value, _mm256_andnot_si256(valid[index], load<__m256i>(output + index * 32)));
                store(output + index * 32, value);
            }
        }
    }
    return blocks * window.elements;
}
}
