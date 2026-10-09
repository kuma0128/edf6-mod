#pragma once

#include <array>
#include <cstddef>
#include <cstdint>

namespace edf6psf {

inline constexpr std::size_t kAbsoluteDetourSize = 20;

struct AbsoluteDetour {
    void* target = nullptr;
    void* trampoline = nullptr;
    std::array<std::uint8_t, kAbsoluteDetourSize> originalBytes{};
};

bool install_absolute_detour(
    AbsoluteDetour& detour,
    void* target,
    void* replacement) noexcept;

void remove_absolute_detour(AbsoluteDetour& detour) noexcept;

} // namespace edf6psf
