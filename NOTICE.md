# Herkunft und Quellen

**Deutsch** | [English](NOTICE.en.md)

BGInfo-Reloaded ist eine eigenständige Neuentwicklung. Diese Datei hält fest, woher Idee, Teile des
Quellcodes und die verwendeten Bibliotheken stammen.

## Vorbild: Sysinternals BGInfo

Idee und Funktionsumfang (Systeminformationen auf dem Desktop-Hintergrund, Felder, Position,
Verteilung per Konfigurationsdatei) folgen dem Programm
[BGInfo](https://learn.microsoft.com/sysinternals/downloads/bginfo) von Mark Russinovich
(Sysinternals, Microsoft). Danke dafür – *and thanks for all the Fish*.

Es wurde **kein Code und keine Datei aus BGInfo übernommen**; das Dateiformat `.bgi` wird weder
gelesen noch geschrieben. Dieses Projekt steht in keiner Verbindung zu Microsoft oder Sysinternals.
„BGInfo“ ist der Name des Originalprodukts und wird hier nur zur Einordnung verwendet.

## Vorbild: PowerBGInfo

[PowerBGInfo](https://github.com/EvotecIT/PowerBGInfo) von Evotec (MIT-Lizenz) war die zuerst
untersuchte Open-Source-Alternative – danke für die offene Vorarbeit. Von dort stammen zwei Konzepte, die hier eigenständig
umgesetzt sind:

- das Merken des unveränderten Original-Wallpapers, damit bei wiederholten Läufen nicht Text auf
  Text gestapelt wird (`src/WallpaperState.cs`; bei PowerBGInfo `PowerBGInfoConfiguration.xml`),
- die Farbgebung des Standard-Panels (`ColorConfig` in `src/AppConfig.cs`).

Code aus PowerBGInfo ist nicht enthalten. Die anfangs in diesem Projekt mitgeführten Setup-Skripte
für PowerBGInfo (Ordner `powerbginfo/`, inklusive eines Wrappers über
[PS2EXE](https://github.com/MScholtes/PS2EXE), MIT) sind nicht Teil dieses Repositories.

## Ursprung des Quellcodes

Die erste Fassung des Quellcodes entstand am 2026-09-24 unter dem Namen **BgInfoNative**
(Version 1.1.0) in einer Chat-Sitzung mit dem KI-Assistenten Claude (Anthropic) und wurde als
fertiges Paket in dieses Projekt übernommen. Aus dieser Fassung stammt der Kern folgender Dateien:

- `src/Program.cs` – Einstiegspunkt und Argument-Parsing
- `src/SystemInfoCollector.cs`, `src/WallpaperState.cs`, `src/WallpaperInterop.cs`
- `src/TaskScheduler.cs` – Registrierung über `schtasks.exe`
- `src/GitHubUpdater.cs`, `src/GitSourceUpdater.cs`, `src/ExecutableSwap.cs` – Selbst-Update

Seit dem 2026-10-01 wird das Projekt mit Claude Code weiterentwickelt (Konfigurationsdatei,
Platzhalter, Rendering über GDI+, Mehrmonitor-Betrieb, Überwachungsmodus, Logdatei, Editor) und
seit Version 2.0.0 unter dem Namen **BGInfo-Reloaded** geführt. Die vollständige
Entstehungsgeschichte mit allen Entscheidungen steht in [`Übergabe.md`](Übergabe.md).

## Verwendete Bibliotheken

| Paket | Lizenz | Herausgeber | Zweck |
|---|---|---|---|
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | MIT | Microsoft (.NET Foundation) | Rendering über GDI+ |
| [System.Management](https://www.nuget.org/packages/System.Management) | MIT | Microsoft (.NET Foundation) | WMI-Abfragen |
| [Microsoft.Win32.Registry](https://www.nuget.org/packages/Microsoft.Win32.Registry) | MIT | Microsoft (.NET Foundation) | Registry-Zugriff |
| .NET 8 Runtime, Windows Forms (nur Editor) | MIT | Microsoft (.NET Foundation) | Laufzeit, in die exe eingebettet |

Bis Version 1.2.0 wurde zum Rendern SixLabors ImageSharp verwendet; es ist seit dem 2026-10-01
vollständig entfernt.

## Demo-Hintergründe und Icons

Die Bilder in `backgrounds/` und die Icons in `icons/` wurden vom Projektautor (Kilowatt) für dieses
Projekt beigesteuert. „Windows“ und „Sysinternals“ sind Marken von Microsoft; die Bilder sind kein
Material von Microsoft.

## Lizenz dieses Projekts

BGInfo-Reloaded steht unter der MIT-Lizenz, siehe [`LICENSE`](LICENSE).
