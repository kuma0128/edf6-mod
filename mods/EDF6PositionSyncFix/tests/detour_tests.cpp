#include "absolute_detour.h"

#include <windows.h>

#include <array>
#include <cstdint>
#include <cstring>
#include <iostream>

namespace {

using TestFunction = int(*)();
TestFunction g_original = nullptr;

int replacement() {
    return g_original() + 1;
}

TestFunction to_function(void* address) {
    static_assert(sizeof(address) == sizeof(TestFunction));
    TestFunction function = nullptr;
    std::memcpy(&function, &address, sizeof(function));
    return function;
}

} // namespace

int main() {
    constexpr std::size_t allocationSize = 64;
    auto* code = static_cast<std::uint8_t*>(VirtualAlloc(
        nullptr,
        allocationSize,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_EXECUTE_READWRITE));
    if (code == nullptr) {
        std::cerr << "VirtualAlloc failed.\n";
        return 1;
    }

    std::memset(code, 0x90, allocationSize);
    // mov eax, 41; then reach ret exactly after the 20-byte trampoline span.
    constexpr std::array<std::uint8_t, 5> return41{0xB8, 0x29, 0x00, 0x00, 0x00};
    std::memcpy(code, return41.data(), return41.size());
    code[edf6psf::kAbsoluteDetourSize] = 0xC3;
    FlushInstructionCache(GetCurrentProcess(), code, allocationSize);

    TestFunction target = to_function(code);
    if (target() != 41) {
        std::cerr << "Synthetic target did not execute before detouring.\n";
        VirtualFree(code, 0, MEM_RELEASE);
        return 1;
    }

    edf6psf::AbsoluteDetour detour{};
    if (!edf6psf::install_absolute_detour(
            detour,
            code,
            reinterpret_cast<void*>(&replacement))) {
        std::cerr << "Detour installation failed.\n";
        VirtualFree(code, 0, MEM_RELEASE);
        return 1;
    }

    g_original = to_function(detour.trampoline);
    bool ok = true;
    if (g_original() != 41) {
        std::cerr << "Trampoline did not preserve the original result.\n";
        ok = false;
    }
    if (target() != 42) {
        std::cerr << "Detoured target did not reach the replacement.\n";
        ok = false;
    }

    edf6psf::remove_absolute_detour(detour);
    if (target() != 41) {
        std::cerr << "Target was not restored after removing the detour.\n";
        ok = false;
    }

    VirtualFree(code, 0, MEM_RELEASE);
    if (!ok) {
        return 1;
    }

    std::cout << "Absolute detour/trampoline test passed.\n";
    return 0;
}
