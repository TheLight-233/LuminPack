#pragma once

// Included by luminpack.h after the collection and Version state declarations.
namespace luminpack
{
/// A nullable field uses marker 0 for a value and 255 for null. A fixed Version slot pads nulls.
template<class Payload, std::size_t Width = 0>
struct nullable_codec
{
    using value_type = std::optional<typename Payload::value_type>;
    static_assert(Width == 0 || Width == 1 + Payload::minimum_size);
    static constexpr std::size_t minimum_size = Width == 0 ? 1 : Width;

    template<class... Context>
    static value_type read(reader& input, Context&... context)
    {
        reader next = input;
        auto transaction = detail::version_transaction(context...);
        const auto marker = next.read<std::uint8_t>();
        value_type result;
        if (marker == 0)
            result.emplace(detail::read_codec<Payload>(next, context...));
        else if (marker != 255)
            throw wire_error("Invalid LuminPack nullable marker.");
        else if constexpr (Width != 0)
            (void)next.take(Width - 1);
        input = next;
        transaction.commit();
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        writer next = output;
        auto transaction = detail::version_transaction(context...);
        next.write<std::uint8_t>(value ? 0 : 255);
        if (value)
            detail::write_codec<Payload>(next, *value, context...);
        else if constexpr (Width != 0)
            std::ranges::fill(next.reserve(Width - 1), std::byte{});
        output = next;
        transaction.commit();
    }
};

/// Describes one CLR nullable storage model. All three values are byte counts or offsets.
struct nullable_storage_layout
{
    std::size_t stride;
    std::size_t presence;
    std::size_t payload;

    constexpr bool valid(std::size_t width) const noexcept
    {
        return presence < stride && payload < stride && width <= stride - payload &&
            (presence < payload || presence - payload >= width);
    }
};

namespace detail
{
template<class Payload>
struct nullable_storage_record
{
    using item_type = typename Payload::value_type;
    using value_type = std::optional<item_type>;
    static constexpr bool boolean = std::same_as<Payload, boolean_codec>;
    static constexpr std::size_t width = Payload::minimum_size;
    static_assert(scalar<item_type> || std::is_enum_v<item_type> || boolean);
    static_assert(width == sizeof(item_type));

    static void validate(std::span<const std::byte> source, nullable_storage_layout layout)
    {
        if (source[layout.presence] > std::byte{1} || (boolean && source[layout.payload] > std::byte{1}))
            throw wire_error("Invalid LuminPack nullable storage flag.");
    }

    static value_type read(std::span<const std::byte> source, nullable_storage_layout layout)
    {
        if (source[layout.presence] == std::byte{})
            return std::nullopt;
        if constexpr (boolean)
            return source[layout.payload] == std::byte{1};
        else
        {
            item_type value;
            std::memcpy(&value, source.data() + layout.payload, width);
            return value;
        }
    }

    static void write(std::span<std::byte> target, nullable_storage_layout layout, const value_type& value)
    {
        std::ranges::fill(target, std::byte{});
        if (!value)
            return;
        target[layout.presence] = std::byte{1};
        if constexpr (boolean)
            target[layout.payload] = *value ? std::byte{1} : std::byte{};
        else
            std::memcpy(target.data() + layout.payload, &*value, width);
    }
};
}

/// A keyed nullable value stored with the selected CLR stride and field offsets.
template<class Payload, nullable_storage_layout Layout>
struct nullable_storage_codec
{
    using storage = detail::nullable_storage_record<Payload>;
    using value_type = typename storage::value_type;
    static_assert(Layout.valid(storage::width));
    static constexpr std::size_t minimum_size = Layout.stride;

    static value_type read(reader& input)
    {
        reader next = input;
        const auto source = next.take(Layout.stride);
        storage::validate(source, Layout);
        auto result = storage::read(source, Layout);
        input = next;
        return result;
    }

    static void write(writer& output, const value_type& value)
    {
        const auto snapshot = value;
        storage::write(output.reserve(Layout.stride), Layout, snapshot);
    }
};

/// Transfers a counted Nullable<T> block with checked CLR models. Multi-model Tagged blocks carry a one-based tag.
template<class Payload, nullable_storage_layout... Layouts>
struct nullable_list_codec
{
    using storage = detail::nullable_storage_record<Payload>;
    using element_type = typename storage::value_type;
    using value_type = std::optional<std::vector<element_type>>;
    static_assert(sizeof...(Layouts) > 0 && sizeof...(Layouts) <= 255 && (Layouts.valid(storage::width) && ...));
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);

    static value_type read(reader& input)
    {
        value_type result;
        read_into(input, result);
        return result;
    }

    /// Checks width and flags before converting to the standard library's optional representation.
    static void read_into(reader& input, value_type& destination)
    {
        reader next = input;
        const auto count = next.read_collection_count();
        if (!count)
            destination.reset();
        else if (*count == 0)
        {
            if (destination)
                destination->clear();
            else
                destination.emplace();
        }
        else
        {
            const auto layout = read_layout(next);
            auto source = next.take(detail::checked_input_width(*count, layout.stride, next.remaining()));
            for (std::size_t index = 0; index < *count; ++index)
                storage::validate(source.subspan(index * layout.stride, layout.stride), layout);

            auto& items = destination ? *destination : destination.emplace();
            std::vector<std::byte> scratch;
            if (detail::overlaps(source, std::as_bytes(std::span(items))))
            {
                scratch.assign(source.begin(), source.end());
                source = scratch;
            }
            items.resize(*count);
            for (std::size_t index = 0; index < *count; ++index)
                items[index] = storage::read(source.subspan(index * layout.stride, layout.stride), layout);
        }
        input = next;
    }

    static void write(writer& output, const value_type& value)
    {
        if (!value || value->empty())
        {
            output.write<std::int32_t>(value ? 0 : -1);
            return;
        }

        constexpr auto layout = models.front();
        constexpr std::size_t header = minimum_size + (tagged ? 1 : 0);
        const auto count = detail::collection_count(value->size());
        if (output.remaining() < header)
            detail::short_output();
        const auto size = detail::checked_output_width(value->size(), layout.stride, output.remaining() - header);
        writer next = output;
        const auto target = next.reserve(header + size);
        std::span<const element_type> items(*value);
        std::vector<element_type> scratch;
        if (detail::overlaps(std::as_bytes(items), target))
        {
            scratch.assign(items.begin(), items.end());
            items = scratch;
        }
        detail::store_count(target, count);
        if constexpr (tagged)
            target[minimum_size] = std::byte{1};
        for (std::size_t index = 0; index < items.size(); ++index)
            storage::write(target.subspan(header + index * layout.stride, layout.stride), layout, items[index]);
        output = next;
    }

private:
    static constexpr std::array models{Layouts...};
    static constexpr bool tagged = models.size() > 1;

    static nullable_storage_layout read_layout(reader& input)
    {
        if constexpr (!tagged)
            return models.front();
        else
        {
            const auto tag = input.read<std::uint8_t>();
            if (tag == 0 || tag > models.size())
                throw wire_error("Unknown LuminPack nullable storage tag.");
            return models[tag - 1];
        }
    }
};
}
