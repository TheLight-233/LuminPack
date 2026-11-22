#include "latin1.h"
#include "ascii_bytes.h"
#include "cpu_isa.h"

#include <cstring>

#if defined(LUMINPACK_CPP_SIMD) && (defined(_M_X64) || defined(__x86_64__))
#define LUMINPACK_CPP_SSE2
#include <emmintrin.h>
#elif defined(LUMINPACK_CPP_SIMD) && (defined(_M_ARM64) || (defined(__aarch64__) && defined(__ARM_NEON)))
#define LUMINPACK_CPP_TEXT_NEON
#if defined(_MSC_VER) && !defined(__clang__)
#include <arm64_neon.h>
#else
#include <arm_neon.h>
#endif
#endif
#if (defined(LUMINPACK_CPP_INLINE_SSE2) || defined(LUMINPACK_CPP_AVX2) || defined(LUMINPACK_CPP_AVX512BW)) && defined(_MSC_VER)
#include <intrin.h>
#endif

namespace luminpack::detail
{
namespace
{
#if defined(LUMINPACK_CPP_AVX2) || defined(LUMINPACK_CPP_AVX512BW)
struct latin1_isa
{
    bool avx2;
    bool avx512bw;
#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
    bool avx512f;
    bool avx512vbmi;
#endif
};

latin1_isa detect_isa() noexcept
{
#if defined(_MSC_VER)
    int registers[4];
    __cpuid(registers, 0);
    if (registers[0] < 7)
        return {};

    __cpuid(registers, 1);
    constexpr int avx_and_osxsave = (1 << 28) | (1 << 27);
    if ((registers[2] & avx_and_osxsave) != avx_and_osxsave)
        return {};

    const auto state = _xgetbv(0);
    if ((state & 0x06u) != 0x06u)
        return {};

    __cpuidex(registers, 7, 0);
    const auto features = static_cast<unsigned>(registers[1]);
    const bool avx2 = (features & (1u << 5)) != 0;
    constexpr auto avx512f_bw = (1u << 16) | (1u << 30);
    const bool zmm_state = (state & 0xe6u) == 0xe6u;
    const bool avx512bw = (features & avx512f_bw) == avx512f_bw && zmm_state;
    return {avx2, avx512bw
#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
        , (features & (1u << 16)) != 0 && zmm_state, (registers[2] & (1 << 1)) != 0 && zmm_state
#endif
    };
#else
    return {__builtin_cpu_supports("avx2") != 0,
        __builtin_cpu_supports("avx512f") != 0 && __builtin_cpu_supports("avx512bw") != 0
#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
        , __builtin_cpu_supports("avx512f") != 0, __builtin_cpu_supports("avx512vbmi") != 0
#endif
    };
#endif
}

const latin1_isa& available_isa() noexcept
{
    static const auto isa = detect_isa();
    return isa;
}
#endif

#if defined(LUMINPACK_CPP_SSE2)
__m128i load128(const void* source) noexcept
{
    __m128i value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

bool is_ascii_bytes_128(std::span<const std::byte> source) noexcept
{
    return ascii_vector_groups(source, load128,
        [](__m128i first, __m128i second) noexcept { return _mm_or_si128(first, second); },
        [](__m128i bytes) noexcept { return _mm_movemask_epi8(bytes) == 0; });
}

bool is_latin1(__m128i value) noexcept
{
    const auto high = _mm_and_si128(value, _mm_set1_epi16(-256));
    return _mm_movemask_epi8(_mm_cmpeq_epi16(high, _mm_setzero_si128())) == 0xffff;
}

void store_narrowed(std::byte* destination, __m128i first, __m128i second) noexcept
{
    const auto packed = _mm_packus_epi16(first, second);
    std::memcpy(destination, &packed, sizeof(packed));
}

bool narrow_block(const char16_t* source, std::byte* destination) noexcept
{
    const auto first = load128(source);
    const auto second = load128(source + 8);
    if (!is_latin1(_mm_or_si128(first, second)))
        return false;
    store_narrowed(destination, first, second);
    return true;
}

bool narrow_latin1_128(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    // The final vector may overlap the preceding block but stays within both ranges.
    const auto last = source.size() - 16;
    for (std::size_t index = 0; index < last; index += 16)
    {
        if (!narrow_block(source.data() + index, destination.data() + index))
            return false;
    }
    return narrow_block(source.data() + last, destination.data() + last);
}

bool narrow_medium_latin1_128(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    const auto last = source.size() - 16;
    const auto first = load128(source.data());
    const auto first_end = load128(source.data() + 8);
    const auto last_start = load128(source.data() + last);
    const auto last_end = load128(source.data() + last + 8);
    if (!is_latin1(_mm_or_si128(_mm_or_si128(first, first_end), _mm_or_si128(last_start, last_end))))
        return false;
    store_narrowed(destination.data(), first, first_end);
    store_narrowed(destination.data() + last, last_start, last_end);
    return true;
}

void widen_block(const std::byte* source, char16_t* destination) noexcept
{
    const auto packed = load128(source);
    const auto low = _mm_unpacklo_epi8(packed, _mm_setzero_si128());
    const auto high = _mm_unpackhi_epi8(packed, _mm_setzero_si128());
    std::memcpy(destination, &low, sizeof(low));
    std::memcpy(destination + 8, &high, sizeof(high));
}
#elif defined(LUMINPACK_CPP_TEXT_NEON)
uint8x16_t load128(const void* source) noexcept
{
    uint8x16_t value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

bool is_ascii_bytes_128(std::span<const std::byte> source) noexcept
{
    return ascii_vector_groups(source, load128,
        [](uint8x16_t first, uint8x16_t second) noexcept { return vorrq_u8(first, second); },
        [](uint8x16_t bytes) noexcept { return vmaxvq_u8(bytes) < 0x80; });
}

void widen_block(const std::byte* source, char16_t* destination) noexcept
{
    const auto bytes = load128(source);
    const auto low = vmovl_u8(vget_low_u8(bytes));
    const auto high = vmovl_high_u8(bytes);
    std::memcpy(destination, &low, sizeof(low));
    std::memcpy(destination + 8, &high, sizeof(high));
}
#endif

template<class Word>
Word load_ascii_word(const std::byte* source) noexcept
{
    Word value;
    std::memcpy(&value, source, sizeof(value));
    return value;
}

bool is_ascii_bytes_scalar(std::span<const std::byte> source) noexcept
{
    const auto* start = source.data();
    const auto length = source.size();
    if (length < 4)
    {
        for (const auto byte : source)
            if (byte >= std::byte{0x80})
                return false;
        return true;
    }

    const auto* end = start + length;
    if (length < 8)
        return ((load_ascii_word<std::uint32_t>(start) | load_ascii_word<std::uint32_t>(end - 4)) & 0x80808080u) == 0;

    constexpr auto high_bits = UINT64_C(0x8080808080808080);
    if (length > 16)
    {
        const auto* last = end - 16;
        for (; start < last; start += 16)
            if (((load_ascii_word<std::uint64_t>(start) | load_ascii_word<std::uint64_t>(start + 8)) & high_bits) != 0)
                return false;
        start = last;
    }
    return ((load_ascii_word<std::uint64_t>(start) | load_ascii_word<std::uint64_t>(end - 8)) & high_bits) == 0;
}
}

bool is_ascii_bytes(std::span<const std::byte> source) noexcept
{
#if defined(LUMINPACK_CPP_AVX2)
    if (source.size() > 32 && has_avx2())
        return is_ascii_bytes_avx2(source);
#endif
#if defined(LUMINPACK_CPP_SSE2) || defined(LUMINPACK_CPP_TEXT_NEON)
    if (source.size() >= 16)
        return is_ascii_bytes_128(source);
#endif
    return is_ascii_bytes_scalar(source);
}

#if defined(LUMINPACK_CPP_INLINE_SSE2)
bool has_ssse3() noexcept
{
#if defined(_MSC_VER)
    static const bool supported = []
    {
        int registers[4];
        __cpuid(registers, 1);
        return (registers[2] & (1 << 9)) != 0;
    }();
    return supported;
#elif defined(__GNUC__) || defined(__clang__)
    return __builtin_cpu_supports("ssse3") != 0;
#else
    return false;
#endif
}
#endif

#if defined(LUMINPACK_CPP_AVX2)
bool has_avx2() noexcept
{
    return available_isa().avx2;
}
#endif

#if defined(LUMINPACK_CPP_AVX512BW)
bool has_avx512bw() noexcept
{
    return available_isa().avx512bw;
}
#endif

#if defined(LUMINPACK_CPP_LAYOUT_AVX512)
bool has_avx512f() noexcept
{
    return available_isa().avx512f;
}

bool has_avx512vbmi() noexcept
{
    return available_isa().avx512vbmi;
}
#endif

bool narrow_latin1(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
#if defined(LUMINPACK_CPP_AVX512BW)
    if (source.size() >= 64 && has_avx512bw())
        return narrow_latin1_avx512(source, destination);
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (source.size() >= 32 && has_avx2())
        return narrow_latin1_avx2(source, destination);
#endif
#if defined(LUMINPACK_CPP_SSE2)
    if (source.size() >= 16)
        return narrow_latin1_128(source, destination);
#endif
    for (std::size_t index = 0; index < source.size(); ++index)
    {
        const auto character = source[index];
        if (character > 0xff)
            return false;
        destination[index] = static_cast<std::byte>(character);
    }
    return true;
}

#if defined(LUMINPACK_CPP_INLINE_SSE2)
std::int32_t write_medium_utf16_payload(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
#if defined(LUMINPACK_CPP_AVX2)
    if (has_avx2())
        return write_medium_utf16_payload_avx2(source, destination);
#endif

    const auto raw_size = static_cast<std::int32_t>(source.size_bytes());
    if (source.size() <= 32 ? narrow_medium_latin1_128(source, destination) : narrow_latin1_128(source, destination))
        return raw_size | 1;

    // A later non-Latin-1 block may leave a compact prefix, which the raw copy overwrites.
    std::memcpy(destination.data(), source.data(), source.size_bytes());
    return raw_size;
}
#endif

void widen_latin1(std::span<const std::byte> source, std::span<char16_t> destination) noexcept
{
#if defined(LUMINPACK_CPP_AVX512BW)
    if (source.size() >= 64 && has_avx512bw())
    {
        widen_latin1_avx512(source, destination);
        return;
    }
#endif
#if defined(LUMINPACK_CPP_AVX2)
    if (source.size() >= 32 && has_avx2())
    {
        widen_latin1_avx2(source, destination);
        return;
    }
#endif
#if defined(LUMINPACK_CPP_SSE2) || defined(LUMINPACK_CPP_TEXT_NEON)
    if (source.size() >= 16)
    {
        const auto last = source.size() - 16;
        for (std::size_t index = 0; index < last; index += 16)
            widen_block(source.data() + index, destination.data() + index);
        widen_block(source.data() + last, destination.data() + last);
        return;
    }
#endif
    for (std::size_t index = 0; index < source.size(); ++index)
        destination[index] = static_cast<char16_t>(source[index]);
}
}
