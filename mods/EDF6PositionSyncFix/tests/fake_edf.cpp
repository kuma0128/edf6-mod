#include <windows.h>
// Deliberately unsupported module for fail-closed tests; contains no game code.
extern "C" __declspec(dllexport) int test_marker() { return 1; }
BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID) { return TRUE; }
