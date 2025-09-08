#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <iostream>
#include <fstream>

BOOL WINAPI DllMain(HMODULE hDll, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        // Disable thread library calls for performance
        DisableThreadLibraryCalls(hDll);
        
        // Create immediate success indicators in multiple locations
        try {
            // Method 1: CreateFile (most reliable)
            HANDLE hFile = CreateFileA("C:\\Users\\WowAdmin\\source\\repos\\sethrhod\\BloogBot\\SIMPLE_DLL_SUCCESS.txt", 
                                      GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
            if (hFile != INVALID_HANDLE_VALUE) {
                DWORD written;
                const char* msg = "SIMPLE DLL INJECTION SUCCESS!\n";
                WriteFile(hFile, msg, (DWORD)strlen(msg), &written, NULL);
                CloseHandle(hFile);
            }
            
            // Method 2: Temp directory
            HANDLE hFile2 = CreateFileA("C:\\Temp\\SIMPLE_DLL_SUCCESS.txt", 
                                       GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
            if (hFile2 != INVALID_HANDLE_VALUE) {
                DWORD written;
                const char* msg = "SIMPLE DLL INJECTION SUCCESS IN TEMP!\n";
                WriteFile(hFile2, msg, (DWORD)strlen(msg), &written, NULL);
                CloseHandle(hFile2);
            }
            
            // Method 3: Debug output
            OutputDebugStringA("SIMPLE DLL INJECTION SUCCESS - DEBUG OUTPUT!");
            
            // Method 4: Message box for immediate confirmation
            MessageBoxA(NULL, "SIMPLE DLL INJECTION SUCCESS!", "Success", MB_OK | MB_TOPMOST);
            
        } catch (...) {
            // Even if logging fails, don't fail the DLL load
        }
    }
    
    // Always return TRUE to indicate successful load
    return TRUE;
}