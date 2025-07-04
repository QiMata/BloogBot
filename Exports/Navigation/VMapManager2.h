#ifndef _VMAPMANAGER2_H
#define _VMAPMANAGER2_H

#include "IVMapManager.h"
#include "UnorderedMapSet.h"
#include "Vec3Ray.h"
#include "MapTree.h"
#include "TileAssembler.h"
#include "DynamicTree.h"

#define MAP_FILENAME_EXTENSION2 ".vmtree"

#define FILENAMEBUFFER_SIZE 500

 /**
  * This is the main Class to manage loading and unloading of maps, line of sight, height calculation and so on.
  * For each map or map tile to load it reads a directory file that contains the ModelContainer files used by this map or map tile.
  * Each global map or instance has its own dynamic BSP-Tree.
  * The loaded ModelContainers are included in one of these BSP-Trees.
  * Additionally a table to match map ids and map names is used.
  */

namespace VMAP
{
    enum class VMOFormat { Unknown, WMO, M2 };
    class StaticMapTree;

    /**
     * @brief Class to manage a model and its reference count.
     */
    class ManagedModel
    {
    public:
        /**
         * @brief Constructor to initialize member variables.
         */
        ManagedModel() : iModel(0), iRefCount(0) {}
        /**
         * @brief Sets the model.
         *
         * @param model Pointer to the Model.
         */
        void setModel(WorldModel* model) { iModel = model; }
        /**
         * @brief Gets the model.
         *
         * @return Model* Pointer to the Model.
         */
        WorldModel* getModel() { return iModel; }
        /**
         * @brief Increments the reference count.
         */
        void incRefCount() { ++iRefCount; }
        /**
         * @brief Decrements the reference count.
         *
         * @return int The new reference count.
         */
        int decRefCount() { return --iRefCount; }
    protected:
        WorldModel* iModel; /**< Pointer to the Model. */
        int iRefCount; /**< Reference count for the model. */
    };

    /**
     * @brief Map of instance trees.
     */
    typedef UNORDERED_MAP<unsigned int, StaticMapTree*> InstanceTreeMap;
    /**
     * @brief Map of loaded model files.
     */
    typedef UNORDERED_MAP<std::string, ManagedModel> ModelFileMap;

    /**
     * @brief Enumeration for disabling various VMAP features.
     */
    enum DisableTypes
    {
        VMAP_DISABLE_AREAFLAG = 0x1,
        VMAP_DISABLE_HEIGHT = 0x2,
        VMAP_DISABLE_LOS = 0x4,
        VMAP_DISABLE_LIQUIDSTATUS = 0x8
    };

    /**
     * @brief Class to manage VMAP operations.
     */
    class VMapManager2 : public IVMapManager
    {
    protected:
        // Tree to check collision
        ModelFileMap iLoadedModelFiles; /**< Map of loaded model files. */

        /**
         * @brief Internal method to load a map tile.
         *
         * @param pMapId The map ID.
         * @param basePath The base path to the map files.
         * @param tileX The x-coordinate of the tile.
         * @param tileY The y-coordinate of the tile.
         * @return bool True if the tile was loaded successfully, false otherwise.
         */
        bool _loadMap(unsigned int pMapId, const std::string& basePath, unsigned int tileX, unsigned int tileY);

    public:
        InstanceTreeMap iInstanceMapTrees; /**< Map of instance trees. */
        /**
         * @brief Converts a position from the game world to the internal representation.
         *
         * @param x The x-coordinate in the game world.
         * @param y The y-coordinate in the game world.
         * @param z The z-coordinate in the game world.
         * @return Vec3 The converted position.
         */
        Vec3 convertPositionToInternalRep(float x, float y, float z) const;
        /**
         * @brief Generates the map file name based on the map ID.
         *
         * @param pMapId The map ID.
         * @return std::string The generated map file name.
         */
        static std::string getMapFileName(unsigned int pMapId);

        /**
         * @brief Constructor for VMapManager2.
         */
        VMapManager2();

        /**
         * @brief Constructor for VMapManager2.
         */
        explicit VMapManager2(bool (*IsVMAPDisabledForPtr)(unsigned int, uint8_t));
        /**
         * @brief Destructor for VMapManager2.
         */
        ~VMapManager2();

        /**
         * @brief Loads a map tile.
         *
         * @param pBasePath The base path to the map files.
         * @param pMapId The map ID.
         * @param x The x-coordinate of the tile.
         * @param y The y-coordinate of the tile.
         * @return VMAPLoadResult The result of the load operation.
         */
        VMAPLoadResult loadMap(const char* pBasePath, unsigned int pMapId, int x, int y) override;

        /**
         * @brief Unloads a specific map tile.
         *
         * @param pMapId The map ID.
         * @param x The x-coordinate of the tile.
         * @param y The y-coordinate of the tile.
         */
        void unloadMap(unsigned int pMapId, int x, int y) override;
        /**
         * @brief Unloads a map.
         *
         * @param pMapId The map ID.
         */
        void unloadMap(unsigned int pMapId) override;

