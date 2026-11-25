#include "prefix_graph.hpp"
#include "prefix_message.hpp"
#include "strict_graph.hpp"
#include "strict_message.hpp"
#include "interop_models.h"

#include <iostream>

namespace prefix_message = luminpack_generated::root_LuminPackCppPrefixBinaryInput_x002eMessage;
namespace prefix_graph = luminpack_generated::root_LuminPackCppPrefixBinaryInput_x002eNode;
namespace strict_message = luminpack_generated::root_LuminPackCppStrictBinaryInput_x002eMessage;
namespace strict_graph = luminpack_generated::root_LuminPackCppStrictBinaryInput_x002eNode;

namespace
{
void require(bool condition, const char* message)
{
    if (!condition)
        throw std::runtime_error(message);
}

void reject(auto read, const char* message)
{
    try
    {
        read();
    }
    catch (const luminpack::wire_error&)
    {
        return;
    }
    throw std::runtime_error(message);
}

template<class Codec, bool Complete, bool Graph = false>
void exchange(const interop_models::path& directory, const std::string& name, std::size_t limit, auto decode, auto verify)
{
    const auto exact = interop_models::load(directory / (name + ".bin"));
    const auto joined = interop_models::load(directory / (name + "-joined.bin"));
    auto value = decode(exact);
    verify(value);
    auto owned = luminpack::serialize(luminpack::deserialize<typename Codec::value_type>(exact));
    require(std::ranges::equal(owned, exact), "One-call Binary operations preserve managed bytes and graph identity.");
    if constexpr (Complete)
        reject([&] { (void)decode(joined); }, "Require complete Binary input on opted-in roots.");
    else
        verify(decode(joined));

    const auto truncated = std::span(exact).first(exact.size() - 1);
    reject([&] { (void)decode(truncated); }, "Keep complete record checks in both assembly modes.");
    std::vector<std::byte> oversized(limit + 1);
    std::ranges::copy(exact, oversized.begin());
    reject([&] { (void)decode(oversized); }, "Check the complete input span before reading either root.");

    std::array<std::byte, 128> storage{};
    std::span<const std::byte> rewritten;
    if constexpr (Graph)
        rewritten = luminpack::write_binary_reference_document<Codec>(storage, value);
    else
        rewritten = luminpack::write_binary_document<Codec>(storage, value);
    require(std::ranges::equal(rewritten, exact), "Rewrite the managed Binary record exactly.");
    interop_models::store(directory / "out" / (name + ".bin"), rewritten);
}
}

int main(int argc, char** argv)
try
{
    require(argc == 2, "Expected the managed Binary input fixture directory.");
    const auto prefix = interop_models::path(argv[1]) / "prefix";
    const auto strict = interop_models::path(argv[1]) / "strict";
    std::filesystem::create_directories(prefix / "out");
    std::filesystem::create_directories(strict / "out");

    static_assert(!prefix_message::root_binary_requires_complete_input && !prefix_graph::root_binary_requires_complete_input);
    static_assert(strict_message::root_binary_requires_complete_input && strict_graph::root_binary_requires_complete_input);
    const auto message = [](const auto& value) {
        require(value && value->field_Value == -901 && value->field_Values &&
            std::ranges::equal(*value->field_Values, std::array{1, 2, 3}), "Decode the Version root and its values.");
    };
    const auto graph = [](const auto& document) {
        require(document.value && document.value->field_Value == 103 && document.value->field_Next == document.value,
            "Keep the tracked root's self reference in either boundary policy.");
    };
    exchange<prefix_message::root_binary, false>(prefix, "message", prefix_message::root_binary_limits.max_input_bytes,
        prefix_message::read_root_binary, message);
    exchange<strict_message::root_binary, true>(strict, "message", strict_message::root_binary_limits.max_input_bytes,
        strict_message::read_root_binary, message);
    exchange<prefix_graph::root_binary, false, true>(prefix, "graph", prefix_graph::root_binary_limits.max_input_bytes,
        prefix_graph::read_root_binary, graph);
    exchange<strict_graph::root_binary, true, true>(strict, "graph", strict_graph::root_binary_limits.max_input_bytes,
        strict_graph::read_root_binary, graph);

    const auto joined = interop_models::load(prefix / "message-joined.bin");
    reject([&] { (void)luminpack::read_binary_document<prefix_message::root_binary>(joined); },
        "The independent C++ document API keeps its required boundary.");
    std::cout << "C++ verified both managed Binary boundary policies.\n";
    return 0;
}
catch (const std::exception& error)
{
    std::cerr << error.what() << '\n';
    return 1;
}
