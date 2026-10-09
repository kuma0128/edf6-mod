#include "absolute_detour.h"

#include <windows.h>

#include <cstring>

namespace edf6psf {
namespace {

constexpr std::size_t kAbsoluteJumpSize = 14;

void write_absolute_jump(std::uint8_t* destination, const void* target) noexcept {
    destination[0] = 0xFF;
    destination[1] = 0x25;
    const std::uint32_t zero = 0;
    std::memcpy(destination + 2, &zero, sizeof(zero));
    std::memcpy(destination + 6, &target, sizeof(target));
}

} // namespace

bool install_absolute_detour(
    AbsoluteDetour& detour,
    void* target,
    void* replacement) noexcept {
    if (target == nullptr || replacement == nullptr || detour.trampoline != nullptr) {
        return false;
    }

    auto* targetBytes = static_cast<std::uint8_t*>(target);
    std::memcpy(detour.originalBytes.data(), targetBytes, kAbsoluteDetourSize);

    auto* trampoline = static_cast<std::uint8_t*>(VirtualAlloc(
        nullptr,
        kAbsoluteDetourSize + kAbsoluteJumpSize,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_EXECUTE_READWRITE));
    if (trampoline == nullptr) {
        return false;
    }

    std::memcpy(trampoline, targetBytes, kAbsoluteDetourSize);
    write_absolute_jump(trampoline + kAbsoluteDetourSize, targetBytes + kAbsoluteDetourSize);
    FlushInstructionCache(
        GetCurrentProcess(),
        trampoline,
        kAbsoluteDetourSize + kAbsoluteJumpSize);

    DWORD oldProtection = 0;
    if (!VirtualProtect(targetBytes, kAbsoluteDetourSize, PAGE_EXECUTE_READWRITE, &oldProtection)) {
        VirtualFree(trampoline, 0, MEM_RELEASE);
        return false;
    }

    // Publish the trampoline before the entry point is redirected so that a
    // concurrent first call can always reach the original implementation.
    detour.target = target;
    detour.trampoline = trampoline;

    std::memset(targetBytes, 0x90, kAbsoluteDetourSize);
    write_absolute_jump(targetBytes, replacement);
    FlushInstructionCache(GetCurrentProcess(), targetBytes, kAbsoluteDetourSize);

    DWORD ignoredProtection = 0;
    VirtualProtect(targetBytes, kAbsoluteDetourSize, oldProtection, &ignoredProtection);
    return true;
}

void remove_absolute_detour(AbsoluteDetour& detour) noexcept {
    if (detour.target == nullptr || detour.trampoline == nullptr) {
        return;
    }

    DWORD oldProtection = 0;
    if (VirtualProtect(
            detour.target,
            kAbsoluteDetourSize,
            PAGE_EXECUTE_READWRITE,
            &oldProtection)) {
        std::memcpy(detour.target, detour.originalBytes.data(), kAbsoluteDetourSize);
        FlushInstructionCache(GetCurrentProcess(), detour.target, kAbsoluteDetourSize);
        DWORD ignoredProtection = 0;
        VirtualProtect(detour.target, kAbsoluteDetourSize, oldProtection, &ignoredProtection);
    }

    VirtualFree(detour.trampoline, 0, MEM_RELEASE);
    detour.target = nullptr;
    detour.trampoline = nullptr;
    detour.originalBytes.fill(0);
}

} // namespace edf6psf
