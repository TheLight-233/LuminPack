#pragma once

#include <cstddef>

struct allocation_counts
{
    std::size_t calls{};
    std::size_t bytes{};
};

void start_allocation_sample() noexcept;
allocation_counts stop_allocation_sample() noexcept;
void record_allocation(std::size_t bytes) noexcept;
void observe(const void* address) noexcept;
