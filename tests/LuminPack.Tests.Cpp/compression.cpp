#include <luminpack/luminpack.h>

#include <iostream>
#include <ranges>

namespace
{
using luminpack::compression::compress;
using luminpack::compression::decompress;
using luminpack::compression::decompressed_size;
using luminpack::compression::max_compressed_size;

void require(bool valid, const char* message)
{
    if (!valid)
        throw std::runtime_error(message);
}

template<class Exception>
void reject(auto action, const char* message)
{
    try
    {
        action();
    }
    catch (const Exception&)
    {
        return;
    }
    throw std::runtime_error(message);
}

std::vector<std::byte> frame(std::uint32_t length, std::initializer_list<std::uint8_t> body = {})
{
    std::vector<std::byte> result{std::byte{'L'}, std::byte{'U'}, std::byte{'M'}, std::byte{'2'}};
    for (unsigned shift = 0; shift < 32; shift += 8)
        result.push_back(static_cast<std::byte>((length >> shift) & 0xff));
    for (const auto value : body)
        result.push_back(static_cast<std::byte>(value));
    return result;
}

std::vector<std::byte> compressed(std::span<const std::byte> source)
{
    std::vector<std::byte> result(max_compressed_size(source.size()));
    result.resize(compress(source, result));
    return result;
}

void verify(std::span<const std::byte> data, std::span<const std::byte> expected)
{
    require(decompressed_size(data) == expected.size(), "Read the exact LUM2 output length.");
    constexpr auto marker = std::byte{0xa7};
    std::vector<std::byte> storage(expected.size() + 2, marker);
    auto output = std::span(storage).subspan(1, expected.size());
    require(decompress(data, output) == output.size(), "Expand the complete LUM2 payload.");
    require(std::ranges::equal(output, expected), "Preserve every expanded byte.");
    require(storage.front() == marker && storage.back() == marker, "Bound decoder writes to the supplied range.");
}

void exact_frames()
{
    const std::span<const std::byte> empty;
    const auto empty_frame = frame(0);
    require(max_compressed_size(0) == empty_frame.size() && compressed(empty) == empty_frame,
        "Encode the managed empty LUM2 header.");
    verify(empty_frame, empty);

    const std::array small{std::byte{0}, std::byte{1}, std::byte{2}, std::byte{3}};
    const auto small_frame = frame(4, {0x40, 0, 1, 2, 3});
    require(compressed(small) == small_frame, "Encode the managed literals-only frame.");
    verify(small_frame, small);

    std::array<std::byte, 21> repeated;
    repeated.fill(std::byte{'A'});
    const auto matched = frame(21, {0x1b, 'A', 1, 0, 0x50, 'A', 'A', 'A', 'A', 'A'});
    require(compressed(repeated) == matched, "Encode the managed match and its five final literals.");
    verify(matched, repeated);
}

void append_length(std::vector<std::byte>& data, std::size_t value)
{
    if (value >= 255)
    {
        data.push_back(std::byte{0xff});
        value -= 255;
        while (value >= 128)
        {
            data.push_back(static_cast<std::byte>((value & 127) | 128));
            value >>= 7;
        }
    }
    data.push_back(static_cast<std::byte>(value));
}

void periodic_matches()
{
    for (const auto distance : {1u, 2u, 7u, 8u, 9u, 15u, 16u, 31u, 32u, 63u, 64u, 65u, 65535u})
    {
        for (const auto length : {4u, 19u, 31u, 32u, 63u, 64u, 127u, 128u, 129u, 255u, 256u, 273u, 274u, 402u, 4099u})
        {
            const auto payload_length = distance + length;
            auto data = frame(payload_length + 1);
            const auto match_nibble = std::min(length - 4, 15u);
            data.push_back(static_cast<std::byte>((std::min(distance, 15u) << 4) | match_nibble));
            if (distance >= 15)
                append_length(data, distance - 15);
            std::vector<std::byte> expected(payload_length + 1);
            for (unsigned i = 0; i < payload_length; ++i)
                expected[i] = static_cast<std::byte>(((i % distance) * 17 + 3) & 0xff);
            data.insert(data.end(), expected.begin(), expected.begin() + distance);
            data.push_back(static_cast<std::byte>(distance & 0xff));
            data.push_back(static_cast<std::byte>(distance >> 8));
            if (match_nibble == 15)
                append_length(data, length - 19);
            data.push_back(std::byte{0x10});
            data.push_back(std::byte{'!'});
            expected.back() = std::byte{'!'};
            verify(data, expected);
        }
    }
}

std::vector<std::byte> pseudorandom(std::size_t length)
{
    std::vector<std::byte> data(length);
    std::uint32_t state = 0x29438c25;
    for (auto& byte : data)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        byte = static_cast<std::byte>(state >> 24);
    }
    return data;
}

