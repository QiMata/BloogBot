using GameData.Core.Constants;
using GameData.Core.Models;
using System;
using System.Runtime.InteropServices;

namespace PathfindingService.Repository
{
    public class Navigation
    {
        private const string DLL_NAME = "Navigation.dll";

        // ===============================
        // ESSENTIAL IMPORTS ONLY
        // ===============================

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FindPath(uint mapId, XYZ start, XYZ end, bool smoothPath, out int length);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void PathArrFree(IntPtr pathArr);

        // ===============================
        // PUBLIC METHODS
        // ===============================
        static Navigation()
        {

        }

        public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
        {
            IntPtr pathPtr = FindPath(mapId, start, end, smoothPath, out int length);

            if (pathPtr == IntPtr.Zero || length == 0)
                return Array.Empty<XYZ>();

            try
            {
                XYZ[] path = new XYZ[length];
                for (int i = 0; i < length; i++)
                {
                    IntPtr currentPtr = IntPtr.Add(pathPtr, i * Marshal.SizeOf<XYZ>());
                    path[i] = Marshal.PtrToStructure<XYZ>(currentPtr);
                }
                return path;
            }
            finally
            {
                PathArrFree(pathPtr);
            }
        }
    }
}