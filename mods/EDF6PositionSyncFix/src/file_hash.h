#pragma once
#include <windows.h>
#include <bcrypt.h>
#include <array>
#include <cstdint>
#include <cstring>

namespace edf6psf {
inline constexpr char kKnownSha256[] = "0D5092393A1B9D8F1B75B0C93D8A8F65615A36A92C46E40349C2B66C733D0916";

inline bool sha256_file(const wchar_t* path, char (&output)[65]) noexcept {
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return false;
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    bool ok = BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) >= 0;
    if (ok) ok = BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) >= 0;
    std::array<unsigned char, 65536> buffer{};
    while (ok) {
        DWORD count = 0;
        if (!ReadFile(file, buffer.data(), static_cast<DWORD>(buffer.size()), &count, nullptr)) { ok = false; break; }
        if (count == 0) break;
        ok = BCryptHashData(hash, buffer.data(), count, 0) >= 0;
    }
    std::array<unsigned char, 32> digest{};
    if (ok) ok = BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) >= 0;
    if (hash) BCryptDestroyHash(hash);
    if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0);
    CloseHandle(file);
    if (ok) {
        constexpr char hex[] = "0123456789ABCDEF";
        for (std::size_t i = 0; i < digest.size(); ++i) { output[i * 2] = hex[digest[i] >> 4]; output[i * 2 + 1] = hex[digest[i] & 15]; }
        output[64] = 0;
    }
    return ok;
}
inline bool is_known_game_file(const wchar_t* path) noexcept {
    char actual[65]{};
    return sha256_file(path, actual) && std::strcmp(actual, kKnownSha256) == 0;
}
}