        /**
         * @brief Checks if there is a line of sight between two points.
         *
         * @param pMapId The map ID.
         * @param x1 The x-coordinate of the first point.
         * @param y1 The y-coordinate of the first point.
         * @param z1 The z-coordinate of the first point.
         * @param x2 The x-coordinate of the second point.
         * @param y2 The y-coordinate of the second point.
         * @param z2 The z-coordinate of the second point.
         * @return bool True if there is a line of sight, false otherwise.
         */
        bool isInLineOfSight(unsigned int pMapId, float x1, float y1, float z1, float x2, float y2, float z2, bool ignoreM2Model) override;
        /**
         * @brief Gets the hit position of an object in the line of sight.
         *
         * @param pMapId The map ID.
         * @param x1 The x-coordinate of the first point.
         * @param y1 The y-coordinate of the first point.
         * @param z1 The z-coordinate of the first point.
         * @param x2 The x-coordinate of the second point.
         * @param y2 The y-coordinate of the second point.
         * @param z2 The z-coordinate of the second point.
         * @param rx The x-coordinate of the hit position.
         * @param ry The y-coordinate of the hit position.
         * @param rz The z-coordinate of the hit position.
         * @param pModifyDist The distance to modify the hit position.
         * @return bool True if an object was hit, false otherwise.
         */
        bool getObjectHitPos(unsigned int pMapId, float x1, float y1, float z1, float x2, float y2, float z2, float& rx, float& ry, float& rz, float pModifyDist) override;
        /**
         * @brief Gets the height at a specific position.
         *
         * @param pMapId The map ID.
         * @param x The x-coordinate of the position.
         * @param y The y-coordinate of the position.
         * @param z The z-coordinate of the position.
         * @param maxSearchDist The maximum search distance.
         * @return float The height at the position, or VMAP_INVALID_HEIGHT_VALUE if no height is available.
         */
        float getHeight(unsigned int pMapId, float x, float y, float z, float maxSearchDist) override;

        /**
         * @brief Processes a command (for debug and extensions).
         *
         * @param pCommand The command to process.
         * @return bool Always returns false.
         */
        bool processCommand(char* /*pCommand*/) override { return false; }

        /**
         * @brief Gets area information at a specific position.
         *
         * @param pMapId The map ID.
         * @param x The x-coordinate of the position.
         * @param y The y-coordinate of the position.
         * @param z The z-coordinate of the position.
         * @param flags The area flags.
         * @param adtId The ADT ID.
         * @param rootId The root ID.
         * @param groupId The group ID.
         * @return bool True if area information was retrieved, false otherwise.
         */
        bool getAreaInfo(unsigned int pMapId, float x, float y, float& z, unsigned int& flags, int& adtId, int& rootId, int& groupId) const override;
        /**
         * @brief Gets the liquid level at a specific position.
         *
         * @param pMapId The map ID.
         * @param x The x-coordinate of the position.
         * @param y The y-coordinate of the position.
         * @param z The z-coordinate of the position.
         * @param ReqLiquidType The required liquid type.
         * @param level The liquid level.
         * @param floor The floor level.
         * @param type The liquid type.
         * @return bool True if the liquid level was retrieved, false otherwise.
         */
        bool GetLiquidLevel(unsigned int pMapId, float x, float y, float z, uint8_t ReqLiquidType, float& level, float& floor, unsigned int& type) const override;

        /**
         * @brief Acquires a model instance.
         *
         * @param basepath The base path to the model files.
         * @param filename The name of the model file.
         * @param flags The flags for the model.
         * @return Model* The acquired model instance.
         */
        WorldModel* acquireUniversalModelInstance(const std::string& basepath, const std::string& rawFilename, unsigned int flags);

        /**
         * @brief Releases a model instance.
         *
         * @param filename The name of the model file.
         */
        void releaseModelInstance(const std::string& filename);

        /**
         * @brief Generates the directory file name based on the map ID.
         *
         * @param pMapId The map ID.
         * @param x The x-coordinate of the tile (unused).
         * @param y The y-coordinate of the tile (unused).
         * @return std::string The generated directory file name.
         */
        std::string getDirFileName(unsigned int pMapId, int /*x*/, int /*y*/) const override
        {
            return getMapFileName(pMapId);
        }
        /**
         * @brief Checks if a map exists.
         *
         * @param pBasePath The base path to the map files.
         * @param pMapId The map ID.
         * @param x The x-coordinate of the tile.
         * @param y The y-coordinate of the tile.
         * @return bool True if the map exists, false otherwise.
         */
        bool existsMap(const char* pBasePath, unsigned int pMapId, int x, int y) override;

        static VMapManager2* Instance();
        void Initialize();
        static void DestroyInstance();
        /**
         * @brief Function pointer to check if VMAP is disabled for a specific entry and flags.
         */
        typedef bool(*IsVMAPDisabledForFn)(unsigned int entry, uint8_t flags);
        IsVMAPDisabledForFn IsVMAPDisabledForPtr;
    private:
        std::unordered_map<std::string, std::vector<std::string>> _modelFileIndex;
        std::unordered_set<std::string> _modelFilesIndexed;
        std::unordered_set<std::string> _visitedRefs; // For recursive/loop safety
        std::string _basePath; // Store VMAP path for index lookups

        void buildModelFileIndex(const std::string& basePath);
        WorldModel* loadModelByReference(const std::string& ref, unsigned int flags);
        void loadSubmodelsRecursively(WorldModel* model, unsigned int flags);

        static VMapManager2* _instance; 
        DynamicMapTree _dynamicTree;

        // (Optional, but good practice to enforce singleton semantics)
        VMapManager2(const VMapManager2&) = delete;
        VMapManager2(VMapManager2&&) = delete;
        VMapManager2& operator=(const VMapManager2&) = delete;
        VMapManager2& operator=(VMapManager2&&) = delete;

#ifdef MMAP_GENERATOR
    public:
        /**
         * @brief Gets the instance map tree.
         *
         * @param instanceMapTree The instance map tree to populate.
         */
        void getInstanceMapTree(InstanceTreeMap& instanceMapTree);
#endif
    };
}
#endif