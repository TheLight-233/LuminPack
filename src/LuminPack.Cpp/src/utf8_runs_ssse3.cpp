#include "utf8_runs.h"
#include "utf8_bmp_shuffle.h"

namespace luminpack::detail
{
namespace
{
__m128i load128(const char16_t* source) noexcept
{
    __m128i chars;
    std::memcpy(&chars, source, sizeof(chars));
    return chars;
}

bool is_bmp(__m128i chars) noexcept
{
    const auto top = _mm_and_si128(chars, _mm_set1_epi16(static_cast<short>(0xf800)));
    const auto invalid = _mm_or_si128(_mm_cmpeq_epi16(top, _mm_setzero_si128()),
        _mm_cmpeq_epi16(top, _mm_set1_epi16(static_cast<short>(0xd800))));
    return _mm_movemask_epi8(invalid) == 0;
}
}

bool is_bmp_sample_ssse3(std::span<const char16_t, 16> source) noexcept
{
    return is_bmp(load128(source.data())) && is_bmp(load128(source.data() + 8));
}

std::size_t encode_bmp_ssse3(std::span<const char16_t> source, std::span<std::byte> destination) noexcept
{
    std::size_t index = 0;
    for (; source.size() - index >= 8 && destination.size() - index * 3 >= 24; index += 8)
    {
        const auto chars = load128(source.data() + index);
        if (!is_bmp(chars))
            break;
        const auto lead = _mm_or_si128(_mm_srli_epi16(chars, 12), _mm_set1_epi16(0xe0));
        const auto middle = _mm_or_si128(_mm_and_si128(_mm_srli_epi16(chars, 6), _mm_set1_epi16(63)), _mm_set1_epi16(0x80));
        const auto last = _mm_or_si128(_mm_and_si128(chars, _mm_set1_epi16(63)), _mm_set1_epi16(0x80));
        const auto a = _mm_packus_epi16(lead, lead);
        const auto b = _mm_packus_epi16(middle, middle);
        const auto c = _mm_packus_epi16(last, last);
        const auto first = _mm_or_si128(_mm_or_si128(_mm_shuffle_epi8(a, bmp::load_shuffle<__m128i, bmp::first_lead>()),
            _mm_shuffle_epi8(b, bmp::load_shuffle<__m128i, bmp::first_middle>())), _mm_shuffle_epi8(c, bmp::load_shuffle<__m128i, bmp::first_last>()));
        const auto second = _mm_or_si128(_mm_or_si128(_mm_shuffle_epi8(a, bmp::load_shuffle<__m128i, bmp::second_lead>()),
            _mm_shuffle_epi8(b, bmp::load_shuffle<__m128i, bmp::second_middle>())), _mm_shuffle_epi8(c, bmp::load_shuffle<__m128i, bmp::second_last>()));
        bmp::store_lane(destination.data() + index * 3, first, second);
    }
    return index;
}
}
