#pragma once

#include <memory>
#include <typeinfo>

// Included by luminpack.h after the binary and JSON codec declarations.
namespace luminpack
{
/// Owns a polymorphic Union value and frames the C# case hash. Dispatch writes the hash and case payload.
template<class Base, class Dispatch>
struct polymorphic_union_codec
{
    static_assert(std::has_virtual_destructor_v<Base>);

    using value_type = std::unique_ptr<Base>;
    static constexpr auto minimum_size = sizeof(std::uint32_t);

    template<class... Context>
    static value_type read(reader& input, Context&... context)
    {
        reader next = input;
        const auto hash = next.read<std::uint32_t>();
        if (hash == 0)
        {
            input = next;
            return {};
        }

        auto transaction = detail::version_transaction(context...);
        auto result = Dispatch::read(next, hash, context...);
        if (!result)
            throw wire_error("Unknown LuminPack Union case hash.");
        input = next;
        transaction.commit();
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if (!value)
        {
            output.write<std::uint32_t>(0);
            return;
        }

        writer next = output;
        auto transaction = detail::version_transaction(context...);
        Dispatch::write(next, *value, context...);
        output = next;
        transaction.commit();
    }
};

/// Owns a polymorphic JSON Union value. Dispatch reads the case payload and writes the JSON envelope.
template<class Base, class Dispatch>
struct json_polymorphic_union_codec
{
    static_assert(std::has_virtual_destructor_v<Base>);

    using value_type = std::unique_ptr<Base>;

    static value_type read(auto& input)
    {
        return detail::read_json_union_envelope<value_type>(input, [](auto& payload, std::uint32_t hash)
        {
            auto result = Dispatch::read(payload, hash);
            if (!result)
                throw wire_error("Unknown LuminPack JSON Union case hash.");
            return result;
        });
    }

    static void write(auto& output, const value_type& value)
    {
        if (value)
            Dispatch::write(output, *value);
        else
            output.write_null();
    }
};

namespace detail
{
// Generated roots choose one slot for the compiled profile before the virtual call.
template<auto Slot, class Base>
bool try_write_virtual_binary_union(writer& output, Base& value, version_state& version,
    reference_write_state* references = nullptr)
{
    return (value.*Slot)(output, version, references);
}

template<auto Slot, class Base>
bool try_write_virtual_binary_union(writer& output, Base& value, version_state& version,
    reference_write_state& references)
{
    return try_write_virtual_binary_union<Slot>(output, value, version, &references);
}

template<auto Slot, class Base>
bool try_write_virtual_binary_union(writer& output, Base& value)
{
    version_state version;
    return try_write_virtual_binary_union<Slot>(output, value, version);
}

/// Carries the enclosing operation's registries into a graph-bearing virtual case payload.
template<class Codec, class Value>
void write_virtual_union_case_payload(writer& output, const Value& value, version_state& version,
    reference_write_state* references)
{
    if (references)
        write_codec<Codec>(output, value, version, *references);
    else
    {
        reference_write_state local;
        write_codec<Codec>(output, value, version, local);
    }
}

template<class Case, std::uint32_t Hash>
struct virtual_union_case
{
    using type = Case;
    static constexpr auto hash = Hash;
    static_assert(hash != 0 && hash != std::numeric_limits<std::uint32_t>::max());
};

/// Resolves a declared generic case group once per closed type for both write formats.
template<class Closed, class... Cases>
inline constexpr std::uint32_t virtual_union_case_hash = [] {
    constexpr auto matches = (std::size_t{0} + ... + static_cast<std::size_t>(std::same_as<Closed, typename Cases::type>));
    static_assert(matches <= 1, "A closed type must identify at most one virtual Union case.");
    auto hash = std::numeric_limits<std::uint32_t>::max();
    [[maybe_unused]] auto select = [&]<class Candidate> {
        if constexpr (std::same_as<Closed, typename Candidate::type>)
            hash = Candidate::hash;
    };
    (select.template operator()<Cases>(), ...);
    return hash;
}();

template<class Base, class ReadCase>
void read_graph_union(reader& input, Base*& destination, version_state& version,
    reference_read_state& references, ReadCase read_case)
{
    reader next = input;
    const auto hash = next.read<std::uint32_t>();
    if (hash == 0)
        destination = nullptr;
    else
    {
        auto transaction = version_transaction(version, references);
        destination = read_case(next, hash);
        transaction.commit();
    }
    input = next;
}

template<class Base, class WriteCase>
void write_graph_union(writer& output, Base* value, version_state& version,
    reference_write_state& references, WriteCase write_case)
{
    if (!value)
    {
        output.write<std::uint32_t>(0);
        return;
    }

    writer next = output;
    auto transaction = version_transaction(version, references);
    write_case(next, *value);
    output = next;
    transaction.commit();
}

template<class Base, class ReadCase>
Base* read_graph_json_union(auto& input, ReadCase read_case)
{
    auto next = input;
    reference_read_state::scope transaction(next.references());
    // A known tracked case may return null. Keep that distinct from an absent envelope payload.
    const auto result = read_json_union_envelope<std::optional<Base*>>(next, read_case);
    input = next;
    transaction.commit();
    return result.value_or(nullptr);
}

template<class Base, class WriteCase>
void write_graph_json_union(auto& output, Base* value, WriteCase write_case)
{
    if (!value)
    {
        output.write_null();
        return;
    }

    auto next = output;
    reference_write_state::scope transaction(next.references());
    write_case(next, *value);
    output = next;
    transaction.commit();
}
}

/// An RTTI Union with a tracked direct case. All case pointers borrow the owning document's graph.
template<class Base, class Dispatch>
struct graph_polymorphic_union_codec
{
    static_assert(std::has_virtual_destructor_v<Base>);

    using value_type = Base*;
    static constexpr auto minimum_size = sizeof(std::uint32_t);

    static value_type read(reader& input, version_state& version, reference_read_state& references)
    {
        value_type result = nullptr;
        read_into(input, result, version, references);
        return result;
    }

    static void read_into(reader& input, value_type& destination, version_state& version, reference_read_state& references)
    {
        detail::read_graph_union(input, destination, version, references, [&](reader& next, std::uint32_t hash)
        {
            return Dispatch::read(next, hash, destination, version, references);
        });
    }

    static void write(writer& output, value_type value, version_state& version, reference_write_state& references)
    {
        detail::write_graph_union(output, value, version, references, [&](writer& next, Base& object)
        {
            Dispatch::write(next, object, version, references);
        });
    }
};

/// The JSON envelope of an RTTI Union whose direct cases borrow one reference graph.
template<class Base, class Dispatch>
struct graph_json_polymorphic_union_codec
{
    static_assert(std::has_virtual_destructor_v<Base>);

    using value_type = Base*;

    static value_type read(auto& input)
    {
        return detail::read_graph_json_union<Base>(input, [](auto& payload, std::uint32_t hash) -> std::optional<Base*>
        {
            return Dispatch::read(payload, hash);
        });
    }

    static void write(auto& output, value_type value)
    {
        detail::write_graph_json_union(output, value, [](auto& next, Base& object)
        {
            Dispatch::write(next, object);
        });
    }
};
}
