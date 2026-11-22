#include <luminpack/luminpack.h>

#include "compression_match.h"

#include <bit>
#include <memory>

namespace luminpack::compression
{
namespace
{
constexpr std::uint32_t magic = 0x324d554c;
constexpr std::size_t header_size = 8;
constexpr std::size_t max_source_length = 2'130'836'464;
constexpr std::size_t min_match = 4;
constexpr std::size_t last_literals = 5;
constexpr std::size_t match_guard = 12;
constexpr std::size_t saturated = 15;
constexpr std::size_t length_escape = 255;

struct position_table
{
    std::unique_ptr<std::uint16_t[]> entries;
    std::size_t size = 0;
};

thread_local position_table positions;

[[noreturn]] void invalid_frame()
{
    throw wire_error("Invalid LUM2 compression frame.");
}

std::size_t frame_output_length(std::span<const std::byte> source)
{
    if (source.size() < header_size || detail::load_match_word<std::uint32_t>(source.data()) != magic)
        invalid_frame();
    const auto length = detail::load_match_word<std::uint32_t>(source.data() + sizeof(magic));
    if (length > std::numeric_limits<std::int32_t>::max() || (length == 0 && source.size() != header_size))
        invalid_frame();
    return length;
}

std::size_t match_hash(std::uint32_t word, int shift) noexcept
{
    return (word * std::uint32_t{2654435761}) >> shift;
}

std::size_t backward_match(const std::byte* source, std::size_t anchor,
    std::size_t& position, std::size_t& candidate) noexcept
{
    const auto start = position;
    while (position > anchor && candidate != 0 && source[position - 1] == source[candidate - 1])
    {
        --position;
        --candidate;
    }
    return start - position;
}

void write_extension(std::byte*& output, std::size_t length) noexcept
{
    if (length >= length_escape)
    {
        *output++ = std::byte{0xff};
        length -= length_escape;
        while (length >= 128)
        {
            *output++ = static_cast<std::byte>((length & 0x7f) | 0x80);
            length >>= 7;
        }
    }
    *output++ = static_cast<std::byte>(length);
}

void write_literals(std::byte*& output, const std::byte* source, std::size_t length,
    std::size_t match_nibble) noexcept
{
    *output++ = static_cast<std::byte>((std::min(length, saturated) << 4) | std::min(match_nibble, saturated));
    if (length >= saturated)
        write_extension(output, length - saturated);
    if (length != 0)
    {
        std::memcpy(output, source, length);
        output += length;
    }
}

std::size_t compress_body(std::span<const std::byte> source, std::byte* destination)
{
    const auto table_bits = std::clamp(static_cast<int>(std::bit_width((source.size() - 1) | std::size_t{1})), 8, 15);
    const auto table_size = std::size_t{1} << table_bits;
    const auto hash_shift = 32 - table_bits;
    if (positions.size < table_size)
    {
        positions.entries = std::make_unique<std::uint16_t[]>(table_size);
        positions.size = table_size;
    }
    auto* table = positions.entries.get();
    const auto* bytes = source.data();
    auto* output = destination;
    std::size_t anchor = 0;
    if (source.size() >= min_match + last_literals + match_guard)
    {
        const auto match_limit = source.size() - match_guard;
        const auto extension_limit = source.size() - last_literals;
        table[match_hash(detail::load_match_word<std::uint32_t>(bytes), hash_shift)] = 0;
        std::size_t position = 1;
        std::uint32_t skip = 32;
        while (position < match_limit)
        {
            const auto word = detail::load_match_word<std::uint32_t>(bytes + position);
            const auto hash = match_hash(word, hash_shift);
            const auto distance = static_cast<std::uint16_t>(position - table[hash]);
            table[hash] = static_cast<std::uint16_t>(position);
            // A low position can come from an earlier source or an earlier wrap of this source.
            if (distance == 0 || distance > position ||
                detail::load_match_word<std::uint32_t>(bytes + position - distance) != word)
            {
                position += skip >> 5;
                ++skip;
                continue;
            }
            skip = 32;

            auto candidate = position - distance;
            (void)backward_match(bytes, anchor, position, candidate);
            auto length = min_match + detail::compression_match_length(bytes + position + min_match,
                bytes + candidate + min_match, extension_limit - position - min_match);

            // Publish the next position's entry even when the lazy match loses.
            const auto lazy_position = position + 1;
            if (lazy_position < match_limit)
            {
                const auto lazy_word = detail::load_match_word<std::uint32_t>(bytes + lazy_position);
                const auto lazy_hash = match_hash(lazy_word, hash_shift);
                const auto lazy_distance = static_cast<std::uint16_t>(lazy_position - table[lazy_hash]);
                table[lazy_hash] = static_cast<std::uint16_t>(lazy_position);
                if (lazy_distance != 0 && lazy_distance <= lazy_position && extension_limit - lazy_position > length)
                {
                    const auto lazy_candidate = lazy_position - lazy_distance;
                    if (detail::load_match_word<std::uint32_t>(bytes + lazy_candidate) == lazy_word &&
                        detail::load_match_word<std::uint32_t>(bytes + lazy_position + length - 3) ==
                            detail::load_match_word<std::uint32_t>(bytes + lazy_candidate + length - 3))
                    {
                        const auto lazy_length = min_match + detail::compression_match_length(
                            bytes + lazy_position + min_match, bytes + lazy_candidate + min_match,
                            extension_limit - lazy_position - min_match);
                        if (lazy_length > length)
                        {
                            position = lazy_position;
                            candidate = lazy_candidate;
                            length = lazy_length + backward_match(bytes, anchor, position, candidate);
                        }
                    }
                }
            }

            const auto match_nibble = length - min_match;
            write_literals(output, bytes + anchor, position - anchor, match_nibble);
            const auto offset = static_cast<std::uint16_t>(position - candidate);
            std::memcpy(output, &offset, sizeof(offset));
            output += sizeof(offset);
            if (match_nibble >= saturated)
                write_extension(output, match_nibble - saturated);

            position += length;
            anchor = position;
            if (position < match_limit)
            {
                for (auto boundary = position - 2; boundary < position; ++boundary)
                {
                    const auto key = detail::load_match_word<std::uint32_t>(bytes + boundary);
                    table[match_hash(key, hash_shift)] = static_cast<std::uint16_t>(boundary);
                }
            }
        }
    }
    write_literals(output, bytes + anchor, source.size() - anchor, 0);
    return static_cast<std::size_t>(output - destination);
}

std::uint8_t read_byte(std::span<const std::byte>& source)
{
    if (source.empty())
        invalid_frame();
    const auto value = std::to_integer<std::uint8_t>(source.front());
    source = source.subspan(1);
    return value;
}

std::size_t read_extension(std::span<const std::byte>& source, std::size_t length, std::size_t maximum)
{
    if (length > maximum)
        invalid_frame();
    const auto first = read_byte(source);
    if (first > maximum - length)
        invalid_frame();
    length += first;
    if (first != length_escape)
        return length;

    std::uint32_t extra = 0;
    for (int shift = 0;; shift += 7)
    {
        const auto next = read_byte(source);
        if (shift == 28 && next > 7)
            invalid_frame();
        extra |= static_cast<std::uint32_t>(next & 0x7f) << shift;
        if (extra > maximum - length)
            invalid_frame();
        if (next < 128)
        {
            if (shift != 0 && next == 0)
                invalid_frame();
            return length + extra;
        }
    }
}

template<bool Expand>
void read_body(std::span<const std::byte> source, std::byte* destination, std::size_t length)
{
    std::size_t produced = 0;
    while (!source.empty())
    {
        const auto token = read_byte(source);
        auto literals = static_cast<std::size_t>(token >> 4);
        const auto match_nibble = static_cast<std::size_t>(token & 0x0f);
        if (literals == saturated)
            literals = read_extension(source, literals, length - produced);
        if (literals > source.size() || literals > length - produced)
            invalid_frame();
        if constexpr (Expand)
        {
            if (literals != 0)
                std::memcpy(destination + produced, source.data(), literals);
        }
        source = source.subspan(literals);
        produced += literals;
        if (source.empty())
        {
            if (match_nibble != 0 || produced != length)
                invalid_frame();
            return;
        }

        if (source.size() < sizeof(std::uint16_t))
            invalid_frame();
        const auto distance = detail::load_match_word<std::uint16_t>(source.data());
        source = source.subspan(sizeof(distance));
        if (distance == 0 || distance > produced)
            invalid_frame();
        auto match_length = match_nibble + min_match;
        if (match_nibble == saturated)
            match_length = read_extension(source, match_length, length - produced);
        if (match_length > length - produced)
            invalid_frame();
        if constexpr (Expand)
            detail::compression_copy_match(destination + produced, match_length, distance);
        produced += match_length;
    }
    invalid_frame();
}

void check_overlap(std::span<const std::byte> source, std::span<std::byte> destination)
{
    if (detail::overlaps(source, destination))
        throw std::invalid_argument("LUM2 source and destination storage overlap.");
}
}

std::size_t max_compressed_size(std::size_t source_length)
{
    if (source_length > max_source_length)
        throw std::length_error("LUM2 source exceeds the maximum length.");
    return source_length == 0 ? header_size : header_size + source_length + (source_length >> 7) + 16;
}

std::size_t decompressed_size(std::span<const std::byte> source)
{
    const auto length = frame_output_length(source);
    if (length == 0)
        return 0;
    const auto body = source.subspan(header_size);
    if (body.empty())
        invalid_frame();
    if ((length - 1) / length_escape >= body.size())
        read_body<false>(body, nullptr, length);
    return length;
}

std::size_t compress(std::span<const std::byte> source, std::span<std::byte> destination)
{
    const auto bound = max_compressed_size(source.size());
    if (destination.size() < bound)
        detail::short_output();
    check_overlap(source, destination.first(bound));
    const auto length = static_cast<std::uint32_t>(source.size());
    std::memcpy(destination.data(), &magic, sizeof(magic));
    std::memcpy(destination.data() + sizeof(magic), &length, sizeof(length));
    return source.empty() ? header_size : header_size + compress_body(source, destination.data() + header_size);
}

std::size_t decompress(std::span<const std::byte> source, std::span<std::byte> destination)
{
    const auto length = frame_output_length(source);
    if (length == 0)
        return 0;
    if (destination.size() < length)
        detail::short_output();
    check_overlap(source, destination.first(length));
    read_body<true>(source.subspan(header_size), destination.data(), length);
    return length;
}
}
