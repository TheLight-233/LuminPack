#pragma once

#include <cstddef>
#include <span>

namespace luminpack::detail
{
/// Attempts to narrow a complete UTF-16 range into an equally sized byte range.
/// On false, the destination may contain a narrowed prefix.
bool narrow_latin1(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;

/// Widens a complete Latin-1 range into an equally sized UTF-16 range.
void widen_latin1(std::span<const std::byte> source, std::span<char16_t> destination) noexcept;

#if defined(LUMINPACK_CPP_AVX2)
bool narrow_latin1_avx2(std::span<const char16_t> source, std::span<std::byte> destination) noexcept;
void widen_latin1_avx2(std::span<const std::byte> source, std::span<char16_t> destination) noexcept;
#endif
}
