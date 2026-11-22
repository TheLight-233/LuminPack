#pragma once

// Included by version_codec.h after the binary codecs and Version state.
namespace luminpack
{
namespace detail
{
struct legacy_version_field
{
    std::span<const std::string_view> names;
    std::string_view contract;
};

struct legacy_version_map
{
    static constexpr std::uint8_t absent = 255;
    std::array<std::uint32_t, maximum_version_fields> lengths;
    std::array<std::uint8_t, maximum_version_fields> slots;
    std::array<bool, maximum_version_fields> present;
    std::size_t count;
};

/// Read the complete legacy name, contract and length tables before decoding any member.
[[nodiscard]] legacy_version_map read_legacy_version_map(reader& input, string_profile profile,
    std::span<const legacy_version_field> fields);

template<class Codec>
struct legacy_version_field_codec
{
    using type = Codec;
};

template<class Payload, std::size_t Width>
struct legacy_version_field_codec<nullable_codec<Payload, Width>>
{
    // Revision 1 bounds the entire field and writes no extra bytes after a null marker.
    using type = nullable_codec<Payload>;
};
}

/// Exact legacy Version metadata in UTF-8. A contract may begin with a zero representation byte.
template<std::size_t Size>
struct version_metadata
{
    char bytes[Size];

    constexpr version_metadata(const char (&text)[Size]) noexcept
    {
        for (std::size_t index = 0; index < Size; ++index)
            bytes[index] = text[index];
    }

    [[nodiscard]] constexpr std::string_view view() const noexcept { return {bytes, Size - 1}; }
};

/// Adds revision-1 metadata to an existing member and preserves its current descriptor and writers.
template<class Member, version_metadata Contract, version_metadata... Names>
struct legacy_version_member : Member
{
    static_assert(sizeof...(Names) > 0);
    static constexpr std::array<std::string_view, sizeof...(Names)> legacy_names{Names.view()...};
    static constexpr detail::legacy_version_field legacy_field{legacy_names, Contract.view()};

    template<class T, class... Context>
    static void read_legacy(reader& input, version_state& state, T& value, Context&... context)
    {
        using codec = typename detail::legacy_version_field_codec<typename Member::field_codec>::type;
        version_state::scope scope(state);
        auto decoded = detail::read_codec<codec>(input, state, context...);
        input.require_end();
        value.*(Member::field) = std::move(decoded);
    }
};
}
