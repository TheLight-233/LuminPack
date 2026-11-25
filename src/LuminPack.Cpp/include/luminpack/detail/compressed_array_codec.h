#pragma once

#include "../compression.h"

// Included by luminpack.h after the cursor and collection codecs.
namespace luminpack
{
/// An attributed unmanaged array or List<T> using the CLR compressed collection framing.
template<class Element>
requires (detail::contiguous_codec<Element> || std::same_as<Element, boolean_codec>)
struct compressed_array_codec
{
    using element_type = typename Element::value_type;
    using value_type = std::optional<std::vector<element_type>>;
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);

    static value_type read(reader& input)
    {
        reader next = input;
        const auto count = next.read_collection_count();
        if (!count || *count == 0)
        {
            input = next;
            return count ? value_type(std::in_place) : std::nullopt;
        }

        if (*count > maximum / element_width)
            throw wire_error("LuminPack compressed collection exceeds its maximum decoded size.");
        const auto width = *count * element_width;
        const auto length = next.read<std::int32_t>();
        if (length < 0 || static_cast<std::size_t>(length) > maximum - header_size)
            throw wire_error("Invalid LuminPack compressed collection frame length.");
        const auto frame = next.take(static_cast<std::size_t>(length));
        if (compression::decompressed_size(frame) != width)
            throw wire_error("LuminPack compressed collection length does not match its frame.");

        std::vector<element_type> elements(*count);
        if constexpr (boolean)
        {
            // std::vector<bool> has no contiguous byte slots. Validate the wire bytes before
            // converting them to the vector's representation or publishing the cursor.
            std::vector<std::byte> decoded(*count);
            (void)compression::decompress(frame, decoded);
            for (std::size_t index = 0; index < *count; ++index)
            {
                if (std::to_integer<unsigned>(decoded[index]) > 1)
                    throw wire_error("Invalid LuminPack Boolean.");
                elements[index] = decoded[index] != std::byte{0};
            }
        }
        else
            (void)compression::decompress(frame, std::as_writable_bytes(std::span(elements)));
        input = next;
        return elements;
    }

    static void write(writer& output, const value_type& value)
    {
        if (!value || value->empty())
        {
            output.write<std::int32_t>(value ? 0 : -1);
            return;
        }

        const auto count = detail::collection_count(value->size());
        if (value->size() > maximum / element_width)
            throw std::length_error("LuminPack compressed collection exceeds its maximum decoded size.");
        const auto capacity = compression::max_compressed_size(value->size() * element_width);
        if (capacity > maximum - header_size)
            throw std::length_error("LuminPack compressed collection exceeds its maximum encoded size.");
        writer next = output;
        const auto reservation = next.reserve(header_size + capacity);

        std::size_t length;
        if constexpr (boolean)
        {
            std::vector<std::byte> source(value->size());
            for (std::size_t index = 0; index < source.size(); ++index)
                source[index] = (*value)[index] ? std::byte{1} : std::byte{0};
            length = compression::compress(source, reservation.subspan(header_size));
        }
        else
        {
            const auto source = std::as_bytes(std::span(*value));
            if (detail::overlaps(source, reservation))
                throw std::invalid_argument("LuminPack compressed collection input and output overlap.");
            length = compression::compress(source, reservation.subspan(header_size));
        }

        detail::store_count(reservation, count);
        detail::store_count(reservation.subspan(minimum_size), static_cast<std::int32_t>(length));
        (void)output.reserve(header_size + length);
    }

private:
    static constexpr bool boolean = std::same_as<Element, boolean_codec>;
    static constexpr auto element_width = boolean ? std::size_t{1} : sizeof(element_type);
    static constexpr auto maximum = static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max());
    static constexpr auto header_size = 2 * minimum_size;
    static_assert(Element::minimum_size == element_width);
};
}
