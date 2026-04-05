# Physics.dll — Local physics engine + scene data
# Builds both x86 and x64 for bot process (FG/BG) and tests
#
# Contains: PhysicsEngine, CollideAndSlide, Movement, GroundSnap, SceneQuery,
#           VMAP, ADT terrain, DynamicObjectRegistry, CapsuleCollision
# Does NOT contain: PathFinder, MoveMap, Detour navmesh (those stay in Navigation.dll)
#
# Usage:
#   cmake -B build_x86 -A Win32
#   cmake --build build_x86 --config Release
#   cmake -B build_x64 -A x64
#   cmake --build build_x64 --config Release

cmake_minimum_required(VERSION 3.20)
project(Physics LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)

# Source directory is the Navigation folder (shared source tree)
set(NAV_SRC "${CMAKE_CURRENT_SOURCE_DIR}/../Navigation")

# Physics engine sources
set(PHYSICS_SOURCES
    ${NAV_SRC}/PhysicsEngine.cpp
    ${NAV_SRC}/PhysicsCollideSlide.cpp
    ${NAV_SRC}/PhysicsMovement.cpp
    ${NAV_SRC}/PhysicsGroundSnap.cpp
    ${NAV_SRC}/PhysicsTestExports.cpp
    ${NAV_SRC}/DllMain.cpp
)

# Scene data sources (physics depends on these for collision queries)
set(SCENE_SOURCES
    ${NAV_SRC}/SceneQuery.cpp
    ${NAV_SRC}/VMapManager2.cpp
    ${NAV_SRC}/StaticMapTree.cpp
    ${NAV_SRC}/ModelInstance.cpp
    ${NAV_SRC}/WorldModel.cpp
    ${NAV_SRC}/MapLoader.cpp
    ${NAV_SRC}/BIH.cpp
    ${NAV_SRC}/DynamicObjectRegistry.cpp
    ${NAV_SRC}/CapsuleCollision.cpp
    ${NAV_SRC}/AABox.cpp
)

# Map/terrain sources
set(MAP_SOURCES
    ${NAV_SRC}/MoveMap.cpp
)

# Detour sources (needed for navmesh queries in physics — ground Z, LOS)
file(GLOB DETOUR_SOURCES "${NAV_SRC}/Detour/Source/*.cpp" "${NAV_SRC}/Detour/Source/*.c")

# G3D math library
file(GLOB G3D_SOURCES "${NAV_SRC}/g3dlite/source/*.cpp")

# Storm library (MPQ reading)
file(GLOB STORM_SOURCES "${NAV_SRC}/Storm/source/*.cpp" "${NAV_SRC}/Storm/source/*.c")

# Combine all sources
set(ALL_SOURCES
    ${PHYSICS_SOURCES}
    ${SCENE_SOURCES}
    ${MAP_SOURCES}
    ${DETOUR_SOURCES}
    ${G3D_SOURCES}
    ${STORM_SOURCES}
)

# Exclude Linux-only test exports on Windows (they have __try which may conflict)
if(NOT WIN32)
    list(FILTER ALL_SOURCES EXCLUDE REGEX "PhysicsTestExports\\.cpp$")
endif()

add_library(Physics SHARED ${ALL_SOURCES})

target_include_directories(Physics PRIVATE
    ${NAV_SRC}
    ${NAV_SRC}/Detour/Include
    ${NAV_SRC}/g3dlite/include
    ${NAV_SRC}/Storm/include
)

# Windows-specific settings
if(WIN32)
    target_compile_definitions(Physics PRIVATE WIN32 _WINDOWS)
    target_compile_options(Physics PRIVATE /EHa)  # Async exception handling for __try compatibility
endif()

# Output to Bot directory
set_target_properties(Physics PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../../Bot/Release/net8.0"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../../Bot/Release/net8.0"
)
