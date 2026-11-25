#include <luminpack/luminpack.h>
#include "model_header.h"
#include <cstdio>
#include <exception>

namespace model = LP_ROOT;
namespace lp = luminpack;
#ifdef LP_GRAPH
using document = lp::reference_document<model::root_value>;
#else
using document = model::root_value;
#endif
#ifdef _WIN32
#define LP_EXPORT extern "C" __declspec(dllexport)
#else
#define LP_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {
enum class format : int { binary, utf8, utf16 };

template<lp::json_encoding Encoding>
document read_json(std::span<const std::byte> input)
{
#ifdef LP_GRAPH
    return lp::read_reference_json_document<model::root_json, Encoding>(input, model::root_json_options);
#else
    return lp::read_json_document<model::root_json, Encoding>(input, model::root_json_options);
#endif
}

document read(std::span<const std::byte> input, format encoding)
{
    switch (encoding) {
    case format::binary:
#ifdef LP_FACADE
        return model::read_root_binary(input);
#elif defined(LP_GRAPH)
        return lp::read_binary_reference_document<model::root_binary>(input, model::root_binary_limits, model::root_binary_reference_depth);
#else
        return lp::read_binary_document<model::root_binary>(input, model::root_binary_limits);
#endif
    case format::utf8: return read_json<lp::json_encoding::utf8>(input);
    case format::utf16: return read_json<lp::json_encoding::utf16>(input);
    }
    throw std::invalid_argument("Unknown document format");
}

template<lp::json_encoding Encoding>
auto write_json(std::span<std::byte> output, const document& value)
{
#ifdef LP_GRAPH
    return lp::write_reference_json_document<model::root_json, Encoding>(output, value.value);
#else
    return lp::write_json_document<model::root_json, Encoding>(output, value);
#endif
}

auto write(std::span<std::byte> output, const document& value, format encoding)
{
    switch (encoding) {
    case format::binary:
#ifdef LP_GRAPH
        return lp::write_binary_reference_document<model::root_binary>(output, value);
#else
        return lp::write_binary_document<model::root_binary>(output, value);
#endif
    case format::utf8: return write_json<lp::json_encoding::utf8>(output, value);
    case format::utf16: return write_json<lp::json_encoding::utf16>(output, value);
    }
    throw std::invalid_argument("Unknown document format");
}

#ifdef LP_REGISTER
void ensure_registered()
{
    static const auto failure = []() -> std::exception_ptr {
        try { luminpack_register_model(); return {}; }
        catch (...) { return std::current_exception(); }
    }();
    if (failure) std::rethrow_exception(failure);
}
#endif

template<class F>
int guarded(char* error, size_t capacity, F action) noexcept
{
    try {
#ifdef LP_REGISTER
        ensure_registered();
#endif
        action();
        return 0;
    }
    catch (const std::exception& e) { if (capacity) std::snprintf(error, capacity, "%s", e.what()); }
    catch (...) { if (capacity) std::snprintf(error, capacity, "Native codec failed"); }
    return 1;
}
}

LP_EXPORT unsigned lp_abi() noexcept { return 1; }
LP_EXPORT int lp_read(const std::byte* input, size_t size, int encoding, document** result, char* error, size_t capacity) noexcept
{
    return guarded(error, capacity, [&] { *result = new document(read({input, size}, static_cast<format>(encoding))); });
}
LP_EXPORT int lp_write(const document* value, std::byte* target, size_t capacity, int encoding, size_t* written,
                       char* error, size_t error_capacity) noexcept
{
    return guarded(error, error_capacity, [&] {
        *written = write({target, capacity}, *value, static_cast<format>(encoding)).size();
    });
}
LP_EXPORT void lp_free(document* value) noexcept { delete value; }