void compression_windows()
{
    for (const auto length : {1u, 14u, 15u, 19u, 20u, 21u, 142u, 143u, 269u, 270u, 398u, 131221u})
    {
        const auto input = pseudorandom(length);
        verify(compressed(input), input);
    }

    auto wrapped = pseudorandom(192 * 1024);
    for (std::size_t block = 0; block + 509 <= wrapped.size(); block += 509)
        std::copy_n(wrapped.begin() + block, 240, wrapped.begin() + block + 247);
    const auto data = compressed(wrapped);
    require(data.size() < wrapped.size() * 3 / 4, "Retain local matches across low-position wraps.");
    verify(data, wrapped);
}

void malformed_frames()
{
    const auto input = pseudorandom(413);
    const auto data = compressed(input);
    std::array<std::byte, 512> output;
    for (std::size_t size = 0; size < data.size(); ++size)
        reject<luminpack::wire_error>([&] { (void)decompress(std::span(data).first(size), output); },
            "Reject every truncated LUM2 prefix.");

    auto old = frame(0);
    old[3] = std::byte{'Z'};
    reject<luminpack::wire_error>([&] { (void)decompressed_size(old); }, "Reject the older LUMZ signature.");
    reject<luminpack::wire_error>([] { (void)decompressed_size(frame(0x80000000u)); }, "Reject a negative declared length.");
    reject<luminpack::wire_error>([] { (void)decompressed_size(frame(0, {0})); }, "Reject trailing bytes in an empty frame.");
    reject<luminpack::wire_error>([] { (void)decompressed_size(frame(0x7fffffffu, {0})); },
        "Reject an impossible high-ratio frame before allocation.");

    for (const auto& invalid : {
        frame(1, {}), frame(1, {0x11, 'A'}), frame(2, {0x10, 'A'}),
        frame(5, {0x10, 'A', 0, 0, 0}), frame(5, {0x10, 'A', 2, 0, 0}), frame(5, {0x10, 'A', 1, 0}),
        frame(300, {0xf0}), frame(300, {0xf0, 0xff}), frame(300, {0xf0, 0xff, 0x80, 0}),
        frame(300, {0xf0, 0xff, 0x80, 0x80, 0x80, 0x80, 8}),
        frame(1, {0xf0, 0x01})})
        reject<luminpack::wire_error>([&] { (void)decompress(invalid, output); }, "Reject malformed LUM2 sequence framing.");

    const std::array valid{std::byte{'A'}, std::byte{'A'}, std::byte{'A'}, std::byte{'A'}, std::byte{'A'}};
    verify(frame(5, {0x10, 'A', 1, 0, 0}), valid);
}

void buffer_contracts()
{
    reject<std::length_error>([] { (void)max_compressed_size(std::numeric_limits<std::size_t>::max()); },
        "Reject the out-of-range LUM2 compression length.");
    const auto payload = pseudorandom(32);
    std::vector<std::byte> storage(max_compressed_size(payload.size()), std::byte{0x6a});
    const auto original = storage;
    reject<std::length_error>([&] { (void)compress(payload, std::span(storage).first(storage.size() - 1)); },
        "Require full advertised LUM2 compression capacity.");
    require(storage == original, "A failed compression capacity check must not publish bytes.");

    const auto data = compressed(payload);
    reject<std::length_error>([&] { (void)decompress(data, std::span(storage).first(payload.size() - 1)); },
        "Require the declared decompression capacity.");
    require(storage == original, "A failed decompression capacity check must not publish bytes.");
    reject<std::invalid_argument>([&] { (void)compress(std::span(storage).first(payload.size()), storage); },
        "Reject overlapping LUM2 input and compressed output.");
    auto aliased = data;
    reject<std::invalid_argument>([&] { (void)decompress(aliased, aliased); },
        "Reject overlapping LUM2 frame and uncompressed output.");
    require(storage == original && aliased == data, "A failed overlap check must not publish bytes.");
}
}

int main()
{
    try
    {
        exact_frames();
        periodic_matches();
        compression_windows();
        malformed_frames();
        buffer_contracts();
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
