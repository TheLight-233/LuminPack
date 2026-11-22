#pragma once

#include <ylt/reflection/member_ptr.hpp>

#include <tuple>
#include <type_traits>
#include <variant>

namespace binary_model
{
// The two generated fixtures are aggregates whose declaration order is the model order.
template<class T>
concept record = std::is_class_v<T> && std::is_aggregate_v<T>
    && !std::is_same_v<T, std::monostate> && !requires { std::tuple_size<T>::value; };
}
