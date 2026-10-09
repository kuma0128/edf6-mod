#include "PluginAPI.h"
#include "player_hook.h"
#include "file_hash.h"
#ifdef _MSC_VER
#include <intrin.h>
#endif
#include "pattern_scan.h"
#include "patterns.h"

#include <windows.h>

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <cwchar>

namespace {

constexpr char kPluginName[] = "EDF6 Position Sync Fix";
constexpr int kDefaultIntervalFrames = 30;
constexpr int kMinIntervalFrames = 10;
constexpr int kMaxIntervalFrames = 120;
constexpr std::size_t kMaxPathLength = 32768;

using CheckerFn = bool(__fastcall*)(void*, void*, void*, void*);
CheckerFn g_originalChecker = nullptr;
void* g_expectedReturn = nullptr;
bool g_installed = false;
bool g_japanese = false;
const char* tr(const char* ja, const char* en) noexcept { return g_japanese ? ja : en; }
void** g_playerCheckerVtable = nullptr;
int g_intervalFrames = kDefaultIntervalFrames;
wchar_t g_logPath[kMaxPathLength]{};

bool make_game_path(const wchar_t* relativePath, wchar_t* output, const std::size_t capacity) noexcept {
    if (relativePath == nullptr || output == nullptr || capacity == 0) {
        return false;
    }

    const DWORD length = GetModuleFileNameW(nullptr, output, static_cast<DWORD>(capacity));
    if (length == 0 || length >= capacity) {
        return false;
    }

    std::size_t directoryLength = length;
    while (directoryLength > 0 && output[directoryLength - 1] != L'\\' && output[directoryLength - 1] != L'/') {
        --directoryLength;
    }

    const std::size_t relativeLength = std::wcslen(relativePath);
    if (directoryLength + relativeLength + 1 > capacity) {
        return false;
    }

    std::wmemcpy(output + directoryLength, relativePath, relativeLength + 1);
    return true;
}

void log_message(const char* level, const char* format, ...) noexcept {
    if (g_logPath[0] == L'\0') {
        return;
    }

    char message[768]{};
    va_list args;
    va_start(args, format);
    std::vsnprintf(message, sizeof(message), format, args);
    va_end(args);

    SYSTEMTIME time{};
    GetLocalTime(&time);

    char line[1024]{};
    const int written = std::snprintf(
        line,
        sizeof(line),
        "%04u-%02u-%02u %02u:%02u:%02u.%03u [%s] %s\r\n",
        time.wYear,
        time.wMonth,
        time.wDay,
        time.wHour,
        time.wMinute,
        time.wSecond,
        time.wMilliseconds,
        level,
        message);
    if (written <= 0) {
        return;
    }

    HANDLE file = CreateFileW(
        g_logPath,
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (file == INVALID_HANDLE_VALUE) {
        return;
    }

    const DWORD byteCount = static_cast<DWORD>(
        written < static_cast<int>(sizeof(line)) ? written : static_cast<int>(sizeof(line) - 1));
    DWORD bytesWritten = 0;
    WriteFile(file, line, byteCount, &bytesWritten, nullptr);
    CloseHandle(file);
}

struct TextSection {
    std::uint8_t* moduleBase;
    std::size_t imageSize;
    std::uint8_t* begin;
    std::size_t size;
};

bool get_loaded_text_section(HMODULE module, TextSection& result) noexcept {
    if (module == nullptr) {
        return false;
    }

    auto* base = reinterpret_cast<std::uint8_t*>(module);
    const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew <= 0 || dos->e_lfanew > 0x100000) {
        return false;
    }

    const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE ||
        nt->FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 ||
        nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC) {
        return false;
    }

    const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(nt);
    for (WORD index = 0; index < nt->FileHeader.NumberOfSections; ++index, ++section) {
        if (std::memcmp(section->Name, ".text", 5) != 0) {
            continue;
        }

        const std::size_t virtualSize = section->Misc.VirtualSize;
        const std::size_t virtualAddress = section->VirtualAddress;
        if (virtualSize == 0 ||
            virtualAddress >= nt->OptionalHeader.SizeOfImage ||
            virtualSize > nt->OptionalHeader.SizeOfImage - virtualAddress) {
            return false;
        }

        result.moduleBase = base;
        result.imageSize = nt->OptionalHeader.SizeOfImage;
        result.begin = base + virtualAddress;
        result.size = virtualSize;
        return true;
    }
    return false;
}

