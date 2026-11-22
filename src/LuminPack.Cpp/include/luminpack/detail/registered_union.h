#pragma once

#include <algorithm>
#include <mutex>
#include <typeindex>
#include <unordered_map>

// Included by luminpack.h after the binary and JSON Union codec declarations.
namespace luminpack
{
namespace detail
{
inline constexpr std::uint8_t registered_union_wide_marker = 250;
}

/// Writes the registered delegate's numeric tag after the Union writer's escape hash.
template<bool Wide>
void write_registered_union_tag(writer& output, std::uint16_t tag)
{
    if constexpr (Wide)
    {
        if (tag >= detail::registered_union_wide_marker)
        {
            output.write(detail::registered_union_wide_marker);
            output.write(tag);
            return;
        }
    }
    output.write(static_cast<std::uint8_t>(tag));
}

namespace detail
{
template<bool Wide>
std::uint16_t read_registered_union_tag(reader& input)
{
    const auto marker = input.read<std::uint8_t>();
    if constexpr (Wide)
    {
        if (marker == registered_union_wide_marker)
            return input.read<std::uint16_t>();
        // A managed wide-header probe assigns zero for the remaining reserved markers.
        if (marker > registered_union_wide_marker)
            return 0;
    }
    return marker;
}

inline version_state* registered_union_version() noexcept { return nullptr; }

template<class... Context>
inline version_state* registered_union_version(version_state& state, Context&...) noexcept { return &state; }

template<class Base>
Base* adopt_registered_union(reference_read_state& references, std::unique_ptr<Base> value)
{
    if (!value)
        throw wire_error("LuminPack Union case has no payload.");
    return references.own(std::move(value));
}
}

/// Registers app-owned callbacks by exact C++ runtime type and managed UInt16 tag.
template<class Policy>
class registered_union_registry
{
public:
    using base_type = typename Policy::base_type;
    static constexpr bool wide_tags = Policy::wide_tags;
    static_assert(std::has_virtual_destructor_v<base_type>);

    /// Binding reads a concrete payload and writes its numeric tag or JSON envelope as well as its payload.
    /// Binary operations may also accept the current version_state pointer.
    template<class Case, class Binding>
    requires std::derived_from<Case, base_type>
    static void register_case(int tag)
    {
        if (tag < 0 || tag > std::numeric_limits<std::uint16_t>::max())
            throw std::invalid_argument("A LuminPack Union tag must fit in UInt16.");
        const auto numeric_tag = static_cast<std::uint16_t>(tag);
        if (std::ranges::find(Policy::reserved_tags, numeric_tag) != Policy::reserved_tags.end())
            throw std::invalid_argument("This LuminPack Union tag belongs to a generated case.");

        const entry operations{numeric_tag, &read_binary<Binding>, &write_binary<Case, Binding>,
            &read_json<json_encoding::utf8, Binding>, &write_json<json_encoding::utf8, Case, Binding>,
            &read_json<json_encoding::utf16, Binding>, &write_json<json_encoding::utf16, Case, Binding>};
        const std::lock_guard lock(table().mutex);
        auto& by_type = table().by_type;
        const auto [type_slot, added] = by_type.try_emplace(std::type_index(typeid(Case)), numeric_tag);
        if (!added)
            throw std::invalid_argument("This LuminPack Union member already has a registration.");
        try
        {
            if (!table().by_tag.try_emplace(numeric_tag, operations).second)
                throw std::invalid_argument("This LuminPack Union tag is already registered.");
        }
        catch (...)
        {
            by_type.erase(type_slot);
            throw;
        }
    }

private:
    template<class, class>
    friend struct registered_polymorphic_union_codec;

    template<class, class>
    friend struct registered_json_polymorphic_union_codec;

    template<class, class>
    friend struct registered_graph_polymorphic_union_codec;

    template<class, class>
    friend struct registered_graph_json_polymorphic_union_codec;

    using pointer = std::unique_ptr<base_type>;

    template<json_encoding Encoding>
    using json_reader_type = basic_json_reader<Encoding>;

    template<json_encoding Encoding>
    using json_writer_type = basic_json_writer<Encoding>;

    struct entry
    {
        std::uint16_t tag;
        pointer (*read_binary)(reader&, version_state*);
        void (*write_binary)(writer&, std::uint16_t, const base_type&, version_state*);
        pointer (*read_utf8)(json_reader_type<json_encoding::utf8>&);
        void (*write_utf8)(json_writer_type<json_encoding::utf8>&, std::uint16_t, const base_type&);
        pointer (*read_utf16)(json_reader_type<json_encoding::utf16>&);
        void (*write_utf16)(json_writer_type<json_encoding::utf16>&, std::uint16_t, const base_type&);
    };

