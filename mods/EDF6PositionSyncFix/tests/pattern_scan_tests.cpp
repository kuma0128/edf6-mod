#include "pattern_scan.h"

#include <array>
#include <cstdint>
#include <iostream>

namespace {

bool expect(bool condition, const char* message) {
    if (!condition) {
        std::cerr << "FAIL: " << message << '\n';
        return false;
    }
    return true;
}

} // namespace

int main() {
    constexpr std::array<std::uint8_t, 12> data{
        0x10, 0x20, 0x30, 0x40, 0x50, 0x20,
        0x30, 0x40, 0x60, 0x70, 0x80, 0x90
    };
    constexpr std::array<std::uint8_t, 3> uniquePattern{0x50, 0x20, 0x30};
    constexpr std::array<std::uint8_t, 2> repeatedPattern{0x20, 0x30};
    constexpr std::array<std::uint8_t, 2> missingPattern{0xAA, 0xBB};

    bool ok = true;

    const auto unique = edf6psf::scan_exact(
        data.data(), data.size(), uniquePattern.data(), uniquePattern.size());
    ok &= expect(unique.matches == 1, "unique signature should match once");
    ok &= expect(unique.address == data.data() + 4, "unique signature address should be exact");

    const auto repeated = edf6psf::scan_exact(
        data.data(), data.size(), repeatedPattern.data(), repeatedPattern.size());
    ok &= expect(repeated.matches == 2, "ambiguous signature should report both matches");
    ok &= expect(repeated.address == data.data() + 1, "first ambiguous match should be retained");

    const auto missing = edf6psf::scan_exact(
        data.data(), data.size(), missingPattern.data(), missingPattern.size());
    ok &= expect(missing.matches == 0, "missing signature should not match");
    ok &= expect(missing.address == nullptr, "missing signature should have a null address");

    const auto invalid = edf6psf::scan_exact(nullptr, 0, nullptr, 0);
    ok &= expect(invalid.matches == 0, "invalid input should fail closed");

    if (ok) {
        std::cout << "All pattern scanner tests passed.\n";
        return 0;
    }
    return 1;
}
