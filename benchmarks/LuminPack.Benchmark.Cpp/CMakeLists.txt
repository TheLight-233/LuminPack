cmake_minimum_required(VERSION 3.21)
project(LuminPackCppBenchmark LANGUAGES CXX)

set(LUMINPACK_CPP_MODEL_FIXTURES "" CACHE PATH "Generated benchmark headers and .NET fixtures")
set(LUMINPACK_CPP_YALANTINGLIBS "" CACHE PATH "Checkout of yalantinglibs 0.5.8")
set(LUMINPACK_CPP_RAPIDJSON "" CACHE PATH "Checkout of RapidJSON 1.1.0")
foreach(file IN ITEMS
    "${LUMINPACK_CPP_MODEL_FIXTURES}/simple-benchmark.hpp"
    "${LUMINPACK_CPP_MODEL_FIXTURES}/serialize-benchmark.hpp"
    "${LUMINPACK_CPP_YALANTINGLIBS}/include/ylt/struct_pack.hpp"
    "${LUMINPACK_CPP_RAPIDJSON}/include/rapidjson/document.h")
    if(NOT EXISTS "${file}")
        message(FATAL_ERROR "Missing benchmark dependency: ${file}")
    endif()
endforeach()

add_subdirectory(../../src/LuminPack.Cpp "${CMAKE_CURRENT_BINARY_DIR}/luminpack")
add_executable(luminpack_cpp_benchmark benchmark.cpp allocations.cpp)
target_link_libraries(luminpack_cpp_benchmark PRIVATE LuminPack::Cpp)
target_include_directories(luminpack_cpp_benchmark PRIVATE "${LUMINPACK_CPP_MODEL_FIXTURES}")
target_include_directories(luminpack_cpp_benchmark SYSTEM PRIVATE
    "${LUMINPACK_CPP_YALANTINGLIBS}/include"
    "${LUMINPACK_CPP_YALANTINGLIBS}/include/ylt/thirdparty"
    "${LUMINPACK_CPP_RAPIDJSON}/include")
if(MSVC)
    target_compile_options(luminpack_cpp_benchmark PRIVATE /bigobj)
endif()
