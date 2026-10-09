#include "pattern_scan.h"
#include "patterns.h"
#include "file_hash.h"

#include <windows.h>

#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <vector>

namespace {

struct RawTextSection {
    const std::uint8_t* data;
    std::size_t size;
    std::uint32_t rva;
};

bool read_file(const std::filesystem::path& path, std::vector<std::uint8_t>& bytes) {
    std::ifstream input(path, std::ios::binary | std::ios::ate);
    if (!input) {
        return false;
    }

    const std::streamoff length = input.tellg();
    if (length <= 0 || static_cast<std::uintmax_t>(length) > std::numeric_limits<std::size_t>::max()) {
        return false;
    }

    bytes.resize(static_cast<std::size_t>(length));
    input.seekg(0, std::ios::beg);
    input.read(reinterpret_cast<char*>(bytes.data()), length);
    return input.good();
}

bool get_raw_text_section(const std::vector<std::uint8_t>& bytes, RawTextSection& result) {
    if (bytes.size() < sizeof(IMAGE_DOS_HEADER)) {
        return false;
    }

    const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(bytes.data());
    if (dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew <= 0) {
        return false;
    }

    const std::size_t ntOffset = static_cast<std::size_t>(dos->e_lfanew);
    if (ntOffset > bytes.size() || bytes.size() - ntOffset < sizeof(IMAGE_NT_HEADERS64)) {
        return false;
    }

    const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(bytes.data() + ntOffset);
    if (nt->Signature != IMAGE_NT_SIGNATURE ||
        nt->FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 ||
        nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC) {
        return false;
    }

    const std::size_t sectionTableOffset =
        ntOffset + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) + nt->FileHeader.SizeOfOptionalHeader;
    const std::size_t sectionTableSize =
        static_cast<std::size_t>(nt->FileHeader.NumberOfSections) * sizeof(IMAGE_SECTION_HEADER);
    if (sectionTableOffset > bytes.size() || sectionTableSize > bytes.size() - sectionTableOffset) {
        return false;
    }

    const auto* sections = reinterpret_cast<const IMAGE_SECTION_HEADER*>(bytes.data() + sectionTableOffset);
    for (WORD index = 0; index < nt->FileHeader.NumberOfSections; ++index) {
        const IMAGE_SECTION_HEADER& section = sections[index];
        if (std::memcmp(section.Name, ".text", 5) != 0) {
            continue;
        }

        const std::size_t rawOffset = section.PointerToRawData;
        const std::size_t rawSize = section.SizeOfRawData;
        if (rawSize == 0 || rawOffset > bytes.size() || rawSize > bytes.size() - rawOffset) {
            return false;
        }

        result.data = bytes.data() + rawOffset;
        result.size = rawSize;
        result.rva = section.VirtualAddress;
        return true;
    }
    return false;
}

bool report_signature(
    const char* name,
    const RawTextSection& text,
    const std::uint8_t* pattern,
    const std::size_t patternSize,
    const std::uint32_t knownRva) {
    const auto result = edf6psf::scan_exact(text.data, text.size, pattern, patternSize);
    if (result.matches != 1) {
        std::wcerr << L"Unsupported: " << name << L" signature matches=" << result.matches << L'\n';
        return false;
    }

    const auto offset = static_cast<std::uint32_t>(result.address - text.data);
    const std::uint32_t rva = text.rva + offset;
    std::cout << name << " RVA=0x" << std::hex << std::uppercase << rva;
    if (rva == knownRva) {
        std::cout << " (known build)";
    } else {
        std::cout << " (unexpected RVA; unsupported)";
    }
    std::cout << '\n';
    return rva == knownRva;
}

} // namespace

int wmain(int argc, wchar_t** argv) {
    if (argc != 2) {
        std::wcerr << L"Usage: signature_check.exe <path-to-EDF.dll>\n";
        return 2;
    }

    if (!edf6psf::is_known_game_file(argv[1])) {
        std::wcerr << L"Unsupported EDF.dll SHA-256. No patch is permitted.\n";
        return 1;
    }
    std::vector<std::uint8_t> bytes;
    if (!read_file(std::filesystem::path(argv[1]), bytes)) {
        std::wcerr << L"Could not read: " << argv[1] << L'\n';
        return 2;
    }

    RawTextSection text{};
    if (!get_raw_text_section(bytes, text)) {
        std::wcerr << L"The file is not a supported x64 PE image with a valid .text section.\n";
        return 1;
    }

    const bool updateOk = report_signature(
        "sync-update",
        text,
        edf6psf::kSyncUpdateSignature,
        sizeof(edf6psf::kSyncUpdateSignature),
        edf6psf::kKnownSyncUpdateRva);
    const bool playerBranchOk = report_signature(
        "player-branch",
        text,
        edf6psf::kPlayerBranchSignature,
        sizeof(edf6psf::kPlayerBranchSignature),
        edf6psf::kKnownPlayerBranchRva);

    if (!updateOk || !playerBranchOk) {
        std::wcerr << L"Result: incompatible; the runtime plugin will not patch this build.\n";
        return 1;
    }

    std::cout << "Result: compatible.\n";
    return 0;
}
