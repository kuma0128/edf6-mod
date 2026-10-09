#pragma once

// Compatible with BlueAmulet/EDFModLoader's public PluginAPI.h.
// See THIRD_PARTY_NOTICES.md for the MIT license notice.

#ifdef __cplusplus
extern "C" {
#endif

#define EDFMLAPI __fastcall

union PluginVersion {
    struct {
        unsigned short build;
        unsigned short patch;
        unsigned short minor;
        unsigned short major;
    } components;
    unsigned long long raw;
};

#define PLUG_VER(a, b, c, d) {{d, c, b, a}}

typedef struct {
    enum { MaxInfoVer = 1 };
    unsigned long infoVersion;
    const char* name;
    PluginVersion version;
} PluginInfo;

#ifdef __cplusplus
}
#endif
