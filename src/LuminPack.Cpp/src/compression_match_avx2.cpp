#include "compression_match.h"

#include <immintrin.h>

namespace luminpack::detail
{
std::size_t compression_match_avx2(const std::byte* a, const std::byte* b, std::size_t length) noexcept
{
    return compression_match_blocks<32>(a, b, length,
        [](const std::byte* first, const std::byte* last) {
            return _mm256_xor_si256(load_match_word<__m256i>(first), load_match_word<__m256i>(last));
        },
        [](__m256i first, __m256i last) { return _mm256_or_si256(first, last); },
        [](__m256i value) { return _mm256_testz_si256(value, value) != 0; },
        [](__m256i value) {
            const auto equal = static_cast<std::uint32_t>(_mm256_movemask_epi8(_mm256_cmpeq_epi8(value, _mm256_setzero_si256())));
            return static_cast<std::size_t>(std::countr_zero(~equal));
        });
}

void compression_copy_avx2(std::byte* destination, const std::byte* source, std::size_t length) noexcept
{
    for (std::size_t i = 0; i < length; i += 32)
    {
        const auto value = load_match_word<__m256i>(source + i);
        std::memcpy(destination + i, &value, sizeof(value));
    }
}
}
