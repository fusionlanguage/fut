cmake_minimum_required(VERSION 3.13)
project(Fibonacci VERSION 1.0.0 LANGUAGES CXX)
list(APPEND CMAKE_MODULE_PATH ${CMAKE_SOURCE_DIR})
option(BUILD_SHARED_LIBS "Build using shared libraries" ON)

# Find the Fusion transpiler.
find_package(Fusion 3.2 REQUIRED)
include(${FUSION_USE_FILE})

# Create the C++ library.
fusion_transpile(
  SOURCES fibonacci.fu
  OUTPUT ${CMAKE_BINARY_DIR}/fibonacci.cpp
)
add_library(fibonacci fibonacci.cpp)
set_target_properties(fibonacci
  PROPERTIES
  INTERFACE_INCLUDE_DIRECTORIES "${CMAKE_BINARY_DIR}"
)

# Create C++ tests.
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

# (Optional) Create bindings via SWIG.
cmake_policy(SET CMP0086 NEW)
find_package(SWIG 4.0)
if(SWIG_FOUND)
  include(${SWIG_USE_FILE})

  find_package(Lua)
  if(Lua_FOUND)
    # See: https://cmake.org/cmake/help/latest/module/FindLua.html
    if (NOT TARGET Lua::Lua)
      add_library(Lua::Lua INTERFACE IMPORTED)
      set_target_properties(Lua::Lua
        PROPERTIES
        INTERFACE_INCLUDE_DIRECTORIES "${LUA_INCLUDE_DIR}"
        INTERFACE_LINK_LIBRARIES "${LUA_LIBRARIES}"
      )
    endif()

    set_source_files_properties(fibonacci.i
      PROPERTIES
      # Call SWIG in C++ mode.
      CPLUSPLUS ON
      # Specify the actual import name of the module in the target language.
      SWIG_MODULE_NAME fibonacci_lua
    )

    # Generate, compile, and link the Lua binding.
    swig_add_library(fibonacci_lua
      SOURCES fibonacci.i
      TYPE SHARED
      LANGUAGE lua)
    target_link_libraries(fibonacci_lua
      PRIVATE fibonacci Lua::Lua
    )

    set_target_properties(fibonacci_lua
      PROPERTIES
      # Align library name with `SWIG_MODULE_NAME` for seamless Lua import.
      PREFIX ""
    )
  endif()
endif()
