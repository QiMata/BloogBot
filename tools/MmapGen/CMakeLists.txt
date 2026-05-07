# MmapGen - In-tree mmap/navmesh generator (vmangos lineage, owned in repo)
# ----------------------------------------------------------------------------
# Phase 2 build wiring (per docs/physics/PATHFINDING_OVERHAUL.md). Builds
# `MmapGen.exe` (the executable upstream calls `MoveMapGenerator`) plus the
# minimal set of static libs it actually needs from the staged vmangos tree.
#
# Build (or use ./build-mmapgen.ps1):
#   cmake -B build -S . -G "Visual Studio 17 2022" -A x64
#   cmake --build build --config Release --target MmapGen
#
# DESIGN NOTES (see docs/physics/PATHFINDING_OVERHAUL.md and tools/MmapGen/AGENTS.md):
#
# * The nested `CMakeLists.txt` files at `dep/`, `dep/src/zlib/`, `dep/loadlib/`,
#   `src/`, `src/game/`, `src/shared/`, `contrib/mmap/` are vmangos-monorepo
#   build files. They reference paths that don't exist in our staged tree
#   (e.g. `${CMAKE_SOURCE_DIR}/dep/windows/include/zlib/zlib.h`) and call
#   macros (`mangos_target_flags`) that only exist at the vmangos top level.
#   We DO NOT call `add_subdirectory(...)` into them. Instead this file
#   defines every target inline against the source files we actually need.
#
# * Library minimization (verified by grep over `contrib/mmap/src/*` and
#   `src/game/vmap/*`):
#   - `Recast`     : all 11 sources from `dep/recastnavigation/Recast/Source/`.
#   - `Detour`     : 7 sources from `dep/recastnavigation/Detour/Source/`.
#                    Compiled with `DT_POLYREF64` to match `Exports/Navigation`.
#   - `vmap`       : 7 of the 9 .cpp files in `src/game/vmap/`. `GameObjectModel.cpp`
#                    and `DynamicTree.cpp` are deliberately EXCLUDED -- they
#                    `#include` server-only headers (`World.h`, `GameObject.h`,
#                    `DBCStores.h`, `DBCStructure.h`) that we did not stage.
#                    Confirmed unreferenced by the mmap closure -- the GO bake
#                    path goes through `MapBuilder::buildGameObject()` ->
#                    `WorldModel::readFile()` and never touches GameObjectModel.
#   - `g3dlite_mmap`: minimal subset of `dep/src/g3dlite/` (geometry primitives
#                    only). Files using zlib (`BinaryInput.cpp`, `BinaryOutput.cpp`,
#                    `Crypto.cpp`), Winsock (`NetworkDevice.cpp`, `NetAddress.cpp`),
#                    or POSIX dirent (`FileSystem.cpp`, `fileutils.cpp`) are
#                    omitted. Linker DCE (`/Gy /OPT:REF`) handles unreferenced
#                    function definitions that touch those headers indirectly.
#   - `framework_mmap`: just `Policies/ObjectLifeTime.cpp` (required by
#                    `INSTANTIATE_SINGLETON_1(Log)` in `Log.cpp`). The rest of
#                    `framework/` is header-only for the closure.
#   - `shared_mmap`: `Common.cpp`, `Errors.cpp`, `Log.cpp`, `Config/Config.cpp`,
#                    `ProgressBar.cpp`. The full `shared` lib has 150+ files
#                    (Database, Auth, Crypto, Networking, Compression, etc.)
#                    that the closure does not reference.
#
# * Omitted entirely (no target):
#   - `dep/src/zlib`     : missing public headers (`zlib.h`, `zconf.h` are not
#                          in our staged copy). Closure does not need it -- only
#                          omitted g3dlite/shared files referenced zlib symbols.
#   - `dep/src/bzip2`    : not referenced by closure.
#   - `dep/src/libseh`   : MinGW-only.
#   - `dep/loadlib`      : ADT/WDT readers; only used by mpq_extractor tools,
#                          not by the navmesh generator. Also requires libmpq
#                          which is not present.
#
# * MSVC runtime: `/MD` (dynamic CRT). Matches `Exports/Navigation` runtime
#   and avoids LNK2038 RuntimeLibrary mismatches across the static archives
#   we build here. MmapGen.exe will pin a runtime dependency on the
#   redistributable msvcp140.dll/vcruntime140.dll, which the WWoW dev box
#   already has via VS 18 Community.
#
# * Detour bit split: vmangos's vendored `dep/recastnavigation/Detour/Include/`
#   declares `DT_SALT_BITS=12`, `DT_TILE_BITS=21`, `DT_POLY_BITS=31`. The
#   runtime `Exports/Navigation/Detour/Include/` uses `16/28/20`. This is a
#   RUNTIME-side polyref encoding; per `docs/physics/MMAP_FORMAT.md`, the
#   generator does NOT bake polyrefs into the tile payload (Detour computes
#   them at load time from `dtNavMeshParams::maxTiles`/`maxPolys`). For
#   Phase 2's "byte-identical to external generator" exit gate, KEEPING the
#   vmangos default 12/21/31 here is correct, since the external generator
#   was built from the same vmangos lineage. Phase 6 (native runtime rewrite)
#   may unify these values; do NOT pre-emptively patch the staged Detour
#   header before that lands.
# ----------------------------------------------------------------------------

