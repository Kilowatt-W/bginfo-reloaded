# BGInfo-Reloaded

<img src="icons/BGInfo-Reloaded_logo.jpg" alt="BGInfo-Reloaded Logo" width="160" align="right">

**Deutsch** | [English](README.en.md)

![BGInfo-Reloaded – System information. Your way.](backgrounds/background_1920x1080.jpg)

**BGInfo-Reloaded** ist ein eigenständiger, moderner Ersatz für
[Sysinternals BGInfo](https://learn.microsoft.com/sysinternals/downloads/bginfo): eine
.NET-Anwendung, die Systeminformationen als Panel auf das Desktop-Wallpaper schreibt – kompiliert zu
einer einzigen, fensterlosen `BGInfo-Reloaded.exe` (self-contained, auf dem Zielrechner muss nichts
vorinstalliert sein, kein PowerShell zur Laufzeit). Inhalt und Aussehen des Panels kommen aus einer
zentral verteilbaren `BGInfo-Reloaded.json`, die sich mit dem mitgelieferten Editor bearbeiten lässt.

Stand: Version 2.13.2 (2026-10-12). Bis Version 1.4.0 hieß das Projekt „BgInfoNative“.

- **Bedienung, alle Aufrufe, Konfiguration und Fehlersuche:** [HOWTO.md](HOWTO.md)
  ([English](HOWTO.en.md))
- **Herkunft des Quellcodes, Vorbilder und verwendete Bibliotheken:** [NOTICE.md](NOTICE.md)
  ([English](NOTICE.en.md))
- **Entstehungsgeschichte und Entscheidungen:** [Übergabe.md](Übergabe.md), offene Punkte in
  [TODO.md](TODO.md) (Arbeitsunterlagen, nur auf Deutsch)
- **Lizenz:** [MIT](LICENSE)

Diese README beschreibt Aufbau, Build und technische Hintergründe.

## Dank und Vorbilder

BGInfo-Reloaded steht auf den Schultern zweier Vorgänger – danke dafür:

- **[Sysinternals BGInfo](https://learn.microsoft.com/sysinternals/downloads/bginfo)** von Mark
  Russinovich (Sysinternals, heute Microsoft). Das Original hat die Idee geprägt,
  Systeminformationen direkt auf den Desktop-Hintergrund zu schreiben, und begleitet
  Administratoren seit über zwanzig Jahren. Ohne BGInfo gäbe es dieses Projekt nicht. Danke an
  Mark Russinovich und das Sysinternals-Team – *and thanks for all the Fish*.
- **[PowerBGInfo](https://github.com/EvotecIT/PowerBGInfo)** von Evotec (MIT-Lizenz), die zuerst
  untersuchte Open-Source-Alternative. Von dort stammen die Idee, sich das ursprüngliche Wallpaper
  zu merken, und die Farbgebung des Standard-Panels. Danke für die offene Vorarbeit.

BGInfo-Reloaded ist ein unabhängiges Projekt: Es steht in keiner Verbindung zu Microsoft,
Sysinternals oder Evotec und enthält keinen Code aus BGInfo oder PowerBGInfo. Einzelheiten stehen
in [NOTICE.md](NOTICE.md).

## Schnellstart

**Fertiges Programm:** `BGInfo-Reloaded.exe` von der Seite
[Releases](https://github.com/Kilowatt-W/bginfo-reloaded/releases) laden, in einen Ordner legen und
starten – eine Installation ist nicht nötig. Der Editor ist kein Teil des Releases und wird aus dem
Quellcode gebaut.

```powershell
.\BGInfo-Reloaded.exe --preview vorschau.png    # ansehen, ohne das Wallpaper zu ändern
.\BGInfo-Reloaded.exe --export-config           # BGInfo-Reloaded.json als Vorlage erzeugen
.\BGInfo-Reloaded.exe                           # Wallpaper setzen
.\BGInfo-Reloaded.exe --register-task --watch   # Autostart einrichten
```

**Aus dem Quellcode:**

```powershell
.\Build.ps1                                                # baut beide Programme nach .\publish_tmp
.\publish_tmp\BGInfo-Reloaded.exe --config .\BGInfo-Reloaded.sample.json --preview vorschau.png
                                                           # Demo ansehen, ohne das Wallpaper zu ändern
.\publish_tmp\BGInfo-Reloaded-Editor.exe                     # Konfiguration grafisch bearbeiten
.\publish_tmp\BGInfo-Reloaded.exe                           # Wallpaper setzen
.\publish_tmp\BGInfo-Reloaded.exe --register-task --watch   # Autostart einrichten
```

## Dateien

| Datei/Ordner | Zweck |
|---|---|
| `src/` | Quellcode von `BGInfo-Reloaded.exe` |
| `editor/` | Quellcode von `BGInfo-Reloaded-Editor.exe`, dem grafischen Editor für die `BGInfo-Reloaded.json` (nur für Admins, wird nicht verteilt) |
| `Build.ps1` | baut beide Programme nach `publish_tmp/` (`-SkipEditor` lässt den Editor weg) |
| `BGInfo-Reloaded.sample.json` | kommentierte Beispiel-Konfiguration mit allen Optionen; nutzt die Demo-Hintergründe |
| `backgrounds/` | Demo-Hintergründe je Auflösung (1920×1080, 3440×1440, 3840×2160, 5120×1440) |
| `icons/` | Programm-Icons; werden beim Bauen in `BGInfo-Reloaded.exe` und `BGInfo-Reloaded-Editor.exe` eingebettet |
| `BGInfo-Reloaded.central.sample.json` | Vorlage für eine zentrale Datei mit gemeinsam genutzten Feldern und Zeilen (`include`) |
| `HOWTO.md`, `HOWTO.en.md` | Anleitung für Betrieb und Konfiguration (Deutsch, Englisch) |
| `NOTICE.md`, `NOTICE.en.md` | Herkunft des Quellcodes, Vorbilder, verwendete Bibliotheken (Deutsch, Englisch) |
| `README.md`, `README.en.md` | diese Datei (Deutsch, Englisch) |
| `Übergabe.md`, `TODO.md`, `CLAUDE.md` | Arbeitsunterlagen: Entstehungsgeschichte, offene Punkte, Hinweise für die Entwicklung mit Claude Code (nur Deutsch) |
| `LICENSE` | Lizenz dieses Projekts (MIT) |
| `.github/workflows/release.yml` | baut bei einem Tag `v*` und hängt `BGInfo-Reloaded.exe` an ein GitHub-Release |

Die Programme werden nicht versioniert, sondern gebaut (siehe unten).

## Demo-Hintergründe

Im Ordner [`backgrounds/`](backgrounds/) liegen Demo-Hintergründe in vier Auflösungen. Die
Beispielkonfiguration [`BGInfo-Reloaded.sample.json`](BGInfo-Reloaded.sample.json) verweist darauf
(`"path": ".\\backgrounds\\background.jpg"`); je Monitor wird automatisch die Datei mit der passenden
Auflösung gewählt, sonst die mit dem ähnlichsten Seitenverhältnis.

| Datei | Auflösung | Seitenverhältnis |
|---|---|---|
| [`background_1920x1080.jpg`](backgrounds/background_1920x1080.jpg) | 1920 × 1080 | 16:9 |
| [`background_3840x2160.jpg`](backgrounds/background_3840x2160.jpg) | 3840 × 2160 | 16:9 |
| [`background_3440x1440.jpg`](backgrounds/background_3440x1440.jpg) | 3440 × 1440 | 21:9 |
| [`background_5120x1440.jpg`](backgrounds/background_5120x1440.jpg) | 5120 × 1440 | 32:9 |

Ein `background.jpg` ohne Auflösung gibt es absichtlich nicht. Ausprobieren, ohne das eigene
Wallpaper zu ändern:

```powershell
.\publish_tmp\BGInfo-Reloaded.exe --config .\BGInfo-Reloaded.sample.json --preview vorschau.png
```

Die Bilder sind Demo-Material dieses Projekts. Das Projekt steht in keiner Verbindung zu Microsoft;
Windows ist eine Marke von Microsoft.

## Funktionsumfang

- Panel mit frei konfigurierbaren Zeilen: eingebaute Werte (Hostname, IP, Betriebssystem, CPU, RAM,
  BIOS, Laufwerke, …) sowie beliebige Werte aus WMI, Registry, Umgebungsvariablen und Dateien.
- Position, Farben, Schrift und optional ein festes Hintergrundbild per `BGInfo-Reloaded.json`; Spalten- und
  Panelbreite passen sich automatisch dem Inhalt an.
- Panelbreite wahlweise als Bruchteil der Bildschirmbreite (1/3 bis 1/8) mit passend skaliertem
  Inhalt; Aufteilung in Bezeichnungs- und Wertspalte einstellbar; zu lange Texte werden wahlweise
  mit „…“ abgeschnitten oder umbrochen (`panel.textOverflow`).
- Konfiguration aus lokaler Datei, UNC-Share oder http(s)-URL, mit lokalem Cache.
- Rendern in Monitorauflösung mit wählbarer Anpassung (`Fill`, `Fit`, `Stretch`, `Center`, `Tile`,
  `None`), Panelgröße folgt der Windows-Skalierung.
- Eigenes Hintergrundbild je Auflösung (`bild_3440x1440.jpg`), sonst das Standardbild.
- Mehrere Monitore: je Monitor ein eigenes Bild, Panel auf allen oder nur auf dem primären.
- Eigene, benannte Felder (`fields`): eine Abfrage einmal definieren und überall als `{name}` nutzen;
  ganze WQL-Abfragen wie in BGInfo (`{wmi:SELECT … FROM …}`); Platzhalter kürzen (`{cpu,20}`).
- Zentrale Datei per `include` mit gemeinsam genutzten Feldern und Zeilen (lokal zwischengespeichert).
- Zeilenfilter (`onlyOn`, `notOn`, `when`) und bedingte Blöcke (`block` … `endBlock`), z. B. Zeilen
  nur auf echter Hardware oder nur auf Servern.
- Panel-Optik: abgerundete Ecken, Schlagschatten, Platzierung über bzw. neben der Taskleiste.
- Version und Beschreibung der Konfigurationsdatei (`version`, `description`, `{configversion}`);
  gespeicherte Dateien bekommen erläuternde Standardkommentare.
- White-/Blacklist von Rechnern (`hosts.include`, `hosts.exclude`): Computernamen mit `*` und `?`
  sowie IP-Adressen, Netze und Bereiche; direkt in der Konfiguration oder aus einer zentral
  gepflegten Textdatei (`hosts.includeFile`, `hosts.excludeFile`); im Editor Import und Export.
- Überwachungsmodus (`--watch`): bei Änderung von Auflösung oder Skalierung automatisch neu rendern.
- `--list-tags`: alle Platzhalter mit aktuellem Wert, auch für ganze WMI-Klassen und Registry-Schlüssel.
- Scheduled Tasks für Anmeldung und periodischen Refresh (`--register-task`), ohne Admin-Rechte.
- Logdatei für jeden Lauf, Vorschau-Modus (`--preview`), Selbst-Update aus einem Ordner bzw.
  UNC-Share (`update.path` in der Konfiguration, auf Wunsch automatisch bei jedem Lauf; wählbar,
  ob Programm, Editor, Konfiguration und Hintergrundbilder geholt werden), aus GitHub-Releases
  (`update.repo` bzw. `--repo`) oder aus dem Git-Quellcode.
- Das Original-Wallpaper bleibt erhalten: Sein Pfad wird einmalig in
  `%LOCALAPPDATA%\BGInfo-Reloaded\original.txt` gemerkt, jeder Lauf rendert wieder von diesem
  unveränderten Bild aus (kein Text-auf-Text-Stapeln).

## Bauen

```powershell
.\Build.ps1
```

Braucht das [.NET SDK](https://dotnet.microsoft.com/download) (8 oder neuer; gebaut und getestet mit
SDK 10.0.401, Ziel-Framework bleibt `net8.0-windows`). Ergebnis ist eine ca. 36 MB große
`BGInfo-Reloaded.exe` (self-contained, single-file, win-x64) im Ordner `publish_tmp/`. Der Ordner wird
dabei nicht geleert; eine dort liegende `BGInfo-Reloaded.json` samt Hintergrundbildern bleibt erhalten. Von Hand:

```
cd src
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish_tmp
```

`src/nuget.config` bindet nuget.org projektlokal ein, damit der Build auch auf Rechnern funktioniert,
auf denen global nur die „Visual Studio Offline Packages“ eingerichtet sind. Cross-Kompilieren von
Linux/macOS aus hat mit dem früheren ImageSharp-Stand funktioniert; seit der Umstellung auf
`System.Drawing.Common` wurde es nicht erneut geprüft.

## Quellcode

| Datei | Zweck |
|---|---|
| `Program.cs` | Einstiegspunkt, Argument-Parsing, Konsolen-Anbindung der fensterlosen exe, Überwachungsschleife (`--watch`) |
| `AppConfig.cs` | Konfigurationsmodell, Suchreihenfolge, Laden von Datei/Share/URL, Cache |
| `ConfigWriter.cs` | schreibt die Konfiguration als kommentiertes JSON (Kopf, Standardkommentare, eine Textzeile je Panel-Zeile) |
| `ValueResolver.cs` | Platzhalter: eingebaute Werte, eigene Felder, `{env:}`, `{reg:}`, `{wmi:}`, `{file:}`, `{filever:}`, `{filetime:}`, `{now:}`, Kürzen |
| `TagList.cs` | Ausgabe von `--list-tags` |
| `SystemInfoCollector.cs` | baut aus den `items` der Konfiguration die Panel-Zeilen |
| `WallpaperRenderer.cs` | zeichnet Panel und Text, Font-Fallback-Kette |
| `WallpaperState.cs` | merkt sich das Original-Wallpaper |
| `WallpaperInterop.cs` | Win32: Wallpaper setzen, aktuellen Pfad, Auflösung und DPI lesen, DPI-Awareness |
| `TaskScheduler.cs` | Scheduled Tasks über `schtasks.exe` mit XML-Definition |
| `RunLog.cs` | Logdatei mit Rotation |
| `FolderUpdater.cs`, `GitHubUpdater.cs`, `GitSourceUpdater.cs`, `ExecutableSwap.cs` | Selbst-Update (Ordner/Share, GitHub-Release, Git-Quellcode; gemeinsamer Tausch der laufenden exe) |

**Editor (`editor/`):** eigenes WinForms-Projekt, das alle Dateien aus `src/` außer `Program.cs`
einbindet. Konfigurationsmodell, Platzhalter und Renderer sind damit dieselben wie in der
Kommandozeilen-App; die Vorschau kann nicht vom echten Ergebnis abweichen. `MainForm.cs` baut die
Oberfläche im Code auf (kein Designer); jedes Eingabefeld hängt über eine get/set-Funktion direkt am
`AppConfig`-Objekt. Der Editor ist mit ca. 72 MB etwa doppelt so groß wie `BGInfo-Reloaded.exe`, weil er
die Fenster-Bibliothek mitbringt – ein Grund, ihn getrennt zu halten. Die Versionsnummer steht
zusätzlich in `editor/BGInfo-Reloaded-Editor.csproj` und sollte mit der in `src/` übereinstimmen.

Neue eingebaute Platzhalter kommen in die Liste `BuiltIns` in `ValueResolver.cs` (sie erscheinen
dann automatisch in `--list-tags`), neue Konfigurationsoptionen in
`AppConfig.cs`. Die Versionsnummer steht in `src/BGInfo-Reloaded.csproj` (`<Version>`).

## Release erstellen

Ein Tag `v<Version>` (z. B. `v2.2.0`) startet den Workflow
[`release.yml`](.github/workflows/release.yml): Er prüft, dass der Tag zur `<Version>` in
`src/BGInfo-Reloaded.csproj` passt, baut `BGInfo-Reloaded.exe` und hängt sie an ein GitHub-Release.
Von dort holt sich `BGInfo-Reloaded.exe --update --repo Kilowatt-W/bginfo-reloaded` neue Versionen.

Als zweites Asset hängt am Release die Beispielkonfiguration `BGInfo-Reloaded.sample.json`. Der
Workflow lässt sie von der gerade gebauten exe aus der Datei im Repository neu schreiben
(`--export-config … --config … --with-placeholders`). Sie enthält damit immer alle Optionen dieser
Version mit ihren Erläuterungen und als Kommentar die vollständige Liste der Platzhalter – auch
wenn die Datei im Repository noch nicht nachgezogen ist. Der Pfad zu den Demo-Hintergründen
(`backgrounds\…`) gilt nur im Repository; ohne diese Bilder wird das Wallpaper des Benutzers benutzt.

## Abhängigkeiten

Stand der Prüfung mit `dotnet list package --outdated/--vulnerable/--deprecated` am 2026-10-01:
keine bekannten Schwachstellen, keine als veraltet markierten Pakete.

| Paket | Version | Lizenz | Zweck |
|---|---|---|---|
| System.Drawing.Common | 10.0.12 | MIT (Microsoft) | Rendering über GDI+ |
| System.Management | 10.0.12 | MIT (Microsoft) | WMI-Abfragen |
| Microsoft.Win32.Registry | 5.0.0 | MIT (Microsoft) | Registry-Zugriff |

Alle drei sind auf dem neuesten Stand, es gibt keine bewusst zurückgehaltenen Versionen mehr.

**Kein SixLabors ImageSharp mehr:** Bis zum 2026-10-01 lief das Rendering über ImageSharp. Das steht
unter der „Six Labors Split License“ (ab 1 Mio. USD Jahresumsatz kostenpflichtig), und neuere
Hauptversionen verlangen beim Build einen Lizenzschlüssel. Da das Tool ohnehin nur unter Windows
läuft, wurde auf `System.Drawing.Common` umgestellt; damit entfällt das Lizenzthema für die
Abhängigkeiten. Dieses Projekt selbst steht unter der [MIT-Lizenz](LICENSE); Herkunft und Quellen
stehen in [NOTICE.md](NOTICE.md).

## Technische Notizen

- **Fensterlos:** `OutputType` ist `WinExe`, damit der Scheduled Task kein Konsolenfenster
  aufblitzen lässt. Aus einer Konsole gestartet hängt sich die exe per `AttachConsole` an – aber nur,
  wenn stdout nicht bereits umgeleitet ist, sonst bliebe eine Umleitung (`> datei`) leer.
- **Scheduled Tasks laufen nicht als SYSTEM:** SYSTEM hat keine interaktive Desktop-Session und kann
  kein sichtbares Wallpaper setzen. Registriert wird für den aufrufenden Benutzer mit
  `InteractiveToken`.
- **Tasks als XML-Definition** (`schtasks /Create /XML`): nur so lassen sich die Standardwerte „nur
  im Netzbetrieb starten“ und „nach 3 Tagen beenden“ abschalten – Letzteres würde eine dauerhaft
  laufende `--watch`-Instanz beenden.
- **Auflösung und Skalierung:** Der Prozess ist DPI-aware (Per-Monitor V2), damit Windows echte
  Pixel meldet. Das Hintergrundbild wird (außer bei `fit = None`) auf die Auflösung des jeweiligen
  Monitors gebracht; Maße und Schrift werden mit dessen DPI/96 multipliziert.
- **Mehrere Monitore:** Monitore kommen aus `EnumDisplayMonitors`; gesetzt wird je Monitor über die
  Shell-Schnittstelle `IDesktopWallpaper` (Zuordnung über die Lage im virtuellen Desktop). Die
  Ausgabedatei bekommt bei jedem Lauf einen neuen Namen, weil die Shell ein Bild nicht neu lädt,
  wenn derselbe Pfad erneut gesetzt wird. Schlägt die Schnittstelle fehl, wird per
  `SystemParametersInfo` ein Bild für alle gesetzt. `monitors.mode = Single` nutzt immer diesen Weg.
- **Original-Erkennung:** Der aktuelle Wallpaper-Pfad wird zuerst bei der Shell erfragt. Nach einem
  Setzen je Monitor steht in der Registry nur noch `Themes\TranscodedWallpaper`; solche
  Windows-Arbeitskopien werden vor dem Merken nach `original_copy.jpg` gesichert, weil Windows sie
  beim nächsten Setzen überschreibt.
- **`--watch`:** reagiert auf `SystemEvents.DisplaySettingsChanged` (WM_DISPLAYCHANGE) und prüft
  zusätzlich alle 15 Sekunden Auflösung und DPI, weil eine reine Skalierungsänderung das Ereignis
  nicht zuverlässig auslöst. Gerendert wird nur bei tatsächlich geändertem Zustand. Ein benannter
  Mutex je Sitzung verhindert eine zweite Instanz, ein weiterer serialisiert das Rendern gegenüber
  dem Refresh-Task.
- **Systeminfos:** `System.Management` (WMI), Registry sowie `DriveInfo`/`NetworkInterface` aus der
  .NET-BCL. Jede Einzelabfrage ist gegen Fehler abgesichert – ein fehlender WMI-Provider ergibt
  `n/a` statt eines Absturzes.
- **Rendering:** GDI+ über `System.Drawing.Common`. Hintergrundbilder in JPG, PNG, BMP, GIF und TIFF
  werden gelesen; WebP nicht – dann wird auf die einfarbige Hintergrundfläche ausgewichen. Das
  Wallpaper wird als JPG mit Qualität 92 gespeichert. Die Schrift fällt bei Bedarf auf Consolas → Segoe UI → Tahoma → Arial → Verdana → erste verfügbare
  Systemschrift zurück; ganz ohne installierte Schriften gibt es eine klare Fehlermeldung.
- **Wallpaper setzen:** `SystemParametersInfo(SPI_SETDESKWALLPAPER)` aus `user32.dll` – der Wechsel
  ist sofort sichtbar, kein Explorer-Neustart nötig.
- **Pfad der exe:** wird über `Environment.ProcessPath` bestimmt, nicht über
  `AppContext.BaseDirectory` – das zeigt bei Single-File mit Self-Extract ins Temp-Verzeichnis.
- **Selbst-Update:** Die laufende exe wird zu `BGInfo-Reloaded.exe.old` umbenannt, die neue rückt an
  ihre Stelle und wird beim nächsten Start aktiv. `--update` erwartet ein Release-Asset, das exakt
  `BGInfo-Reloaded.exe` heißt.

## Teststand

Auf Windows 11 Enterprise (VM) geprüft:

- Build und Single-File-Publish
- Werte aus WMI, Registry und Umgebungsvariablen
- Rendering per `--preview` mit GDI+ (Bilder gesichtet): einfarbiger Hintergrund und echtes
  JPG-Hintergrundbild, Ausgabe als PNG und JPG, Positionierung, automatische Breite, Schrift-Rückfall
  bei nicht installierter Schrift, Fehlermeldung bei ungültiger Farbe
- Konfiguration im Startverzeichnis (hat Vorrang vor der neben der exe), neben der exe und per `--config`; Cache-Rückfall bei nicht erreichbarem Share;
  Fehlermeldung bei ungültiger Konfiguration
- relative Panelbreite (`screenFraction` 3, 5, 8), Spaltenanteil (`labelShare`), Spaltenabstand und
  Kürzen mit „…“ bei fester Breite, per `--preview` auf 3440×1440 bei 125 % (Bilder gesichtet)
- Editor: Auflösung und Skalierung in der Titelleiste und über der Vorschau, neue Felder im Reiter
  „Aussehen“ (Bildschirmfoto gesichtet; Bedienung per Maus nicht geprüft)
- Rendern in Bildschirmauflösung: alle sechs `fit`-Werte mit einem 1920×1080-Bild auf 3440×1440
  (Bilder gesichtet)
- eigenes Bild je Auflösung: Variante `_3440x1440` wird gefunden, ohne Option bleibt das Standardbild
- echtes Setzen des Wallpapers über die Shell-Schnittstelle (ein Monitor), mehrfach hintereinander
- `--register-task --watch`, Lauf beider Tasks, `--unregister-task` (beendet auch die laufende
  Überwachung) – ohne Admin-Rechte
- eigene Felder, zentrale Datei (`include`) samt Cache-Rückfall, alle Filtervarianten und
  verschachtelte Blöcke – per `--preview` und Log
- Kürzen von Platzhaltern, `{configversion}`, WQL-Abfragen; kommentierte Ausgabe wieder einlesbar
- abgerundete Ecken, Schatten und Abstand zur Taskleiste (unten) – Vorschaubilder gesichtet
- Editor per UI-Automation: Tippen in Wert- und Filterfeldern, WMI-Dialog, Anzeige von Blöcken;
  Symbolleiste, Fußzeile und die neuen Reiter per Bildschirmfoto
- Editor: Start mit Datei, alle vier Reiter und die Vorschau (Bildschirmfotos gesichtet);
  Speichern/Laden-Rundlauf der Konfiguration, auch für `false` und erste Enum-Werte. Nicht geprüft:
  Bedienung per Maus (Zeilen bearbeiten, Farbdialog, Speichern, „Jetzt anwenden“)
- relativer `wallpaper.path` (`.\background.jpg`) und Bild je Auflösung mit den echten Dateien
  (`background_5120x1440.jpg` wird gewählt)
- Selbst-Update aus einem Ordner (`update.path`): von 2.3.9 auf 2.4.0 mit `--update`, `--update
  --check-only`, `--update --from`, automatisch bei `update.auto`; Editor wird mit aktualisiert;
  `.old` wird beim nächsten Lauf aufgeräumt; nicht erreichbarer Ordner ergibt nur eine Warnung.
  Geprüft mit lokalen Ordnern, nicht mit einem echten UNC-Share
- Auswahl beim Ordner-Update (`update.program`, `editor`, `config`, `backgrounds`): Standard holt
  nur Programm und Editor; Konfiguration (mit `.bak`) und Hintergrundbilder samt Unterordner nur mit
  Schalter; lokal neuere Konfiguration bleibt; zweiter Lauf holt nichts mehr. `update.repo` wird
  ohne Ordner benutzt (gegen das private Repository nur bis zur 404-Meldung prüfbar)
- White-/Blacklist: `*`, `?`, Groß-/Kleinschreibung, Treffer über den FQDN, Vorrang von `exclude`,
  `onExcluded` = `BackgroundOnly` und `Skip`
- IP-Angaben in beiden Listen und in `onlyOn`/`notOn`: Adresse, Netz (`/16`, `/24`, `/0`), Bereich
  lang und kurz, ungültige Angaben, Abgrenzung zu Namen wie `pc-10` (21 Fälle gegen den Code, fünf
  mit der exe auf dem Testrechner). Import einer echten Textdatei mit 134 Rechnernamen. Nicht
  geprüft: die Knöpfe „Importieren…“/„Exportieren…“ im Editor per Maus, Rechner mit mehreren Adaptern
- Editor: „Aktueller Wert“ für alle Zeilenarten und für eigene Felder; Startabfrage ohne
  Konfiguration legt die Standard-Konfiguration im Programmordner an (per UI-Automation)
- Erster Start ohne Konfiguration: exe (echter Lauf in leerem Ordner) und Editor (Startabfrage per
  UI-Automation) kopieren das Wallpaper nach `backgrounds\background_user.jpg`, tragen Pfad und
  `wallpaper.originalPath` ein; `--preview` legt nichts an, ein zweiter Lauf kopiert nicht erneut.
  Nicht geprüft: Wallpaper ohne Dateiendung (Windows-Arbeitskopie), nicht beschreibbarer Ordner
- Doppelte in den Rechnerlisten: entfernt beim Import, beim Verlassen des Felds und beim Speichern
  (im Editor per UI-Automation nachgestellt)
- Listendateien (`hosts.excludeFile`, `hosts.includeFile`): echte Datei auf einem UNC-Share
  (134 Einträge), relativer Pfad, Summe aus Liste und Datei, Cache-Rückfall nach Löschen der Datei,
  getrennter Cache je Quelle, Datei-Einträge landen nicht in der gespeicherten Konfiguration. Nicht
  geprüft: Datei per http(s)-URL
- `--list-tags`, auch mit `wmi:` und `reg:`, Fehlermeldung bei unbekannter WMI-Klasse
- `--watch` (mit `--preview`): rendert beim Start, bleibt aktiv, zweite Instanz beendet sich sofort
- fensterlose exe: Ausgabe und Exit-Code über Pipe und Dateiumleitung (UTF-8, Umlaute korrekt)
  sowie Ausgabe in einer interaktiven PowerShell-Konsole
- Logdatei bei konsolenlosem Start: Erfolgs- und Fehlerlauf, Rotation

Noch nicht geprüft:

- alles nur auf einer VMware-VM: echte Hardware (`{chassis}`, `{battery}`, Block „Blech“), Windows
  Server und RDP-Sitzung fehlen; Taskleiste nur am unteren Rand
- Editor: Speichern mit den neuen Angaben, Feldtabelle, Reiter „Autostart und Werkzeuge“

- das Merken des Benutzer-Originals (`original.txt`) – bisher lief der echte Betrieb nur mit
  festem `wallpaper.path`
- Start des Logon-Tasks durch eine echte Anmeldung (bisher nur von Hand ausgelöst)
- `--watch` bei einem echten Auflösungs- oder Skalierungswechsel (bisher nur Start und Leerlauf)
- Skalierung über 100 % (Testrechner hat 100 %)
- `--config` mit http(s)-URL
- `--update` und `--update-source` gegen ein echtes Repository
- Mehrere Monitore: der Testrechner hat nur einen. Die Zuordnung Bild ↔ Monitor, `panel = Primary`
  und unterschiedliche Auflösungen/Skalierungen je Monitor sind ungetestet.
- Sicherung des Originals, wenn Windows nur `TranscodedWallpaper` kennt (Diashow, Bilder je Monitor)
