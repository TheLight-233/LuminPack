#pragma once

// Included by luminpack.h after the Binary and JSON collection codecs.
namespace luminpack
{
/// A CLR memory value decodes into owned elements and writes the existing collection payload.
template<class Sequence>
struct memory_codec
{
    using value_type = typename Sequence::value_type::value_type;
    static constexpr auto minimum_size = Sequence::minimum_size;

    template<class... Context>
    [[nodiscard]] static value_type read(reader& input, Context&... context)
    {
        auto result = detail::read_codec<Sequence>(input, context...);
        return result ? std::move(*result) : value_type{};
    }

    template<class... Context>
    static void write(writer& output, const value_type& value, Context&... context)
    {
        if constexpr (requires { Sequence::write_elements(output, value, context...); })
            Sequence::write_elements(output, value, context...);
        else
            Sequence::write_elements(output, value);
    }
};

/// CLR memory writes a JSON array and accepts null as default memory.
template<class Sequence>
struct json_memory_codec
{
    using value_type = typename Sequence::value_type::value_type;

    [[nodiscard]] static value_type read(auto& input)
    {
        auto result = Sequence::read(input);
        return result ? std::move(*result) : value_type{};
    }

    static void write(auto& output, const value_type& value)
    {
        Sequence::write_elements(output, value);
    }
};

template<class Sequence>
struct json_codec<memory_codec<Sequence>> : json_memory_codec<json_codec<Sequence>>
{
};
}
