
# 4RCADE 5TICK

A highly customizable, portable MAME® frontend launcher built in C#/WPF — designed for arcade cabinets, USB deployments, or any MAME enthusiast who wants more control over their MAME collection, without having to worry about digging into confusing, cfg, json, or ini files.

**Status:** v1.0.0-beta.3

## 4RCADE 5TICK Features:

### ROM Sorting
<img width="1280" height="720" alt="01_first_launch" src="https://github.com/user-attachments/assets/20b89f08-2a2f-4c53-b316-d2901c4ef877" />
All 39k MAME ROMs shown here are unorganized inside a single folder and automatically sorted into virtual folders using catver.ini.  If you have already organized your ROMs into subfolders, you can turn off auto sort and it will display your folder names.

---

### ROM Filters
<img width="1280" height="720" alt="02_filters" src="https://github.com/user-attachments/assets/7d0782e7-b494-4cd8-9127-e0749308f54c" />
ROMs can also be filtered.  Here is the base filter and genre filters turned on, removing most of the junk and reducing the game pool from 39K to 3.6K ROMs.

---

### Game Previews
<img width="1280" height="720" alt="03_game_info" src="https://github.com/user-attachments/assets/d154bad4-10f9-4e8a-83dc-e2debcf03474" />
Selecting a game displays a preview video, and other artwork and metadata, including game info, trivia, tips & tricks and more.

---

### Themes
<img width="1280" height="720" alt="04_themes" src="https://github.com/user-attachments/assets/2070db61-f787-4399-a662-d5d687d991e2" />
Customize the look using themes that are saved to zip files containing all the theme elements for easy sharing.

---

### Asset Scraper
<img width="1280" height="720" alt="05_scraper" src="https://github.com/user-attachments/assets/ee3d0069-5000-4adc-b6e9-358f8c69af81" />
Turn on and configure the Scraper to automatically download game assets if available.  You can also right click a game title and choose 'Get Artwork'.


## Some other features

- **Random Game** — Click the 'die' icon to trigger the random game function, which includes an animated marquee cycling before landing on the winning game.
- **Auto ROM Scan** — Automatically scans your ROMs folder(s) on every boot and builds your game list, updating `mame.ini` automatically so you never get a missing ROM files error.
- **Controller Support** — Drop the mouse and grab your controller to navigate the game list, open and close folders, and launch games. (more controller functionality coming soon)
- **Easy MAME Settings Access** — Adjust several MAME settings directly from the Options Menu, such as Video Renderer, Pixel Aspect Ratio, and turning bezels on or off globally.
- **Custom Folders** — Create, name, and color custom folders with a right-click context menu in the games list or by hitting `Ctrl+G`. Or just use the built-in Favorites folder with `Ctrl+F`.
- **Custom Folder Ordering** — Rearrange folder order at any time — never feel stuck in an alphabetical list again.
- **Marquee Window** — Displays marquee images above the preview window, falling back to the default logo (or a custom one set in Theme Builder) if no marquee is found for the selected game.
- **Media Asset Paths** — Easily point to all your media assets (marquees/videos/flyers/screen caps/title screens/cabinets) in the Options Menu - Asset Paths tab.
- **Systems Paths** — Set your ROM, CHD, and BIOS paths to any directory on any hard drive. 
- **Search** — Quickly find games in a large collection with the search box above the game list.
- **No Installation Required** — No installer, no setup wizard. Unzip to your MAME folder and run.
- **Fullscreen Toggle** — Jump in and out of fullscreen mode with one hotkey (`F11`).
- **Mouse Toggle** — Lock in mouse support for games that benefit from it with a hotkey (`Ctrl+M`). Shows a visual cue next to the game and automatically updates `mame.ini` to enable mouse support on launch and disable it on exit. Works great for trackball, light gun, and some paddle games.

## Planned for beta.4

- Complete Theme Builder overhaul to make almost any layout possible.

## Known Issues

- **Third-party overlays can conflict with the video preview panel.** RivaTuner Statistics Server (RTSS) and Nvidia's overlay are confirmed to collide with the app's Direct3D11 video pipeline; Other overlays (Discord, Steam, Xbox Game Bar, etc.) haven't been tested yet — if you run into a crash with one, try adding 4RCADE 5TICK to the overlay app's exclusion list if possible.

## Installation

1. Download the latest release zip from the [Releases](../../releases) page.
2. Extract the contents into your MAME folder.
3. If your ROMs live in MAME's default `roms` folder, 4RCADE 5TICK will find them automatically. Otherwise, point it at your ROMs folder via Options → System Paths.
4. Run `4RCADE5TICK.exe` — no installer needed.

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

## AI Disclosure

AI was used both for coding and some artwork.  If you have an issue with the use of AI, at least consider the fact that hundreds if not thousands of hours of my time were needed to plan, place code, and thouroughly test every part of 4RCADE 5TICK.  As a passion project, I hope you can at least give it a chance before denouncing it due to AI help in it's creation.

---

Created by **4RCHIMEDE5**
