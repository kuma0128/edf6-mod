# Technical references and attribution

This project contains original implementation code. No game executable or game art is bundled. Self-contained releases embed the Microsoft .NET runtime; its license and third-party notices are included in `licenses`. Promotional thumbnails are AI-generated original illustrations, not gameplay captures.

The EDF6 save format implementation was validated against these public primary sources:

- [FevGrave/EDFSaveEditor](https://github.com/FevGrave/EDFSaveEditor) — reference for EDF6 filename-derived MD5 key/IV generation, AES-256 CTR encryption, CRC-32C placement, and the `MAIN.GST` weapon-table layout. That repository declares CC0 1.0.
- [MadKirbyLV/edf-modding-env](https://github.com/MadKirbyLV/edf-modding-env) — reference used to validate the 1,564 known EDF6 catalog entries and the base game, mission-pack A/B, and individual-DLC ID boundaries. No files from that repository are redistributed here.
- [NIST SP 800-38A](https://csrc.nist.gov/pubs/sp/800/38/a/final) — AES-256 CTR known-answer vector used by the self-test suite.

The runtime position synchronization mod also uses the public plugin ABI from
[BlueAmulet/EDFModLoader](https://github.com/BlueAmulet/EDFModLoader). Its small
compatible header is included under the upstream MIT license; the full notice
is reproduced in `mods/EDF6PositionSyncFix/THIRD_PARTY_NOTICES.md`.

Earth Defense Force, EDF, and related names are trademarks of their respective owners. This is an unofficial community tool.
