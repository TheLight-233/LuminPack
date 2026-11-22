#pragma once

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <span>
#include <utility>

namespace luminpack::detail
{
struct layout_run
{
    std::size_t source;
    std::size_t destination;
    std::size_t length;
};

struct layout_window
{
    std::size_t source_step = 0;
    std::size_t destination_step = 0;
    std::size_t elements = 0;
    std::size_t reserve = 0;
    std::size_t input_vectors = 0;
    std::size_t output_vectors = 0;

    constexpr std::size_t blocks(std::size_t count) const noexcept
    {
        return elements != 0 && count >= reserve ? (count - reserve) / elements + 1 : 0;
    }
};

inline constexpr std::uint8_t unused_layout_byte = 255;

struct layout_shuffle_plan
{
    layout_window window;
    std::array<std::array<std::uint8_t, 16>, 4> masks{};
    std::array<std::array<std::uint8_t, 16>, 2> valid{};
};

template<std::size_t Width, std::size_t Vectors>
struct layout_grouped_plan
{
    layout_window window;
    bool dword_eligible = false;
    bool has_padding = false;
    std::array<std::array<std::uint8_t, Width>, Vectors> bytes{};
    std::array<std::array<std::uint32_t, Width / 4>, Vectors> dwords{};
    std::array<std::array<std::uint8_t, Width>, Vectors> valid{};
};

struct layout_avx2_plan : layout_grouped_plan<32, 2>
{
    std::array<unsigned, 2> banks{};
    std::array<std::array<std::array<std::uint8_t, 32>, 4>, 2> masks{};
    std::array<std::array<std::uint8_t, 32>, 2> select{};
};

struct layout_conversion_plan
{
    layout_shuffle_plan shuffle;
    layout_avx2_plan avx2;
    layout_grouped_plan<64, 2> avx512;
    layout_grouped_plan<16, 4> arm64;
};

constexpr std::size_t layout_divide_up(std::size_t value, std::size_t divisor) noexcept
{
    return (value + divisor - 1) / divisor;
}

constexpr layout_window make_layout_window(
    std::size_t source_stride, std::size_t destination_stride, std::size_t elements, std::size_t width) noexcept
{
    const auto source_step = elements * source_stride;
    const auto destination_step = elements * destination_stride;
    const auto inputs = layout_divide_up(source_step, width);
    const auto outputs = layout_divide_up(destination_step, width);
    const auto reserve = std::max({elements, layout_divide_up(inputs * width, source_stride),
        layout_divide_up(outputs * width, destination_stride)});
    return {source_step, destination_step, elements, reserve, inputs, outputs};
}

template<auto Widths, auto Source, auto Destination>
constexpr auto make_layout_runs() noexcept
{
    std::array<layout_run, Widths.size()> runs{};
    std::size_t count = 0;
    for (std::size_t field = 0; field < Widths.size(); ++field)
    {
        const auto from = Source.offsets[field];
        const auto to = Destination.offsets[field];
        if (count != 0 && runs[count - 1].source + runs[count - 1].length == from &&
            runs[count - 1].destination + runs[count - 1].length == to)
            runs[count - 1].length += Widths[field];
        else
            runs[count++] = {from, to, Widths[field]};
    }
    return std::pair{runs, count};
}

template<std::size_t Width, std::size_t Vectors>
constexpr void make_layout_group(layout_grouped_plan<Width, Vectors>& plan, std::span<const layout_run> runs,
    std::size_t source_stride, std::size_t destination_stride) noexcept
{
    constexpr auto window = Width == 16 ? 64 : Width * 2;
    const auto elements = window / std::max(source_stride, destination_stride);
    plan.window = make_layout_window(source_stride, destination_stride, elements, Width);
    for (auto& bytes : plan.bytes)
        bytes.fill(unused_layout_byte);
    for (std::size_t element = 0; element < elements; ++element)
        for (const auto run : runs)
            for (std::size_t byte = 0; byte < run.length; ++byte)
            {
                const auto from = element * source_stride + run.source + byte;
                const auto to = element * destination_stride + run.destination + byte;
                plan.bytes[to / Width][to % Width] = static_cast<std::uint8_t>(from);
            }
    plan.dword_eligible = true;
    for (std::size_t vector = 0; vector < plan.window.output_vectors; ++vector)
    {
        const auto& bytes = plan.bytes[vector];
        for (std::size_t index = 0; index < Width; ++index)
        {
            const auto from = bytes[index];
            const auto first = bytes[index - index % 4];
            const bool padding = from == unused_layout_byte;
            plan.has_padding |= padding;
            plan.valid[vector][index] = padding ? 0 : 255;
            if (index % 4 == 0)
                plan.dwords[vector][index / 4] = padding ? 0 : from / 4;
            plan.dword_eligible &= first == unused_layout_byte ? padding
                : first % 4 == 0 && from == first + index % 4;
        }
    }
}

template<auto Widths, auto Source, auto Destination>
constexpr layout_conversion_plan make_layout_conversion() noexcept
{
    layout_conversion_plan result{};
    constexpr auto source_stride = Source.stride;
    constexpr auto destination_stride = Destination.stride;
    constexpr auto runs = make_layout_runs<Widths, Source, Destination>();
    if constexpr (source_stride != 0 && destination_stride != 0 &&
        source_stride <= 32 && destination_stride <= 32)
    {
        constexpr bool one_vector = source_stride <= 16 && destination_stride <= 16;
        if constexpr (runs.second >= (one_vector ? 2 : 3))
        {
            constexpr auto elements = one_vector ? 16 / std::max(source_stride, destination_stride) : 1;
            result.shuffle.window = make_layout_window(source_stride, destination_stride, elements, 16);
            for (auto& mask : result.shuffle.masks)
                mask.fill(unused_layout_byte);
            for (std::size_t element = 0; element < elements; ++element)
                for (std::size_t index = 0; index < runs.second; ++index)
                    for (std::size_t byte = 0; byte < runs.first[index].length; ++byte)
                    {
                        const auto from = element * source_stride + runs.first[index].source + byte;
                        const auto to = element * destination_stride + runs.first[index].destination + byte;
                        result.shuffle.masks[(to / 16) * 2 + from / 16][to % 16] =
                            static_cast<std::uint8_t>(from % 16);
                        result.shuffle.valid[to / 16][to % 16] = 255;
                    }
            if constexpr (source_stride != destination_stride)
            {
                const auto segments = std::span(runs.first).first(runs.second);
                make_layout_group<32, 2>(result.avx2, segments, source_stride, destination_stride);
                make_layout_group(result.avx512, segments, source_stride, destination_stride);
                make_layout_group(result.arm64, segments, source_stride, destination_stride);
                for (std::size_t vector = 0; vector < result.avx2.window.output_vectors; ++vector)
                {
                    auto& masks = result.avx2.masks[vector];
                    for (auto& mask : masks)
                        mask.fill(unused_layout_byte);
                    for (std::size_t index = 0; index < 32; ++index)
                    {
                        const auto from = result.avx2.bytes[vector][index];
                        if (from == unused_layout_byte)
                            continue;
                        const auto bank = (from / 32) * 2 + ((from / 16 % 2) ^ (index / 16));
                        result.avx2.banks[vector] |= 1u << bank;
                        masks[bank][index] = static_cast<std::uint8_t>(from % 16);
                        result.avx2.select[vector][index] = from >= 32 ? 255 : 0;
                    }
                }
            }
        }
    }
    return result;
}

std::size_t convert_layout_prefix(const std::byte* source, std::byte* destination, std::size_t count,
    const layout_conversion_plan& plan, bool preserve_padding = false) noexcept;
}
