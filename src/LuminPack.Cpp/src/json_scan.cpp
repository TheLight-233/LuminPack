#include "json_scan.h"
#include "cpu_isa.h"

namespace luminpack::detail
{
namespace
{
#if defined(LUMINPACK_CPP_JSON_NEON)
template<bool Utf16>
bool has_special(const std::byte* source) noexcept
{
    const auto lanes = vld1q_u8(reinterpret_cast<const std::uint8_t*>(source));
    if constexpr (Utf16)
    {
        const auto units = vreinterpretq_u16_u8(lanes);
        const auto control = vcltq_u16(units, vdupq_n_u16(0x20));
        const auto punctuation = vorrq_u16(vceqq_u16(units, vdupq_n_u16('"')), vceqq_u16(units, vdupq_n_u16('\\')));
        return vmaxvq_u16(vorrq_u16(control, punctuation)) != 0;
    }
    else
    {
        const auto control_or_nonascii = vcltq_s8(vreinterpretq_s8_u8(lanes), vdupq_n_s8(0x20));
        const auto punctuation = vorrq_u8(vceqq_u8(lanes, vdupq_n_u8('"')), vceqq_u8(lanes, vdupq_n_u8('\\')));
        return vmaxvq_u8(vorrq_u8(control_or_nonascii, punctuation)) != 0;
    }
}
#endif

template<bool Utf16>
std::size_t scan_prefix(std::span<const std::byte> source) noexcept
{
    std::size_t offset = 0;
#if defined(LUMINPACK_CPP_AVX512BW)
    if (source.size() >= 64 && has_avx512bw())
    {
        const auto match = json_special_avx512(source, Utf16);
        if (match < source.size())
            return match;
        offset = source.size() & ~std::size_t{63};
    }
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (source.size() - offset >= 32 && has_avx2())
    {
        const auto rest = source.subspan(offset);
        const auto match = json_special_avx2(rest, Utf16);
        if (match < rest.size())
            return offset + match;
        offset += rest.size() & ~std::size_t{31};
    }
#endif
#if defined(LUMINPACK_CPP_INLINE_SSE2)
    const auto rest = source.subspan(offset);
    const auto match = scan_json_blocks<16>(rest, json_special_mask128<Utf16>);
    if (match < rest.size())
        return offset + match;
    offset += rest.size() & ~std::size_t{15};
#elif defined(LUMINPACK_CPP_JSON_NEON)
    for (; source.size() - offset >= 16; offset += 16)
        if (has_special<Utf16>(source.data() + offset))
            return offset + json_special_scalar<Utf16>(source.subspan(offset, 16));
#endif
    return offset + json_special_scalar<Utf16>(source.subspan(offset));
}
}

std::size_t json_literal_prefix_wide(std::span<const std::byte> source, bool utf16) noexcept
{
    return utf16 ? scan_prefix<true>(source) : scan_prefix<false>(source);
}
}
