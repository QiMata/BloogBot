# Navigation Project CMakeLists.txt
cmake_minimum_required(VERSION 3.20)

project(Navigation LANGUAGES C CXX)

# Collect source files
file(GLOB_RECURSE NAVIGATION_SOURCES
    "*.cpp"
    "*.c"
)

# Collect header files
file(GLOB_RECURSE NAVIGATION_HEADERS
    "*.h"
    "*.hpp"
)

# Remove main from sources if it exists (since we're building a library)
list(FILTER NAVIGATION_SOURCES EXCLUDE REGEX "main\\.cpp$")

# Exclude CMake build directories (compiler ID files contain _main)
list(FILTER NAVIGATION_SOURCES EXCLUDE REGEX "cmake_build")
list(FILTER NAVIGATION_HEADERS EXCLUDE REGEX "cmake_build")

# The service runtime only needs the production exports. Keep native test export
# objects out of Linux service builds to avoid pulling in Windows-only helpers.
if(NOT WIN32)
    list(FILTER NAVIGATION_SOURCES EXCLUDE REGEX "TestExports\\.cpp$")
endif()

# Create the shared library (Navigation.dll on Windows, libNavigation.so on Linux)
add_library(Navigation SHARED ${NAVIGATION_SOURCES} ${NAVIGATION_HEADERS})

set_target_properties(Navigation PROPERTIES
    CXX_STANDARD 20
    CXX_STANDARD_REQUIRED ON
    C_STANDARD 17
    C_STANDARD_REQUIRED ON
    FOLDER "Native"
)

# Include directories
target_include_directories(Navigation PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/Detour
    ${CMAKE_CURRENT_SOURCE_DIR}/Detour/Include
    ${CMAKE_CURRENT_SOURCE_DIR}/Detour/Source
    ${CMAKE_CURRENT_SOURCE_DIR}/g3dlite
    ${CMAKE_CURRENT_SOURCE_DIR}/g3dlite/Include
    ${CMAKE_CURRENT_SOURCE_DIR}/Utilities
)

# Preprocessor definitions
target_compile_definitions(Navigation PRIVATE
    $<$<CONFIG:Debug>:_DEBUG>
    $<$<CONFIG:Release>:NDEBUG>
    NAVIGATION_EXPORTS
    DT_POLYREF64
    USE_STANDARD_MALLOC
    _CRT_SECURE_NO_WARNINGS
)

if(WIN32)
    target_compile_definitions(Navigation PRIVATE
        WIN32
        _WINDOWS
        _USRDLL
    )
endif()

# Compiler options
if(MSVC)
    target_compile_options(Navigation PRIVATE
        /W3
        /MP
        /Zc:strictStrings-
        /FS
        $<$<CONFIG:Debug>:/ZI>
        $<$<CONFIG:Release>:/O2>
    )

    target_link_options(Navigation PRIVATE
        /SUBSYSTEM:$<$<CONFIG:Debug>:CONSOLE>$<$<CONFIG:Release>:WINDOWS>
        $<$<CONFIG:Debug>:/DEBUG>
        $<$<CONFIG:Release>:/OPT:REF>
        $<$<CONFIG:Release>:/OPT:ICF>
    )

    set_property(TARGET Navigation PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
else()
    target_compile_options(Navigation PRIVATE
        -fPIC
        -Wall
        -Wextra
        -Wno-unused-parameter
        -Wno-unused-function
    )
endif()

# Windows-specific libraries
if(WIN32)
    target_link_libraries(Navigation PRIVATE
        kernel32
        user32
        gdi32
        winspool
        comdlg32
        advapi32
        shell32
        ole32
        oleaut32
        uuid
        odbc32
        odbccp32
    )
endif()

# Export header for DLL interface
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/Navigation.h")
    set_target_properties(Navigation PROPERTIES
        PUBLIC_HEADER "Navigation.h"
    )
endif()