bool __fastcall player_checker_hook(void* checker, void* currentPosition, void* targetPosition, void* velocity) {
#ifdef _MSC_VER
    void* caller = _ReturnAddress();
#else
    void* caller = __builtin_return_address(0);
#endif
    // Only the verified SerializeIntervalCtrl call site passes targetPosition
    // as controller+0x10. Other callers retain unmodified behavior.
    if (caller == g_expectedReturn) edf6psf::configure_interval(targetPosition, g_intervalFrames);
    return g_originalChecker(checker, currentPosition, targetPosition, velocity);
}

int read_interval_frames(const wchar_t* configPath) noexcept {
    int value = GetPrivateProfileIntW(
        L"PositionSyncFix",
        L"IntervalFrames",
        kDefaultIntervalFrames,
        configPath);
    if (value < kMinIntervalFrames) {
        value = kMinIntervalFrames;
    } else if (value > kMaxIntervalFrames) {
        value = kMaxIntervalFrames;
    }
    return value;
}

} // namespace

extern "C" __declspec(dllexport) bool EDFMLAPI EML6_Load(PluginInfo* pluginInfo) {
    if (pluginInfo == nullptr) {
        return false;
    }

    pluginInfo->infoVersion = PluginInfo::MaxInfoVer;
    pluginInfo->name = kPluginName;
    pluginInfo->version = PLUG_VER(0, 2, 0, 0);

    wchar_t configPath[kMaxPathLength]{};
    make_game_path(L"Mods\\EDF6PositionSyncFix.log", g_logPath, kMaxPathLength);
    if (!make_game_path(L"Mods\\EDF6PositionSyncFix.ini", configPath, kMaxPathLength)) {
        log_message("ERROR", tr("設定ファイルのパスを取得できません。", "Could not construct the configuration path."));
        return false;
    }

    wchar_t language[16]{};
    GetPrivateProfileStringW(L"PositionSyncFix", L"Language", L"auto", language, 16, configPath);
    g_japanese = _wcsicmp(language, L"ja") == 0 ||
        (_wcsicmp(language, L"en") != 0 && PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_JAPANESE);
    if (g_installed) return true;

    const bool enabled = GetPrivateProfileIntW(
        L"PositionSyncFix",
        L"Enabled",
        1,
        configPath) != 0;
    if (!enabled) {
        log_message("INFO", tr("設定で無効です。フックを設定しません。", "Disabled by configuration; no hook installed."));
        return true;
    }
    g_intervalFrames = read_interval_frames(configPath);

    HMODULE edfModule = GetModuleHandleW(L"EDF.dll");
    TextSection text{};
    if (!get_loaded_text_section(edfModule, text)) {
        log_message("ERROR", tr("EDF.dllの有効なx64コード領域が見つかりません。", "Could not locate a valid x64 .text section in EDF.dll."));
        return false;
    }

    wchar_t modulePath[kMaxPathLength]{};
    const DWORD modulePathLength = GetModuleFileNameW(edfModule, modulePath, static_cast<DWORD>(kMaxPathLength));
    if (!modulePathLength || modulePathLength >= kMaxPathLength || !edf6psf::is_known_game_file(modulePath)) {
        log_message("ERROR", tr("未対応のEDF.dllです。SHA-256検証に失敗したため何も変更しません。", "Unsupported EDF.dll SHA-256. Nothing was changed."));
        return false;
    }

    const edf6psf::ScanResult update = edf6psf::scan_exact(
        text.begin,
        text.size,
        edf6psf::kSyncUpdateSignature,
        sizeof(edf6psf::kSyncUpdateSignature));
    const edf6psf::ScanResult playerBranch = edf6psf::scan_exact(
        text.begin,
        text.size,
        edf6psf::kPlayerBranchSignature,
        sizeof(edf6psf::kPlayerBranchSignature));

    if (update.matches != 1 || playerBranch.matches != 1 ||
        update.address != text.moduleBase + edf6psf::kKnownSyncUpdateRva ||
        playerBranch.address != text.moduleBase + edf6psf::kKnownPlayerBranchRva ||
        static_cast<std::size_t>(text.begin + text.size - playerBranch.address) < sizeof(edf6psf::kPlayerBranchSignature) + 4) {
        log_message(
            "ERROR",
            tr("未対応のEDF.dll: update一致=%llu、player一致=%llu。何も変更しません。", "Unsupported EDF.dll: sync-update matches=%llu, player-branch matches=%llu. No patch applied."),
            static_cast<unsigned long long>(update.matches),
            static_cast<unsigned long long>(playerBranch.matches));
        return false;
    }

    // kPlayerBranchSignature ends at the LEA opcode. Its displacement is the
    // next int32, and the instruction ends four bytes after the signature.
    std::int32_t vtableDisplacement = 0;
    std::memcpy(
        &vtableDisplacement,
        playerBranch.address + sizeof(edf6psf::kPlayerBranchSignature),
        sizeof(vtableDisplacement));
    auto* playerVtableAddress = const_cast<std::uint8_t*>(playerBranch.address) +
        sizeof(edf6psf::kPlayerBranchSignature) + sizeof(vtableDisplacement) +
        vtableDisplacement;

    const auto imageStart = reinterpret_cast<std::uintptr_t>(text.moduleBase);
    const auto imageEnd = imageStart + text.imageSize;
    const auto vtableValue = reinterpret_cast<std::uintptr_t>(playerVtableAddress);
    if (vtableValue != imageStart + edf6psf::kKnownPlayerVtableRva ||
        vtableValue > imageEnd - (2 * sizeof(void*)) || vtableValue % alignof(void*) != 0) {
        log_message("ERROR", tr("プレイヤー判定の仮想テーブルが想定外です。何も変更しません。", "Player checker vtable resolved outside EDF.dll. No patch applied."));
        return false;
    }
    g_playerCheckerVtable = reinterpret_cast<void**>(playerVtableAddress);

    const auto checkerMethodValue = reinterpret_cast<std::uintptr_t>(g_playerCheckerVtable[1]);
    const auto textStart = reinterpret_cast<std::uintptr_t>(text.begin);
    const auto textEnd = textStart + text.size;
    if (checkerMethodValue != imageStart + 0x58EDB0 || checkerMethodValue < textStart ||
        checkerMethodValue > textEnd - sizeof(edf6psf::kPlayerCheckerMethodPrefix) ||
        std::memcmp(
            reinterpret_cast<const void*>(checkerMethodValue),
            edf6psf::kPlayerCheckerMethodPrefix,
            sizeof(edf6psf::kPlayerCheckerMethodPrefix)) != 0) {
        log_message("ERROR", tr("プレイヤー判定関数の検証に失敗しました。何も変更しません。", "Player checker method validation failed. No patch applied."));
        g_playerCheckerVtable = nullptr;
        return false;
    }

    constexpr std::uint8_t callSite[] = {
        0x49,0x8B,0x01,0x4C,0x8D,0x41,0x10,0x48,0x8B,0x52,0x10,
        0x4C,0x8D,0x4D,0xB7,0x49,0x8B,0xCA,0xFF,0x50,0x08,0x84,0xC0
    };
    if (std::memcmp(text.moduleBase + 0x77AED9, callSite, sizeof(callSite)) != 0) {
        log_message("ERROR", tr("呼び出し元が変更されています。競合するMODを確認してください。", "Call site changed. Check for conflicting mods."));
        return false;
    }
    // Pin before publishing the callback. EDFModLoader has no unload callback.
    HMODULE pinned = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
            reinterpret_cast<LPCWSTR>(&player_checker_hook), &pinned)) {
        log_message("ERROR", tr("プラグインの保持に失敗しました。何も変更しません。", "Could not pin the plugin. Nothing was changed."));
        return false;
    }
    g_originalChecker = reinterpret_cast<CheckerFn>(checkerMethodValue);
    g_expectedReturn = text.moduleBase + 0x77AEEE;
    bool protectionRestored = false;
    if (!edf6psf::replace_pointer(g_playerCheckerVtable + 1,
            reinterpret_cast<void*>(g_originalChecker), reinterpret_cast<void*>(&player_checker_hook), protectionRestored)) {
        log_message("ERROR", tr("フックの設定に失敗しました。競合するMODを確認してください。", "Hook installation failed. Check for conflicting mods."));
        return false;
    }
    g_installed = true;
    if (!protectionRestored) {
        log_message("WARN", tr("フックは有効ですがメモリ保護の復元に失敗しました。ゲームを終了してください。", "Hook active, but page protection restoration failed. Close the game."));
    }

    const auto moduleBase = reinterpret_cast<std::uintptr_t>(edfModule);
    log_message(
        "INFO",
        tr("v0.2.0 読み込み完了: 補正間隔=%d、update RVA=0x%llX、player-vtable RVA=0x%llX。", "Loaded v0.2.0: IntervalFrames=%d, update RVA=0x%llX, player-vtable RVA=0x%llX."),
        g_intervalFrames,
        static_cast<unsigned long long>(reinterpret_cast<std::uintptr_t>(update.address) - moduleBase),
        static_cast<unsigned long long>(vtableValue - moduleBase));
    return true;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);

    }
    return TRUE;
}
