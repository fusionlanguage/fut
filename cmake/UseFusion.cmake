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

  Transpile the given source file(s).

  .. code-block:: cmake

     fusion_transpile(
       SOURCES <file1.fu> <file2.fu> ...
       OUTPUT <output>
       [NAMESPACE <namespace>]
       [DEFINES <symbol1> <symbol2> ...]
       [REFERENCE <file1.fu> <file2.fu> ...]
       [INCLUDE_DIRS <dir1> <dir2> ...]
     )

  ``SOURCES``
    The input source file(s) to transpile (required).
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
  ``REFERENCE``
    List of additional source files to read but not emit code for.
  ``INCLUDE_DIRS``
    List of directories to add to resource search path.

  Example usage:

  .. code-block:: cmake

     fusion_transpile(
       SOURCES hello.fu
       OUTPUT ${CMAKE_BINARY_DIR}/hello.cpp
     )

     add_library(hello hello.cpp)

     fusion_transpile(
       SOURCES tests.fu
       OUTPUT ${CMAKE_BINARY_DIR}/tests.cpp
       REFERENCE hello.fu
     )

     add_executable(tests tests.cpp)
     target_link_libraries(tests hello)

#]=======================================================================]

function(fusion_transpile)
  if(NOT Fusion_FOUND)
    message(FATAL_ERROR "Fusion not found")
  endif()

  set(options "")
  set(oneValueArgs OUTPUT NAMESPACE)
  set(multiValueArgs DEFINES REFERENCE INCLUDE_DIRS SOURCES)
  cmake_parse_arguments(FUSION
    "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN}
  )

  if(NOT FUSION_SOURCES)
    message(FATAL_ERROR "SOURCES is required")
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
  foreach(res ${FUSION_REFERENCE})
    list(APPEND FUSION_ARGS -r ${res})
  endforeach()
  foreach(inc ${FUSION_INCLUDE_DIRS})
    list(APPEND FUSION_ARGS -I ${inc})
  endforeach()
  foreach(src ${FUSION_SOURCES})
    list(APPEND FUSION_ARGS ${src})
  endforeach()

  # Create output directory structure.
  get_filename_component(OUTPUT_DIR ${FUSION_OUTPUT} DIRECTORY)
  file(MAKE_DIRECTORY ${OUTPUT_DIR})

  add_custom_command(
    OUTPUT ${FUSION_OUTPUT}
    COMMAND ${FUSION_EXECUTABLE} ${FUSION_ARGS}
    DEPENDS ${FUSION_SOURCES} ${FUSION_REFERENCE}
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
    COMMENT "Transpiling ${FUSION_SOURCES} to ${FUSION_OUTPUT}"
    VERBATIM
  )
endfunction()
