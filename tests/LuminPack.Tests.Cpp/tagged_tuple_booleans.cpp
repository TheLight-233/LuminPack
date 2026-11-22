#include <luminpack/luminpack.h>

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <stdexcept>
#include <tuple>
#include <vector>

namespace
{
using namespace luminpack;
using inner = tuple_codec<boolean_codec, scalar_codec<std::uint32_t>>;
using tuple = tuple_codec<scalar_codec<std::uint8_t>, inner, scalar_codec<std::int64_t>, boolean_codec>;
// The exported CLR models for (byte, (bool, uint), long, bool).
constexpr storage_model<5> first{32, {0, 4, 8, 16, 24}};
constexpr std::array models{
    first,
    storage_model<5>{24, {0, 4, 8, 12, 20}},
    storage_model<5>{24, {8, 16, 12, 0, 9}},
    storage_model<5>{20, {8, 16, 12, 0, 9}},
    storage_model<5>{24, {8, 20, 16, 0, 9}}
};
using codec = tagged_tuple_list_codec<tuple, models[0], models[1], models[2], models[3], models[4]>;
constexpr std::size_t header = sizeof(std::int32_t) + 1;
constexpr std::array<tuple::value_type, 2> values{
    tuple::value_type{7, inner::value_type{true, 0x01234567}, -17, false},
    tuple::value_type{41, inner::value_type{false, 0x89abcdef}, 0x1122334455667788, true}
};

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

std::vector<std::byte> make_block(std::uint8_t tag, const storage_model<5>& model)
{
    std::vector<std::byte> bytes(header + model.stride * values.size());
    writer output(bytes);
    output.write<std::int32_t>(static_cast<std::int32_t>(values.size()));
    output.write(tag);
    for (std::size_t index = 0; index < values.size(); ++index)
    {
        const auto& [first_value, nested, wide, last] = values[index];
        const auto& [boolean, number] = nested;
        auto* target = bytes.data() + header + index * model.stride;
        target[model.offsets[0]] = static_cast<std::byte>(first_value);
        target[model.offsets[1]] = boolean ? std::byte{1} : std::byte{0};
        std::memcpy(target + model.offsets[2], &number, sizeof(number));
        std::memcpy(target + model.offsets[3], &wide, sizeof(wide));
        target[model.offsets[4]] = last ? std::byte{1} : std::byte{0};
    }
    return bytes;
}

void verify_block(std::uint8_t tag, const storage_model<5>& model)
{
    const auto bytes = make_block(tag, model);
    reader cursor(bytes);
    codec::value_type destination(std::in_place, values.size());
    auto* previous_storage = destination->data();
    codec::read_into(cursor, destination);
    cursor.require_end();
    require(destination->data() == previous_storage && std::ranges::equal(*destination, values),
        "Valid Tagged tuple read must preserve reusable storage and decode both Boolean leaves.");

    for (const std::size_t leaf : {1, 4})
    {
        auto malformed = bytes;
        malformed[header + (values.size() - 1) * model.stride + model.offsets[leaf]] = std::byte{2};
        cursor = reader(malformed);
        destination->assign(values.rbegin(), values.rend());
        previous_storage = destination->data();
        const auto previous = *destination;
        bool rejected = false;
        try
        {
            codec::read_into(cursor, destination);
        }
        catch (const wire_error&)
        {
            rejected = true;
        }
        require(rejected && cursor.remaining() == malformed.size() && destination->data() == previous_storage &&
            *destination == previous,
            "Invalid nested or direct Boolean must fail before publishing any Tagged tuple element or cursor advance.");
    }
}
}

int main()
try
{
    static_assert(detail::tuple_storage<tuple>::boolean_fields == std::array<std::size_t, 2>{1, 4});
    for (std::size_t index = 0; index < models.size(); ++index)
        verify_block(static_cast<std::uint8_t>(index + 1), models[index]);

    std::array<std::byte, header + first.stride * values.size()> bytes;
    writer output(bytes);
    const codec::value_type input(std::in_place, values.begin(), values.end());
    codec::write(output, input);
    require(std::ranges::equal(output.written(), make_block(1, first)),
        "Canonical Tagged tuple writes must emit the exact Boolean bytes and tag.");
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
