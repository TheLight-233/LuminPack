#include <luminpack/luminpack.h>

#include <algorithm>

namespace luminpack
{
namespace
{
constexpr std::uint8_t version_marker = 251;
constexpr std::uint8_t legacy_form = 1;
constexpr std::uint8_t full_form = 0x40;
constexpr std::uint8_t short_form = 0x50;
constexpr auto maximum_fields = detail::maximum_version_fields;
constexpr std::size_t small_table = 8;
constexpr std::size_t maximum_ordinal = (1u << 30) - 1;

std::size_t schema_bucket(std::uint64_t hash, std::size_t mask) noexcept
{
    const auto mixed = (hash ^ (hash >> 33)) * 0xff51afd7ed558ccdull;
    return static_cast<std::size_t>((mixed ^ (mixed >> 32)) & mask);
}

void insert_ordinal(std::span<std::uint32_t> index, std::uint64_t hash, std::size_t ordinal) noexcept
{
    const auto mask = index.size() - 1;
    auto slot = schema_bucket(hash, mask);
    while (index[slot] != 0)
        slot = (slot + 1) & mask;
    index[slot] = static_cast<std::uint32_t>(ordinal + 1);
}

void remove_ordinal(std::span<std::uint32_t> index, std::uint64_t hash, std::size_t ordinal) noexcept
{
    const auto mask = index.size() - 1;
    auto slot = schema_bucket(hash, mask);
    while (index[slot] != ordinal + 1)
        slot = (slot + 1) & mask;
    index[slot] = 0;
}

std::uint8_t profile_tag(string_profile profile)
{
    const auto tag = static_cast<std::uint8_t>(profile);
    if (tag > 5)
        throw std::invalid_argument("Invalid LuminPack Version string profile.");
    return tag;
}

void validate_descriptors(std::span<const std::uint64_t> descriptors)
{
    if (descriptors.size() > maximum_fields)
        throw wire_error("Invalid LuminPack Version descriptor count.");
    // At most 249 entries, with zero reserved for an empty slot in this temporary table.
    constexpr std::size_t mask = 511;
    std::array<std::uint16_t, mask + 1> buckets{};
    for (std::size_t index = 0; index < descriptors.size(); ++index)
    {
        const auto descriptor = descriptors[index];
        const auto width = descriptor >> 56;
        if (width > 250 && width < 254)
            throw wire_error("Invalid LuminPack Version width.");
        const auto key = static_cast<std::uint32_t>(descriptor);
        auto bucket = static_cast<std::size_t>((key * 0x9e3779b9u) >> 23);
        for (; buckets[bucket] != 0; bucket = (bucket + 1) & mask)
        {
            if (static_cast<std::uint32_t>(descriptors[buckets[bucket] - 1]) == key)
                throw wire_error("Duplicate LuminPack Version member key.");
        }
        buckets[bucket] = static_cast<std::uint16_t>(index + 1);
    }
}

std::uint32_t read_ordinal(reader& input)
{
    std::uint32_t ordinal = 0;
    for (unsigned shift = 0; shift <= 28; shift += 7)
    {
        const auto byte = input.read<std::uint8_t>();
        if (shift == 28 && byte > 3)
            break;
        ordinal |= static_cast<std::uint32_t>(byte & 127u) << shift;
        if (byte < 128)
        {
            if (shift == 0 || byte != 0)
                return ordinal;
            break;
        }
    }
    throw wire_error("Invalid LuminPack Version schema ordinal.");
}

void write_ordinal(writer& output, std::uint32_t ordinal)
{
    while (ordinal >= 128)
    {
        output.write(static_cast<std::uint8_t>((ordinal & 127u) | 128u));
        ordinal >>= 7;
    }
    output.write(static_cast<std::uint8_t>(ordinal));
}

std::string_view read_legacy_metadata(reader& input)
{
    std::uint32_t length = 0;
    for (unsigned shift = 0; shift <= 28; shift += 7)
    {
        const auto byte = input.read<std::uint8_t>();
        if (shift == 28 && byte > 7)
            break;
        length |= static_cast<std::uint32_t>(byte & 127u) << shift;
        if (byte < 128)
        {
            const auto bytes = input.take(length);
            return {reinterpret_cast<const char*>(bytes.data()), bytes.size()};
        }
    }
    throw wire_error("Invalid LuminPack legacy Version metadata length.");
}
}

void version_state::reset() noexcept
{
    truncate(0);
}

std::span<const std::uint64_t> version_state::get(std::uint32_t ordinal) const
{
    if (ordinal >= entries_.size())
        throw wire_error("Unknown LuminPack Version schema ordinal.");
    return entries_[ordinal].descriptors;
}

std::optional<std::size_t> version_state::find(std::uint64_t hash, std::span<const std::uint64_t> descriptors)
{
    const auto matches = [&](std::size_t index) {
        const auto& registered = entries_[index];
        return registered.hash == hash && std::ranges::equal(registered.descriptors, descriptors);
    };
    if (last_ && matches(*last_))
        return last_;

    if (!indexed_)
    {
        for (std::size_t ordinal = 0; ordinal < entries_.size(); ++ordinal)
        {
            if (matches(ordinal))
                return last_ = ordinal;
        }
    }
    else
    {
        const auto mask = index_.size() - 1;
        for (auto slot = schema_bucket(hash, mask); index_[slot] != 0; slot = (slot + 1) & mask)
        {
            const auto ordinal = static_cast<std::size_t>(index_[slot] - 1);
            if (matches(ordinal))
                return last_ = ordinal;
        }
    }
    return std::nullopt;
}

std::span<const std::uint64_t> version_state::append(
    std::uint64_t hash, std::span<const std::uint64_t> descriptors, bool index_for_writes)
{
    const auto ordinal = entries_.size();
    if (ordinal > maximum_ordinal)
        throw wire_error("LuminPack Version registry exceeds the ordinal range.");
    entries_.push_back({hash, std::vector<std::uint64_t>(descriptors.begin(), descriptors.end())});
    try
    {
        if (indexed_ || (index_for_writes && ordinal >= small_table))
        {
            if (index_.empty() || ordinal >= index_.size() / 2)
            {
                auto capacity = index_.empty() ? small_table * 4 : index_.size() * 2;
                // A state used for reads may already hold more than one growth step of registrations.
                while (ordinal >= capacity / 2)
                    capacity *= 2;
                std::vector<std::uint32_t> index(capacity);
                for (std::size_t current = 0; current < ordinal; ++current)
                    insert_ordinal(index, entries_[current].hash, current);
                index_.swap(index);
            }
            else if (!indexed_)
            {
                for (std::size_t current = 0; current < ordinal; ++current)
                    insert_ordinal(index_, entries_[current].hash, current);
            }
            insert_ordinal(index_, hash, ordinal);
            indexed_ = true;
        }
    }
    catch (...)
    {
        entries_.pop_back();
        throw;
    }
    if (index_for_writes)
        last_ = ordinal;
    return entries_.back().descriptors;
}

void version_state::truncate(std::size_t size) noexcept
{
    if (last_ && *last_ >= size)
        last_.reset();
    if (indexed_)
    {
        // Remove in reverse insertion order so retained probe chains never cross a removed slot.
        const auto end = size > small_table ? size : 0;
        for (auto ordinal = entries_.size(); ordinal > end;)
        {
            --ordinal;
            remove_ordinal(index_, entries_[ordinal].hash, ordinal);
        }
        indexed_ = size > small_table;
    }
    while (entries_.size() > size)
        entries_.pop_back();
}

detail::version_read_header detail::read_version_record_header(reader& input, version_state& state, string_profile profile)
{
    reader next = input;
    const auto marker = next.read<std::uint8_t>();
    if (marker == 255)
    {
        input = next;
        return {{}, version_read_form::null_record};
    }
    if (marker != version_marker)
        throw wire_error("Invalid LuminPack Version record marker.");
    const auto form = next.read<std::uint8_t>();
    const auto tag = profile_tag(profile);
    if (form == (short_form | tag))
    {
        const auto descriptors = state.get(read_ordinal(next));
        input = next;
        return {descriptors, version_read_form::modern};
    }
    if (form != (full_form | tag))
    {
        if (form == legacy_form)
            return {{}, version_read_form::legacy};
        throw wire_error("Unexpected LuminPack Version record form or string profile.");
    }

    const auto count = next.read<std::uint8_t>();
    if (count > maximum_fields)
        throw wire_error("Invalid LuminPack Version descriptor count.");
    const auto hash = next.read<std::uint64_t>();
    const auto raw = next.take(count * sizeof(std::uint64_t));
    std::array<std::uint64_t, maximum_fields> descriptors;
    if (!raw.empty())
        std::memcpy(descriptors.data(), raw.data(), raw.size());
    const auto table = std::span<const std::uint64_t>(descriptors).first(count);
    if (detail::version_schema_hash(table) != hash)
        throw wire_error("Invalid LuminPack Version schema hash.");
    validate_descriptors(table);
    const auto stored = state.append(hash, table, false);
    input = next;
    return {stored, version_read_form::modern};
}

std::optional<std::span<const std::uint64_t>> read_version_header(
    reader& input, version_state& state, string_profile profile)
{
    const auto header = detail::read_version_record_header(input, state, profile);
    if (header.form == detail::version_read_form::legacy)
        throw wire_error("A revision-1 Version record requires its full legacy member contracts.");
    if (header.form == detail::version_read_form::null_record)
        return std::nullopt;
    return header.descriptors;
}

detail::legacy_version_map detail::read_legacy_version_map(reader& input, string_profile profile,
    std::span<const legacy_version_field> fields)
{
    const auto marker = input.read<std::uint8_t>();
    const auto revision = input.read<std::uint8_t>();
    const auto tag = input.read<std::uint8_t>();
    const auto count = input.read<std::uint8_t>();
    if (marker != version_marker || revision != legacy_form || count > maximum_fields || fields.size() > maximum_fields)
        throw wire_error("Invalid LuminPack legacy Version header.");
    if (tag != profile_tag(profile))
        throw wire_error("The LuminPack legacy Version string profile does not match.");

    legacy_version_map map{};
    map.count = count;
    for (std::size_t index = 0; index < count; ++index)
    {
        const auto name = read_legacy_metadata(input);
        const auto contract = read_legacy_metadata(input);
        const auto matches = [&](const legacy_version_field& field) {
            return std::ranges::find(field.names, name) != field.names.end();
        };
        const auto field = index < fields.size() && matches(fields[index])
            ? fields.begin() + static_cast<std::ptrdiff_t>(index) : std::ranges::find_if(fields, matches);
        if (field == fields.end())
        {
            map.slots[index] = legacy_version_map::absent;
            continue;
        }
        if (contract != field->contract)
            throw wire_error("LuminPack legacy Version member type or wire profile changed.");
        const auto slot = static_cast<std::size_t>(field - fields.begin());
        if (map.present[slot])
            throw wire_error("Duplicate LuminPack legacy Version destination member.");
        map.present[slot] = true;
        map.slots[index] = static_cast<std::uint8_t>(slot);
    }

    constexpr auto maximum_payload = static_cast<std::uint64_t>(std::numeric_limits<std::int32_t>::max());
    std::uint64_t total = 0;
    for (std::size_t index = 0; index < count; ++index)
    {
        const auto length = read_reference_id(input);
        total += length;
        if (total > maximum_payload)
            throw wire_error("LuminPack legacy Version payload exceeds its maximum size.");
        map.lengths[index] = length;
    }
    if (total > input.unread().size())
        truncated_input();
    return map;
}

void write_version_header(writer& output, version_state& state, string_profile profile,
    std::span<const std::uint64_t> descriptors)
{
    if (descriptors.size() > maximum_fields)
        throw wire_error("Invalid LuminPack Version descriptor count.");
    detail::write_version_prehashed(output, state, profile, detail::version_schema_hash(descriptors), descriptors);
}

void detail::write_version_prehashed(writer& output, version_state& state, string_profile profile,
    std::uint64_t hash, std::span<const std::uint64_t> descriptors)
{
    const auto tag = profile_tag(profile);
    writer next = output;
    next.write(version_marker);
    if (const auto ordinal = state.find(hash, descriptors))
    {
        next.write(static_cast<std::uint8_t>(short_form | tag));
        write_ordinal(next, static_cast<std::uint32_t>(*ordinal));
    }
    else
    {
        validate_descriptors(descriptors);
        next.write(static_cast<std::uint8_t>(full_form | tag));
        next.write(static_cast<std::uint8_t>(descriptors.size()));
        next.write(hash);
        const auto bytes = next.reserve(descriptors.size_bytes());
        if (!bytes.empty())
            std::memcpy(bytes.data(), descriptors.data(), bytes.size());
        (void)state.append(hash, descriptors, true);
    }
    output = next;
}
}
