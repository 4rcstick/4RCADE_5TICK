# 4RCADE 5TICK

A highly customizable, portable MAME® frontend launcher built in C#/WPF — designed for arcade cabinets, USB deployments, and anyone who wants full control over how their game list looks and feels.

**Status:** v1.0 Beta

![Default theme with custom folder tree](github01.png)

![Summer theme](github02.png)

![Theme Builder comparison](github03.png)

## About

4RCADE 5TICK was not "vibe coded" — it took months of diligent planning. All visual elements and architecture were designed by the author, with heavy use of AI-assisted support (Claude, Anthropic) for coding and troubleshooting. AI was also used to create the images included in this beta release.

The project started life as a ~1600-line PowerShell prototype before being rebuilt from the ground up in C#/WPF.

## Features

- **Theme Builder** — Customize the look of the launcher to your specific needs at any time. Save, load, and share themes as simple files.
- **Auto ROM Scan** — Automatically scans your ROM folder(s) on every boot and builds your game list, updating `mame.ini` automatically so you never get a missing ROM files error.
- **Controller Support** — Drop the mouse and grab your controller to navigate the game list, open and close folders, and launch games.
- **Easy MAME Settings Access** — Adjust several MAME settings directly from the Options window, such as Video Renderer, Pixel Aspect Ratios, and more.
- **Custom Folders** — Create custom folders by selecting a game and hitting `Ctrl+G`. Or just use the built-in Favorites folder with `Ctrl+F`.
- **Custom Folder Ordering** — Rearrange folder order at any time — never feel stuck in an alphabetical list again.
- **Media Previews** — A large preview window for game videos and art assets, with rearrangeable display order.
- **Media Asset Paths** — Uses MAME's standard folders for artwork, flyers, and screenshots out of the box, plus a default folder for preview videos. Every path is configurable.
- **Search** — Quickly find games in a large collection with the search box above the game list.
- **No Installation Required** — No installer, no setup wizard. Unzip to your MAME folder and run.
- **Fullscreen Toggle** — Jump in and out of fullscreen mode with one hotkey (`F11`).
- **Mouse Toggle** — Lock in games that require mouse support with a hotkey (`Ctrl+M`), automatically updating MAME settings on launch and close.

*A feature to auto-sort games into folders (via catver.ini) is planned for a later release.*

## Installation

1. Download the latest release zip from the [Releases](../../releases) page.
2. Extract the contents into your MAME folder.
3. If your ROMs live in MAME's default `roms` folder, 4RCADE 5TICK will find them automatically. Otherwise, point it at your ROMs folder via Options → System Paths.
4. Run `4rcade5tick.exe` — no installer needed.

## Requirements

- Windows 10/11
- MAME installed separately (not included)
- Your own legally obtained ROM files (not included)

## Legal

4RCADE 5TICK contains no ROMs, BIOS files, or copyrighted game assets. You are responsible for supplying your own legally obtained files.

Powered by MAME® — 4RCADE 5TICK is not affiliated with or endorsed by the MAME development team.

4RCADE 5TICK is free to use and modify. No warranty is provided or implied.

## Support & Feedback

Found a bug or have a feature request? Please [open an issue](../../issues).

For general feedback, reach out at Archimedes2012@hotmail.com.

If you've found 4RCADE 5TICK worthy of your hard drive space, consider [buying me a coffee](https://buymeacoffee.com/4rchimede5) — it helps me keep improving the project.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

Created by **4RCHIMEDE5**
