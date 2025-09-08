#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

BOOL WINAPI DllMain(HMODULE hDll, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        // Create immediate success indicator
        CreateFileA("C:\\Users\\WowAdmin\\source\\repos\\sethrhod\\BloogBot\\DLL_SUCCESS.txt", 
                    GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
        
        // Also try to create in temp
        CreateFileA("C:\\Temp\\DLL_SUCCESS.txt", 
                    GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    }
    return TRUE;
}