#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

BOOL WINAPI DllMain(HMODULE hDll, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        // Create immediate success indicators in multiple locations
        CreateFileA("C:\\Users\\WowAdmin\\source\\repos\\sethrhod\\BloogBot\\MINIMAL_INJECTION_SUCCESS.txt", 
                    GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
        
        CreateFileA("C:\\Temp\\MINIMAL_INJECTION_SUCCESS.txt", 
                    GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
                    
        // Also create in current directory
        CreateFileA("MINIMAL_SUCCESS.txt", 
                    GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
                    
        // Show message box for immediate confirmation
        MessageBoxA(NULL, "MINIMAL DLL INJECTION SUCCESS!", "Success", MB_OK | MB_TOPMOST);
    }
    return TRUE;
}