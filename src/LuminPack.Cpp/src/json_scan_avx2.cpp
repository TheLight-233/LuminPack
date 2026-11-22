#include "json_scan.h"

#include <cstdint>
#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
template<bool Utf16>
std::uint32_t special_mask(const std::byte* source) noexcept
{
    __m256i lanes;
    std::memcpy(&lanes, source, sizeof(lanes));
    if constexpr (Utf16)
    {
        const auto biased = _mm256_xor_si256(lanes, _mm256_set1_epi16(-32768));
        const auto control = _mm256_cmpgt_epi16(_mm256_set1_epi16(0x20 - 32768), biased);
        const auto punctuation = _mm256_or_si256(_mm256_cmpeq_epi16(lanes, _mm256_set1_epi16('"')),
            _mm256_cmpeq_epi16(lanes, _mm256_set1_epi16('\\')));
        return static_cast<std::uint32_t>(_mm256_movemask_epi8(_mm256_or_si256(control, punctuation)));
    }
    else
    {
        const auto control_or_nonascii = _mm256_cmpgt_epi8(_mm256_set1_epi8(0x20), lanes);
        const auto punctuation = _mm256_or_si256(_mm256_cmpeq_epi8(lanes, _mm256_set1_epi8('"')),
            _mm256_cmpeq_epi8(lanes, _mm256_set1_epi8('\\')));
        return static_cast<std::uint32_t>(_mm256_movemask_epi8(_mm256_or_si256(control_or_nonascii, punctuation)));
    }
}
}

std::size_t json_special_avx2(std::span<const std::byte> source, bool utf16) noexcept
{
    return utf16 ? scan_json_blocks<32>(source, special_mask<true>) : scan_json_blocks<32>(source, special_mask<false>);
}
}
