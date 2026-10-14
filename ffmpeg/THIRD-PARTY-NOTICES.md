# Validated prototype native runtime

This application uses libraries from the FFmpeg project, licensed under LGPL
version 2.1 or later. FFmpeg is copyright its authors and contributors; individual
copyright notices are retained in the corresponding source files. This local
prototype is not a completed public binary/source distribution package.

FFmpeg snapshot: `N-127491-g0eeb79fcc`, commit
`0eeb79fcc5b2761e8fdd3d0cfc5e5ba0e786645a`.
Source repository: <https://code.ffmpeg.org/FFmpeg/FFmpeg.git>.
Local source: `E:\Arcade Stick Dev\mabs\build\FFmpeg-git`.
Exact runtime files and SHA-256 hashes: `runtime-manifest.json`.
License: `COPYING.LGPLv2.1.txt`; upstream component licensing: `licenses/FFmpeg-LICENSE.md`.
Build/configuration records: `docs/FFmpeg-build.md` and `docs/mabs-config/` in the
prototype repository. Source availability/release steps: `docs/Distribution.md`.

## Included support-library notices

The recorded FFmpeg configuration enables zlib, bzlib, lzma and iconv. These are
statically incorporated support libraries, even though they have no separate DLL
in the runtime import inventory. Preserve their notices with the FFmpeg DLL set.

| Component | Local build evidence | Notice/license files |
| --- | --- | --- |
| zlib | Installed MABS header 1.3.2.1-motley; source commit `767c4c947852e143f582c85f14cf573411df1b35` | `licenses/zlib-LICENSE.txt` |
| bzip2 / libbzip2 | MSYS2 UCRT package 1.0.8-4 | `licenses/bzip2-LICENSE.txt` |
| XZ / liblzma | MSYS2 UCRT package 5.8.4-1 | `licenses/xz-COPYING.txt`, `licenses/xz-COPYING.0BSD.txt` |
| GNU libiconv / libcharset | MSYS2 UCRT package 1.19-1 | `licenses/libiconv-README.txt`, `licenses/libiconv-COPYING.LIB.txt`, `licenses/libcharset-COPYING.LIB.txt` |

zlib: Copyright (C) 1995–2026 Jean-loup Gailly and Mark Adler.
bzip2/libbzip2: Copyright (C) 1996–2019 Julian R Seward.
XZ Utils: Copyright (C) The XZ Utils authors and contributors.
GNU libiconv/libcharset: copyright their respective authors as retained in source.
The libiconv README distinguishes its LGPL libraries from its GPL command-line
program/documentation; this application does not ship the iconv program. Likewise,
the installed XZ COPYING identifies liblzma as 0BSD and distinguishes other tools.

Versions above are backed by the current installed headers, package database and
local source tree, not an independently reconstructed historical link map. Preserve
the matching archives and package recipes when preparing a public source bundle.

Keep the FFmpeg shared libraries user-replaceable. The prototype checks exact library
versions because its bindings include header-derived native field offsets. Another
FFmpeg version requires matching generated bindings and validation; same-major DLL
names alone do not establish compatibility.
