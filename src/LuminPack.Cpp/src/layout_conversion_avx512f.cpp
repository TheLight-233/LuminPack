#include "layout_conversion_kernels.h"

#include <immintrin.h>

namespace luminpack::detail
{
std::size_t group_layout_avx512f(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_grouped_plan<64, 2>& plan, bool preserve_padding) noexcept
{
    const auto window = plan.window;
    const auto blocks = window.blocks(count);
    for (std::size_t block = 0; block < blocks; ++block)
    {
        const auto* input = source + block * window.source_step;
        auto* output = destination + block * window.destination_step;
        const auto low = _mm512_loadu_si512(input);
        const auto high = window.input_vectors == 2 ? _mm512_loadu_si512(input + 64) : _mm512_setzero_si512();
        for (std::size_t index = 0; index < window.output_vectors; ++index)
        {
            const auto indices = _mm512_loadu_si512(plan.dwords[index].data());
            auto value = window.input_vectors == 2 ? _mm512_permutex2var_epi32(low, indices, high)
                : _mm512_permutexvar_epi32(indices, low);
            auto* target = output + index * 64;
            const auto valid = _mm512_loadu_si512(plan.valid[index].data());
            if (preserve_padding)
                value = _mm512_or_si512(_mm512_and_si512(value, valid),
                    _mm512_andnot_si512(valid, _mm512_loadu_si512(target)));
            else if (plan.has_padding)
                value = _mm512_and_si512(value, valid);
            _mm512_storeu_si512(target, value);
        }
    }
    return blocks * window.elements;
}
}
