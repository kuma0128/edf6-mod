#include "PluginAPI.h"

#include <windows.h>

#include <cstring>
#include <iostream>
#include <filesystem>
#include <fstream>
#include <iterator>

using LoadPluginFn = bool(EDFMLAPI*)(PluginInfo*);

int wmain(int argc, wchar_t** argv) {
    if (argc != 3) {
        std::wcerr << L"Usage: plugin_load_smoke.exe <plugin-dll>\n";
        return 2;
    }
    if (GetModuleHandleW(L"EDF.dll") != nullptr) {
        std::wcerr << L"Test isolation failure: EDF.dll is already loaded.\n";
        return 2;
    }

    HMODULE plugin = LoadLibraryW(argv[1]);
    if (plugin == nullptr) {
        std::wcerr << L"LoadLibraryW failed with error " << GetLastError() << L".\n";
        return 1;
    }

    const FARPROC procedure = GetProcAddress(plugin, "EML6_Load");
    if (procedure == nullptr) {
        std::cerr << "EML6_Load export was not found.\n";
        FreeLibrary(plugin);
        return 1;
    }
    static_assert(sizeof(procedure) == sizeof(LoadPluginFn));
    LoadPluginFn load = nullptr;
    std::memcpy(&load, &procedure, sizeof(load));

    PluginInfo info{};
    const bool result = load(&info);
    bool ok = true;
    if (result) {
        std::cerr << "Plugin should fail closed when EDF.dll is absent.\n";
        ok = false;
    }
    if (info.infoVersion != PluginInfo::MaxInfoVer) {
        std::cerr << "PluginInfo version was not initialized.\n";
        ok = false;
    }
    if (info.name == nullptr || std::strcmp(info.name, "EDF6 Position Sync Fix") != 0) {
        std::cerr << "PluginInfo name was not initialized.\n";
        ok = false;
    }
    if (info.version.components.major != 0 ||
        info.version.components.minor != 2 ||
        info.version.components.patch != 0 ||
        info.version.components.build != 0) {
        std::cerr << "PluginInfo semantic version was not initialized as 0.2.0.0.\n";
        ok = false;
    }

    // The plugin resolves config beside the current executable. Only create an
    // isolated test directory when none exists; never overwrite user config.
    wchar_t executable[32768]{};
    GetModuleFileNameW(nullptr, executable, 32768);
    const auto mods = std::filesystem::path(executable).parent_path() / L"Mods";
    if (!CreateDirectoryW(mods.c_str(), nullptr)) {
        std::cerr << "Isolation failure: test Mods directory already exists.\n";
        FreeLibrary(plugin); return 1;
    }
    const auto config = mods / L"EDF6PositionSyncFix.ini";
    const auto log = mods / L"EDF6PositionSyncFix.log";
    auto logContains = [&](const char* text) {
        std::ifstream input(log, std::ios::binary);
        const std::string data((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
        return data.find(text) != std::string::npos;
    };
    WritePrivateProfileStringW(L"PositionSyncFix", L"Enabled", L"0", config.c_str());
    WritePrivateProfileStringW(L"PositionSyncFix", L"Language", L"en", config.c_str());
    ok = load(&info) && logContains("Disabled by configuration") && ok;
    WritePrivateProfileStringW(L"PositionSyncFix", L"Language", L"ja", config.c_str());
    ok = load(&info) && logContains("設定で無効です") && ok;
    WritePrivateProfileStringW(L"PositionSyncFix", L"Enabled", L"1", config.c_str());
    ok = !load(&info) && logContains("有効なx64コード領域が見つかりません") && ok;
    HMODULE fake = LoadLibraryW(argv[2]);
    if (!fake) ok = false;
    else {
        ok = !load(&info) && logContains("SHA-256検証に失敗") && ok;
        FreeLibrary(fake);
    }
    DeleteFileW(config.c_str()); DeleteFileW(log.c_str()); RemoveDirectoryW(mods.c_str());
    FreeLibrary(plugin);
    if (!ok) {
        return 1;
    }

    std::cout << "Plugin load/fail-closed smoke test passed.\n";
    return 0;
}
