#include "allocations.h"

#include <cstdlib>
#include <new>

#if defined(_WIN32)
#include <malloc.h>
#endif

namespace
{
thread_local allocation_counts counts;
thread_local bool sampling;
const void* volatile observation;

void* allocate(std::size_t size)
{
    for (;;)
    {
        if (auto* result = std::malloc(size != 0 ? size : 1))
        {
            record_allocation(size);
            return result;
        }
        const auto handler = std::get_new_handler();
        if (!handler)
            throw std::bad_alloc();
        handler();
    }
}

void* allocate_aligned(std::size_t size, std::size_t alignment)
{
    for (;;)
    {
        const auto nonempty = size != 0 ? size : 1;
#if defined(_WIN32)
        auto* result = _aligned_malloc(nonempty, alignment);
#else
        void* result = nullptr;
        if (posix_memalign(&result, alignment, nonempty) != 0)
            result = nullptr;
#endif
        if (result)
        {
            record_allocation(size);
            return result;
        }
        const auto handler = std::get_new_handler();
        if (!handler)
            throw std::bad_alloc();
        handler();
    }
}

void free_aligned(void* address) noexcept
{
#if defined(_WIN32)
    _aligned_free(address);
#else
    std::free(address);
#endif
}
}

void start_allocation_sample() noexcept
{
    counts = {};
    sampling = true;
}

allocation_counts stop_allocation_sample() noexcept
{
    sampling = false;
    return counts;
}

void record_allocation(std::size_t bytes) noexcept
{
    if (sampling)
    {
        ++counts.calls;
        counts.bytes += bytes;
    }
}

void observe(const void* address) noexcept { observation = address; }

void* operator new(std::size_t size) { return allocate(size); }
void* operator new[](std::size_t size) { return allocate(size); }
void operator delete(void* address) noexcept { std::free(address); }
void operator delete[](void* address) noexcept { std::free(address); }
void operator delete(void* address, std::size_t) noexcept { std::free(address); }
void operator delete[](void* address, std::size_t) noexcept { std::free(address); }
void* operator new(std::size_t size, std::align_val_t alignment)
{
    return allocate_aligned(size, static_cast<std::size_t>(alignment));
}
void* operator new[](std::size_t size, std::align_val_t alignment) { return ::operator new(size, alignment); }
void operator delete(void* address, std::align_val_t) noexcept { free_aligned(address); }
void operator delete[](void* address, std::align_val_t) noexcept { free_aligned(address); }
void operator delete(void* address, std::size_t, std::align_val_t) noexcept { free_aligned(address); }
void operator delete[](void* address, std::size_t, std::align_val_t) noexcept { free_aligned(address); }
