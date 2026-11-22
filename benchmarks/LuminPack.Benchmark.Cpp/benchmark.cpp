#include "simple-benchmark.hpp"
#include "serialize-benchmark.hpp"
#include "json_dom.h"

#ifdef LUMINPACK_BENCHMARK_BITSERY
#include "bitsery_reference.h"
#endif
#ifdef LUMINPACK_BENCHMARK_CEREAL
#include "cereal_reference.h"
#endif
#ifdef LUMINPACK_BENCHMARK_MSGPACK
#include "msgpack_reference.h"
#endif
#ifdef LUMINPACK_BENCHMARK_JSONCONS
#include "jsoncons_reference.h"
#endif
#ifdef LUMINPACK_BENCHMARK_NLOHMANN_JSON
#include "nlohmann_reference.h"
#endif
#ifdef LUMINPACK_BENCHMARK_SIMDJSON
#include "simdjson_reference.h"
#endif

#include <ylt/struct_pack.hpp>

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <string_view>

namespace simple = luminpack_generated::root_LuminPackBenchmark_x002eSimpleClass;
namespace save = luminpack_generated::root_LuminPackBenchmark_x002eCharacterSaveData;

namespace
{
using std::filesystem::path;
using timer = std::chrono::steady_clock;
using operation = std::function<void()>;
constexpr int repetitions = 7;
constexpr auto sample_duration = std::chrono::milliseconds(25);
constexpr int allocation_iterations = 32;

template<class Unit>
std::basic_string<Unit> load(const path& name)
{
    std::ifstream file(name, std::ios::binary | std::ios::ate);
    const auto length = static_cast<std::streamoff>(file.tellg());
    if (length < 0 || length % sizeof(Unit) != 0)
        throw std::runtime_error("Missing benchmark fixture or incomplete code unit: " + name.string());
    std::basic_string<Unit> data(static_cast<std::size_t>(length) / sizeof(Unit), Unit{});
    file.seekg(0);
    file.read(reinterpret_cast<char*>(data.data()), length);
    if (!file)
        throw std::runtime_error("Failed to read fixture: " + name.string());
    return data;
}

template<class T>
std::size_t pack(std::span<char> output, const T& model)
{
    const auto capacity = struct_pack::get_needed_size(model);
    if (capacity.size() > output.size())
        throw std::length_error("struct_pack output buffer is too small.");
    struct_pack::serialize_to(output.data(), capacity, model);
    return capacity.size();
}

template<class T>
T unpack(std::span<const char> input)
{
    T result;
    std::size_t consumed = 0;
    if (struct_pack::deserialize_to(result, input.data(), input.size(), consumed) || consumed != input.size())
        throw std::runtime_error("struct_pack did not read the complete document.");
    return result;
}

void order_dictionary(rapid_reference::document& model, const char16_t* field)
{
    auto it = model.FindMember(field);
    if (it == model.MemberEnd() || !it->value.IsArray())
        throw std::runtime_error("Missing integer-key dictionary in benchmark fixture.");
    auto entries = it->value.GetArray();
    std::sort(entries.Begin(), entries.End(), [](const auto& left, const auto& right)
    {
        return left[0u].GetInt() < right[0u].GetInt();
    });
}

double time_sample(const operation& run)
{
    constexpr int batch_size = 16;
    auto start = timer::now();
    const auto stop = start + sample_duration;
    auto now = start;
    std::size_t iterations = 0;
    do
    {
        for (int index = 0; index < batch_size; ++index)
            run();
        iterations += batch_size;
        now = timer::now();
    }
    while (now < stop);
    return std::chrono::duration<double, std::nano>(now - start).count() / static_cast<double>(iterations);
}

struct statistics
{
    double median;
    double cv;
};

statistics summarize(std::array<double, repetitions> samples)
{
    const auto mean = std::accumulate(samples.begin(), samples.end(), 0.0) / samples.size();
    double squared_deviations = 0;
    for (double sample : samples)
        squared_deviations += (sample - mean) * (sample - mean);
    std::sort(samples.begin(), samples.end());
    return {samples[samples.size() / 2], 100 * std::sqrt(squared_deviations / (samples.size() - 1)) / mean};
}

struct job
{
    const char* library;
    const char* format;
    const char* direction;
    std::size_t bytes;
    operation run;
    bool reference = false;
};

void report(const char* model, const std::vector<job>& jobs)
{
    std::vector<std::array<double, repetitions>> samples(jobs.size());
    for (auto& current : jobs)
        time_sample(current.run);
    for (int repetition = 0; repetition < repetitions; ++repetition)
    {
        for (std::size_t offset = 0; offset < jobs.size(); ++offset)
        {
            const auto index = (offset + static_cast<std::size_t>(repetition)) % jobs.size();
            samples[index][repetition] = time_sample(jobs[index].run);
        }
    }
    std::vector<statistics> results;
    results.reserve(samples.size());
    for (const auto& sample : samples)
        results.push_back(summarize(sample));
    for (std::size_t index = 0; index < jobs.size(); ++index)
    {
        const auto& current = jobs[index];
        const auto result = results[index];
        const auto reference = std::ranges::find_if(jobs, [&](const job& candidate)
        {
            return candidate.reference && std::string_view(candidate.format) == current.format
                && std::string_view(candidate.direction) == current.direction;
        });
        if (reference == jobs.end())
            throw std::logic_error("Missing reference for benchmark group.");
        const auto reference_time = results[reference - jobs.begin()].median;
        start_allocation_sample();
        for (int iteration = 0; iteration < allocation_iterations; ++iteration)
            current.run();
        const auto allocated = stop_allocation_sample();
        std::cout << model << ',' << current.format << ',' << current.direction << ',' << current.library << ','
            << current.bytes << ',' << result.median << ',' << result.cv << ',' << result.median / reference_time << ','
            << static_cast<double>(allocated.calls) / allocation_iterations << ','
            << static_cast<double>(allocated.bytes) / allocation_iterations << '\n';
    }
}

template<class Binary, class Json>
void benchmark_model(const path& directory, const char* name, const char* fixture, const char16_t* dictionary)
{
    using model_type = typename Binary::value_type;
    const auto binary = load<char>(directory / (std::string(fixture) + ".bin"));
    const auto json = load<char16_t>(directory / (std::string(fixture) + ".json"));
    if (json.find(u'\0') != std::u16string::npos)
        throw std::runtime_error("Embedded zero code unit in benchmark JSON.");
    const auto json_input = std::as_bytes(std::span(json));
    const auto utf8_json = json_dom::to_utf8(json);
    const auto utf8_input = std::as_bytes(std::span(utf8_json));
    const auto model = luminpack::read_binary_document<Binary>(std::as_bytes(std::span(binary)));
    if (!model)
        throw std::runtime_error("Unexpected null benchmark model.");

    std::vector<char> output(1u << 20);
    std::vector<char16_t> json_output(1u << 19);
    auto json_writable = std::as_writable_bytes(std::span(json_output).first(json_output.size() - 1));
    auto render_check_value = [&](const model_type& data)
    {
        const auto size = luminpack::write_json_document<Json>(json_writable, data).size();
        json_output[size / sizeof(char16_t)] = u'\0';
        return size;
    };
    const auto lumin_json_size = render_check_value(model);
    auto reference_output = rapid_reference::parse(json_output.data());
    order_dictionary(reference_output, dictionary);
    auto verify = [&](const model_type& data, const char* reader)
    {
        render_check_value(data);
        auto parsed = rapid_reference::parse(json_output.data());
        order_dictionary(parsed, dictionary);
        if (!(parsed == reference_output))
            throw std::runtime_error(std::string(reader) + " did not recover the original " + name + " model.");
    };

    verify(luminpack::read_json_document<Json>(json_input), "LuminPack default JSON");
    const auto sp_size = pack(output, model);
    const std::vector<char> sp_input(output.begin(), output.begin() + sp_size);
    verify(unpack<model_type>(sp_input), "struct_pack");
    rapid_reference::reusable_writer rapid_writer;
    const auto rapid_output = rapid_writer.write<Json>(model);
    const auto rapid_size = rapid_output.size() * sizeof(char16_t);
    const auto rapid_decoded = rapid_reference::read<Json>(rapid_output.data());
    verify(rapid_decoded, "RapidJSON round trip");
    verify(rapid_reference::read<Json>(json.c_str()), "RapidJSON UTF-16");
    const auto lumin_binary_size = luminpack::write_binary_document<Binary>(std::as_writable_bytes(std::span(output)), model).size();

    constexpr auto utf8 = luminpack::json_encoding::utf8;
    std::vector<std::byte> utf8_output(1u << 20);
    const auto lumin_utf8 = luminpack::write_json_document<Json, utf8>(utf8_output, model);
    verify(luminpack::read_json_document<Json, utf8>(lumin_utf8), "LuminPack UTF-8 round trip");
    verify(luminpack::read_json_document<Json, utf8>(utf8_input), "LuminPack UTF-8 fixture");
    rapid_reference::reusable_utf8_writer rapid_utf8_writer;
    const auto rapid_utf8 = rapid_utf8_writer.write<Json>(model);
    verify(rapid_reference::read_utf8<Json>(rapid_utf8.data()), "RapidJSON UTF-8 round trip");
    verify(rapid_reference::read_utf8<Json>(utf8_json.c_str()), "RapidJSON UTF-8 fixture");

    std::vector<job> jobs{
        {"luminpack", "binary", "write", lumin_binary_size, [&]
            {
                const auto written = luminpack::write_binary_document<Binary>(std::as_writable_bytes(std::span(output)), model);
                observe(written.data() + written.size());
            }},
        {"struct_pack", "binary", "write", sp_size, [&]
            { observe(output.data() + pack(output, model)); }, true},
        {"luminpack", "binary", "read", binary.size(), [&]
            { auto object = luminpack::read_binary_document<Binary>(std::as_bytes(std::span(binary))); observe(&object); }},
        {"struct_pack", "binary", "read", sp_input.size(), [&]
            { auto object = unpack<model_type>(sp_input); observe(&object); }, true},
        {"luminpack", "json-utf16", "write", lumin_json_size, [&]
            { const auto written = luminpack::write_json_document<Json>(json_writable, model); observe(written.data() + written.size()); }},
        {"rapidjson", "json-utf16", "write", rapid_size, [&]
            { const auto written = rapid_writer.write<Json>(model); observe(written.data() + written.size()); }, true},
        {"luminpack", "json-utf16", "read", json_input.size(), [&]
            { auto object = luminpack::read_json_document<Json>(json_input); observe(&object); }},
        {"rapidjson", "json-utf16", "read", json_input.size(), [&]
            { auto object = rapid_reference::read<Json>(json.c_str()); observe(&object); }, true},
        {"luminpack", "json-utf8", "write", lumin_utf8.size(), [&]
            { const auto written = luminpack::write_json_document<Json, utf8>(utf8_output, model); observe(written.data() + written.size()); }},
        {"rapidjson", "json-utf8", "write", rapid_utf8.size(), [&]
            { const auto written = rapid_utf8_writer.write<Json>(model); observe(written.data() + written.size()); }, true},
        {"luminpack", "json-utf8", "read", utf8_input.size(), [&]
            { auto object = luminpack::read_json_document<Json, utf8>(utf8_input); observe(&object); }},
        {"rapidjson", "json-utf8", "read", utf8_input.size(), [&]
            { auto object = rapid_reference::read_utf8<Json>(utf8_json.c_str()); observe(&object); }, true}
    };

#if defined(LUMINPACK_BENCHMARK_BITSERY) || defined(LUMINPACK_BENCHMARK_CEREAL) || defined(LUMINPACK_BENCHMARK_MSGPACK)
    auto add_binary = [&](const char* library, auto write, auto read)
    {
        const auto size = write(output, model);
        std::vector<char> input(output.begin(), output.begin() + size);
        verify(read(input), library);
        jobs.push_back({library, "binary", "write", size, [&, write]
        {
            const auto written = write(output, model);
            observe(output.data() + written);
        }});
        jobs.push_back({library, "binary", "read", size, [read, input = std::move(input)]
        {
            auto object = read(input);
            observe(&object);
        }});
    };
#endif
#ifdef LUMINPACK_BENCHMARK_BITSERY
    add_binary("bitsery", bitsery_reference::write<model_type>, bitsery_reference::read<model_type>);
#endif
#ifdef LUMINPACK_BENCHMARK_CEREAL
    add_binary("cereal", cereal_reference::write<model_type>, cereal_reference::read<model_type>);
#endif
#ifdef LUMINPACK_BENCHMARK_MSGPACK
    add_binary("msgpack-cxx", msgpack_reference::write<model_type>, msgpack_reference::read<model_type>);
#endif

#if defined(LUMINPACK_BENCHMARK_JSONCONS) || defined(LUMINPACK_BENCHMARK_NLOHMANN_JSON)
    auto add_json = [&](const char* library, auto& writer, auto read)
    {
        const auto written = writer.template write<Json>(model);
        verify(read(written), library);
        verify(read(utf8_json), library);
        jobs.push_back({library, "json-utf8", "write", written.size(), [&, writer = &writer]
        {
            const auto result = writer->template write<Json>(model);
            observe(result.data() + result.size());
        }});
        jobs.push_back({library, "json-utf8", "read", utf8_input.size(), [&, read]
        {
            auto object = read(utf8_json);
            observe(&object);
        }});
    };
#endif
#ifdef LUMINPACK_BENCHMARK_JSONCONS
    jsoncons_reference::reusable_writer jsoncons_writer;
    add_json("jsoncons", jsoncons_writer, jsoncons_reference::read<Json>);
#endif
#ifdef LUMINPACK_BENCHMARK_NLOHMANN_JSON
    nlohmann_reference::reusable_writer nlohmann_writer;
    add_json("nlohmann-json", nlohmann_writer, nlohmann_reference::read<Json>);
#endif
#ifdef LUMINPACK_BENCHMARK_SIMDJSON
    simdjson_reference::reusable_reader simdjson_reader(utf8_json);
    verify(simdjson_reader.template read<Json>(), "simdjson");
    jobs.push_back({"simdjson", "json-utf8", "read", utf8_input.size(), [&]
    {
        auto object = simdjson_reader.template read<Json>();
        observe(&object);
    }});
#endif
    report(name, jobs);
}
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 2)
            throw std::invalid_argument("Usage: luminpack_cpp_benchmark <fixture-directory>");
        const path directory(argv[1]);
        std::cout << std::fixed << std::setprecision(3)
            << "model,format,direction,library,wire_bytes,median_ns,cv_percent,ratio_to_reference,allocations,allocated_bytes\n";
        benchmark_model<simple::root_binary, simple::root_json>(directory, "SimpleClass", "simple", u"Map1");
        benchmark_model<save::root_binary, save::root_json>(directory, "CharacterSaveData", "serialize", u"SkillLevels");
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
