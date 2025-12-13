# Distributed under the OSI-approved BSD 3-Clause License.

#[=======================================================================[.rst:
UseFusion
---------

This file provides support for ``Fusion``. It is assumed that the
:module:`FindFusion` module has already been loaded. For example:

.. code-block:: cmake

   find_package(Fusion REQUIRED)
   include(${FUSION_USE_FILE})

CMake Commands
^^^^^^^^^^^^^^

The following command is defined for use with ``Fusion``:

.. command:: fusion_transpile

  Transpile the given source file.

  .. code-block:: cmake

     fusion_transpile(
       SOURCE <input>
       OUTPUT <output>
       [NAMESPACE <namespace>]
       [DEFINES <symbol1> <symbol2> ...]
       [RESOURCES <file1.fu> <file2.fu> ...]
       [INCLUDE_DIRS <dir1> <dir2> ...]
     )

  ``SOURCE``
    The input source file to transpile (required).
  ``OUTPUT``
    The output path for the transpiled code (required). The target
    language is derived from the name suffix. For example, hello.c
    emits C code. The following suffixes (targets) are supported:
    c (C), cpp (C++), cs (C#), d (D), java (Java), js (JavaScript),
    py (Python), swift (Swift), ts (TypeScript), d.ts (TypeScript
    declarations), cl (OpenCL C).
  ``NAMESPACE``
    Specify C++/C# namespace, Java package, or C name prefix.
  ``DEFINES``
    List of conditional compilation symbols.
  ``RESOURCES``
    List of additional source files to read but not emit code for.
  ``INCLUDE_DIRS``
    List of directories to add to resource search path.

  Example usage:

  .. code-block:: cmake

     fusion_transpile(
       SOURCE hello.fu
       OUTPUT ${CMAKE_BINARY_DIR}/hello.cpp
     )

     add_library(hello hello.cpp)

     fusion_transpile(
       SOURCE tests.fu
       OUTPUT ${CMAKE_BINARY_DIR}/tests.cpp
       RESOURCES hello.fu
     )

     add_executable(tests tests.cpp)
     target_link_libraries(tests hello)

#]=======================================================================]

function(fusion_transpile)
  if(NOT Fusion_FOUND)
    message(FATAL_ERROR "Fusion not found")
  endif()

  set(options "")
  set(oneValueArgs SOURCE OUTPUT NAMESPACE)
  set(multiValueArgs DEFINES RESOURCES INCLUDE_DIRS)
  cmake_parse_arguments(FUSION
    "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN}
  )

  if(NOT FUSION_SOURCE)
    message(FATAL_ERROR "SOURCE is required")
  endif()
  if(NOT FUSION_OUTPUT)
    message(FATAL_ERROR "OUTPUT is required")
  endif()

  set(FUSION_ARGS -o ${FUSION_OUTPUT})
  if(FUSION_NAMESPACE)
    list(APPEND FUSION_ARGS -n ${FUSION_NAMESPACE})
  endif()
  foreach(def ${FUSION_DEFINES})
    list(APPEND FUSION_ARGS -D ${def})
  endforeach()
  foreach(res ${FUSION_RESOURCES})
    list(APPEND FUSION_ARGS -r ${res})
  endforeach()
  foreach(inc ${FUSION_INCLUDE_DIRS})
    list(APPEND FUSION_ARGS -I ${inc})
  endforeach()
  list(APPEND FUSION_ARGS ${FUSION_SOURCE})

  # Create output directory structure.
  get_filename_component(OUTPUT_DIR ${FUSION_OUTPUT} DIRECTORY)
  file(MAKE_DIRECTORY ${OUTPUT_DIR})

  add_custom_command(
    OUTPUT ${FUSION_OUTPUT}
    COMMAND ${FUSION_EXECUTABLE} ${FUSION_ARGS}
    DEPENDS ${FUSION_SOURCE} ${FUSION_RESOURCES}
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
    COMMENT "Transpiling ${FUSION_SOURCE} to ${FUSION_OUTPUT}"
    VERBATIM
  )
endfunction()
