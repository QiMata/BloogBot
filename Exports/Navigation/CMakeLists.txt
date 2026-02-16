# Navigation Project CMakeLists.txt
cmake_minimum_required(VERSION 3.20)

project(Navigation)

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

# Create the DLL
add_library(Navigation SHARED ${NAVIGATION_SOURCES} ${NAVIGATION_HEADERS})

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
    WIN32
    $<$<CONFIG:Debug>:_DEBUG>
    $<$<CONFIG:Release>:NDEBUG>
    NAVIGATION_EXPORTS
    _WINDOWS
    _USRDLL
    _CRT_SECURE_NO_WARNINGS
)

# Compiler options
target_compile_options(Navigation PRIVATE
    /W3                    # Warning level 3
    /MP                    # Multi-processor compilation
    /Zc:strictStrings-     # Disable strict string literal checking
    /FS                    # Force synchronous PDB writes
    $<$<CONFIG:Debug>:/ZI> # Debug information format
    $<$<CONFIG:Release>:/O2> # Optimization level 2 for release
)

# Linker options
target_link_options(Navigation PRIVATE
    /SUBSYSTEM:$<$<CONFIG:Debug>:CONSOLE>$<$<CONFIG:Release>:WINDOWS>
    $<$<CONFIG:Debug>:/DEBUG>
    $<$<CONFIG:Release>:/OPT:REF>
    $<$<CONFIG:Release>:/OPT:ICF>
)

# Runtime library
set_property(TARGET Navigation PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")

# Output properties
set_target_properties(Navigation PROPERTIES
    CXX_STANDARD 20
    CXX_STANDARD_REQUIRED ON
    FOLDER "Native"
)

# Windows-specific libraries
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

# Export header for DLL interface
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/Navigation.h")
    set_target_properties(Navigation PROPERTIES
        PUBLIC_HEADER "Navigation.h"
    )
endif()
