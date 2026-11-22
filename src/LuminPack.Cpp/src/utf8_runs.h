#pragma once

#include <cstddef>
#include <span>

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
/// Encodes complete homogeneous two-byte blocks into a checked, disjoint payload.
/// Returns the consumed UTF-16 code units, with two UTF-8 bytes produced per unit.
std::size_t encode_two_byte_prefix(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX2)
bool is_two_byte_sample_avx2(std::span<const char16_t, 16> source) noexcept;
std::size_t encode_two_byte_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX512BW)
std::size_t encode_two_byte_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif
}
