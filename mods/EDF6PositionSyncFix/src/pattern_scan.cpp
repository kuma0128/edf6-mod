#include "pattern_scan.h"

#include <cstring>

namespace edf6psf {

ScanResult scan_exact(
    const std::uint8_t* data,
    const std::size_t dataSize,
    const std::uint8_t* pattern,
    const std::size_t patternSize) noexcept {
    ScanResult result{nullptr, 0};
    if (data == nullptr || pattern == nullptr || patternSize == 0 || dataSize < patternSize) {
        return result;
    }

    const std::size_t lastStart = dataSize - patternSize;
    for (std::size_t offset = 0; offset <= lastStart; ++offset) {
        if (data[offset] != pattern[0]) {
            continue;
        }
        if (std::memcmp(data + offset, pattern, patternSize) == 0) {
            if (result.matches == 0) {
                result.address = data + offset;
            }
            ++result.matches;
        }
    }
    return result;
}

} // namespace edf6psf
