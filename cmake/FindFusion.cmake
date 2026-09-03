# Distributed under the OSI-approved BSD 3-Clause License.

#[=======================================================================[.rst:
FindFusion
----------

Find the Fusion transpiler (fut) executable.

This module finds an installed Fusion transpiler and determines its
version. The following variables are defined:

``Fusion_FOUND``
  Whether Fusion was found on the system.
``FUSION_EXECUTABLE``
  Path to the fut executable.
``FUSION_VERSION``
  Fusion executable version (result of ``fut --version``).
``FUSION_USE_FILE``
  Path to UseFusion.cmake for convenience macros.

All information is collected from the ``FUSION_EXECUTABLE``, so the
version to be found can be changed from the command line by means of
setting ``FUSION_EXECUTABLE``.

Example usage:

.. code-block:: cmake

   find_package(Fusion 3.0 REQUIRED)
   include(${FUSION_USE_FILE})

#]=======================================================================]

find_program(FUSION_EXECUTABLE
  NAMES fut
  DOC "Path to Fusion transpiler"
)

if(FUSION_EXECUTABLE)
  execute_process(
    COMMAND "${FUSION_EXECUTABLE}" --version
    OUTPUT_VARIABLE FUSION_VERSION_OUTPUT
    ERROR_VARIABLE FUSION_VERSION_OUTPUT
    RESULT_VARIABLE FUSION_VERSION_RESULT
    OUTPUT_STRIP_TRAILING_WHITESPACE
  )

  if(FUSION_VERSION_RESULT EQUAL 0)
    string(REGEX MATCH "[0-9]+\\.[0-9]+(\\.[0-9]+)?"
      FUSION_VERSION "${FUSION_VERSION_OUTPUT}")
  endif()
endif()

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(Fusion
  REQUIRED_VARS FUSION_EXECUTABLE
  VERSION_VAR FUSION_VERSION
)

if(Fusion_FOUND)
  set(FUSION_USE_FILE "${CMAKE_CURRENT_LIST_DIR}/UseFusion.cmake")

  if(NOT TARGET Fusion::fut)
    add_executable(Fusion::fut IMPORTED)
    set_target_properties(Fusion::fut PROPERTIES
      IMPORTED_LOCATION "${FUSION_EXECUTABLE}"
    )
  endif()
endif()

mark_as_advanced(FUSION_EXECUTABLE FUSION_VERSION)
