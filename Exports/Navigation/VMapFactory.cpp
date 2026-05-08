// VMapFactory.cpp - Fixed version with proper initialization
#include "VMapFactory.h"
#include "VMapManager2.h"
#include <filesystem>
#include <string>
#include <iostream>

#ifdef _WIN32
#include <windows.h>
extern "C" IMAGE_DOS_HEADER __ImageBase;
#endif

namespace VMAP
{
    static VMapManager2* gVMapManager = nullptr;
    static bool gInitialized = false;

    IVMapManager* VMapFactory::createOrGetVMapManager()
    {
        if (!gVMapManager)
        {
            gVMapManager = new VMapManager2();

            std::string vmapsPath = getVMapsPath();
            gVMapManager->setBasePath(vmapsPath);
        }
        return gVMapManager;
    }

    void VMapFactory::clear()
    {
        if (gVMapManager)
        {
            delete gVMapManager;
            gVMapManager = nullptr;
            gInitialized = false;
        }
    }

    std::string VMapFactory::getVMapsPath()
    {
        // PFS-OVERHAUL-006 strict gate: WWOW_DATA_DIR must be set and vmaps/
        // must exist. The cwd / DLL-relative / DLL-parent / last-resort
        // fallbacks were removed because they silently loaded stale
        // build-output vmaps mirrors. See docs/physics/MMAP_DATA_FLOW.md.
#ifdef _WIN32
        char envDataRoot[1024] = { 0 };
        DWORD len = GetEnvironmentVariableA("WWOW_DATA_DIR", envDataRoot, sizeof(envDataRoot));
        if (len == 0 || len >= sizeof(envDataRoot))
        {
            fprintf(stderr, "[Navigation.dll] FATAL: VMapFactory::getVMapsPath: WWOW_DATA_DIR is not set.\n");
            std::fflush(stderr);
            std::exit(1);
        }

        std::string dataRoot = envDataRoot;
        if (!dataRoot.empty() && dataRoot.back() != '/' && dataRoot.back() != '\\')
            dataRoot += '\\';

        std::string vmapsPath = dataRoot + "vmaps\\";
        if (!std::filesystem::exists(vmapsPath))
        {
            fprintf(stderr, "[Navigation.dll] FATAL: VMapFactory::getVMapsPath: %s does not exist.\n", vmapsPath.c_str());
            std::fflush(stderr);
            std::exit(1);
        }
        return vmapsPath;
#else
        const char* envDataRoot = std::getenv("WWOW_DATA_DIR");
        if (!envDataRoot || !envDataRoot[0])
        {
            fprintf(stderr, "[Navigation.dll] FATAL: VMapFactory::getVMapsPath: WWOW_DATA_DIR is not set.\n");
            std::fflush(stderr);
            std::exit(1);
        }
        std::string dataRoot = envDataRoot;
        if (!dataRoot.empty() && dataRoot.back() != '/' && dataRoot.back() != '\\')
            dataRoot += '/';
        std::string vmapsPath = dataRoot + "vmaps/";
        if (!std::filesystem::exists(vmapsPath))
        {
            fprintf(stderr, "[Navigation.dll] FATAL: VMapFactory::getVMapsPath: %s does not exist.\n", vmapsPath.c_str());
            std::fflush(stderr);
            std::exit(1);
        }
        return vmapsPath;
#endif
    }

    void VMapFactory::initialize()
    {
        if (gInitialized)
            return;

        VMapManager2* manager = static_cast<VMapManager2*>(createOrGetVMapManager());
        if (!manager)
        {
            return;
        }

        gInitialized = true;
    }

    void VMapFactory::initializeMapForContinent(unsigned int mapId)
    {
        if (!gInitialized)
        {
            initialize();
        }

        VMapManager2* manager = static_cast<VMapManager2*>(createOrGetVMapManager());
        if (!manager)
        {
            return;
        }

        // Check if the map file exists before trying to initialize
        std::string vmapsPath = getVMapsPath();
        char filename[256];
        snprintf(filename, sizeof(filename), "%03u.vmtree", mapId);
        std::string fullPath = vmapsPath + filename;

        if (!std::filesystem::exists(fullPath))
        {
            return;
        }

        if (!manager->isMapInitialized(mapId))
        {
            try
            {
                manager->initializeMap(mapId);
            }
            catch (const std::exception& e)
            {
                std::cerr << "[VMAP] Failed to initialize map " << mapId << ": " << e.what() << std::endl;
            }
        }
    }
}
