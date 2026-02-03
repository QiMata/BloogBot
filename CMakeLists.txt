# BloogBot/WWoW CMake Build System
cmake_minimum_required(VERSION 3.20)

# Project definition
project(BloogBot 
    VERSION 1.0.0
    DESCRIPTION "Westworld of Warcraft - AI-driven WoW automation system"
    LANGUAGES CXX
)

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Enable compile_commands.json for code intelligence
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Build configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Debug" CACHE STRING "Choose the type of build" FORCE)
endif()

# Platform detection
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(PLATFORM_TARGET "x64")
else()
    set(PLATFORM_TARGET "Win32")
endif()

# Output directories to match existing structure
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${CMAKE_BUILD_TYPE})
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${CMAKE_BUILD_TYPE})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${CMAKE_BUILD_TYPE})

# Multi-config generators (Visual Studio)
foreach(OUTPUTCONFIG ${CMAKE_CONFIGURATION_TYPES})
    string(TOUPPER ${OUTPUTCONFIG} OUTPUTCONFIG_UPPER)
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${OUTPUTCONFIG})
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${OUTPUTCONFIG})
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${OUTPUTCONFIG_UPPER} ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${OUTPUTCONFIG})
endforeach()

# Compiler-specific flags
if(MSVC)
    # Enable debug information in all configurations
    add_compile_options(/Zi)
    add_link_options(/DEBUG)
    
    # Warning level
    add_compile_options(/W3)
    
    # Multi-processor compilation
    add_compile_options(/MP)
    
    # Runtime library
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

# Generate compile_commands.json for AI tooling
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Options
option(BUILD_TESTS "Build test projects" ON)
option(BUILD_NATIVE_ONLY "Build only C++ native projects" OFF)

# Add subdirectories for native C++ projects
add_subdirectory(Exports/Loader)
add_subdirectory(Exports/FastCall)
add_subdirectory(Exports/Navigation)

# Add MCP servers for AI-readiness (optional, commented out for now)
# add_subdirectory(Services/CppCodeIntelligenceMCP)

# Custom targets for .NET builds
if(NOT BUILD_NATIVE_ONLY)
    # Find dotnet CLI
    find_program(DOTNET_EXECUTABLE dotnet)
    if(NOT DOTNET_EXECUTABLE)
        message(WARNING "dotnet CLI not found. .NET projects will not be built through CMake.")
    else()
        message(STATUS "Found dotnet: ${DOTNET_EXECUTABLE}")
        
        # Define .NET build configurations
        set(DOTNET_CONFIGURATION $<IF:$<CONFIG:Debug>,Debug,Release>)
        
        # .NET restore target
        add_custom_target(dotnet_restore
            COMMAND ${DOTNET_EXECUTABLE} restore WestworldOfWarcraft.sln
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            COMMENT "Restoring .NET packages"
        )
        
        # .NET build target
        add_custom_target(dotnet_build
            COMMAND ${DOTNET_EXECUTABLE} build WestworldOfWarcraft.sln 
                --configuration ${DOTNET_CONFIGURATION}
                --no-restore
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            DEPENDS dotnet_restore
            COMMENT "Building .NET projects"
        )
        
        # .NET test target
        if(BUILD_TESTS)
            add_custom_target(dotnet_test
                COMMAND ${DOTNET_EXECUTABLE} test WestworldOfWarcraft.sln
                    --configuration ${DOTNET_CONFIGURATION}
                    --no-build
                    --logger "trx;LogFileName=TestResults.trx"
                    --results-directory ${CMAKE_BINARY_DIR}/TestResults
                WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
                DEPENDS dotnet_build
                COMMENT "Running .NET tests"
            )
        endif()
        
        # Main build target that includes both native and .NET
        add_custom_target(build_all
            DEPENDS dotnet_build
            COMMENT "Building all projects (native + .NET)"
        )
        
        # Make dotnet_build depend on native projects
        add_dependencies(dotnet_build Loader FastCall Navigation)
    endif()
endif()

# Install configuration
install(TARGETS Loader FastCall Navigation
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

# CPack configuration for packaging
set(CPACK_PACKAGE_NAME "BloogBot")
set(CPACK_PACKAGE_VERSION ${PROJECT_VERSION})
set(CPACK_PACKAGE_DESCRIPTION_SUMMARY ${PROJECT_DESCRIPTION})
set(CPACK_PACKAGE_VENDOR "BloogBot Team")
set(CPACK_RESOURCE_FILE_LICENSE "${CMAKE_SOURCE_DIR}/LICENSE")
set(CPACK_PACKAGE_INSTALL_DIRECTORY "BloogBot")

include(CPack)

# Print configuration summary
message(STATUS "")
message(STATUS "=== BloogBot Build Configuration ===")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Platform: ${PLATFORM_TARGET}")
message(STATUS "Output directory: ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
message(STATUS "Build tests: ${BUILD_TESTS}")
message(STATUS "Native only: ${BUILD_NATIVE_ONLY}")
message(STATUS "Compile commands: ${CMAKE_EXPORT_COMPILE_COMMANDS}")
if(DOTNET_EXECUTABLE)
    message(STATUS "Dotnet CLI: ${DOTNET_EXECUTABLE}")
endif()
message(STATUS "=====================================")
message(STATUS "")
