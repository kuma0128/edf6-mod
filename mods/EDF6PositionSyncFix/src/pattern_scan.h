#pragma once

#include <cstddef>
#include <cstdint>

namespace edf6psf {

struct ScanResult {
    const std::uint8_t* address;
    std::size_t matches;
};

ScanResult scan_exact(
    const std::uint8_t* data,
    std::size_t dataSize,
    const std::uint8_t* pattern,
    std::size_t patternSize) noexcept;

} // namespace edf6psf
