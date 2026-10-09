#pragma once
#include <windows.h>
#include <cstdint>
#include <cstring>

namespace edf6psf {
// Atomic aligned pointer replacement: no stolen instructions or executable trampoline.
// The caller must keep both functions loaded for the lifetime of the slot.
inline bool replace_pointer(void** slot, void* expected, void* replacement, bool& protectionRestored) noexcept {
    protectionRestored = true;
    if (!slot || !expected || !replacement || reinterpret_cast<std::uintptr_t>(slot) % alignof(void*) != 0) return false;
    DWORD oldProtection = 0;
    if (!VirtualProtect(slot, sizeof(void*), PAGE_READWRITE, &oldProtection)) return false;
    void* previous = InterlockedCompareExchangePointer(reinterpret_cast<void* volatile*>(slot), replacement, expected);
    DWORD ignored = 0;
    protectionRestored = VirtualProtect(slot, sizeof(void*), oldProtection, &ignored) != 0;
    return previous == expected;
}
inline void configure_interval(void* targetPosition, const int interval) noexcept {
    if (!targetPosition) return;
    auto* state = static_cast<std::uint8_t*>(targetPosition) - 0x10;
    const int bounded = interval < 10 ? 10 : interval > 120 ? 120 : interval;
    std::memcpy(state + 0x54, &bounded, sizeof(bounded));
    // Preserve +0x21 (fresh network sample), +0x22 (forced correction) and all
    // position data. Never pretend an extrapolated/stale target was just received.
}
}