    struct registry_table
    {
        std::mutex mutex;
        std::unordered_map<std::uint16_t, entry> by_tag;
        std::unordered_map<std::type_index, std::uint16_t> by_type;
    };

    static registry_table& table()
    {
        static registry_table registry;
        return registry;
    }

    static std::optional<entry> lookup(std::uint16_t tag)
    {
        const std::lock_guard lock(table().mutex);
        const auto found = table().by_tag.find(tag);
        if (found == table().by_tag.end())
            return std::nullopt;
        return found->second;
    }

    static std::optional<entry> lookup(std::type_index type)
    {
        const std::lock_guard lock(table().mutex);
        const auto found = table().by_type.find(type);
        if (found == table().by_type.end())
            return std::nullopt;
        return table().by_tag.at(found->second);
    }

    template<class Case>
    static const Case& concrete(const base_type& value)
    {
        if constexpr (requires { static_cast<const Case&>(value); })
            return static_cast<const Case&>(value);
        else
            return dynamic_cast<const Case&>(value);
    }

    template<class Binding>
    static pointer read_binary(reader& input, version_state* context)
    {
        if constexpr (requires { Binding::read_binary(input, context); })
            return Binding::read_binary(input, context);
        else
            return Binding::read_binary(input);
    }

    template<class Case, class Binding>
    static void write_binary(writer& output, std::uint16_t tag, const base_type& value, version_state* context)
    {
        if constexpr (requires { Binding::write_binary(output, tag, concrete<Case>(value), context); })
            Binding::write_binary(output, tag, concrete<Case>(value), context);
        else
            Binding::write_binary(output, tag, concrete<Case>(value));
    }

    template<json_encoding Encoding, class Binding>
    static pointer read_json(json_reader_type<Encoding>& input)
    {
        return Binding::read_json(input);
    }

    template<json_encoding Encoding, class Case, class Binding>
    static void write_json(json_writer_type<Encoding>& output, std::uint16_t tag, const base_type& value)
    {
        Binding::write_json(output, tag, concrete<Case>(value));
    }
};

/// Uses generated case hashes first and dispatches escaped Binary cases through the typed registry.
template<class Policy, class Dispatch>
struct registered_polymorphic_union_codec
{
    using registry = registered_union_registry<Policy>;
    using value_type = std::unique_ptr<typename Policy::base_type>;
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
        value_type result;
        if (hash == std::numeric_limits<std::uint32_t>::max())
        {
            const auto tag = detail::read_registered_union_tag<Policy::wide_tags>(next);
            const auto found = registry::lookup(tag);
            if (!found)
                throw wire_error("Unknown registered LuminPack Union tag.");
            result = found->read_binary(next, detail::registered_union_version(context...));
            if (!result)
                throw wire_error("LuminPack Union case has no payload.");
        }
        else
        {
            result = Dispatch::read(next, hash, context...);
            if constexpr (!detail::nullable_union_case<Dispatch>)
            {
                if (!result)
                    throw wire_error("LuminPack Union case has no payload.");
            }
        }
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
        if (!Dispatch::try_write(next, *value, context...))
        {
            const auto found = registry::lookup(std::type_index(typeid(*value)));
            if (!found)
                throw std::invalid_argument("Unknown LuminPack Union runtime type.");
            next.write(std::numeric_limits<std::uint32_t>::max());
            found->write_binary(next, found->tag, *value, detail::registered_union_version(context...));
        }
        output = next;
        transaction.commit();
    }
};

/// Uses generated JSON cases first, with registered numeric tags in the same envelope.
template<class Policy, class Dispatch>
struct registered_json_polymorphic_union_codec
{
    using registry = registered_union_registry<Policy>;
    using value_type = std::unique_ptr<typename Policy::base_type>;

