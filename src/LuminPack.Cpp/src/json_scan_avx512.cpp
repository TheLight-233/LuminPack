#include "json_scan.h"

#include <cstdint>
#include <cstring>
#include <immintrin.h>

namespace luminpack::detail
{
namespace
{
template<bool Utf16>
auto special_mask(const std::byte* source) noexcept
{
    __m512i lanes;
    std::memcpy(&lanes, source, sizeof(lanes));
    if constexpr (Utf16)
    {
        return static_cast<std::uint32_t>(_mm512_cmplt_epu16_mask(lanes, _mm512_set1_epi16(0x20)) |
            _mm512_cmpeq_epi16_mask(lanes, _mm512_set1_epi16('"')) |
            _mm512_cmpeq_epi16_mask(lanes, _mm512_set1_epi16('\\')));
    }
    else
    {
        return static_cast<std::uint64_t>(_mm512_cmplt_epi8_mask(lanes, _mm512_set1_epi8(0x20)) |
            _mm512_cmpeq_epi8_mask(lanes, _mm512_set1_epi8('"')) |
            _mm512_cmpeq_epi8_mask(lanes, _mm512_set1_epi8('\\')));
    }
}
}

std::size_t json_special_avx512(std::span<const std::byte> source, bool utf16) noexcept
{
    return utf16 ? scan_json_blocks<64, 2>(source, special_mask<true>) : scan_json_blocks<64>(source, special_mask<false>);
}
}
