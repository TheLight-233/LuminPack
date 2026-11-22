#pragma once

// Included by luminpack.h after the raw storage operations and collection codecs.
namespace luminpack
{
/// The local strides and field addresses for an aggregate in declaration-order Packed storage.
template<std::size_t Fields>
struct packed_layout
{
    std::array<std::size_t, Fields> widths;
    storage_model<Fields> local;

    [[nodiscard]] constexpr storage_model<Fields> packed_model() const noexcept
    {
        storage_model<Fields> packed{};
        for (std::size_t field = 0; field < Fields; ++field)
        {
            packed.offsets[field] = packed.stride;
            packed.stride += widths[field];
        }
        return packed;
    }

    [[nodiscard]] constexpr bool valid() const noexcept
    {
        if (Fields == 0 || !detail::valid_storage_model(widths, local))
            return false;
        // Disjoint local fields together cannot exceed the local stride.
        return detail::valid_storage_model(widths, packed_model());
    }
};

namespace detail
{
template<auto Layout, auto BooleanFields>
consteval bool valid_boolean_storage()
{
    if constexpr (BooleanFields.size() == 0)
        return true;
    else if constexpr (sizeof(bool) != 1)
        return false;
    else
    {
        for (const auto field : BooleanFields)
            if (field >= Layout.widths.size() || Layout.widths[field] != 1)
                return false;
        return std::bit_cast<std::byte>(false) == std::byte{0} && std::bit_cast<std::byte>(true) == std::byte{1};
    }
}

template<auto BooleanFields, std::size_t Fields>
void check_storage_booleans(std::span<const std::byte> payload, std::size_t count, const storage_model<Fields>& model)
{
    if constexpr (BooleanFields.size() != 0)
    {
        for (std::size_t element = 0; element < count; ++element)
        {
            const auto* record = payload.data() + element * model.stride;
            for (const auto field : BooleanFields)
                if (std::to_integer<unsigned>(record[model.offsets[field]]) > 1)
                    throw wire_error("Invalid LuminPack Boolean.");
        }
    }
}

template<auto Layout, bool Write>
void transfer_packed(std::span<const std::byte> source, std::span<std::byte> destination, std::size_t count)
{
    constexpr auto packed = Layout.packed_model();
    if (source.empty())
        return;
    if constexpr (packed == Layout.local)
        std::memmove(destination.data(), source.data(), source.size());
    else if constexpr (Write)
        convert_layout<Layout>(source, destination, count, Layout.local, packed, false);
    else
        convert_layout<Layout>(source, destination, count, packed, Layout.local, false);
}
}

/// A standalone portable aggregate, including a value whose contiguous collections are Tagged.
template<class T, auto Layout, auto BooleanFields = std::array<std::size_t, 0>{}>
struct packed_struct_codec
{
    static_assert(std::is_trivially_copyable_v<T> && std::is_standard_layout_v<T>);
    static_assert(Layout.valid() && Layout.local.stride == sizeof(T));
    static_assert(detail::valid_boolean_storage<Layout, BooleanFields>());

    using value_type = T;
    static constexpr std::size_t minimum_size = Layout.packed_model().stride;

    static T read(reader& input)
    {
        reader next = input;
        const auto payload = next.take(minimum_size);
        detail::check_storage_booleans<BooleanFields>(payload, 1, Layout.packed_model());
        T value{};
        detail::transfer_packed<Layout, false>(payload, std::as_writable_bytes(std::span(&value, 1)), 1);
        input = next;
        return value;
    }

    static void write(writer& output, const T& value)
    {
        writer next = output;
        const auto target = next.reserve(minimum_size);
        detail::transfer_packed<Layout, true>(std::as_bytes(std::span(&value, 1)), target, 1);
        output = next;
    }
};

/// The contiguous Packed or Tagged block of an array or List<T>. A dictionary value uses the standalone codec.
template<class T, auto Layout, auto BooleanFields = std::array<std::size_t, 0>{}>
struct storage_list_codec
{
    static_assert(std::is_trivially_copyable_v<T> && std::is_standard_layout_v<T>);
    static_assert(Layout.valid() && Layout.local.stride == sizeof(T));
    static_assert(detail::valid_boolean_storage<Layout, BooleanFields>());

    using value_type = std::optional<std::vector<T>>;
    static constexpr std::size_t minimum_size = sizeof(std::int32_t);

    [[nodiscard]] static value_type read(reader& input)
    {
        value_type result;
        read_into(input, result);
        return result;
    }

    /// Checks the complete storage block and Boolean fields before resizing the destination.
    static void read_into(reader& input, value_type& destination)
    {
        reader next = input;
        auto block = read_block(next);
        if (block.count)
        {
            auto& items = destination ? *destination : destination.emplace();
            std::vector<std::byte> scratch;
            if (*block.count != items.size() && detail::overlaps(block.payload, std::as_bytes(std::span(items))))
            {
                scratch.assign(block.payload.begin(), block.payload.end());
                block.payload = scratch;
            }
            items.resize(*block.count);
            const auto bytes = std::as_writable_bytes(std::span(items));
            if constexpr (tagged)
                detail::transfer_tagged_block<Layout>(block, bytes);
            else
                detail::transfer_packed<Layout, false>(block.payload, bytes, *block.count);
        }
        else
            destination.reset();
        input = next;
    }

    static void write(writer& output, const value_type& value)
    {
        if (!value)
            output.write<std::int32_t>(-1);
        else
            write_elements(output, *value);
    }

    static void write_elements(writer& output, const std::vector<T>& value)
    {
        if constexpr (tagged)
            write_tagged_array<T, Layout>(output, std::span<const T>(value));
        else
        {
            const auto count = detail::collection_count(value.size());
            if (output.remaining() < minimum_size)
                detail::short_output();
            const auto width = detail::checked_output_width(value.size(), Layout.packed_model().stride,
                output.remaining() - minimum_size);
            writer next = output;
            const auto record = next.reserve(minimum_size + width);
            detail::transfer_packed<Layout, true>(std::as_bytes(std::span(value)), record.subspan(minimum_size), value.size());
            detail::store_count(record, count);
            output = next;
        }
    }

private:
    static constexpr bool tagged = requires { Layout.wire; };

    static detail::tagged_block read_block(reader& input)
    {
        if constexpr (tagged)
        {
            const auto block = detail::read_tagged_block<Layout>(input);
            if (block.tag != 0)
                detail::check_storage_booleans<BooleanFields>(block.payload, *block.count, Layout.wire[block.tag - 1]);
            return block;
        }
        else
        {
            constexpr auto packed = Layout.packed_model();
            const auto count = input.read_collection_count(packed.stride);
            if (!count)
                return {};
            const auto payload = input.take(*count * packed.stride);
            detail::check_storage_booleans<BooleanFields>(payload, *count, packed);
            return {count, payload};
        }
    }
};
}
