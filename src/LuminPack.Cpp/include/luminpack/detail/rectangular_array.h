#pragma once

// Included by luminpack.h after the binary and JSON sequence codecs.
namespace luminpack
{
/// A CLR rectangular array in row-major order.
template<class T, std::size_t Rank>
struct rectangular_array
{
    static_assert(Rank > 1 && Rank <= 32);

    std::array<std::int32_t, Rank> lengths{};
    std::array<std::int32_t, Rank> lower_bounds{};
    std::vector<T> values;
};

namespace detail
{
template<class Error, std::size_t Rank>
std::size_t rectangular_count(const std::array<std::int32_t, Rank>& lengths)
{
    bool empty = false;
    for (const auto length : lengths)
    {
        if (length < 0)
            throw Error("Negative LuminPack rectangular array dimension.");
        empty |= length == 0;
    }
    if (empty)
        return 0;

    auto count = std::int64_t{1};
    for (const auto length : lengths)
    {
        count *= length;
        if (count > std::numeric_limits<std::int32_t>::max())
            throw Error("LuminPack rectangular array dimensions exceed the supported element count.");
    }
    return static_cast<std::size_t>(count);
}

template<class Error, class T, std::size_t Rank>
void check_rectangular_values(const rectangular_array<T, Rank>& value)
{
    if (rectangular_count<Error>(value.lengths) != value.values.size())
        throw Error("LuminPack rectangular array dimensions and element count differ.");
}

inline constexpr auto rectangular_lengths = json_name{u"Lengths"};
inline constexpr auto rectangular_lower_bounds = json_name{u"LowerBounds"};
inline constexpr auto rectangular_values = json_name{u"Values"};
}

/// CLR binary uses a non-null object marker, the dimensions, and one contiguous element record.
template<std::size_t Rank, class Sequence>
struct rectangular_array_codec
{
    using sequence_type = typename Sequence::value_type::value_type;
    using value_type = std::optional<rectangular_array<typename sequence_type::value_type, Rank>>;
    static constexpr std::size_t minimum_size = 1;

    template<class... Context>
    static value_type read(reader& input, Context&... context)
    {
        reader next = input;
        if (next.read<std::uint8_t>() == 255)
        {
            input = next;
            return std::nullopt;
        }

        typename value_type::value_type result;
        for (auto& length : result.lengths)
            length = next.read<std::int32_t>();
        reader count_window = next;
        const auto count = count_window.read_collection_count();
        if (!count || *count != detail::rectangular_count<wire_error>(result.lengths))
            throw wire_error("LuminPack rectangular array dimensions and element count differ.");

        auto values = detail::read_codec<Sequence>(next, context...);
        result.values = std::move(*values);
        input = next;
        return result;
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if (!value)
        {
            write_object_header(output, std::nullopt);
            return;
        }

        detail::check_rectangular_values<std::invalid_argument>(*value);
        for (const auto lower_bound : value->lower_bounds)
            if (lower_bound != 0)
                throw std::invalid_argument("LuminPack binary rectangular arrays require zero lower bounds.");

        writer next = output;
        write_object_header(next, 0);
        for (const auto length : value->lengths)
            next.write(length);
        memory_codec<Sequence>::write(next, value->values, context...);
        output = next;
    }
};

/// CLR JSON uses the Lengths, LowerBounds and Values envelope in both document encodings.
template<std::size_t Rank, class Sequence>
struct json_rectangular_array_codec
{
    using sequence_type = typename Sequence::value_type::value_type;
    using value_type = std::optional<rectangular_array<typename sequence_type::value_type, Rank>>;

    static value_type read(auto& input)
    {
        if (input.consume_null())
            return std::nullopt;
        const auto container = input.begin('{');
        std::optional<dimensions> lengths;
        std::optional<dimensions> lower_bounds;
        std::optional<sequence_type> values;
        if (!input.consume('}'))
        {
            do
            {
                const auto key = input.read_name();
                input.expect(':');
                if (key.template matches<detail::rectangular_lengths>())
                    lengths = read_dimensions(input);
                else if (key.template matches<detail::rectangular_lower_bounds>())
                    lower_bounds = read_dimensions(input);
                else if (key.template matches<detail::rectangular_values>())
                    values = Sequence::read(input);
                else
                    input.skip_value();
            }
            while (input.consume(','));
            input.expect('}');
        }
        if (!lengths || !lower_bounds || !values)
            throw wire_error("LuminPack rectangular JSON requires Lengths, LowerBounds, and Values.");
        typename value_type::value_type result{*lengths, *lower_bounds, std::move(*values)};
        detail::check_rectangular_values<wire_error>(result);
        check_upper_bounds<wire_error>(result);
        return result;
    }

    static void write(auto& output, const value_type& value)
    {
        if (!value)
        {
            output.write_null();
            return;
        }
        detail::check_rectangular_values<std::invalid_argument>(*value);
        check_upper_bounds<std::invalid_argument>(*value);
        output.template write_opening_property<detail::rectangular_lengths>();
        write_dimensions(output, value->lengths);
        output.template write_ordered_property<detail::rectangular_lower_bounds>(false);
        write_dimensions(output, value->lower_bounds);
        output.template write_ordered_property<detail::rectangular_values>(false);
        Sequence::write_elements(output, value->values);
        output.token('}');
    }

private:
    using dimensions = std::array<std::int32_t, Rank>;

    static dimensions read_dimensions(auto& input)
    {
        const auto container = input.begin('[');
        dimensions result;
        for (std::size_t index = 0; index < Rank; ++index)
        {
            if (index != 0)
                input.expect(',');
            result[index] = input.template read_number<std::int32_t>();
        }
        input.expect(']');
        return result;
    }

    static void write_dimensions(auto& output, const dimensions& shape)
    {
        output.token('[');
        bool first = true;
        for (const auto dimension : shape)
        {
            if (!std::exchange(first, false))
                output.token(',');
            output.write_number(dimension);
        }
        output.token(']');
    }

    template<class Error>
    static void check_upper_bounds(const typename value_type::value_type& value)
    {
        for (std::size_t index = 0; index < Rank; ++index)
            if (value.lengths[index] > 0 &&
                std::int64_t{value.lower_bounds[index]} + value.lengths[index] - 1 > std::numeric_limits<std::int32_t>::max())
                throw Error("LuminPack rectangular array upper bound exceeds Int32.");
    }
};
}
