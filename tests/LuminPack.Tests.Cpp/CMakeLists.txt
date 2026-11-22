add_executable(luminpack_cpp_wire wire.cpp)
target_link_libraries(luminpack_cpp_wire PRIVATE LuminPack::Cpp)
add_test(NAME luminpack_cpp_wire
    COMMAND luminpack_cpp_wire "${CMAKE_CURRENT_SOURCE_DIR}/../LuminPack.Tests.Aot/Fixtures")

add_executable(luminpack_cpp_json json.cpp)
target_link_libraries(luminpack_cpp_json PRIVATE LuminPack::Cpp)
add_test(NAME luminpack_cpp_json
    COMMAND luminpack_cpp_json "${CMAKE_CURRENT_SOURCE_DIR}/../LuminPack.Tests.Aot/Fixtures")

set(LUMINPACK_CPP_MODEL_FIXTURES "" CACHE PATH "Generated benchmark headers and .NET model documents")
if(LUMINPACK_CPP_MODEL_FIXTURES)
    add_executable(luminpack_cpp_models benchmark_models.cpp)
    target_include_directories(luminpack_cpp_models PRIVATE "${LUMINPACK_CPP_MODEL_FIXTURES}")
    target_link_libraries(luminpack_cpp_models PRIVATE LuminPack::Cpp)
    add_test(NAME luminpack_cpp_models COMMAND luminpack_cpp_models "${LUMINPACK_CPP_MODEL_FIXTURES}")
endif()
