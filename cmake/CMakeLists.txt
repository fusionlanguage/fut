cmake_minimum_required(VERSION 3.13)
project(Fibonacci VERSION 1.0.0 LANGUAGES CXX)
list(APPEND CMAKE_MODULE_PATH ${CMAKE_SOURCE_DIR})
option(BUILD_SHARED_LIBS "Build using shared libraries" ON)

# Find the Fusion transpiler.
find_package(Fusion 3.2 REQUIRED)
include(${FUSION_USE_FILE})

# Create the library.
fusion_transpile(
  SOURCES fibonacci.fu
  OUTPUT ${CMAKE_BINARY_DIR}/fibonacci.cpp
)
add_library(fibonacci fibonacci.cpp)

# Create tests.
fusion_transpile(
  SOURCES tests.fu
  OUTPUT ${CMAKE_BINARY_DIR}/tests.cpp
  REFERENCE fibonacci.fu
)
add_executable(tests tests.cpp)
target_link_libraries(tests fibonacci)

# Add test target.
include(CTest)
add_test(NAME tests COMMAND tests)
