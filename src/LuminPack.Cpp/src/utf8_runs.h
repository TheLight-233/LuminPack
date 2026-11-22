#pragma once

#include <cstddef>
#include <span>

namespace luminpack::detail
{
#if defined(LUMINPACK_CPP_INLINE_SSE2)
struct utf8_prefix
{
    std::size_t consumed;
    std::size_t written;
};

/// Encodes a selected non-ASCII-leading prefix into a checked, disjoint payload.
utf8_prefix encode_non_ascii_utf8_prefix(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif

#if defined(LUMINPACK_CPP_SSSE3)
bool is_bmp_sample_ssse3(std::span<const char16_t, 16> source) noexcept;
std::size_t encode_bmp_ssse3(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
utf8_prefix encode_mixed_latin1_ssse3(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX2)
bool is_two_byte_sample_avx2(std::span<const char16_t, 16> source) noexcept;
std::size_t encode_two_byte_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#if defined(LUMINPACK_CPP_SSSE3)
bool is_bmp_sample_avx2(std::span<const char16_t, 16> source) noexcept;
std::size_t encode_bmp_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
utf8_prefix encode_mixed_latin1_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif
#endif

#if defined(LUMINPACK_CPP_AVX512BW)
std::size_t encode_two_byte_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#if defined(LUMINPACK_CPP_SSSE3)
std::size_t encode_bmp_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
utf8_prefix encode_mixed_latin1_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif
#endif
}
