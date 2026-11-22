#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

namespace luminpack::detail
{
/// Attempts to narrow a complete UTF-16 range into an equally sized byte range.
/// On false, the destination may contain a narrowed prefix.
bool narrow_latin1(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;

#if defined(LUMINPACK_CPP_INLINE_SSE2)
/// Writes a UTF-16 length payload of 17 to 64 code units into disjoint storage sized for its raw form.
/// Returns the wire header, without writing it into the payload.
std::int32_t write_medium_utf16_payload(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
#endif

/// Widens a complete Latin-1 range into an equally sized UTF-16 range.
void widen_latin1(std::span<const std::byte> source, std::span<char16_t> destination) noexcept;

#if defined(LUMINPACK_CPP_AVX2)
bool narrow_latin1_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
std::int32_t write_medium_utf16_payload_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
void widen_latin1_avx2(std::span<const std::byte> source, std::span<char16_t> destination) noexcept;
#endif

#if defined(LUMINPACK_CPP_AVX512BW)
bool narrow_latin1_avx512(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
void widen_latin1_avx512(std::span<const std::byte> source, std::span<char16_t> destination) noexcept;
#endif
}
