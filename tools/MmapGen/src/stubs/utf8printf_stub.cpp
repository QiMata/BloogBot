// MmapGen-only stubs for shared/Util.cpp's UTF-8 console-printf wrappers.
//
// vmangos's real `utf8printf`/`vutf8printf` (in `src/shared/Util.cpp`) convert
// UTF-8 input to UTF-16 before writing to the Windows console so that
// non-Latin glyphs render correctly. To pull in the real implementation we'd
// need `Util.cpp` plus its IO/Networking + utf8cpp + MersenneTwister fan-out
// (~30 extra translation units) which is dead weight for an offline navmesh
// generator that prints ASCII status banners.
//
// These stubs just forward to `printf`/`vfprintf`. Acceptable because all
// MmapGen log output is ASCII (numeric tile coords, file paths, status).
//
// Defined inside `shared_mmap` (see top-level `tools/MmapGen/CMakeLists.txt`)
// so it satisfies the symbol references from `shared_mmap.lib(Log.cpp.obj)`.
//
// If a future MmapGen change starts emitting localized strings, swap this
// for the real `Util.cpp`-backed implementation.

#include <cstdarg>
#include <cstdio>

void utf8printf(FILE* out, char const* str, ...)
{
    va_list ap;
    va_start(ap, str);
    vfprintf(out, str, ap);
    va_end(ap);
}

void vutf8printf(FILE* out, char const* str, va_list* ap)
{
    vfprintf(out, str, *ap);
}
