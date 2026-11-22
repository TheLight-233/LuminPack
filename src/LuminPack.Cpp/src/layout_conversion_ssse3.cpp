#include "layout_conversion_kernels.h"

#include <cstring>
#include <tmmintrin.h>

namespace luminpack::detail
{
namespace
{
__m128i load(const void* source) noexcept
{
    __m128i result;
    std::memcpy(&result, source, sizeof(result));
    return result;
}

void store(void* destination, __m128i value, __m128i valid, bool preserve_padding) noexcept
{
    if (preserve_padding)
        value = _mm_or_si128(value, _mm_andnot_si128(valid, load(destination)));
    std::memcpy(destination, &value, sizeof(value));
}
}

void shuffle_layout_ssse3(const std::byte* source, std::byte* destination, std::size_t blocks,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept
{
    const auto& window = plan.window;
    const auto mask0 = load(plan.masks[0].data());
    const auto mask1 = load(plan.masks[1].data());
    const auto mask2 = load(plan.masks[2].data());
    const auto mask3 = load(plan.masks[3].data());
    const auto valid0 = load(plan.valid[0].data());
    const auto valid1 = load(plan.valid[1].data());
    if (window.input_vectors == 1 && window.output_vectors == 1)
    {
        for (std::size_t block = 0; block < blocks; ++block)
            store(destination + block * window.destination_step, _mm_shuffle_epi8(
                load(source + block * window.source_step), mask0), valid0, preserve_padding);
        return;
    }
    for (std::size_t block = 0; block < blocks; ++block)
    {
        const auto* input = source + block * window.source_step;
        auto* output = destination + block * window.destination_step;
        const auto low = load(input);
        const auto high = window.input_vectors == 2 ? load(input + 16) : _mm_setzero_si128();
        auto first = _mm_shuffle_epi8(low, mask0);
        if (window.input_vectors == 2)
            first = _mm_or_si128(first, _mm_shuffle_epi8(high, mask1));
        store(output, first, valid0, preserve_padding);
        if (window.output_vectors == 2)
        {
            auto second = _mm_shuffle_epi8(low, mask2);
            if (window.input_vectors == 2)
                second = _mm_or_si128(second, _mm_shuffle_epi8(high, mask3));
            store(output + 16, second, valid1, preserve_padding);
        }
    }
}
}
