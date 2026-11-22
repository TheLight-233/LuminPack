#pragma once

// Included by luminpack.h after reference state and the static JSON codecs.
namespace luminpack
{
/// A checked JSON cursor for an operation that owns a reference graph.
template<json_encoding Encoding>
class reference_json_reader : public basic_json_reader<Encoding>
{
public:
    reference_json_reader(std::span<const std::byte> input, json_read_options options, reference_read_state& references)
        : base(input, options), references_(&references) {}

    [[nodiscard]] reference_read_state& references() const noexcept { return *references_; }

    /// A deferred value borrows the enclosing document's graph and depth limit.
    [[nodiscard]] reference_json_reader take_value()
    {
        return reference_json_reader(base::take_value(), *references_);
    }

private:
    using base = basic_json_reader<Encoding>;

    reference_json_reader(base input, reference_read_state& references) noexcept
        : base(std::move(input)), references_(&references) {}

    reference_read_state* references_;
};

/// A checked JSON writer with a document-local object identity table.
template<json_encoding Encoding>
class reference_json_writer : public basic_json_writer<Encoding>
{
public:
    reference_json_writer(std::span<std::byte> output, reference_write_state& references) noexcept
        : basic_json_writer<Encoding>(output), references_(&references) {}

    [[nodiscard]] reference_write_state& references() const noexcept { return *references_; }

private:
    reference_write_state* references_;
};

namespace detail
{
inline constexpr auto json_reference_id_name = json_name{u"$id"};
inline constexpr auto json_reference_ref_name = json_name{u"$ref"};

template<class Codec>
inline constexpr bool tracked_json_null_scalar = false;

template<scalar T>
inline constexpr bool tracked_json_null_scalar<json_codec<scalar_codec<T>>> = true;

template<>
inline constexpr bool tracked_json_null_scalar<json_codec<boolean_codec>> = true;

template<class T>
inline constexpr bool tracked_json_null_scalar<json_codec<enum_codec<T>>> = true;

template<class Member>
struct tracked_json_member;

template<json_name Name, auto Field, class Codec, json_name... FormerNames>
struct tracked_json_member<json_record_member<Name, Field, Codec, FormerNames...>>
{
    using value_type = typename Codec::value_type;

    static value_type read(auto& input)
    {
        if constexpr (tracked_json_null_scalar<Codec>)
        {
            if (input.consume_null())
                return {};
        }
        return Codec::read(input);
    }

    template<class T>
    static constexpr bool nothrow_publish = std::is_nothrow_assignable_v<decltype(std::declval<T&>().*Field), value_type&&>;

    template<class T>
    static void publish(T& value, value_type& field) noexcept(nothrow_publish<T>)
    {
        value.*Field = std::move(field);
    }
};
}

/// Generated JSON using the existing reference metadata and a graph-owned object shell.
template<class T, class Lookup, class... Members>
struct tracked_json_codec
{
    using value_type = T*;

    static value_type read(auto& input)
    {
        value_type value = nullptr;
        read_into(input, value);
        return value;
    }

    /// Reads and reuses an unregistered caller-owned shell when supplied.
    static void read_into(auto& input, value_type& destination)
    {
        if (input.consume_null())
        {
            destination = nullptr;
            return;
        }

        auto& state = input.references();
        reference_read_state::scope transaction(state);
        constexpr bool can_reuse = (detail::tracked_json_member<Members>::template nothrow_publish<T> && ...);
        const bool reuse = can_reuse && destination && !state.registered(destination);
        auto pending = reuse ? std::unique_ptr<T>{} : std::make_unique<T>();
        auto* shell = reuse ? destination : pending.get();
        staged_fields fields{};
        const auto container = input.begin('{');
        if (!input.consume('}'))
        {
            do
            {
                const auto name = input.read_name();
                input.expect(':');
                const auto property = Lookup::find(name);
                if (property == id_property)
                {
                    const auto id = input.template read_number<std::uint32_t>();
                    if (pending)
                        shell = state.own(std::move(pending));
                    state.add(id, shell);
                }
                else if (property == ref_property)
                {
                    const auto id = input.template read_number<std::uint32_t>();
                    auto* existing = state.template get<T>(id);
                    while (input.consume(','))
                    {
                        (void)input.read_name();
                        input.expect(':');
                        input.skip_value();
                    }
                    input.expect('}');
                    destination = existing;
                    transaction.commit();
                    return;
                }
                else if (property < sizeof...(Members))
                    read_field(input, fields, property, indices{});
                else
                    input.skip_value();
            }
            while (input.consume(','));
            input.expect('}');
        }

        if (pending)
            shell = state.own(std::move(pending));
        publish(*shell, fields, indices{});
        destination = shell;
        transaction.commit();
    }

    static void write(auto& output, const value_type& value)
    {
        if (!value)
        {
            output.write_null();
            return;
        }

        const auto id = output.references().track(value);
        output.token('{');
        if (id < 0)
        {
            output.template write_property<detail::json_reference_ref_name>();
            output.write_number(static_cast<std::uint32_t>(~id));
        }
        else
        {
            output.template write_property<detail::json_reference_id_name>();
            output.write_number(static_cast<std::uint32_t>(id));
            [[maybe_unused]] bool first = false;
            (Members::write(output, *value, first), ...);
        }
        output.token('}');
    }

private:
    static constexpr std::size_t id_property = sizeof...(Members) + 1;
    static constexpr std::size_t ref_property = sizeof...(Members) + 2;
    using staged_fields = std::tuple<typename detail::tracked_json_member<Members>::value_type...>;
    using indices = std::index_sequence_for<Members...>;

    template<std::size_t Index, class Member, class Reader>
    static void read_member(Reader& input, staged_fields& fields)
    {
        std::get<Index>(fields) = detail::tracked_json_member<Member>::read(input);
    }

    template<class Reader, std::size_t... Index>
    static void read_field(Reader& input, staged_fields& fields, std::size_t property, std::index_sequence<Index...>)
    {
        using operation = void (*)(Reader&, staged_fields&);
        static constexpr std::array<operation, sizeof...(Members)> reads{&read_member<Index, Members, Reader>...};
        reads[property](input, fields);
    }

    template<std::size_t... Index>
    static void publish(T& value, staged_fields& fields, std::index_sequence<Index...>)
    {
        (detail::tracked_json_member<Members>::publish(value, std::get<Index>(fields)), ...);
    }
};

/// Reads one JSON document and retains every object used by its borrowed references.
template<class Codec, json_encoding Encoding = json_encoding::utf16>
[[nodiscard]] reference_document<typename Codec::value_type> read_reference_json_document(
    std::span<const std::byte> input, json_read_options options = {})
{
    detail::check_document_size(input.size(), options.max_input_bytes);
    reference_document<typename Codec::value_type> result;
    reference_read_state state(result.graph);
    reference_json_reader<Encoding> cursor(input, options, state);
    result.value = Codec::read(cursor);
    cursor.require_end();
    return result;
}

/// Writes one JSON document with an independent table of object identities.
template<class Codec, json_encoding Encoding = json_encoding::utf16>
[[nodiscard]] std::span<const std::byte> write_reference_json_document(
    std::span<std::byte> output, const typename Codec::value_type& value)
{
    reference_write_state state;
    reference_json_writer<Encoding> cursor(output, state);
    Codec::write(cursor, value);
    return cursor.written();
}
}
