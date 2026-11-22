#pragma once

#include <luminpack/detail/layout_conversion.h>

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_SSSE3)
void shuffle_layout_ssse3(const std::byte* source, std::byte* destination, std::size_t blocks,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX2)
std::size_t shuffle_layout_avx2(const std::byte* source, std::byte* destination, std::size_t blocks,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept;
std::size_t group_layout_avx2(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_avx2_plan& plan, bool preserve_padding) noexcept;
#endif

#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
std::size_t shuffle_layout_avx512bw(const std::byte* source, std::byte* destination, std::size_t blocks,
    const layout_shuffle_plan& plan, bool preserve_padding) noexcept;
std::size_t group_layout_avx512f(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_grouped_plan<64, 2>& plan, bool preserve_padding) noexcept;
std::size_t group_layout_avx512vbmi(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_grouped_plan<64, 2>& plan, bool preserve_padding) noexcept;
#endif
}
