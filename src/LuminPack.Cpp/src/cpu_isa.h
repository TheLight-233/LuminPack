#pragma once

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
bool has_ssse3() noexcept;
bool has_sse41() noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX2)
bool has_avx2() noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX512BW)
bool has_avx512bw() noexcept;
#endif

#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
bool has_avx512f() noexcept;
bool has_avx512vbmi() noexcept;
#endif
}
