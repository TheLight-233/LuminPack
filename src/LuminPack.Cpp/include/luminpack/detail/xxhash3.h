#pragma once

#include <cstddef>
#include <cstdint>
#include <span>

namespace luminpack::detail
{
// Hashes the complete encoded name as bytes with seed zero. A match still requires name equality.
std::uint64_t xxhash3(std::span<const std::byte> source) noexcept;
}
