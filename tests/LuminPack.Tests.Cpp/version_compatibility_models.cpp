#include "archive.hpp"
#include "evolved.hpp"
#include "nullable.hpp"
#include "closed-scalar.hpp"
#include "closed-list.hpp"
#include "interop_models.h"

#include <iostream>

namespace archived = luminpack_generated::root_CompatVersion;
namespace evolved = luminpack_generated::root_EvolvedVersion;
namespace optional = luminpack_generated::root_LegacyNullable;
namespace scalar = luminpack_generated::root_ClosedVersion_x003cint_x003e;
namespace collection = luminpack_generated::root_ClosedVersion_x003cglobal_x003a_x003aSystem_x002eCollections_x002eGeneric_x002eList_x003cint_x003e_x003e;

namespace
{
using byte = std::byte;

struct initialized_member
{
    std::int32_t value = 123;
};

using missing = luminpack::legacy_version_member<luminpack::version_member<&initialized_member::value,
    luminpack::scalar_codec<std::int32_t>, luminpack::version_descriptor(1, 0, 4)>, "@", "Absent">;
using missing_codec = luminpack::version_codec<initialized_member, luminpack::string_profile::utf16_length, missing>;

void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

template<class Codec>
void migrate(const interop_models::path& directory, const std::string& filename, const typename Codec::value_type& value)
{
    const auto expected = interop_models::load(directory / filename);
    std::array<byte, 2048> output;
    const auto actual = luminpack::write_binary_document<Codec>(output, value);
    require(std::ranges::equal(actual, expected), "The migrated Version record differs from the managed revision-4 bytes.");
    interop_models::store(directory / "out" / filename, actual);
}

template<class Codec>
void reject(luminpack::reader& input, luminpack::version_state& state)
{
    const auto before = input.unread();
    try
    {
        (void)Codec::read(input, state);
    }
    catch (const luminpack::wire_error&)
    {
        require(input.unread().data() == before.data() && input.unread().size() == before.size(),
            "A failed legacy Version read published its cursor.");
        return;
    }
    throw std::runtime_error("A malformed legacy Version document was accepted.");
}

void check_errors(const interop_models::path& directory, std::span<const byte> original)
{
    for (std::size_t length = 0; length < original.size(); ++length)
    {
        luminpack::reader input(original.first(length));
        luminpack::version_state state;
        reject<archived::root_binary>(input, state);
    }
    for (const char* name : {"duplicate-alias.bin", "changed-contract.bin"})
    {
        const auto wire = interop_models::load(directory / name);
        luminpack::reader input(wire);
        luminpack::version_state state;
        reject<evolved::root_binary>(input, state);
    }
    auto wrong_profile = std::vector<byte>(original.begin(), original.end());
    wrong_profile[2] = byte{0};
    luminpack::reader profile(wrong_profile);
    luminpack::version_state state;
    reject<archived::root_binary>(profile, state);
    constexpr std::array oversize_name{byte{251}, byte{1}, byte{2}, byte{1},
        byte{0x80}, byte{0x80}, byte{0x80}, byte{0x80}, byte{8}};
    luminpack::reader metadata(oversize_name);
    reject<archived::root_binary>(metadata, state);
}

void check_scope(const interop_models::path& directory)
{
    auto wire = interop_models::load(directory / "mixed-scope.bin");
    constexpr std::array trailing{byte{251}, byte{0x52}, byte{0}, byte{1}, byte{99}, byte{0}, byte{0},
        byte{0}, byte{0}, byte{0}, byte{0}, byte{0}};
    wire.insert(wire.end(), trailing.begin(), trailing.end());
    luminpack::reader input(wire);
    luminpack::version_state state;
    const auto value = archived::root_binary::read(input, state);
    require(value && value->field_Child && value->field_Child->field_Stamp == 99 &&
        std::ranges::equal(input.unread(), trailing), "An archived field could not read the current nested Version body.");
    using child = archived::type_CompatVersionChild_binary;
    reject<child>(input, state);
}
}

int main(int argc, char** argv)
try
{
    if (argc != 2)
        return 2;
    const interop_models::path directory(argv[1]);
    std::filesystem::create_directories(directory / "out");
    const auto original = interop_models::load(directory / "archive.bin");
    const auto missing_value = luminpack::read_binary_document<missing_codec>(original);
    require(missing_value && missing_value->value == 0, "An absent legacy field retained a non-default initializer.");
    const auto value = luminpack::read_binary_document<archived::root_binary>(original);
    require(value && value->field_Id == 7 && value->field_Name == u"name" &&
        value->field_Values == std::vector<std::int32_t>{1, 2, 3} && value->field_Child &&
        value->field_Child->field_Flag && value->field_Child->field_Stamp == 99, "The released revision-1 record changed.");
    migrate<archived::root_binary>(directory, "migrated.bin", value);
    const auto renamed = luminpack::read_binary_document<evolved::root_binary>(original);
    require(renamed && renamed->field_Id == 7 && renamed->field_DisplayName == u"name" && renamed->field_Child &&
        renamed->field_Child->field_Stamp == 99 && renamed->field_Added == 0, "Revision-1 aliases or evolution changed.");
    migrate<evolved::root_binary>(directory, "evolved.bin", renamed);
    const auto nullable = luminpack::read_binary_document<optional::root_binary>(interop_models::load(directory / "nullable.bin"));
    require(nullable && nullable->field_Number == 17 && !nullable->field_Maybe, "The unpadded legacy nullable changed.");
    migrate<optional::root_binary>(directory, "nullable-modern.bin", nullable);
    const auto fixed = luminpack::read_binary_document<scalar::root_binary>(interop_models::load(directory / "closed-scalar.bin"));
    require(fixed && fixed->field_Item == -17, "The closed Version scalar changed.");
    migrate<scalar::root_binary>(directory, "closed-scalar.bin", fixed);
    const auto list = luminpack::read_binary_document<collection::root_binary>(interop_models::load(directory / "closed-list.bin"));
    require(list && list->field_Item == std::vector<std::int32_t>{2, 5, 7}, "The closed Version list changed.");
    migrate<collection::root_binary>(directory, "closed-list.bin", list);
    check_errors(directory, original);
    check_scope(directory);
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
