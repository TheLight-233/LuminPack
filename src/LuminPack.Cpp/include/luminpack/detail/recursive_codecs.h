#pragma once

#include <memory>

namespace luminpack::detail
{
/// Limits native recursive call chains independently of a managed Binary read limit.
inline constexpr std::size_t recursive_record_stack_limit = 512;

class recursive_record_depth
{
public:
    explicit recursive_record_depth(std::size_t input_limit = std::numeric_limits<std::size_t>::max()) noexcept
        : input_limit_(input_limit) {}

    void check_input() const
    {
        if (depth_ >= input_limit_)
            throw wire_error("LuminPack Binary exceeds the configured recursive depth.");
    }

    class scope
    {
    public:
        explicit scope(recursive_record_depth& state) : state_(state)
        {
            state_.check_input();
            if (state_.depth_ >= recursive_record_stack_limit)
                throw wire_error("LuminPack recursive record exceeds the native stack limit.");
            ++state_.depth_;
        }

        ~scope() { --state_.depth_; }
        scope(const scope&) = delete;
        scope& operator=(const scope&) = delete;

    private:
        recursive_record_depth& state_;
    };

private:
    std::size_t input_limit_;
    std::size_t depth_ = 0;
};

inline version_state::scope version_transaction(version_state& version, recursive_record_depth&) noexcept
{
    return version_state::scope(version);
}

/// Supplies one recursion context from the generated root without changing the binary cursor.
template<class Codec, std::size_t MaxDepth>
struct recursive_binary_root
{
    using value_type = typename Codec::value_type;
    static constexpr std::size_t minimum_size = Codec::minimum_size;

    static value_type read(reader& input)
    {
        version_state version;
        return read(input, version);
    }

    static value_type read(reader& input, version_state& version)
    {
        recursive_record_depth depth(MaxDepth);
        return read_codec<Codec>(input, version, depth);
    }

    static void write(writer& output, const value_type& value)
    {
        version_state version;
        write(output, value, version);
    }

    static void write(writer& output, const value_type& value, version_state& version)
    {
        recursive_record_depth depth;
        write_codec<Codec>(output, value, version, depth);
    }
};

/// Boxes a complete Default or Version record without adding wire framing.
template<class T, class Body>
struct recursive_binary_record
{
    using value_type = std::unique_ptr<T>;

    static value_type read(reader& input, version_state& version, recursive_record_depth& depth)
    {
        reader marker = input;
        if (marker.read<std::uint8_t>() == 255)
        {
            input = marker;
            return {};
        }

        recursive_record_depth::scope nesting(depth);
        reader next = input;
        version_state::scope transaction(version);
        auto value = read_codec<Body>(next, version, depth);
        auto result = std::make_unique<T>(std::move(*value));
        input = next;
        transaction.commit();
        return result;
    }

    static void write(writer& output, const value_type& value, version_state& version, recursive_record_depth& depth)
    {
        if (value)
        {
            recursive_record_depth::scope nesting(depth);
            Body::write_body(output, *value, version, depth);
        }
        else
            output.write<std::uint8_t>(255);
    }
};

template<class Cursor>
struct recursive_json_cursor : Cursor
{
    explicit recursive_json_cursor(const Cursor& cursor) : Cursor(cursor) {}

    recursive_record_depth recursive_depth;
};

/// Reuses the generated JSON object body while owning each recursive child separately.
template<class T, class Body>
struct recursive_json_record
{
    using value_type = std::unique_ptr<T>;

    static value_type read(auto& input)
    {
        if constexpr (requires { input.recursive_depth; })
        {
            if (input.consume_null())
                return {};
            recursive_record_depth::scope nesting(input.recursive_depth);
            return std::make_unique<T>(Body::read(input));
        }
        else
        {
            recursive_json_cursor scoped(input);
            auto result = read(scoped);
            input = scoped;
            return result;
        }
    }

    static void write(auto& output, const value_type& value)
    {
        if constexpr (requires { output.recursive_depth; })
        {
            if (value)
            {
                recursive_record_depth::scope nesting(output.recursive_depth);
                Body::write(output, *value);
            }
            else
                output.write_null();
        }
        else
        {
            recursive_json_cursor scoped(output);
            write(scoped, value);
            output = scoped;
        }
    }
};
}
