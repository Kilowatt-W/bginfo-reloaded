# Origin and sources

[Deutsch](NOTICE.md) | **English**

BGInfo-Reloaded is an independent new development. This file records where the idea, parts of the
source code and the libraries used come from.

## Model: Sysinternals BGInfo

The idea and the feature set (system information on the desktop background, fields, position,
distribution through a configuration file) follow the program
[BGInfo](https://learn.microsoft.com/sysinternals/downloads/bginfo) by Mark Russinovich
(Sysinternals, Microsoft). Thank you – *and thanks for all the Fish*.

**No code and no file was taken from BGInfo**; the `.bgi` file format is neither read nor written.
This project is not affiliated with Microsoft or Sysinternals. "BGInfo" is the name of the original
product and is used here only for reference.

## Model: PowerBGInfo

[PowerBGInfo](https://github.com/EvotecIT/PowerBGInfo) by Evotec (MIT license) was the first
open-source alternative that was examined – thanks for the open groundwork. Two concepts come from there and are implemented
independently here:

- remembering the unmodified original wallpaper, so that repeated runs do not stack text on text
  (`src/WallpaperState.cs`; in PowerBGInfo `PowerBGInfoConfiguration.xml`),
- the colour scheme of the default panel (`ColorConfig` in `src/AppConfig.cs`).

No code from PowerBGInfo is included. The setup scripts for PowerBGInfo that were part of this
project at the beginning (folder `powerbginfo/`, including a wrapper using
[PS2EXE](https://github.com/MScholtes/PS2EXE), MIT) are not part of this repository.

## Origin of the source code

The first version of the source code was created on 2026-09-24 under the name **BgInfoNative**
(version 1.1.0) in a chat session with the AI assistant Claude (Anthropic) and was taken over into
this project as a finished package. The core of the following files comes from that version:

- `src/Program.cs` – entry point and argument parsing
- `src/SystemInfoCollector.cs`, `src/WallpaperState.cs`, `src/WallpaperInterop.cs`
- `src/TaskScheduler.cs` – registration through `schtasks.exe`
- `src/GitHubUpdater.cs`, `src/GitSourceUpdater.cs`, `src/ExecutableSwap.cs` – self-update

Since 2026-10-01 the project has been developed further with Claude Code (configuration file,
placeholders, rendering through GDI+, multi-monitor support, watch mode, log file, editor), and
since version 2.0.0 it has been called **BGInfo-Reloaded**. The complete history with all decisions
is in [`Übergabe.md`](Übergabe.md) (German only).

## Libraries used

| Package | License | Publisher | Purpose |
|---|---|---|---|
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | MIT | Microsoft (.NET Foundation) | rendering through GDI+ |
| [System.Management](https://www.nuget.org/packages/System.Management) | MIT | Microsoft (.NET Foundation) | WMI queries |
| [Microsoft.Win32.Registry](https://www.nuget.org/packages/Microsoft.Win32.Registry) | MIT | Microsoft (.NET Foundation) | registry access |
| .NET 8 runtime, Windows Forms (editor only) | MIT | Microsoft (.NET Foundation) | runtime, embedded in the exe |

Up to version 1.2.0, SixLabors ImageSharp was used for rendering; it was removed completely on
2026-10-01.

## Demo backgrounds and icons

The images in `backgrounds/` and the icons in `icons/` were contributed to this project by the
project author (Kilowatt). "Windows" and "Sysinternals" are trademarks of Microsoft; the images are
not Microsoft material.

## License of this project

BGInfo-Reloaded is released under the MIT license, see [`LICENSE`](LICENSE).
