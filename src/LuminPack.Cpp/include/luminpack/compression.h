#pragma once

#include <cstddef>
#include <span>

namespace luminpack::compression
{
/// Returns the full destination capacity required to compress a source of this length.
[[nodiscard]] std::size_t max_compressed_size(std::size_t source_length);

/// Reads the LUM2 header and validates the complete expansion of a high-ratio frame.
[[nodiscard]] std::size_t decompressed_size(std::span<const std::byte> source);

/// Writes a LUM2 frame into non-overlapping storage of at least max_compressed_size bytes.
/// Returns the actual frame length.
[[nodiscard]] std::size_t compress(std::span<const std::byte> source, std::span<std::byte> destination);

/// Validates and expands a LUM2 frame into non-overlapping storage.
/// Returns the declared output length. A malformed frame can leave a decoded prefix.
[[nodiscard]] std::size_t decompress(std::span<const std::byte> source, std::span<std::byte> destination);
}
