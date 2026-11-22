#include "compression_match.h"

#include <immintrin.h>

namespace luminpack::detail
{
std::size_t compression_match_avx512(const std::byte* a, const std::byte* b, std::size_t length) noexcept
{
    return compression_match_blocks<64>(a, b, length,
        [](const std::byte* first, const std::byte* last) {
            return _mm512_xor_si512(load_match_word<__m512i>(first), load_match_word<__m512i>(last));
        },
        [](__m512i first, __m512i last) { return _mm512_or_si512(first, last); },
        [](__m512i value) { return _mm512_test_epi64_mask(value, value) == 0; },
        [](__m512i value) {
            const auto different = static_cast<std::uint64_t>(_mm512_cmpneq_epi8_mask(value, _mm512_setzero_si512()));
            return static_cast<std::size_t>(std::countr_zero(different));
        });
}

void compression_copy_avx512(std::byte* destination, const std::byte* source, std::size_t length) noexcept
{
    for (std::size_t i = 0; i < length; i += 64)
    {
        const auto value = load_match_word<__m512i>(source + i);
        std::memcpy(destination + i, &value, sizeof(value));
    }
}
}
