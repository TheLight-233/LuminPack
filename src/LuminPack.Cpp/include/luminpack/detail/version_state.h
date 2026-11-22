#pragma once

#include <utility>

// Included by luminpack.h after the binary cursors and string profiles.
namespace luminpack
{
class version_state;

namespace detail
{
inline constexpr std::size_t maximum_version_fields = 249;

enum class version_read_form : std::uint8_t { modern, null_record, legacy };

struct version_read_header
{
    std::span<const std::uint64_t> descriptors;
    version_read_form form;
};

[[nodiscard]] version_read_header read_version_record_header(reader& input, version_state& state, string_profile profile);

constexpr std::uint64_t version_schema_hash(std::span<const std::uint64_t> descriptors) noexcept
{
    constexpr auto basis = std::uint64_t{14695981039346656037ull};
    constexpr auto prime = std::uint64_t{1099511628211ull};
    auto hash = (basis ^ descriptors.size()) * prime;
    for (auto descriptor : descriptors)
    {
        for (std::size_t byte = 0; byte < sizeof(descriptor); ++byte)
        {
            hash = (hash ^ (descriptor & 0xffu)) * prime;
            descriptor >>= 8;
        }
    }
    return hash;
}

void write_version_prehashed(writer& output, version_state& state, string_profile profile,
    std::uint64_t hash, std::span<const std::uint64_t> descriptors);
}

/// Schema registrations for one Version read or write operation.
class version_state
{
public:
    /// Discards registrations between independent documents.
    void reset() noexcept;

    class scope
    {
    public:
        explicit scope(version_state& state) noexcept : state_(state), size_(state.entries_.size()) {}
        ~scope() { if (!keep_) state_.truncate(size_); }
        scope(const scope&) = delete;
        scope& operator=(const scope&) = delete;

        /// Retains the registrations accumulated by a completed enclosing operation.
        void commit() noexcept { keep_ = true; }

    private:
        version_state& state_;
        std::size_t size_;
        bool keep_ = false;
    };

private:
    struct entry
    {
        std::uint64_t hash;
        std::vector<std::uint64_t> descriptors;
    };

    friend detail::version_read_header detail::read_version_record_header(
        reader& input, version_state& state, string_profile profile);
    friend void detail::write_version_prehashed(writer& output, version_state& state, string_profile profile,
        std::uint64_t hash, std::span<const std::uint64_t> descriptors);

    [[nodiscard]] std::span<const std::uint64_t> get(std::uint32_t ordinal) const;
    [[nodiscard]] std::optional<std::size_t> find(std::uint64_t hash, std::span<const std::uint64_t> descriptors);
    [[nodiscard]] std::span<const std::uint64_t> append(
        std::uint64_t hash, std::span<const std::uint64_t> descriptors, bool index_for_writes);
    void truncate(std::size_t size) noexcept;

    std::vector<entry> entries_;
    std::vector<std::uint32_t> index_;
    std::optional<std::size_t> last_;
    bool indexed_ = false;
};

/// Resolves a full or ordinal Version header. A null object has no descriptors.
[[nodiscard]] std::optional<std::span<const std::uint64_t>> read_version_header(
    reader& input, version_state& state, string_profile profile);

/// Emits a full Version header or an ordinal for an exact descriptor already in scope.
void write_version_header(writer& output, version_state& state, string_profile profile,
    std::span<const std::uint64_t> descriptors);

namespace detail
{
struct no_version_scope
{
    void commit() const noexcept {}
};

inline no_version_scope version_transaction() noexcept { return {}; }

inline version_state::scope version_transaction(version_state& state) noexcept
{
    return version_state::scope(state);
}

template<class ReferenceState>
class reference_version_scope
{
public:
    reference_version_scope(version_state& state, ReferenceState& references) noexcept
        : version_(state), references_(references) {}

    void commit() noexcept
    {
        version_.commit();
        references_.commit();
    }

private:
    version_state::scope version_;
    typename ReferenceState::scope references_;
};

template<class ReferenceState>
requires std::same_as<ReferenceState, reference_read_state> || std::same_as<ReferenceState, reference_write_state>
inline reference_version_scope<ReferenceState> version_transaction(version_state& state, ReferenceState& references) noexcept
{
    return reference_version_scope<ReferenceState>(state, references);
}

template<class Codec, class... Context>
decltype(auto) read_codec(reader& input, Context&... context)
{
    if constexpr (requires { Codec::read(input, context...); })
        return Codec::read(input, context...);
    else
        return Codec::read(input);
}

template<class Codec>
decltype(auto) read_codec(reader& input, version_state& state, reference_read_state& references)
{
    if constexpr (requires { Codec::read(input, state, references); })
        return Codec::read(input, state, references);
    else
        return detail::read_codec<Codec>(input, state);
}

template<class Codec, class Value, class... Context>
void write_codec(writer& output, const Value& value, Context&... context)
{
    if constexpr (requires { Codec::write(output, value, context...); })
        Codec::write(output, value, context...);
    else
        Codec::write(output, value);
}

template<class Codec, class Value>
void write_codec(writer& output, const Value& value, version_state& state, reference_write_state& references)
{
    if constexpr (requires { Codec::write(output, value, state, references); })
        Codec::write(output, value, state, references);
    else
        detail::write_codec<Codec>(output, value, state);
}
}
}
