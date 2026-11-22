#include "layout_conversion_kernels.h"

#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
__m128i load128(const void* source) noexcept
{
    __m128i result;
    std::memcpy(&result, source, sizeof(result));
    return result;
}

void store128(void* destination, __m128i value, __m128i valid, bool preserve_padding) noexcept
{
    if (preserve_padding)
        value = _mm_or_si128(value, _mm_andnot_si128(valid, load128(destination)));
    std::memcpy(destination, &value, sizeof(value));
}

__m512i load_lanes(const std::byte* source, std::size_t step) noexcept
{
    return _mm512_inserti32x4(_mm512_inserti32x4(_mm512_inserti32x4(
        _mm512_castsi128_si512(load128(source)), load128(source + step), 1),
        load128(source + step * 2), 2), load128(source + step * 3), 3);
}

template<int Lane>
void store_lanes(std::byte* output, std::size_t step, __m512i low, __m512i high, bool wide,
    __m128i valid0, __m128i valid1, bool preserve_padding) noexcept
{
    store128(output + Lane * step, _mm512_extracti32x4_epi32(low, Lane), valid0, preserve_padding);
    if (wide)
        store128(output + Lane * step + 16, _mm512_extracti32x4_epi32(high, Lane), valid1, preserve_padding);
}
}

std::size_t shuffle_layout_avx512bw(const std::byte* source, std::byte* destination, std::size_t blocks,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept
{
    const auto& window = plan.window;
    const auto mask0 = _mm512_broadcast_i32x4(load128(plan.masks[0].data()));
    const auto mask1 = _mm512_broadcast_i32x4(load128(plan.masks[1].data()));
    const auto mask2 = _mm512_broadcast_i32x4(load128(plan.masks[2].data()));
    const auto mask3 = _mm512_broadcast_i32x4(load128(plan.masks[3].data()));
    const auto valid0 = load128(plan.valid[0].data());
    const auto valid1 = load128(plan.valid[1].data());
    const bool wide_input = window.input_vectors == 2;
    const bool wide_output = window.output_vectors == 2;
    const auto done = blocks - blocks % 4;
    for (std::size_t block = 0; block < done; block += 4)
    {
        const auto* input = source + block * window.source_step;
        auto* output = destination + block * window.destination_step;
        const auto low = load_lanes(input, window.source_step);
        const auto high = wide_input ? load_lanes(input + 16, window.source_step) : _mm512_setzero_si512();
        auto first = _mm512_shuffle_epi8(low, mask0);
        if (wide_input)
            first = _mm512_or_si512(first, _mm512_shuffle_epi8(high, mask1));
        auto second = _mm512_setzero_si512();
        if (wide_output)
        {
            second = _mm512_shuffle_epi8(low, mask2);
            if (wide_input)
                second = _mm512_or_si512(second, _mm512_shuffle_epi8(high, mask3));
        }
        store_lanes<0>(output, window.destination_step, first, second, wide_output, valid0, valid1, preserve_padding);
        store_lanes<1>(output, window.destination_step, first, second, wide_output, valid0, valid1, preserve_padding);
        store_lanes<2>(output, window.destination_step, first, second, wide_output, valid0, valid1, preserve_padding);
        store_lanes<3>(output, window.destination_step, first, second, wide_output, valid0, valid1, preserve_padding);
    }
    return done;
}

}
