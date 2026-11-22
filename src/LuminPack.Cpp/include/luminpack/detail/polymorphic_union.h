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
}
