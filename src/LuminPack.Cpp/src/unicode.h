#pragma once

namespace luminpack::detail
{
inline bool high_surrogate(char16_t value) noexcept { return value >= 0xd800 && value <= 0xdbff; }
inline bool low_surrogate(char16_t value) noexcept { return value >= 0xdc00 && value <= 0xdfff; }

inline std::uint32_t read_utf16_scalar(std::u16string_view source, std::size_t& offset, bool replace = false)
{
    const auto character = source[offset++];
    if (high_surrogate(character))
    {
        if (offset != source.size() && low_surrogate(source[offset]))
        {
            const auto second = source[offset++];
            return 0x10000u + ((character - 0xd800u) << 10) + (second - 0xdc00u);
        }
    }
    else if (!low_surrogate(character))
        return character;
    if (replace)
        return 0xfffd;
    throw std::invalid_argument("LuminPack UTF-8 encoding requires valid UTF-16.");
}

inline std::uint32_t read_utf8_scalar(std::span<const std::byte> source, std::size_t& offset, bool replace)
{
    const auto first = std::to_integer<unsigned>(source[offset++]);
    if (first < 0x80)
        return first;

    unsigned width = 0;
    if (first >= 0xc2 && first <= 0xdf)
        width = 2;
    else if (first >= 0xe0 && first <= 0xef)
        width = 3;
    else if (first >= 0xf0 && first <= 0xf4)
        width = 4;

    if (width != 0)
    {
        std::uint32_t value = first & (0x7fu >> width);
        auto lower = first == 0xe0 ? 0xa0u : first == 0xf0 ? 0x90u : 0x80u;
        auto upper = first == 0xed ? 0x9fu : first == 0xf4 ? 0x8fu : 0xbfu;
        for (unsigned part = 1; part < width; ++part)
        {
            if (offset == source.size())
                break;
            const auto next = std::to_integer<unsigned>(source[offset]);
            if (next < lower || next > upper)
                break;
            ++offset;
            value = (value << 6) | (next & 0x3fu);
            if (part + 1 == width)
                return value;
            lower = 0x80u;
            upper = 0xbfu;
        }
    }

    if (replace)
        return 0xfffd;
    throw wire_error("Invalid UTF-8 in a LuminPack string.");
}
}
