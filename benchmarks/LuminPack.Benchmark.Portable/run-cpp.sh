#!/usr/bin/env bash
set -euo pipefail

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
models=$(cd -- "$1" && pwd)
mkdir -p -- "$2"
output=$(cd -- "$2" && pwd)
work="$output/cpp-work"
mkdir -p -- "$work"

fetch() {
    local name=$1 url=$2 revision=$3
    local directory="$work/$name"
    if [[ ! -d "$directory/.git" ]]; then
        git init --quiet "$directory"
    fi
    git -C "$directory" fetch --quiet --depth=1 "$url" "$revision"
    git -C "$directory" checkout --quiet --detach FETCH_HEAD
    git -C "$directory" log -1 --format='%H %s' > "$output/cpp-$name-version.txt"
}

fetch yalantinglibs https://github.com/alibaba/yalantinglibs.git refs/tags/0.5.8
fetch rapidjson https://github.com/Tencent/rapidjson.git 297637bc0a0f00dc504dbc1a47ebdaa577f8ae19
fetch bitsery https://github.com/fraillt/bitsery.git refs/tags/v5.2.5
fetch cereal https://github.com/USCiLab/cereal.git refs/tags/v1.3.2
fetch msgpack https://github.com/msgpack/msgpack-c.git refs/tags/cpp-7.0.0
fetch jsoncons https://github.com/danielaparker/jsoncons.git refs/tags/v1.3.2
fetch nlohmann https://github.com/nlohmann/json.git refs/tags/v3.12.0
fetch simdjson https://github.com/simdjson/simdjson.git refs/tags/v3.12.3

cmake -S "$root/benchmarks/LuminPack.Benchmark.Cpp" -B "$work/build" -DCMAKE_BUILD_TYPE=Release \
    -DLUMINPACK_CPP_MODEL_FIXTURES="$models" -DLUMINPACK_CPP_YALANTINGLIBS="$work/yalantinglibs" \
    -DLUMINPACK_CPP_RAPIDJSON="$work/rapidjson" -DLUMINPACK_CPP_BITSERY="$work/bitsery" \
    -DLUMINPACK_CPP_CEREAL="$work/cereal" -DLUMINPACK_CPP_MSGPACK="$work/msgpack" \
    -DLUMINPACK_CPP_JSONCONS="$work/jsoncons" -DLUMINPACK_CPP_NLOHMANN_JSON="$work/nlohmann" \
    -DLUMINPACK_CPP_SIMDJSON="$work/simdjson"
cmake --build "$work/build" --parallel 2
"${CXX:-c++}" --version > "$output/cpp-compiler.txt"

for sample in 1 2 3 4 5; do
    "$work/build/luminpack_cpp_benchmark" "$models" > "$output/cpp-process-$sample.csv"
done
python3 "$root/benchmarks/LuminPack.Benchmark.Portable/normalize_cpp.py" "$output/cpp.csv" "$output"/cpp-process-*.csv
