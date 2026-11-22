#include "compressed-record.hpp"
#include "compressed-shapes.hpp"
#include "compressed-version.hpp"
#include "interop_models.h"

#include <iostream>
#include <limits>

namespace ordinary = luminpack_generated::root_LuminPackCppCompressionFixtures_x002eCompressedPacket;
namespace shapes = luminpack_generated::root_LuminPackCppCompressionFixtures_x002eCompressedShapePacket;
namespace version = luminpack_generated::root_LuminPackCppCompressionFixtures_x002eVersionCompressedPacket;

namespace
{
using namespace luminpack;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void verify_ordinary(const ordinary::root_value& value)
{
    require(value && value->field_Bytes && value->field_Numbers, "Read the compressed array and list.");
    require(value->field_Bytes->size() == 384 && value->field_Numbers->size() == 127, "Retain compressed counts.");
    for (std::size_t index = 0; index < value->field_Bytes->size(); ++index)
        require(value->field_Bytes->at(index) == index % 13, "Read every decompressed byte.");
    for (std::size_t index = 0; index < value->field_Numbers->size(); ++index)
        require(value->field_Numbers->at(index) == static_cast<std::int32_t>(index % 5) - 2,
            "Read every decompressed List<int> element.");
    require(value->field_Bits == std::vector<bool>{true, false, false, true, true, false}, "Decode compressed Boolean slots.");
    require(value->field_Empty && value->field_Empty->empty() && !value->field_Missing && value->field_Tail == -127,
        "Preserve empty and null fields and the following field's cursor.");
}

void verify_shapes(const shapes::root_value& value)
{
    require(value && value->field_Codes && value->field_Codes->size() == 3 &&
        static_cast<std::uint16_t>(value->field_Codes->at(2)) == 0xabcdu, "Read an unnamed compressed enum value.");
    require(value->field_Raw && value->field_Raw->size() == 2 && value->field_Raw->at(0).field_Low == -13 &&
        value->field_Raw->at(0).field_High == 17 && value->field_Raw->at(1).field_Low == 19 &&
        value->field_Raw->at(1).field_High == -23, "Read the proven raw CLR layout.");
    require(value->field_NativeIntegers ==
        std::vector<std::intptr_t>{-1, std::numeric_limits<std::intptr_t>::max(), 113}, "Preserve native integer width.");
    using vector = std::vector<std::int32_t>;
    using vectors = std::vector<std::optional<vector>>;
    require(value->field_Jagged == vectors{vector{1, 2, 3}, vector{}, std::nullopt} &&
        value->field_JaggedList ==
            std::vector<std::optional<vectors>>{vectors{vector{17, -19}, std::nullopt}, vectors{}},
        "Keep the ordinary jagged counts and compress only the raw leaves.");
}

void verify_version(const version::root_value& value)
{
    require(value && value->field_Values == std::vector<std::int32_t>{17, 0, -3, 17, -3, 0} && value->field_Tail == 89,
        "Read the compressed field inside its Version subrecord.");
}

template<class Json, json_encoding Encoding>
void exchange_json(const interop_models::path& directory, const std::string& filename, auto verify)
{
    const auto original = interop_models::load(directory / filename);
    const auto value = read_json_document<Json, Encoding>(original);
    verify(value);
    std::vector<std::byte> buffer(original.size());
    const auto encoded = write_json_document<Json, Encoding>(buffer, value);
    require(std::ranges::equal(encoded, original), "Keep the exact ordinary managed JSON representation.");
    interop_models::store(directory / "out" / filename, encoded);
}

template<class Binary, class Json>
void exchange(const interop_models::path& directory, const std::string& name, auto verify, reader_limits limits = {})
{
    const auto filename = name + ".bin";
    const auto original = interop_models::load(directory / filename);
    const auto value = read_binary_document<Binary>(original, limits);
    verify(value);
    std::vector<std::byte> buffer(4096);
    const auto encoded = write_binary_document<Binary>(buffer, value);
    require(std::ranges::equal(encoded, original), "Keep the managed compressed Binary framing and bytes.");
    interop_models::store(directory / "out" / filename, encoded);
    exchange_json<Json, json_encoding::utf8>(directory, name + ".utf8.json", verify);
    exchange_json<Json, json_encoding::utf16>(directory, name + ".utf16.json", verify);
}

template<class Codec>
void reject(std::span<const std::byte> source, reader_limits limits = {})
{
    reader input(source, limits);
    try
    {
        (void)Codec::read(input);
    }
    catch (const wire_error&)
    {
        require(input.remaining() == source.size(), "Do not publish a failed compressed collection cursor.");
        return;
    }
    throw std::runtime_error("Accepts malformed or oversized compressed collection storage.");
}

std::vector<std::byte> encode_raw(std::span<const std::byte> raw, std::int32_t count)
{
    constexpr std::size_t header = 2 * sizeof(std::int32_t);
    std::vector<std::byte> bytes(header + compression::max_compressed_size(raw.size()));
    const auto length = compression::compress(raw, std::span(bytes).subspan(header));
    writer output(bytes);
    output.write(count);
    output.write(static_cast<std::int32_t>(length));
    bytes.resize(header + length);
    return bytes;
}

void verify_limits(const interop_models::path& directory)
{
    using bytes_codec = compressed_array_codec<scalar_codec<std::uint8_t>>;
    using boolean_values = compressed_array_codec<boolean_codec>;
    using wide_values = compressed_array_codec<scalar_codec<std::uint64_t>>;
    const auto record = interop_models::load(directory / "compressed-record.bin");
    auto changed_count = record;
    changed_count[1] = std::byte{1};
    changed_count[2] = std::byte{2};
    reject<ordinary::root_binary>(changed_count, ordinary::root_binary_limits);
    for (std::size_t size = 0; size < record.size(); ++size)
        reject<ordinary::root_binary>(std::span(record).first(size), ordinary::root_binary_limits);

    constexpr std::array raw{std::byte{1}, std::byte{0}, std::byte{2}};
    auto valid = encode_raw(raw, 3);
    reject<boolean_values>(valid);
    reject<wide_values>(encode_raw(raw, std::numeric_limits<std::int32_t>::max()));
    reject<bytes_codec>(encode_raw(raw, 4));
    reject<bytes_codec>(encode_raw(raw, -2));
    std::fill_n(valid.begin() + sizeof(std::int32_t), sizeof(std::int32_t), std::byte{0xff});
    reject<bytes_codec>(valid);

    const std::vector<std::byte> oversize(16384);
    const auto high_ratio = encode_raw(oversize, static_cast<std::int32_t>(oversize.size()));
    require(compression::decompressed_size(std::span(high_ratio).subspan(8)) == oversize.size(),
        "The standalone LUM2 decoder accepts this high-ratio frame.");
    reject<bytes_codec>(high_ratio);

    std::array<std::byte, 64> short_storage{};
    writer output(short_storage);
    try
    {
        bytes_codec::write(output, std::vector<std::uint8_t>(128));
    }
    catch (const std::length_error&)
    {
        require(output.written().empty(), "Do not publish a short compressed reservation.");
        return;
    }
    throw std::runtime_error("Accepts a destination smaller than the advertised compression capacity.");
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed compressed field fixture directory.");
    const auto directory = interop_models::path(argv[1]);
    std::filesystem::create_directories(directory / "out");
    exchange<ordinary::root_binary, ordinary::root_json>(directory, "compressed-record", verify_ordinary,
        ordinary::root_binary_limits);
    exchange<version::root_binary, version::root_json>(directory, "compressed-version", verify_version);
    exchange<shapes::root_binary, shapes::root_json>(directory, "compressed-shapes", verify_shapes);
    verify_limits(directory);
    std::cout << "Exchanged the compressed fields and their ordinary JSON forms.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
