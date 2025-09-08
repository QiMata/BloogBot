cmake_minimum_required(VERSION 3.20)
project(CppCodeIntelligenceMCP VERSION 1.0.0)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Try to find nlohmann_json, but don't fail if it's not found
find_package(nlohmann_json QUIET)

if(NOT nlohmann_json_FOUND)
    message(STATUS "nlohmann_json not found, fetching from GitHub...")
    include(FetchContent)
    FetchContent_Declare(
        nlohmann_json
        URL https://github.com/nlohmann/json/releases/download/v3.11.3/json.tar.xz
    )
    FetchContent_MakeAvailable(nlohmann_json)
endif()

# Add executable
add_executable(CppCodeIntelligenceMCP
    src/main.cpp
    src/mcp_server.cpp
    src/symbol_parser.cpp
    src/ast_analyzer.cpp
    src/compile_commands_parser.cpp
)

# Include directories
target_include_directories(CppCodeIntelligenceMCP PRIVATE
    include
    ${CMAKE_SOURCE_DIR}/Bot
    ${CMAKE_SOURCE_DIR}/FastCall
    ${CMAKE_SOURCE_DIR}/Loader
    ${CMAKE_SOURCE_DIR}/Navigation
)

# Link libraries
target_link_libraries(CppCodeIntelligenceMCP PRIVATE
    nlohmann_json::nlohmann_json
)

# Output to Build directory
set_target_properties(CppCodeIntelligenceMCP PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/Build/${PLATFORM_TARGET}/${CMAKE_BUILD_TYPE}
)

# Install target
install(TARGETS CppCodeIntelligenceMCP 
    RUNTIME DESTINATION bin
)