cmake_minimum_required(VERSION 3.20)
project(MmapGen LANGUAGES CXX C)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Build type" FORCE)
endif()

# Compatibility shim. The staged vmangos CMakeLists.txt files (which we do
# not call into) reference this macro globally. Keep the no-op shim around in
# case any stray include slips through; remove it if/when nested CMakeLists
# files are pruned.
function(mangos_target_flags target)
endfunction()

# ----------------------------------------------------------------------------
# Global compile defines + MSVC flags.
# ----------------------------------------------------------------------------
add_compile_definitions(
    MMAP_GENERATOR
    NO_vsnprintf
    _CRT_SECURE_NO_WARNINGS
    _SILENCE_ALL_CXX17_DEPRECATION_WARNINGS
    NOMINMAX
    WIN32_LEAN_AND_MEAN
)

if(MSVC)
    # /MP   : parallel compile.
    # /W3   : default warning level (W4 drowns signal in vmangos sources).
    # /Zi   : debug info even in Release for crash dumps + symbol resolution.
    # /Gy   : function-level linking (load-bearing for g3dlite minimization
    #         -- lets the linker drop unreferenced inline definitions that
    #         transitively reference zlib/etc. via header pulls).
    # /EHsc : standard C++ exceptions.
    # /bigobj: nlohmann/json + g3dlite templates can exceed 65535 sections.
    add_compile_options(/MP /W3 /Zi /Gy /EHsc /bigobj)
    # /OPT:REF /OPT:ICF: drop unreferenced functions + COMDAT folding (Release
    # default already, but assert it).
    add_link_options(/DEBUG /OPT:REF /OPT:ICF)
    # Dynamic CRT. Must be set before any add_library/add_executable so every
    # target picks it up. Mismatch (e.g. /MT exe linking /MD lib) causes
    # LNK2038 RuntimeLibrary errors and pulls in conflicting msvcprt/libcpmt.
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreadedDLL$<$<CONFIG:Debug>:Debug>")
endif()

# ----------------------------------------------------------------------------
# Common include paths.
# ----------------------------------------------------------------------------
set(MMAPGEN_INC_FRAMEWORK   "${CMAKE_CURRENT_SOURCE_DIR}/src/framework")
set(MMAPGEN_INC_SHARED      "${CMAKE_CURRENT_SOURCE_DIR}/src/shared")
set(MMAPGEN_INC_GAME        "${CMAKE_CURRENT_SOURCE_DIR}/src/game")
set(MMAPGEN_INC_VMAP        "${CMAKE_CURRENT_SOURCE_DIR}/src/game/vmap")
set(MMAPGEN_INC_G3DLITE     "${CMAKE_CURRENT_SOURCE_DIR}/dep/include/g3dlite")
set(MMAPGEN_INC_NLOHMANN    "${CMAKE_CURRENT_SOURCE_DIR}/dep/include")
set(MMAPGEN_INC_ZLIB        "${CMAKE_CURRENT_SOURCE_DIR}/dep/windows/include/zlib")
# Load-bearing: src/game/Maps/MoveMapSharedDefines.h does
#   #include "../recastnavigation/Detour/Include/DetourNavMesh.h"
# which only resolves when an include search dir is `dep/`. Naming this
# `RECAST_PARENT` to make the purpose explicit.
set(MMAPGEN_INC_RECAST_PARENT "${CMAKE_CURRENT_SOURCE_DIR}/dep")
set(MMAPGEN_INC_RECAST      "${CMAKE_CURRENT_SOURCE_DIR}/dep/recastnavigation/Recast/Include")
set(MMAPGEN_INC_DETOUR      "${CMAKE_CURRENT_SOURCE_DIR}/dep/recastnavigation/Detour/Include")