    static value_type read(auto& input)
    {
        constexpr bool nullable = detail::nullable_union_case<Dispatch>;
        using case_result = std::conditional_t<nullable, std::optional<value_type>, value_type>;
        const auto read_case = [](auto& payload, std::uint32_t hash) -> case_result
        {
            if (auto result = Dispatch::try_read(payload, hash))
                return result;
            if (hash <= std::numeric_limits<std::uint16_t>::max())
            {
                if (const auto found = registry::lookup(static_cast<std::uint16_t>(hash)))
                {
                    auto result = [&]() {
                        if constexpr (std::same_as<typename std::remove_cvref_t<decltype(payload)>::unit, char>)
                            return found->read_utf8(payload);
                        else
                            return found->read_utf16(payload);
                    }();
                    if constexpr (nullable)
                    {
                        if (!result)
                            throw wire_error("LuminPack JSON Union has no payload.");
                        return case_result(std::in_place, std::move(result));
                    }
                    else
                        return result;
                }
            }
            throw wire_error("Unknown LuminPack JSON Union case hash or tag.");
        };
        if constexpr (nullable)
            return detail::read_nullable_json_union_envelope<value_type>(input, read_case);
        else
            return detail::read_json_union_envelope<value_type>(input, read_case);
    }

    static void write(auto& output, const value_type& value)
    {
        if (!value)
        {
            output.write_null();
            return;
        }
        if (!Dispatch::try_write(output, *value))
        {
            const auto found = registry::lookup(std::type_index(typeid(*value)));
            if (!found)
                throw std::invalid_argument("Unknown LuminPack Union runtime type.");
            if constexpr (std::same_as<typename std::remove_cvref_t<decltype(output)>::unit, char>)
                found->write_utf8(output, found->tag, *value);
            else
                found->write_utf16(output, found->tag, *value);
        }
    }
};

/// A graph-owned RTTI Union with existing typed registration fallback for escaped Binary tags.
template<class Policy, class Dispatch>
struct registered_graph_polymorphic_union_codec
{
    using registry = registered_union_registry<Policy>;
    using base_type = typename Policy::base_type;
    using value_type = base_type*;
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
            if (hash != std::numeric_limits<std::uint32_t>::max())
                return Dispatch::read(next, hash, destination, version, references);

            const auto tag = detail::read_registered_union_tag<Policy::wide_tags>(next);
            const auto found = registry::lookup(tag);
            if (!found)
                throw wire_error("Unknown registered LuminPack Union tag.");
            return detail::adopt_registered_union(references, found->read_binary(next, &version));
        });
    }

    static void write(writer& output, value_type value, version_state& version, reference_write_state& references)
    {
        detail::write_graph_union(output, value, version, references, [&](writer& next, base_type& object)
        {
            if (Dispatch::try_write(next, object, version, references))
                return;

            const auto found = registry::lookup(std::type_index(typeid(object)));
            if (!found)
                throw std::invalid_argument("Unknown LuminPack Union runtime type.");
            next.write(std::numeric_limits<std::uint32_t>::max());
            found->write_binary(next, found->tag, object, &version);
        });
    }
};

/// A graph-owned RTTI JSON Union with generated cases preceding typed registered delegates.
template<class Policy, class Dispatch>
struct registered_graph_json_polymorphic_union_codec
{
    using registry = registered_union_registry<Policy>;
    using base_type = typename Policy::base_type;
    using value_type = base_type*;

    static value_type read(auto& input)
    {
        return detail::read_graph_json_union<base_type>(input, [](auto& payload, std::uint32_t hash) -> std::optional<value_type>
        {
            if (auto result = Dispatch::try_read(payload, hash))
                return result;
            if (hash <= std::numeric_limits<std::uint16_t>::max())
            {
                if (const auto found = registry::lookup(static_cast<std::uint16_t>(hash)))
                {
                    if constexpr (std::same_as<typename std::remove_cvref_t<decltype(payload)>::unit, char>)
                        return detail::adopt_registered_union(payload.references(), found->read_utf8(payload));
                    else
                        return detail::adopt_registered_union(payload.references(), found->read_utf16(payload));
                }
            }
            throw wire_error("Unknown LuminPack JSON Union case hash or tag.");
        });
    }

    static void write(auto& output, value_type value)
    {
        detail::write_graph_json_union(output, value, [](auto& next, base_type& object)
        {
            if (Dispatch::try_write(next, object))
                return;

            const auto found = registry::lookup(std::type_index(typeid(object)));
            if (!found)
                throw std::invalid_argument("Unknown LuminPack Union runtime type.");
            if constexpr (std::same_as<typename std::remove_cvref_t<decltype(next)>::unit, char>)
                found->write_utf8(next, found->tag, object);
            else
                found->write_utf16(next, found->tag, object);
        });
    }
};
}
