# FastCall Project CMakeLists.txt
cmake_minimum_required(VERSION 3.20)

project(FastCall)

# Source files
set(FASTCALL_SOURCES
    dllmain.cpp
    stdafx.cpp
)

# Header files
set(FASTCALL_HEADERS
    stdafx.h
    targetver.h
)

# Create the DLL
add_library(FastCall SHARED ${FASTCALL_SOURCES} ${FASTCALL_HEADERS})

# Preprocessor definitions
target_compile_definitions(FastCall PRIVATE
    WIN32
    $<$<CONFIG:Debug>:_DEBUG>
    $<$<CONFIG:Release>:NDEBUG>
    FASTCALL_EXPORTS
    _WINDOWS
    _USRDLL
    _CRT_SECURE_NO_WARNINGS
)

# Compiler options
target_compile_options(FastCall PRIVATE
    /W3                    # Warning level 3
    /MP                    # Multi-processor compilation
    /Zc:strictStrings-     # Disable strict string literal checking
    /FS                    # Force synchronous PDB writes
    $<$<CONFIG:Debug>:/ZI> # Debug information format
    $<$<CONFIG:Release>:/O2> # Optimization level 2 for release
)

# Linker options
target_link_options(FastCall PRIVATE
    /SUBSYSTEM:$<$<CONFIG:Debug>:CONSOLE>$<$<CONFIG:Release>:WINDOWS>
    $<$<CONFIG:Debug>:/DEBUG>
    $<$<CONFIG:Release>:/OPT:REF>
    $<$<CONFIG:Release>:/OPT:ICF>
)

# Runtime library
set_property(TARGET FastCall PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")

# Output properties
set_target_properties(FastCall PROPERTIES
    CXX_STANDARD 17
    CXX_STANDARD_REQUIRED ON
    FOLDER "Native"
)

# Windows-specific libraries
target_link_libraries(FastCall PRIVATE
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
