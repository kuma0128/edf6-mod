#include "player_hook.h"
#include "file_hash.h"
#include <array>
#include <cstring>
#include <iostream>
#include <vector>
int original() { return 41; }
int replacement() { return 42; }
int main() {
    int failures = 0;
    auto check = [&](bool value, const char* name) { if (!value) { ++failures; std::cerr << "FAIL " << name << "\n"; } };
    auto** slot = static_cast<void**>(VirtualAlloc(nullptr, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE));
    if (!slot) return 1;
    *slot = reinterpret_cast<void*>(&original);
    DWORD old = 0; VirtualProtect(slot, 4096, PAGE_READONLY, &old);
    bool restored = false;
    check(edf6psf::replace_pointer(slot, reinterpret_cast<void*>(&original), reinterpret_cast<void*>(&replacement), restored), "install");
    check(restored && reinterpret_cast<int(*)()>(*slot)() == 42, "call");
    check(!edf6psf::replace_pointer(slot, reinterpret_cast<void*>(&original), reinterpret_cast<void*>(&replacement), restored), "conflict refusal");
    check(edf6psf::replace_pointer(slot, reinterpret_cast<void*>(&replacement), reinterpret_cast<void*>(&original), restored), "restore");
    check(restored && reinterpret_cast<int(*)()>(*slot)() == 41, "original preserved");
    VirtualFree(slot, 0, MEM_RELEASE);
    for (int interval : {-1, 10, 30, 120, 1000}) {
        std::array<std::uint8_t, 128> state{}; state.fill(0xA5); auto expected = state;
        const int value = interval < 10 ? 10 : interval > 120 ? 120 : interval;
        std::memcpy(expected.data() + 0x54, &value, sizeof(value));
        edf6psf::configure_interval(state.data() + 0x10, interval);
        check(state == expected, "only interval changes; fresh-sample flag and position preserved");
    }
    edf6psf::configure_interval(nullptr, 30);
    wchar_t directory[MAX_PATH]{}, file[MAX_PATH]{};
    GetTempPathW(MAX_PATH, directory); GetTempFileNameW(directory, L"eds", 0, file);
    HANDLE handle = CreateFileW(file, GENERIC_WRITE, 0, nullptr, TRUNCATE_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    DWORD written = 0; WriteFile(handle, "abc", 3, &written, nullptr); CloseHandle(handle);
    char hash[65]{};
    check(edf6psf::sha256_file(file, hash), "file hash");
    check(std::strcmp(hash, "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD") == 0, "SHA256 known vector");
    check(!edf6psf::is_known_game_file(file), "unknown build refusal"); DeleteFileW(file);
    check(!edf6psf::is_known_game_file(file), "missing build refusal");
    std::cout << (failures ? "FAILED" : "PASS") << " pointer, field isolation and SHA256 tests\n";
    return failures ? 1 : 0;
}
