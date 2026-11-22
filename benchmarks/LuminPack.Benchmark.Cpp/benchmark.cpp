#include "simple-benchmark.hpp"
#include "serialize-benchmark.hpp"
#include "rapid_json.h"

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

std::vector<char> load(const path& name)
{
    std::ifstream file(name, std::ios::binary | std::ios::ate);
    const auto length = file.tellg();
    if (length < 0)
        throw std::runtime_error("Missing benchmark fixture: " + name.string());
    std::vector<char> data(static_cast<std::size_t>(length));
    file.seekg(0);
    file.read(data.data(), static_cast<std::streamsize>(data.size()));
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
};

void report(const char* model, std::array<job, 8> jobs)
{
    std::array<std::array<double, repetitions>, jobs.size()> samples{};
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
    for (std::size_t index = 0; index < jobs.size(); ++index)
    {
        const auto& current = jobs[index];
        const auto result = summarize(samples[index]);
        const auto reference = summarize(samples[index | 1u]);
        start_allocation_sample();
        for (int iteration = 0; iteration < allocation_iterations; ++iteration)
            current.run();
        const auto allocated = stop_allocation_sample();
        std::cout << model << ',' << current.format << ',' << current.direction << ',' << current.library << ','
            << current.bytes << ',' << result.median << ',' << result.cv << ',' << result.median / reference.median << ','
            << static_cast<double>(allocated.calls) / allocation_iterations << ','
            << static_cast<double>(allocated.bytes) / allocation_iterations << '\n';
    }
}

template<class Binary, class Json>
void benchmark_model(const path& directory, const char* name, const char* fixture, const char16_t* dictionary)
{
    using model_type = typename Binary::value_type;
    constexpr auto utf8 = luminpack::json_encoding::utf8;
    const auto binary = load(directory / (std::string(fixture) + ".bin"));
    const auto json = load(directory / (std::string(fixture) + ".utf8.json"));
    const auto model = luminpack::read_binary_document<Binary>(std::as_bytes(std::span(binary)));
    if (!model)
        throw std::runtime_error("Unexpected null benchmark model.");

    std::vector<char> output(1u << 20);
    auto writable = std::as_writable_bytes(std::span(output));
    const auto lumin_json_size = luminpack::write_json_document<Json, utf8>(writable, model).size();
    auto reference_output = rapid_reference::parse(std::span(output).first(lumin_json_size));
    order_dictionary(reference_output, dictionary);
    auto verify = [&](const model_type& data, const char* reader)
    {
        const auto size = luminpack::write_json_document<Json, utf8>(writable, data).size();
        auto parsed = rapid_reference::parse(std::span(output).first(size));
        order_dictionary(parsed, dictionary);
        if (!(parsed == reference_output))
            throw std::runtime_error(std::string(reader) + " did not recover the original " + name + " model.");
    };

    verify(luminpack::read_json_document<Json, utf8>(std::as_bytes(std::span(json))), "LuminPack UTF-8");
    const auto sp_size = pack(output, model);
    const std::vector<char> sp_input(output.begin(), output.begin() + sp_size);
    verify(unpack<model_type>(sp_input), "struct_pack");
    rapid_reference::reusable_writer rapid_writer;
    const auto rapid_output = rapid_writer.write<Json>(model);
    const auto rapid_size = rapid_output.size();
    const auto rapid_decoded = rapid_reference::read<Json>(rapid_output);
    verify(rapid_decoded, "RapidJSON round trip");
    verify(rapid_reference::read<Json>(json), "RapidJSON UTF-8");
    const auto lumin_binary_size = luminpack::write_binary_document<Binary>(writable, model).size();

    report(name, {{
        {"luminpack", "binary", "write", lumin_binary_size, [&]
            { const auto written = luminpack::write_binary_document<Binary>(writable, model); observe(written.data() + written.size()); }},
        {"struct_pack", "binary", "write", sp_size, [&]
            { observe(output.data() + pack(output, model)); }},
        {"luminpack", "binary", "read", binary.size(), [&]
            { auto object = luminpack::read_binary_document<Binary>(std::as_bytes(std::span(binary))); observe(&object); }},
        {"struct_pack", "binary", "read", sp_input.size(), [&]
            { auto object = unpack<model_type>(sp_input); observe(&object); }},
        {"luminpack", "json-utf8", "write", lumin_json_size, [&]
            { const auto written = luminpack::write_json_document<Json, utf8>(writable, model); observe(written.data() + written.size()); }},
        {"rapidjson", "json-utf8", "write", rapid_size, [&]
            { const auto written = rapid_writer.write<Json>(model); observe(written.data() + written.size()); }},
        {"luminpack", "json-utf8", "read", json.size(), [&]
            { auto object = luminpack::read_json_document<Json, utf8>(std::as_bytes(std::span(json))); observe(&object); }},
        {"rapidjson", "json-utf8", "read", json.size(), [&]
            { auto object = rapid_reference::read<Json>(json); observe(&object); }}
    }});
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