# ----------------------------------------------------------------------------
# Recast (static lib).
# ----------------------------------------------------------------------------
add_library(Recast STATIC
    dep/recastnavigation/Recast/Source/Recast.cpp
    dep/recastnavigation/Recast/Source/RecastAlloc.cpp
    dep/recastnavigation/Recast/Source/RecastArea.cpp
    dep/recastnavigation/Recast/Source/RecastAssert.cpp
    dep/recastnavigation/Recast/Source/RecastContour.cpp
    dep/recastnavigation/Recast/Source/RecastFilter.cpp
    dep/recastnavigation/Recast/Source/RecastLayers.cpp
    dep/recastnavigation/Recast/Source/RecastMesh.cpp
    dep/recastnavigation/Recast/Source/RecastMeshDetail.cpp
    dep/recastnavigation/Recast/Source/RecastRasterization.cpp
    dep/recastnavigation/Recast/Source/RecastRegion.cpp
)
target_include_directories(Recast PUBLIC ${MMAPGEN_INC_RECAST})
set_target_properties(Recast PROPERTIES FOLDER "MmapGen/3rd Party")

# ----------------------------------------------------------------------------
# Detour (static lib). DT_POLYREF64 is already #define'd at
# DetourNavMesh.h:28 in the staged copy; we add it as a PUBLIC compile define
# anyway so that consumers (vmap, MmapGen) inherit it consistently and to
# document the parity with Exports/Navigation.
# ----------------------------------------------------------------------------
add_library(Detour STATIC
    dep/recastnavigation/Detour/Source/DetourAlloc.cpp
    dep/recastnavigation/Detour/Source/DetourAssert.cpp
    dep/recastnavigation/Detour/Source/DetourCommon.cpp
    dep/recastnavigation/Detour/Source/DetourNavMesh.cpp
    dep/recastnavigation/Detour/Source/DetourNavMeshBuilder.cpp
    dep/recastnavigation/Detour/Source/DetourNavMeshQuery.cpp
    dep/recastnavigation/Detour/Source/DetourNode.cpp
)
target_include_directories(Detour PUBLIC
    ${MMAPGEN_INC_DETOUR}
    ${MMAPGEN_INC_RECAST_PARENT}
)
target_compile_definitions(Detour PUBLIC DT_POLYREF64)
set_target_properties(Detour PROPERTIES FOLDER "MmapGen/3rd Party")

# ----------------------------------------------------------------------------
# zlib_mmap (static lib).
# Public headers (zlib.h, zconf.h) live at dep/windows/include/zlib/ -- those
# were copied into the staged tree from the vmangos vendored set; the upstream
# `dep/src/zlib/CMakeLists.txt` references the same path. The zlib symbols are
# referenced by g3dlite's BinaryInput/BinaryOutput/Crypto, which g3dlite_mmap
# needs to define for linker symbol resolution (see Plan agent Risk 6 / Risk 11
# in PATHFINDING_OVERHAUL.md design notes -- /Gy + /OPT:REF cannot DCE
# external symbol references that originate in *compiled* g3dlite member
# functions like CoordinateFrame(Any const&)).
# ----------------------------------------------------------------------------
add_library(zlib_mmap STATIC
    dep/src/zlib/adler32.c
    dep/src/zlib/compress.c
    dep/src/zlib/crc32.c
    dep/src/zlib/deflate.c
    dep/src/zlib/gzclose.c
    dep/src/zlib/gzlib.c
    dep/src/zlib/gzread.c
    dep/src/zlib/gzwrite.c
    dep/src/zlib/infback.c
    dep/src/zlib/inffast.c
    dep/src/zlib/inflate.c
    dep/src/zlib/inftrees.c
    dep/src/zlib/trees.c
    dep/src/zlib/uncompr.c
    dep/src/zlib/zutil.c
)
target_include_directories(zlib_mmap PUBLIC
    ${MMAPGEN_INC_ZLIB}
    ${CMAKE_CURRENT_SOURCE_DIR}/dep/src/zlib
)
set_target_properties(zlib_mmap PROPERTIES FOLDER "MmapGen/3rd Party")

