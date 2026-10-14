# Local FFmpeg runtime

Built by the existing MABS environment for this prototype; these are x64 shared
libraries from FFmpeg commit `0eeb79fcc5b2761e8fdd3d0cfc5e5ba0e786645a`.
Runtime version: `N-127491-g0eeb79fcc`.

| DLL | Exported library version |
| --- | --- |
| avutil-61.dll | 61.14.100 |
| swresample-7.dll | 7.3.101 |
| swscale-10.dll | 10.2.100 |
| avcodec-63.dll | 63.20.100 |
| avformat-63.dll | 63.10.100 |

All five report `LGPL version 2.1 or later`. See `COPYING.LGPLv2.1.txt`.
`runtime-manifest.json` records exact SHA-256 hashes, sizes, source revision, and
local source/build locations. This is a development snapshot from the repository
configured in MABS, not a pinned FFmpeg release package. Keep the DLLs together as
one set; future decoding bindings must match these ABI versions.

PE import inspection found only these FFmpeg libraries plus Windows system/UCRT
libraries. No other third-party DLLs are needed. `avdevice`, `avfilter`, command-line
programs, development headers, import libraries, and old MABS archives are omitted.
The five selected DLLs occupy approximately 26.8 MiB in total.

The exact configure report, input options, compiler feature flags, and import lists
are retained under the project's `docs/mabs-config` directory. The local FFmpeg
source remains in `E:\Arcade Stick Dev\mabs\build\FFmpeg-git`. MABS statically links
the enabled support libraries (bzlib, iconv, lzma, zlib) into this shared build; their
sources and license material belong to that build environment. This folder is a
local prototype runtime, not a prepared public binary/source distribution bundle.

Collected notices and support-library licenses are now included in
`THIRD-PARTY-NOTICES.md` and `licenses/`. See `docs/Distribution.md` for source
availability/rebuild instructions and the remaining public-distribution checklist.
The current DLLs and generated native bindings are frozen together for initial
launcher integration; do not replace them with arbitrary same-major builds.
