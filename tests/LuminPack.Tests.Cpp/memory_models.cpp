#include "memory.hpp"
#include "memory-tagged.hpp"
#include "memory-version.hpp"
#include "interop_models.h"

#include <iostream>

namespace packet = luminpack_generated::root_LuminPackCppMemoryFixtures_x002eMemoryPacket;
namespace tagged = luminpack_generated::root_LuminPackCppMemoryFixtures_x002eTaggedMemory;
namespace revision = luminpack_generated::root_LuminPackCppMemoryFixtures_x002eVersionedMemory;

namespace
{
void check_null_policy()
{
    using list = luminpack::list_codec<luminpack::scalar_codec<std::int32_t>>;
    using memory = luminpack::memory_codec<list>;
    using json_memory = luminpack::json_codec<memory>;
    constexpr std::array null_header{std::byte{0xff}, std::byte{0xff}, std::byte{0xff}, std::byte{0xff}};
    auto value = luminpack::read_binary_document<memory>(null_header);
    std::array<std::byte, 4> output;
    const auto encoded = luminpack::write_binary_document<memory>(output, value);
    if (!value.empty() || !std::ranges::all_of(encoded, [](std::byte byte) { return byte == std::byte{}; }))
        throw std::runtime_error("A legacy null memory marker did not map to an empty buffer.");
    constexpr std::u16string_view input = u"null";
    auto decoded = luminpack::read_json_document<json_memory>(std::as_bytes(std::span(input)));
    constexpr std::u16string_view expected = u"[]";
    if (!decoded.empty() || !std::ranges::equal(luminpack::write_json_document<json_memory>(output, decoded),
        std::as_bytes(std::span(expected))))
        throw std::runtime_error("A null JSON memory did not map to an empty array.");
}

void check_generated_null_policy(const interop_models::path& directory)
{
    auto value = luminpack::read_binary_document<packet::root_binary>(interop_models::load(directory / "memory.bin"));
    if (!value || value->field_MissingSegment || value->field_MissingImmutable ||
        !value->field_EmptySegment || !value->field_EmptySegment->empty() ||
        !value->field_EmptyImmutable || !value->field_EmptyImmutable->empty() ||
        !value->field_EmptyMemory.empty())
        throw std::runtime_error("Generated memory, segment, or immutable buffer changed its null and empty policy.");
}

void check_tagged_memory(const interop_models::path& directory)
{
    interop_models::exchange<tagged::root_binary, tagged::root_json>(directory, "memory-tagged");
    const auto managed = luminpack::read_binary_document<tagged::root_binary>(
        interop_models::load(directory / "memory-tagged.bin"));
    const auto canonical = interop_models::load(directory / "out" / "memory-tagged.bin");
    const auto native = luminpack::read_binary_document<tagged::root_binary>(canonical);
    constexpr auto tag_offset = 1 + sizeof(std::int32_t);
    const std::vector<std::tuple<std::int32_t, std::int64_t>> expected{{33, -1024}, {-44, 0x1122334455667788}};
    if (!managed || !native || managed->field_Points != expected || native->field_Points != expected ||
        canonical.size() <= tag_offset || canonical[tag_offset] != std::byte{1})
        throw std::runtime_error("Tagged memory did not retain its logical elements or canonical model.");
}
}

int main(int argc, char** argv)
try
{
    if (argc != 2)
        return 2;
    const interop_models::path directory = argv[1];
    std::filesystem::create_directories(directory / "out");
    check_null_policy();
    check_generated_null_policy(directory);
    interop_models::exchange<packet::root_binary, packet::root_json>(directory, "memory", true);
    interop_models::exchange<revision::root_binary, revision::root_json>(directory, "memory-version", true);
    check_tagged_memory(directory);
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