# ----------------------------------------------------------------------------
# g3dlite_mmap (static lib).
# Mirrors the upstream `dep/src/g3dlite/CMakeLists.txt:20-64` source list so
# every g3dlite symbol referenced by transitively compiled g3dlite member
# functions (e.g. `CoordinateFrame(G3D::Any const&)` -> `Any::number()`) has
# a definition. Pulls zlib via BinaryInput/BinaryOutput/Crypto.
# Excludes only:
#   - GThread.cpp (unused, pulls pthread/Win32 thread junk we don't need)
#   - NetworkDevice.cpp / NetAddress.cpp (Winsock; not in upstream's list)
#   - Image*/Mesh*/etc. (also not in upstream's list)
# ----------------------------------------------------------------------------
add_library(g3dlite_mmap STATIC
    dep/src/g3dlite/AABox.cpp
    dep/src/g3dlite/Any.cpp
    dep/src/g3dlite/AnyTableReader.cpp
    dep/src/g3dlite/BinaryFormat.cpp
    dep/src/g3dlite/BinaryInput.cpp
    dep/src/g3dlite/BinaryOutput.cpp
    dep/src/g3dlite/Box.cpp
    dep/src/g3dlite/Capsule.cpp
    dep/src/g3dlite/CollisionDetection.cpp
    dep/src/g3dlite/CoordinateFrame.cpp
    dep/src/g3dlite/Crypto.cpp
    dep/src/g3dlite/Cylinder.cpp
    dep/src/g3dlite/debugAssert.cpp
    dep/src/g3dlite/FileSystem.cpp
    dep/src/g3dlite/fileutils.cpp
    dep/src/g3dlite/format.cpp
    dep/src/g3dlite/g3dfnmatch.cpp
    dep/src/g3dlite/g3dmath.cpp
    dep/src/g3dlite/GThread.cpp
    dep/src/g3dlite/Line.cpp
    dep/src/g3dlite/LineSegment.cpp
    dep/src/g3dlite/Log.cpp
    dep/src/g3dlite/Matrix3.cpp
    dep/src/g3dlite/Matrix4.cpp
    dep/src/g3dlite/MemoryManager.cpp
    dep/src/g3dlite/PhysicsFrame.cpp
    dep/src/g3dlite/Plane.cpp
    dep/src/g3dlite/prompt.cpp
    dep/src/g3dlite/Quat.cpp
    dep/src/g3dlite/Random.cpp
    dep/src/g3dlite/Ray.cpp
    dep/src/g3dlite/RegistryUtil.cpp
    dep/src/g3dlite/Sphere.cpp
    dep/src/g3dlite/stringutils.cpp
    dep/src/g3dlite/System.cpp
    dep/src/g3dlite/TextInput.cpp
    dep/src/g3dlite/TextOutput.cpp
    dep/src/g3dlite/Triangle.cpp
    dep/src/g3dlite/uint128.cpp
    dep/src/g3dlite/UprightFrame.cpp
    dep/src/g3dlite/Vector2.cpp
    dep/src/g3dlite/Vector3.cpp
    dep/src/g3dlite/Vector4.cpp
)
target_include_directories(g3dlite_mmap PUBLIC ${MMAPGEN_INC_G3DLITE})
target_link_libraries(g3dlite_mmap PUBLIC zlib_mmap)
set_target_properties(g3dlite_mmap PROPERTIES FOLDER "MmapGen/3rd Party")

# ----------------------------------------------------------------------------
# framework_mmap (static lib).
# Just ObjectLifeTime.cpp -- required by the singleton instantiation chain in
# Log.cpp. The rest of framework/ is header-only for our closure.
# ----------------------------------------------------------------------------
add_library(framework_mmap STATIC
    src/framework/Policies/ObjectLifeTime.cpp
)
target_include_directories(framework_mmap PUBLIC
    ${MMAPGEN_INC_FRAMEWORK}
    ${MMAPGEN_INC_SHARED}
)
set_target_properties(framework_mmap PROPERTIES FOLDER "MmapGen/Game Libs")

# ----------------------------------------------------------------------------
# shared_mmap (static lib, minimal subset).
# Just enough to satisfy Log.cpp / Config.cpp / Errors.cpp / ProgressBar.cpp
# linker references from vmap and MmapGen. Util.cpp is deliberately NOT
# included; its IO/utf8cpp/MersenneTwister deps are heavy and the closure
# does not call any function it defines (only Util.h is header-included).
# ----------------------------------------------------------------------------
add_library(shared_mmap STATIC
    src/shared/Common.cpp
    src/shared/Errors.cpp
    src/shared/Log.cpp
    src/shared/Config/Config.cpp
    src/shared/ProgressBar.cpp
    # In-tree stub (not from upstream). See file header for rationale.
    src/stubs/utf8printf_stub.cpp
)
target_include_directories(shared_mmap PUBLIC
    ${MMAPGEN_INC_SHARED}
    ${MMAPGEN_INC_FRAMEWORK}
)
target_link_libraries(shared_mmap PUBLIC framework_mmap)
set_target_properties(shared_mmap PROPERTIES FOLDER "MmapGen/Game Libs")

# ----------------------------------------------------------------------------
# vmap (static lib).
# 7 of the 9 .cpp files. GameObjectModel.cpp + DynamicTree.cpp excluded --
# see header note.
# ----------------------------------------------------------------------------
add_library(vmap STATIC
    src/game/vmap/BIH.cpp
    src/game/vmap/MapTree.cpp
    src/game/vmap/ModelInstance.cpp
    src/game/vmap/TileAssembler.cpp
    src/game/vmap/VMapFactory.cpp
    src/game/vmap/VMapManager2.cpp
    src/game/vmap/WorldModel.cpp
)
target_include_directories(vmap PUBLIC
    ${MMAPGEN_INC_VMAP}
    ${MMAPGEN_INC_GAME}
    ${MMAPGEN_INC_G3DLITE}
    ${MMAPGEN_INC_FRAMEWORK}
    ${MMAPGEN_INC_SHARED}
    ${MMAPGEN_INC_RECAST_PARENT}
    ${MMAPGEN_INC_DETOUR}
)
target_link_libraries(vmap PUBLIC g3dlite_mmap shared_mmap framework_mmap Detour)
set_target_properties(vmap PROPERTIES FOLDER "MmapGen/Game Libs")

# ----------------------------------------------------------------------------
# MmapGen (executable). The actual binary.
# ----------------------------------------------------------------------------
add_executable(MmapGen
    contrib/mmap/src/IntermediateValues.cpp
    contrib/mmap/src/generator.cpp
    contrib/mmap/src/MapBuilder.cpp
    contrib/mmap/src/TerrainBuilder.cpp
    contrib/mmap/src/VMapExtensions.cpp
    contrib/mmap/src/TileWorker.cpp
)
target_compile_definitions(MmapGen PRIVATE DEBUG)
target_include_directories(MmapGen PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/contrib/mmap/src
    ${MMAPGEN_INC_VMAP}
    ${MMAPGEN_INC_GAME}
    ${MMAPGEN_INC_FRAMEWORK}
    ${MMAPGEN_INC_SHARED}
    ${MMAPGEN_INC_G3DLITE}
    ${MMAPGEN_INC_NLOHMANN}
    ${MMAPGEN_INC_RECAST_PARENT}
    ${MMAPGEN_INC_RECAST}
    ${MMAPGEN_INC_DETOUR}
)
target_link_libraries(MmapGen PRIVATE
    vmap
    Detour
    Recast
    shared_mmap
    framework_mmap
    g3dlite_mmap
)
set_target_properties(MmapGen PROPERTIES
    FOLDER "MmapGen"
    OUTPUT_NAME "MmapGen"
)
