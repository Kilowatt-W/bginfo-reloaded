# Übergabe: BGInfo-Ersatz (Recherche → PowerBGInfo-Setup → eigene native App)

Stand: 2026-09-24, fortgeschrieben bis 2026-10-02. Diese Datei fasst den gesamten Werdegang dieses
Repos aus der Konversation zusammen, damit eine neue Session (Mensch oder Claude Code) ohne
Rückfragen weiterarbeiten kann.

> **Zur Lesart:** Das Projekt heißt seit Version 2.0.0 (2026-10-02) **bginfo-reloaded**; die
> Anwendung hieß bis dahin `BgInfoNative` und lag im Unterordner `bginfonative/`. In dieser Datei
> sind die Namen durchgehend auf den neuen Stand gebracht (`BGInfo-Reloaded`, Pfade ohne Unterordner),
> auch in den älteren Phasen. Die Setup-Skripte aus Phase 2 (`powerbginfo/`) sind nicht mehr Teil
> des Repos.

## Ausgangsfrage

> "Es gibt von Microsoft bzw. Sysinternals das bekannte BGInfo. Suche nach offenen gleichwertigen
> Alternativen oder entwickle eine eigene Version."

BGInfo zeigt Systeminfos (Hostname, IP, OS, CPU, RAM, Laufwerke, ...) direkt auf dem Desktop-Wallpaper
an. Sysinternals-BGInfo selbst ist kostenlos, aber **proprietär** und seit Jahren funktional
eingefroren – daher die Suche nach echten Open-Source-Alternativen.

## Phase 1 – Recherche

Ergebnis der Websuche (Details/Quellen siehe Git-Historie bzw. Chatverlauf, hier nur die Essenz):

| Projekt | Plattform | Lizenz | Einschätzung |
|---|---|---|---|
| **[PowerBGInfo](https://github.com/EvotecIT/PowerBGInfo)** (EvotecIT) | Windows | MIT | **Klarer Sieger.** 300+ Sterne, aktiv gepflegt, PowerShell-Modul, kein Installer nötig, sehr flexibel (WMI/CIM/AD/API/RMM als Datenquellen, Logon-Screen, Chart-Overlays) |
| [zodiacon/BgInfo](https://github.com/zodiacon/BgInfo) | Windows | – | WPF-Neuimplementierung von Pavel Yosifovich, nur 32 Sterne, eher Lernprojekt als Produktivtool |
| gxgl/BGinfo, BgLight (navanem) | Windows | – | Ein-Personen-Forks/Bastelprojekte, keine nennenswerte Wartung |
| [Conky](https://github.com/brndnmtthws/conky) | Linux | GPL | Spiritueller Nachfolger, aber Live-Overlay statt gebranntes Wallpaper – kein 1:1-Äquivalent |
| asad4ntrp2/bginfo | Linux | – | Bastel-Script, Ein-Mann-Show |

**Entscheidung des Nutzers:** PowerBGInfo einsetzen, Zielplattform Windows.

## Phase 2 – PowerBGInfo-Setup (`powerbginfo/`)

Vier Skripte + README, gebaut gegen den **echten** PowerBGInfo-Quellcode (nicht aus dem Gedächtnis
geraten – `New-BGInfo.ps1`, `New-BGInfoValue.ps1`, `New-BGInfoLabel.ps1` sowie die mitgelieferten
Beispiele `Run.BGInfo1-3.ps1` wurden direkt von GitHub geladen, um Parameter-Namen und -Syntax zu
verifizieren).

- **`01-Install-PowerBGInfo.ps1`** – installiert das Modul aus der PowerShell Gallery
  (`Scope CurrentUser`, kein Admin nötig; `-AllUsers`-Schalter vorhanden, prüft dann auf erhöhte
  Rechte).
- **`02-Set-DesktopBGInfo.ps1`** – die eigentliche Konfiguration. Zeigt Hostname, FQDN, angemeldeten
  Benutzer, IPv4, OS-Name/Version/Build/Architektur, letzten Boot, CPU, logische Kerne, RAM-Größe/-Takt,
  BIOS-Version sowie alle Laufwerke mit belegtem/freiem Speicher. Nutzt `New-BGInfo -Target Wallpaper`
  (Parameter `Target` unterstützt auch `LogonScreen`/`Both`), rendert auf das **aktuelle** Wallpaper
  (kein `-FilePath` nötig – PowerBGInfo liest es selbst über `Get-DesktopWallpaper` und merkt sich das
  Original in `PowerBGInfoConfiguration.xml`, damit nicht wiederholt Text auf Text gestapelt wird).
- **`03-Register-BGInfoTask.ps1`** – Scheduled Task via `Register-ScheduledTask`. **Wichtig:** läuft
  bewusst **nicht** als SYSTEM (kein sichtbarer Desktop dort), sondern als der registrierende Benutzer
  (`LogonType Interactive`, `RunLevel Limited`). Trigger: `AtLogOn` + optional periodisches Refresh
  (Default alle 30 Minuten, per `-RefreshMinutes 0` abschaltbar).
- **`04-Build-StandaloneExe.ps1`** – beantwortet die Zwischenfrage "Kann man aus PowerShell eine
  eigene, direkt aufrufbare Anwendung machen?": Ja, via
  [PS2EXE](https://github.com/MScholtes/PS2EXE) (MIT). Wrapped `02-Set-DesktopBGInfo.ps1` in eine
  `BGInfoUpdater.exe` (kein sichtbares Konsolenfenster, eigenes Icon möglich). Wichtiger Hinweis im
  Code/README: das ist **kein echtes natives Kompilat** – die .NET/PowerShell-Runtime bleibt im
  Hintergrund nötig (auf jedem Windows aber ohnehin vorhanden).

**Verifikation:** Alle vier Skripte wurden mit dem echten PowerShell-Parser
(`[System.Management.Automation.Language.Parser]::ParseFile`) auf Syntaxfehler geprüft (dafür wurde
PowerShell 7 in der Linux-Entwicklungsumgebung nachinstalliert, siehe unten). Alle vier: fehlerfrei.
**Nicht getestet:** tatsächliches Laufzeitverhalten auf echtem Windows (kein Windows-Host verfügbar).

## Phase 3 – "Punkt 4 machen": eigene native .NET-Anwendung (`src/`)

Nutzerwunsch: statt nur PS2EXE-Wrapper eine **echte, eigenständige Applikation**. Umgesetzt als
.NET-8-Konsolenanwendung `BGInfo-Reloaded`, komplett ohne PowerShell-Abhängigkeit zur Laufzeit.

### Architektur

| Datei | Zweck |
|---|---|
| `Program.cs` | CLI-Einstiegspunkt, Argument-Parsing (`--register-task`, `--unregister-task`, `--update`, `--update-source`, `--version`, `--help`) |
| `SystemInfoCollector.cs` | Sammelt Werte via `System.Management` (WMI: CPU, RAM, BIOS, OS) + .NET-BCL (`DriveInfo`, `NetworkInterface`, `IPGlobalProperties`). Jede Einzelabfrage einzeln try/catch-abgesichert → "n/a" statt Absturz bei fehlendem WMI-Provider. |
| `WallpaperRenderer.cs` | Zeichnet Panel + Text via SixLabors.ImageSharp (dieselbe Lib, auf der auch PowerBGInfo aufbaut). Font-Fallback-Kette (siehe unten). |
| `WallpaperInterop.cs` | P/Invoke `SystemParametersInfo(SPI_SETDESKWALLPAPER)` (user32.dll) zum Setzen des Wallpapers, `GetSystemMetrics` für Bildschirmauflösung (Fallback ohne bestehendes Wallpaper), Registry-Lesen des aktuellen Wallpaper-Pfads. |
| `WallpaperState.cs` | Merkt sich das **unveränderte Original-Wallpaper** in `%LOCALAPPDATA%\BGInfo-Reloaded\original.txt`, damit jeder Lauf vom sauberen Original rendert (kein Text-auf-Text-Stapeln bei wiederholten Läufen – Pendant zu PowerBGInfos `PowerBGInfoConfiguration.xml`). |
| `TaskScheduler.cs` (`AppTaskScheduler`) | Registriert/entfernt Scheduled Tasks über `schtasks.exe` (bewusst nicht PowerShell, damit die exe wirklich eigenständig bleibt). Auch hier: **nicht SYSTEM**, sondern `/IT /RU <User>` (interaktiv, kein gespeichertes Passwort nötig). |
| `GitHubUpdater.cs` | Selbst-Update aus GitHub Releases (siehe Phase 4). |
| `GitSourceUpdater.cs` | Selbst-Update aus Git-Quellcode via `git clone`/`pull` + `dotnet publish` (siehe Phase 4). |
| `ExecutableSwap.cs` | Gemeinsame "Rename-then-Replace"-Logik für beide Updater. |

### Wichtige Design-Entscheidungen

- **`TargetFramework=net8.0-windows`** (nicht `net8.0`): schaltet den `SupportedOSPlatform`-Analyzer
  sauber frei für WMI/Registry/P-Invoke, ohne eine echte Windows-Desktop-SDK-Abhängigkeit (WPF/WinForms)
  zu erzwingen – lässt sich dadurch trotzdem von Linux aus cross-kompilieren.
- **Self-contained, Single-File, `win-x64`**: `dotnet publish -r win-x64 --self-contained true
  -p:PublishSingleFile=true` – Ergebnis ist eine ~35 MB `BGInfo-Reloaded.exe`, auf dem Zielrechner ist
  keinerlei .NET-Installation nötig.
- **Cross-Kompilierung von Linux nach Windows funktioniert nachweislich** – wurde in der
  Entwicklungsumgebung (Linux-Container ohne Windows-Zugriff) genau so gemacht und verifiziert
  (`file BGInfo-Reloaded.exe` → `PE32+ executable (console) x86-64, for MS Windows`).

### Der SixLabors-Lizenz-Stolperstein (wichtig für zukünftige Upgrades!)

Beim ersten Build-Versuch mit den jeweils neuesten NuGet-Versionen (`SixLabors.ImageSharp.Drawing
3.1.0`) brach `dotnet build` sofort ab:

```
error : No Six Labors license found. Set $(SixLaborsLicenseKey), set $(SixLaborsLicenseFile),
        or add a 'sixlabors.lic' file to the project/workspace.
error : Please obtain a license from https://sixlabors.com/pricing/
```

Ab `ImageSharp.Drawing` 3.x verlangt der Build-Prozess selbst einen (kostenlos erhältlichen, aber
separat zu beantragenden) Lizenzschlüssel. Lösung: Downgrade auf die letzte lizenzschlüssel-freie
2.x-Linie:

```xml
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
<PackageReference Include="SixLabors.ImageSharp.Drawing" Version="2.1.4" />
<PackageReference Include="SixLabors.Fonts" Version="2.0.4" />
```

(`2.1.4` verlangt laut eigenem `.nuspec` exakt `ImageSharp >= 3.1.5` und `Fonts 2.0.4` – auf `3.1.12`
statt `3.1.5` gepinnt, weil `3.1.5` zwei bekannte CVEs hat: `GHSA-2cmq-823j-5qj8`,
`GHSA-rxmq-m78w-7wmc`. `3.1.12` ist der letzte Patch-Release der 3.1.x-Reihe ohne diese.)

**Lizenzhinweis fürs Unternehmen:** ImageSharp selbst steht unter der "Six Labors Split License" –
kostenlos für Firmen unter 1 Mio. USD Jahresumsatz sowie für Open-Source-Nutzung, darüber
kommerziell lizenzpflichtig (siehe https://sixlabors.com/pricing/). Vor produktivem Rollout im
Unternehmen prüfen, ob das zutrifft.

### Gefundener und gefixter Bug: Font-Fallback

Beim Smoke-Test unter Wine (siehe "Verifikation" unten) crashte der ursprüngliche Font-Fallback mit:

```
Fehler: Cannot use the default value type instance to create a font.
```

Ursache: `SystemFonts.Families.FirstOrDefault()` liefert bei leerer Font-Collection einen
`default(FontFamily)`-Struct zurück (kein `null`, da `FontFamily` ein Value-Type ist), und
`.CreateFont()` darauf wirft eine kryptische Exception. Das ist kein rein akademisches
Wine-Artefakt: auf schlanken Windows-Server-Core-Installationen ohne "Desktop Experience" kann
die Systemschriftart-Liste ähnlich leer/eingeschränkt sein.

**Fix** (`WallpaperRenderer.CreateFontSafe`): Fallback-Kette über mehrere gängige Windows-Schriften
(`Consolas → Segoe UI → Tahoma → Arial → Verdana`), dann `SystemFonts.Families.FirstOrDefault()` nur
noch als letzter Versuch mit explizitem Null-Check, und wenn wirklich keine einzige Schrift verfügbar
ist: eine klare, actionable Fehlermeldung statt des kryptischen ImageSharp-Fehlers.

## Phase 4 – "Version + Git-Pull einbauen"

Nutzerwunsch: Versionsnummer in der App sichtbar machen, plus Möglichkeit, sich direkt aus einem
Git-Repository zu aktualisieren. Umgesetzt als zwei unabhängige Mechanismen:

### `--version`
Liest `typeof(...).Assembly.GetName().Version`, gespeist aus `<Version>1.1.0</Version>` im `.csproj`
(einzige Quelle der Wahrheit für die Versionsnummer – beim Release-Bauen hier hochzählen).

### `--update --repo <owner/name>` (GitHub Releases, `GitHubUpdater.cs`)
- Fragt `https://api.github.com/repos/{repo}/releases/latest` ab.
- Vergleicht `tag_name` (z.B. `v1.2.0`) mit der eingebauten Version.
- Sucht im Release ein Asset, das **exakt** `BGInfo-Reloaded.exe` heißt, lädt es herunter.
- Ersetzt sich selbst per "Rename-then-Replace"-Trick (`ExecutableSwap.cs`): laufende exe →
  `BGInfo-Reloaded.exe.old` umbenennen (funktioniert unter Windows auch im laufenden Betrieb, da das
  Image schon im Speicher liegt), neue Datei an ihre Stelle. Wird beim nächsten Start aktiv; die
  `.old`-Datei räumt der nächste Lauf automatisch auf.
- Merkt sich das Repo nach dem ersten Aufruf in `%LOCALAPPDATA%\BGInfo-Reloaded\update.json`, damit
  spätere Aufrufe ohne `--repo` auskommen (wichtig für den Scheduled-Task-Betrieb).
- `--check-only`: nur prüfen, nichts installieren.
- **Setzt voraus, dass der Nutzer selbst ein GitHub-Repo mit Releases pflegt** und dort die per
  `Build.ps1`/`dotnet publish` erzeugte Datei exakt als `BGInfo-Reloaded.exe` anhängt. Das wurde in
  dieser Session noch **nicht** end-to-end gegen ein echtes Repo getestet (kein Repo vorhanden).

### `--update-source <git-url> [--branch main]` (Quellcode-Pull, `GitSourceUpdater.cs`)
- Alternative für Admin-/Build-Rechner, die kein GitHub-Release-Konzept wollen oder einen internen
  Git-Server nutzen.
- Klont (`git clone --depth 1`) bzw. pullt (`git fetch` + `git reset --hard`) nach
  `%LOCALAPPDATA%\BGInfo-Reloaded\src`.
- Baut dort per `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true` neu.
- Tauscht sich selbst aus (dieselbe `ExecutableSwap`-Logik).
- **Braucht `git.exe` UND das .NET SDK auf der Zielmaschine** – auf einem normalen Anwender-Desktop
  eher unüblich, für Admin-/Buildserver-Workstations aber passend. Prüft beide Tools vorab
  (`RequireTool`) und wirft eine klare Fehlermeldung mit Downloadlink, falls eins fehlt.

Beide Updater rufen zu Beginn `ExecutableSwap.CleanupOldBinary()` auf (räumt eine `.old`-Datei aus
einem vorherigen Update auf), ebenso der normale `RunOnce()`-Pfad in `Program.cs`.

## Verifikation & bekannte Lücken

Die gesamte Entwicklung fand in einem **Linux-Container ohne echten Windows-Zugriff** statt. Was
trotzdem geprüft wurde:

1. **PowerShell-Skripte**: PowerShell 7 nachinstalliert (`packages-microsoft-prod.deb` +
   `apt install powershell`), alle vier `.ps1`-Dateien per `Parser::ParseFile` syntaktisch geprüft
   (0 Fehler).
2. **.NET-Cross-Build**: .NET 8 SDK nachinstalliert (`apt install dotnet-sdk-8.0`, derselbe
   Microsoft-Paketquelle wie oben), `dotnet build` (0 Fehler/Warnungen nach dem SixLabors-Fix) und
   `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` erfolgreich durchgeführt.
   Ergebnis als echtes `PE32+ executable ... for MS Windows` via `file` verifiziert.
3. **Wine-Smoketest**: `wine` nachinstalliert, `BGInfo-Reloaded.exe --version` / `--help` /
   `--update` (ohne `--repo`) / `--update-source` (ohne URL) liefen durch und zeigten korrekte,
   saubere Ausgaben bzw. Fehlermeldungen. Ein echter Bug wurde dabei gefunden und gefixt (siehe
   Font-Fallback oben). **Wine konnte NICHT die eigentliche Kernfunktionalität prüfen** (WMI-Abfragen,
   tatsächliches Wallpaper-Setzen, `schtasks.exe`-Registrierung) – Wine emuliert diese Windows-APIs
   nicht vollständig bzw. nicht realistisch genug.

**Was fehlt / nächste Schritte:**

- [ ] Echter Laufzeittest auf Windows: stimmen die WMI-Werte (CPU-Name, RAM-Takt, BIOS-Version),
      wird das Wallpaper korrekt gesetzt und sieht das Panel vernünftig aus (Positionierung,
      Lesbarkeit bei verschiedenen Auflösungen/Multi-Monitor)?
- [ ] `--register-task` real gegenprüfen (insbesondere `/IT`-Verhalten ohne Passwort-Prompt).
- [ ] `--update`-Flow end-to-end gegen ein echtes GitHub-Repo testen (Repo existiert noch nicht).
- [ ] `--update-source`-Flow gegen ein echtes Git-Repo testen.
- [ ] Kein CI-Workflow (z.B. GitHub Actions) vorhanden, der bei einem Tag automatisch baut und als
      Release-Asset `BGInfo-Reloaded.exe` hochlädt – für den `--update`-Mechanismus aber Voraussetzung.
- [ ] Keine `LICENSE`-Datei im Repo – Lizenzmodell für `bginfo-reloaded` noch nicht festgelegt (die
      verwendeten Third-Party-Komponenten sind MIT/Six-Labors-Split-License, das eigene Repo hat
      noch keine eigene Lizenz).
- [x] Panel-Layout und -Inhalt sind seit 1.2.0 (2026-10-01) per `BGInfo-Reloaded.json` konfigurierbar, siehe
      Phase 5.
- [x] Fensterlose exe (2026-10-01): `OutputType=WinExe`, damit der Refresh-Task kein Konsolenfenster
      aufblitzen lässt; CLI-Ausgabe per `AttachConsole` (nur wenn stdout nicht schon umgeleitet ist –
      sonst blieb die Umleitung leer, real reproduziert). Geprüft: PE-Subsystem = GUI, Ausgabe und
      Exit-Code über Pipe/Dateiumleitung. Nicht geprüft: Ausgabe in einer interaktiven Konsole und
      der Task-Lauf selbst.
- [x] Logdatei (2026-10-01): `RunLog.cs` protokolliert jeden Lauf nach
      `%LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log` (Rotation ab 256 KB). Geprüft mit konsolenlosen
      Starten (`Start-Process`): Erfolgs- und Fehlerlauf, Exit-Code, Rotation.
- [ ] Multi-Monitor/DPI: Panel-Maße sind Pixel des Quellbilds; ist das Wallpaper größer/kleiner als
      der Bildschirm, skaliert Windows das Panel mit.

## Phase 5 – Konfigurationsdatei (2026-10-01, erstmals auf echtem Windows)

Ziel des Nutzers: **eine exe + eine zentral verteilbare Konfigurationsdatei**. Entscheidung:
`bginfo-reloaded` ist dafür die Basis (PowerBGInfo/PS2EXE bleibt ein Skript mit Modul-Abhängigkeit,
dessen "Konfiguration" PowerShell-Code ist).

- `AppConfig.cs`: Modell + Laden von `BGInfo-Reloaded.json` (`--config` Pfad/UNC/URL → `BGINFORELOADED_CONFIG`
  → neben der exe → `%ProgramData%\BGInfo-Reloaded\` → Standardwerte), Cache für zentrale Quellen.
- `ValueResolver.cs`: Platzhalter (`{hostname}` …, `{env:}`, `{reg:}`, `{wmi:}`, `{file:}`, `{now:}`).
- `SystemInfoCollector.cs` baut die Zeilen jetzt aus `items`; `WallpaperRenderer.cs` nimmt
  Position/Farben/Schrift aus der Konfiguration und berechnet Spalten-/Panelbreite automatisch.
- Neue CLI-Optionen: `--config`, `--export-config`, `--preview`; `--register-task` reicht
  `--config` an den Task durch.

**Auf Windows 11 (VM) real geprüft:** Build + Single-File-Publish, WMI-/Registry-/Env-Werte,
Rendering (per `--preview`, Bild gesichtet), Config neben der exe, Cache-Fallback bei nicht
erreichbarem Share, Fehlermeldung bei ungültiger Konfiguration. **Nicht geprüft:** tatsächliches
Setzen des Wallpapers, `--register-task`, `--config` mit http(s)-URL, beide Update-Wege.

## Phase 6 – Umstellung von SixLabors ImageSharp auf System.Drawing.Common (2026-10-01)

Grund: ImageSharp steht unter der Split License (ab 1 Mio. USD Jahresumsatz kostenpflichtig) und
verlangt in neuen Hauptversionen einen Lizenzschlüssel beim Build. Da das Tool nur unter Windows
läuft, ist GDI+ über `System.Drawing.Common` (MIT) die einfachste lizenzfreie Alternative. **Die
Abschnitte oben zum SixLabors-Lizenz-Stolperstein und zu gepinnten Versionen sind damit historisch.**

- `WallpaperRenderer.cs` komplett auf GDI+ umgeschrieben; Layout-Logik (Position, automatische
  Breiten, Zeilenhöhen) unverändert. Font-Fallback-Kette neu über `InstalledFontCollection`.
- Pakete: die drei SixLabors-Pakete entfernt, `System.Drawing.Common` 10.0.12 hinzugefügt.
- Folgen: kein WebP als Hintergrundbild mehr; JPG-Ausgabe jetzt mit Qualität 92.
- Geprüft per `--preview` (Bilder gesichtet): einfarbiger Hintergrund, echtes JPG-Hintergrundbild,
  PNG- und JPG-Ausgabe, Schrift-Rückfall, ungültige Farbe. Nicht geprüft: Cross-Build unter Linux.

## Phase 7 – Auflösungswechsel und Platzhalter-Liste (Version 1.3.0, 2026-10-01)

- **Rendern in Bildschirmauflösung:** `wallpaper.fit` (`Fill` Standard / `None`) und
  `panel.scaleWithDpi`. Ohne das hätte ein Neu-Rendern bei Auflösungswechsel nichts geändert, weil
  das Panel vorher in Pixeln des Quellbilds gezeichnet und von Windows mitskaliert wurde.
- **`--watch`:** rendert und bleibt aktiv; reagiert auf `DisplaySettingsChanged` und prüft alle
  15 Sekunden Auflösung/DPI. Eine Instanz je Sitzung (Mutex), Rendern gegenüber dem Refresh-Task
  serialisiert. `--register-task --watch` hängt `--watch` an den Logon-Task.
- **Task-Registrierung auf XML umgestellt**, damit Akkubetrieb-Sperre und 72-Stunden-Limit
  abgeschaltet sind. **Noch nie real ausgeführt.**
- **`--list-tags`** (auch `wmi:<Klasse>`, `reg:<Schlüssel>`); neue Platzhalter `{ipv6}`, `{mac}`,
  `{gateway}`, `{dns}`, `{uptime}`, `{timezone}`, `{resolution}`, `{scaling}`.
- Geprüft: `--list-tags` in allen Varianten, Fill/None per `--preview`, `--watch` Start/Leerlauf/
  zweite Instanz. Nicht geprüft: echter Auflösungswechsel, Skalierung über 100 %, Task-Registrierung.

## Phase 8 – Realtest der Tasks, Anpassungsarten, Bilder je Auflösung, Mehrmonitor (2026-10-01)

- **Realtest `--register-task --watch`** (vom Nutzer freigegeben, Hintergrund `background.jpg`):
  Registrierung per XML funktioniert ohne Admin-Rechte, beide Tasks laufen, Wallpaper wird gesetzt.
  Gefundener Fehler: `--unregister-task` ließ die laufende `--watch`-Instanz weiterlaufen → jetzt
  `/End` vor `/Delete`, erneut geprüft.
- **`wallpaper.fit`** um `Fit`, `Stretch`, `Center`, `Tile` erweitert (Anlass: `Fill` schnitt beim
  16:9-Firmenbild auf dem 21:9-Monitor Logo und Schriftzug ab).
- **`wallpaper.resolutionVariants`**: Bild mit angehängter Auflösung (`_3440x1440`, auch `-` oder
  ohne Trenner) wird bevorzugt, sonst das Ausgangsbild.
- **`monitors.mode`** (`PerMonitor`/`Single`) und **`monitors.panel`** (`All`/`Primary`): je Monitor
  ein eigenes Bild über `IDesktopWallpaper`; Panel auf allen Monitoren oder nur auf dem primären.
- Gefundenes Folgeproblem: nach dem Setzen je Monitor steht in der Registry nur noch
  `TranscodedWallpaper` → Original-Erkennung fragt jetzt zuerst die Shell und sichert
  Windows-Arbeitskopien nach `original_copy.jpg`.
- Geprüft: alle `fit`-Werte, Bild je Auflösung, echtes Setzen über die Shell (ein Monitor).
  **Nicht geprüft: echter Mehrmonitor-Betrieb** (Testrechner hat einen Monitor), Auflösungswechsel.

## Phase 9 – White-/Blacklist und GUI-Überlegung (2026-10-02)

- **`hosts.include` / `hosts.exclude`** (Muster mit `*` und `?`, Prüfung gegen Rechnername und FQDN,
  `exclude` hat Vorrang) und **`hosts.onExcluded`** (`BackgroundOnly` Standard / `Skip`). Per
  `--preview` in acht Fällen geprüft.
- **GUI zum Bearbeiten der Konfiguration:** noch nicht gebaut, Entscheidung offen (siehe `TODO.md`).
  Empfehlung: WinForms-Editor als eigene `BGInfo-Reloaded-Editor.exe`, die `AppConfig`, `ValueResolver` und
  `WallpaperRenderer` aus demselben Quellcode nutzt (Live-Vorschau mit dem echten Renderer,
  Platzhalter-Liste aus `ValueResolver.BuiltIns`). Die verteilte `BGInfo-Reloaded.exe` bleibt damit
  ohne GUI-Abhängigkeit. Zu beachten: Kommentare in einer von Hand gepflegten `BGInfo-Reloaded.json` gehen
  beim Speichern über die GUI verloren.

## Phase 10 – Editor und zwei Korrekturen aus der Nutzung (Version 1.4.0, 2026-10-02)

- **`BGInfo-Reloaded-Editor.exe`** (`editor/`): WinForms-Editor mit Reitern Inhalt, Aussehen,
  Hintergrund, Rechner; Platzhalter-Liste mit aktuellen Werten; Vorschau (ganzer Bildschirm +
  Panel in Originalgröße) über den echten Renderer; „Jetzt anwenden“ ruft `BGInfo-Reloaded.exe`
  im selben Ordner auf. Entscheidung des Nutzers: eigenes Programm, wird nicht verteilt.
- Dabei gefunden: `WhenWritingDefault` hätte beim Speichern `false`/erste Enum-Werte verschluckt →
  auf `WhenWritingNull` umgestellt; `--export-config` schreibt jetzt alle Optionen aus.
- **Relativer `wallpaper.path`**: der Nutzer hatte `.\background.jpg` eingetragen – das galt bisher
  relativ zum Arbeitsverzeichnis und wäre im Scheduled Task nicht gefunden worden. Jetzt relativ zum
  Ordner der Konfigurationsdatei.
- **`resolutionVariants` ist jetzt standardmäßig an**: der Nutzer hatte `background_5120x1440.jpg`
  usw. neben das Bild gelegt und erwartet, dass sie ohne weitere Einstellung benutzt werden.

## Phase 11 – Umbenennung in bginfo-reloaded, flache Ordnerstruktur, Git (Version 2.0.0, 2026-10-02)

- **Name:** Projekt `bginfo-reloaded`, Produkt „BGInfo-Reloaded“, Programme `BGInfo-Reloaded.exe` und
  `BGInfo-Reloaded-Editor.exe`, C#-Namespace `BgInfoReloaded` (ein Bindestrich ist dort nicht
  erlaubt). Geprüft vorab: kein gleichnamiges Repo,
  Paket oder Produkt gefunden (Markenregister nicht abgefragt). Der Alternativvorschlag „DeskTag“
  war durch Hardware-Produkte und ein ähnliches GitHub-Tool belegt.
- **Mit umbenannt:** Zustandsordner `%LOCALAPPDATA%\BGInfo-Reloaded`, `%ProgramData%\BGInfo-Reloaded`,
  Umgebungsvariable `BGINFORELOADED_CONFIG`, Aufgaben „BGInfo-Reloaded - Logon/Refresh“, Mutexe,
  Release-Asset-Name für `--update`.
- **Keine Rückwärtskompatibilität** (Entscheidung des Nutzers): alter Zustandsordner
  `%LOCALAPPDATA%\BgInfoNative`, alte Aufgabennamen und alte Umgebungsvariable werden nicht mehr
  berücksichtigt. Rechner mit der alten Fassung müssen einmal neu eingerichtet werden.
- **Struktur:** Inhalt von `bginfonative/` in die Wurzel verschoben (`src/`, `editor/`, `Build.ps1`,
  …). `Build.ps1` baut jetzt nach `publish_tmp/` und leert den Ordner nicht mehr (dort liegen
  Konfiguration und Hintergrundbilder des Nutzers). `Initialize-GitRepo.ps1` entfernt.
- **`powerbginfo/`** war zu diesem Zeitpunkt nicht mehr im Ordner vorhanden (nicht in dieser Session
  gelöscht) und ist nicht Teil des Repos.
- **`NOTICE.md`** hält Herkunft und Quellen fest (Vorbilder BGInfo und PowerBGInfo, Ursprung des
  Quellcodes, Bibliotheken).
- **Git:** Repository mit erstem Commit angelegt, Branch `main`, kein Remote.
- **Konfigurationsdatei umbenannt** (nach dem ersten Commit): Standardname `BGInfo-Reloaded.json`
  statt des früheren Namens, Vorlage `BGInfo-Reloaded.sample.json`. In dieser Datei ist der Name
  ebenfalls durchgehend ersetzt. Kein Rückfall auf den alten Dateinamen.
- **Stolperstein Dateisynchronisation:** der Projektordner wird von einem Sync-Client synchronisiert. Beim Verschieben hat der Client
  den alten Unterordner wiederhergestellt und Konfliktkopien erzeugt; der leere Ordner
  `bginfonative\publish_tmp` blieb gesperrt zurück.

## Phase 12 – Umzug nach `bginfo_reloaded`, Icons, Demo-Hintergründe (2026-10-02)

- **Neuer Projektordner** `bginfo_reloaded`: kompletter Stand samt `.git` und
  `publish_tmp` (Konfiguration und Hintergründe des Nutzers) aus `bginfo_ps` kopiert, bei
  ausgeschaltetem Sync-Client. Der alte Ordner wird vom Nutzer gelöscht.
- **Icons** (`icons/`, vom Nutzer geliefert) in beide Programme eingebettet; der Editor zeigt das
  Icon auch im Fenster.
- **Demo-Hintergründe** (`backgrounds/`, vom Nutzer geliefert, nur Dateien je Auflösung). Dafür
  erweitert: Das Bild ohne Auflösungszusatz darf fehlen; gibt es für einen Monitor keine exakt
  passende Datei, wird die mit dem ähnlichsten Seitenverhältnis gewählt. Die Beispielkonfiguration
  verweist auf diese Bilder. Ein Dateiname hatte einen Tippfehler (`3440x144`) und wurde korrigiert.
- **`README_BGInfo-Reloaded.md`**: englisches README-Gerüst des Nutzers, unverändert übernommen.
- **Schreibweise „BGInfo-Reloaded“** (wie auf Icons und im README-Gerüst): Zunächst hießen die
  Programme ohne Bindestrich (`BgInfoReloaded…`). Auf Wunsch des Nutzers umgestellt auf
  `BGInfo-Reloaded.exe`, `BGInfo-Reloaded-Editor.exe`, `BGInfo-Reloaded.json`, Ordner
  `%LOCALAPPDATA%\BGInfo-Reloaded` und `%ProgramData%\BGInfo-Reloaded`, Aufgaben
  „BGInfo-Reloaded - Logon/Refresh“, Projektdateien `BGInfo-Reloaded.csproj` und
  `BGInfo-Reloaded-Editor.csproj`. Unverändert: C#-Namespace `BgInfoReloaded` und die
  Umgebungsvariable `BGINFORELOADED_CONFIG`. In dieser Datei ist die Schreibweise durchgehend
  ersetzt; wieder ohne Rückwärtskompatibilität.

## Phase 13 – Ausbau zu Version 2.2.0 und Zusammenführung zweier Arbeitsstände (2026-10-02)

Am Nachmittag liefen zwei Sitzungen parallel: eine im alten Ordner `bginfo_ps` (Funktionsausbau),
eine in `bginfo_reloaded` (Umzug, Icons, Schreibweise). Beide Stände wurden am Abend per `git merge`
hier zusammengeführt; `bginfo_reloaded` ist seitdem der einzige aktuelle Stand, `bginfo_ps` ist
überholt. Die Texte aus `bginfo_ps` wurden dabei auf die Schreibweise `BGInfo-Reloaded` umgestellt.

Was dazukam:

- **Editor-Fehler behoben:** Bezeichnungs- und Wertfeld verloren nach dem ersten Tastendruck den
  Fokus, weil das Ersetzen des Listeneintrags die Felder kurz deaktivierte. Per UI-Automation
  nachgestellt; `SelectedIndexChanged` wird während `_loading` ignoriert.
- **Platzhalter:** Abgleich mit der Feldliste von BGInfo (`{dhcp}`, `{nic}`, `{netspeed}`,
  `{nettype}`, `{machinedomain}`, `{logonserver}`, `{systemtype}`, `{ramfree}`, `{subnet}`,
  `{filever:}`, `{filetime:}`), dazu `{allips}`, `{adsite}`, `{ou}`, `{session}`, `{rdpclient}`,
  `{language}`, `{installdate}`, `{lastupdate}`, `{pendingreboot}`, `{secureboot}`, `{antivirus}`,
  `{virtual}`, `{chassis}`, `{assettag}`, `{uuid}`, `{biosdate}`, `{gpu}`, `{disk}`,
  `{rampercent}`, `{battery}`, `{monitors}`, `{configversion}`. Bewusst nicht: BitLocker/TPM
  (brauchen Adminrechte), WLAN-Name (nur über sprachabhängiges `netsh`), öffentliche IP, CPU-Last.
- **WMI wie in BGInfo:** `{wmi:SELECT … FROM …}`; im Editor der Dialog „WMI-Abfrage…“. Die
  Klassenliste lässt `Win32_Product` (stößt eine MSI-Prüfung an) und Leistungsindikatoren weg.
- **Wiederverwendung:** `fields` (benannte Platzhalter), `include` (zentrale Datei liefert Felder
  und Zeilen, Cache in `include.cache.json`), Zeilenfilter `onlyOn`/`notOn`/`when`, bedingte Blöcke
  (`block` … `endBlock`, flach statt verschachtelt gespeichert, damit der Editor sie in seiner
  Zeilenliste führen kann).
- **Kürzen:** `{name,20}`, `{name,20...}`, `{name,-8}`.
- **Panel-Optik:** `cornerRadius`, Schlagschatten (`shadowSize`, `shadowColor` – GDI+ hat keinen
  Weichzeichner, deshalb übereinandergelegte Formen), `avoidTaskbar` (Arbeitsbereich des Monitors).
- **Kommentare:** System.Text.Json kann eigene Kommentare nicht erhalten. `ConfigWriter.cs` schreibt
  deshalb bei jedem Speichern einen festen Kopf und Standardkommentare; für eigene Anmerkungen gibt
  es `description`. Der Editor fragt nur noch bei fremden Kommentaren nach.
  `--export-config <datei> --config <datei>` bringt eine vorhandene Datei auf das aktuelle Format.
- **Editor:** Symbolleiste mit Symbolen aus der Windows-Symbolschrift, Fußzeile mit Datei und
  Programmversion, Reiter „Datei und Felder“ und „Autostart und Werkzeuge“.
- **Ausgabedateien:** Der Zeitstempel im Namen (`wallpaper_bginfo_m1_HHmmssfff.jpg`) wechselt bei
  jedem Lauf, weil die Shell denselben Pfad nicht neu lädt; `CleanupOldOutputs` löscht danach alle
  älteren. Es bleibt also eine Datei je Monitor liegen.

## Phase 14 – Vorbereitung der Veröffentlichung (2026-10-02)

- **Geprüft:** `bginfo_ps` und `bginfo_reloaded` haben je nur den Branch `main`, keine Stashes, keine
  Worktrees; alle Commits aus `bginfo_ps` sind hier enthalten. Beide Projekte bauen ohne Warnungen.
  In den versionierten Dateien stehen keine Zugangsdaten, internen Rechnernamen oder lokalen Pfade.
- **Lizenz:** MIT (`LICENSE`, Entscheidung des Nutzers).
- **Release-Workflow** `.github/workflows/release.yml`: baut bei einem Tag `v*`, prüft den Tag gegen
  `<Version>` und hängt `BGInfo-Reloaded.exe` an ein GitHub-Release. Noch nie gelaufen.
- **`README_BGInfo-Reloaded.md`** (englisches Gerüst mit Platzhaltern) entfernt; `README.md` bleibt
  das Haupt-README. `Übergabe.md`, `CLAUDE.md` und `TODO.md` werden mitveröffentlicht.
- **Demo-Hintergründe** bleiben trotz Nähe des Motivs zum Windows-Logo im Repo (Entscheidung des
  Nutzers).
- **Veröffentlicht** auf `https://github.com/Kilowatt-W/bginfo-reloaded` (Remote `origin`). Das Repo
  hatte schon einen von GitHub erzeugten Erstcommit (LICENSE, README-Platzhalter); er wurde per
  `git merge --allow-unrelated-histories` eingemischt, nichts wurde überschrieben.
- **Dokumentation zweisprachig:** `README.md`, `HOWTO.md` und `NOTICE.md` bleiben deutsch, daneben
  stehen `README.en.md`, `HOWTO.en.md` und `NOTICE.en.md`; jede Datei verlinkt oben auf die andere
  Sprache. Beide Fassungen müssen bei Änderungen gemeinsam gepflegt werden. `Übergabe.md`, `TODO.md`
  und `CLAUDE.md` sind Arbeitsunterlagen und bleiben deutsch. Das Programm selbst (Ausgabe, Log,
  Editor) ist weiterhin nur deutsch; die englischen Texte weisen darauf hin.
- **README** zeigt den Demo-Hintergrund als Titelbild und hat einen Abschnitt „Demo-Hintergründe“
  mit Verweisen auf alle vier Dateien in `backgrounds/`.

## Phase 14 – Relative Panelbreite, Spaltenaufteilung, Bildschirmanzeige im Editor (Version 2.3.0, 2026-10-11)

Anlass: Rückmeldung nach dem Einsatz auf einer VM – der Editor zeigte die erkannte Auflösung und
Skalierung nicht, das Panel sollte sich relativ zur Bildschirmbreite festlegen lassen, und die
Aufteilung Bezeichnung/Wert ließ sich nur über einen Pixelwert einstellen.

- **`panel.screenFraction`** (0 = aus, 3 bis 8 = 1/3 bis 1/8 der Bildbreite): `WallpaperRenderer`
  misst das Panel zuerst in normaler Größe (`Measure`, Klasse `PanelLayout`) und skaliert dann
  Schrift, Innenabstand, Spaltenabstand, Ecken und Schatten mit dem Faktor Zielbreite/Naturbreite
  (begrenzt durch die verfügbare Höhe). Textbreiten wachsen nicht exakt linear, deshalb bis zu zwei
  Nachverkleinerungen. `margin` wird bewusst nicht mitskaliert.
- **`panel.labelShare`** (Prozent, 10 bis 90) und **`panel.columnGap`**: Rangfolge für die
  Bezeichnungsspalte ist `labelShare` → `labelWidth` → automatisch.
- **Kürzen mit „…“**: Texte werden in ein Rechteck fester Breite gezeichnet
  (`StringTrimming.EllipsisCharacter`); vorher liefen sie bei fester Breite über das Panel hinaus.
  Zeilen, die nicht mehr in die Höhe passen, werden weggelassen.
- **Editor**: Titelleiste und Überschrift der Vorschau zeigen Auflösung und Skalierung aus
  `WallpaperInterop.GetMonitors()`; bei `DisplaySettingsChanged`/`DpiChanged` werden Anzeige und
  Vorschau aktualisiert. Reiter „Aussehen“: Auswahl der relativen Breite, Schieberegler für den
  Spaltenanteil (neuer Baustein `Slider`), Spaltenabstand.
- Geprüft per `--preview` auf 3440×1440 bei 125 % (Bilder gesichtet) und per Bildschirmfoto des
  Editors. Nicht geprüft: Bedienung der neuen Felder per Maus, Wechsel der Auflösung bei offenem
  Editor, relative Breite auf mehreren Monitoren.

### Nachtrag 2026-10-11: Konfiguration aus dem Startverzeichnis

- Ohne `--config` und ohne Umgebungsvariable sucht `AppConfig.FindDefaultFile()` die
  `BGInfo-Reloaded.json` jetzt zuerst im Startverzeichnis (`Environment.CurrentDirectory`), dann
  neben der exe, dann unter `%ProgramData%`. Der Editor öffnet beim Start ohne Dateiangabe dieselbe
  Datei (`AppConfig.FindFileForEditor()`), statt mit Standardwerten zu beginnen.
- Editor-Fußzeile zeigt nur noch den Dateinamen, der volle Pfad steht im Tooltip.
- Geprüft: exe mit und ohne Datei im Startverzeichnis, Vorrang von `--config`; Editor öffnet in
  beiden Fällen eine Datei. Nicht geprüft: Anzeige des Tooltips selbst.

### Nachtrag 2026-10-11: Release v2.3.0 und Beispielkonfiguration als zweites Asset

- Tag `v2.3.0` gepusht (Commit `255d8c1`); ob der Workflow durchlief, war von hier nicht prüfbar
  (Repository privat, kein `gh` auf dem Entwicklungsrechner). Dieses Release trägt nur die exe.
- `release.yml` erzeugt ab dem nächsten Tag zusätzlich `BGInfo-Reloaded.sample.json`: die exe
  schreibt die Datei aus dem Repository neu (`--export-config … --config … --with-placeholders`),
  liest das Ergebnis zur Gegenprobe wieder ein und prüft, dass die Platzhalter-Liste enthalten ist.
- Neu dafür: `--with-placeholders` (`ConfigWriter.PlaceholderLines`), gemeinsame Listen
  `ValueResolver.DriveTags`/`GenericTags` (vorher doppelt in `TagList.cs`).
- Dabei gefundener Fehler: `HasForeignComments` hielt die eingerückte Kopfzeile jeder vom Programm
  geschriebenen Datei für einen eigenen Kommentar – der Editor hätte bei jeder Datei vor dem
  Speichern nachgefragt. Behoben (Vergleichsliste wird getrimmt).
- Geprüft: die Workflow-Schritte lokal mit denselben Befehlen. Nicht geprüft: der Lauf auf GitHub.

### Nachtrag 2026-10-11: abgeschnittenes Wallpaper bei fehlendem Bild für die Auflösung

- Gemeldet auf einer VM mit 2454×960: Vorschau im Editor stimmte, auf dem Desktop war das Bild samt
  Panel unten abgeschnitten. Ursache: die Konfiguration hatte `"fit": "None"`, für 2454×960 gab es
  kein eigenes Bild, also kam `background.jpg` (1920×1080) unskaliert heraus – und Windows hat es
  danach selbst skaliert und beschnitten. Die Vorschau zeigt bei `None` das unskalierte Bild.
- `WallpaperRenderer.CutsOffImage`/`TryGetImageSize`: gemeinsame Prüfung für exe (Hinweiszeile im
  Log), Editor-Statuszeile und den neuen Dialog beim Speichern (`MainForm.CheckBackgroundFit`), der
  auf `Stretch` oder `Fit` umstellt. „Beibehalten“ wird je Lage für die Sitzung gemerkt.
- Geprüft: Fall auf dem Entwicklungsrechner nachgestellt (nur `background.jpg`, 3440×1440), Dialog
  per Fensternachrichten bedient, Datei danach mit `Stretch` gespeichert, zweites Speichern ohne
  Dialog. Nicht geprüft: die VM des Nutzers selbst.

### Nachtrag 2026-10-11: Release v2.3.1 bestätigt, Selbst-Update aus einem Ordner (Version 2.4.0)

- Release `v2.3.1`: Lauf auf GitHub grün, beide Assets vorhanden (vom Nutzer bestätigt).
- Neu: `"update": { "path": …, "auto": … }` und `FolderUpdater.cs`. Liegt im Ordner (lokal/UNC)
  eine `BGInfo-Reloaded.exe` mit höherer Dateiversion, wird sie neben die eigene exe kopiert und
  über `ExecutableSwap` getauscht; ein vorhandener Editor wird mit aktualisiert. `--update` ohne
  `--repo` nimmt zuerst diesen Ordner, `--update --from <ordner>` einen anderen; mit
  `update.auto` prüft jeder normale Lauf selbst. Im Editor im Reiter „Autostart und Werkzeuge“.
- Bewusst nur Ordner, keine URL: für einen Versionsvergleich per HTTP bräuchte es eine eigene
  Versionsdatei; für GitHub gibt es `--update --repo`.
- Sicherheit: wer in den Ordner schreiben darf, bestimmt das Programm auf den Rechnern – steht so in
  HOWTO und im Editor. `auto` ist deshalb standardmäßig aus.
- Geprüft mit lokalen Ordnern und einer eigens gebauten 2.3.9: Prüfen, Aktualisieren, erneuter
  Lauf (räumt `.old` auf), automatischer Weg, nicht erreichbarer Ordner, `--from`. Nicht geprüft:
  echter UNC-Share, fehlende Schreibrechte im Programmordner, Lauf aus dem Scheduled Task.

### Nachtrag 2026-10-11: Auswahl beim Ordner-Update, Repository in der Konfiguration (Version 2.5.0)

- Release `v2.4.0`: Tag gepusht; Bestätigung des Laufs steht aus.
- `update.program`, `update.editor` (Standard an), `update.config`, `update.backgrounds` (Standard
  aus): Konfiguration und Bilder werden nach Änderungsdatum abgeglichen, landen neben der exe, die
  alte Konfiguration bleibt als `.bak`. Wunsch des Nutzers, samt Kästchen und Tooltips mit den
  betroffenen Dateien im Editor (Reiter „Autostart und Werkzeuge“).
- `update.repo`: GitHub-Repository zentral in der Konfiguration statt nur je Benutzer in
  `%LOCALAPPDATA%\BGInfo-Reloaded\update.json`. Bewusst nur auf Aufruf von `--update`.
- GitHub-Antwort 404 ergibt jetzt eine verständliche Meldung (Schreibweise/privates Repository).
- Geprüft mit lokalen Ordnern und einer eigens gebauten 2.4.9 (siehe README, Teststand). Die neuen
  Editor-Felder sind per Bildschirmfoto gesichtet; Tooltips und Bedienung nicht geprüft.

### Nachtrag 2026-10-11: Rechnerlisten mit IP-Angaben, Import/Export, keine .pdb (Version 2.6.0)

- Anlass: beim Nutzer wird die Liste der ausgeschlossenen Rechner bisher als Textdatei gepflegt
  (auf einem zentralen Share, ein Rechnername je Zeile, 134 Einträge).
- Editor, Reiter „Rechner“: je Liste „Importieren…“ (anhängen oder ersetzen, ohne Doppelte) und
  „Exportieren…“; Leser ist `HostsConfig.ParseList`.
- IP-Adressen, Netze (CIDR) und Bereiche in `hosts.include`/`hosts.exclude` und – weil derselbe
  Vergleich benutzt wird – in `onlyOn`/`notOn` der Zeilen. Geprüft gegen alle IPv4-Adressen der
  aktiven Adapter.
- Release-Builds erzeugen keine `.pdb` mehr (`DebugType=none` in beiden Projekten).
- Doppelte Einträge: `HostsConfig.Normalize` (ohne Groß-/Kleinschreibung); im Editor beim Import,
  beim Verlassen des Felds und beim Speichern. Vorher blieben von Hand getippte Doppelte stehen.
- `hosts.includeFile` / `hosts.excludeFile`: Listendatei (Pfad, UNC, URL), die jeder Lauf frisch
  liest, zusätzlich zur Liste in der Konfiguration; Cache je Quelle bei nicht erreichbarer Datei.
  Wunsch des Nutzers: beides ermöglichen. Die bestehende `disallowed_hosts.txt` lässt sich so
  unverändert weiterverwenden.
- Nebenbefund auf dem Entwicklungsrechner: das aktuell gesetzte Wallpaper stammt noch aus einem
  früheren Zustandsordner und wird deshalb nicht als eigene Ausgabe erkannt – in der Vorschau einer
  Konfiguration ohne `wallpaper.path` dient es als Hintergrund (siehe TODO, Zustandsordner).

### Nachtrag 2026-10-11: „Aktueller Wert“ im Editor, Startabfrage ohne Konfiguration

- Reiter „Inhalt“: das Feld „Wert“ heißt jetzt „Value“; darunter zeigt „Aktueller Wert“ das Ergebnis
  der gewählten Zeile auf diesem Rechner (`MainForm.UpdateCurrentValues`/`DescribeCurrentValue`,
  im Hintergrund auf einer Kopie der Konfiguration, mit kurzer Wartezeit nach dem Tippen). Mit
  Hinweis, wenn die Zeile hier nicht erscheint. Die Tabelle der eigenen Felder hat dieselbe Spalte.
  Das Füllen der Spalte darf die Datei nicht als geändert markieren (`_fillingFieldValues`).
- Start ohne auffindbare Konfiguration: Abfrage (TaskDialog) – Standard-Konfiguration erstellen,
  vorhandene laden oder ohne Datei beginnen. Die Standard-Konfiguration kommt auf Wunsch des
  Nutzers immer in den Ordner des Programms, nicht ins Startverzeichnis.
- Geprüft per UI-Automation: Werte aller Zeilenarten, Titel bleibt ohne „*“; Startabfrage erscheint,
  „erstellen“ legt die Datei im Programmordner an und öffnet sie, beim zweiten Start keine Abfrage.
  Nicht geprüft: „laden“ und „ohne Datei“, nicht beschreibbarer Programmordner.

### Nachtrag 2026-10-11: Abschneiden oder Umbrechen bei fester Breite (Version 2.7.0)

- Release `v2.6.0`: Tag gepusht; Bestätigung des Laufs steht aus.
- Meldung des Nutzers: bei fester Breite in Pixeln sei das Panel trotzdem so breit wie der längste
  Text. Mit 2.6.0 nicht nachstellbar – `width` wird eingehalten und abgeschnitten. Möglich ist die
  Kombination mit `screenFraction` (hat Vorrang, Panel folgt dann wieder dem Inhalt) oder eine
  ältere exe auf der VM. Der Editor sperrt deshalb jetzt das Feld für die feste Breite, solange
  eine relative Breite gewählt ist.
- Neu: `panel.textOverflow` (`Truncate` Standard / `Wrap`). Beim Umbrechen werden Zeilenhöhen je
  Zeile berechnet (`rowHeights`). Automatische Bezeichnungsspalte höchstens 45 % der Innenbreite,
  wenn die feste Breite nicht für beide Spalten reicht.
- Geprüft per `--preview` (Bilder gesichtet): 400 px mit Abschneiden und Umbrechen, mit
  Spaltenanteil 50 %, ohne feste Breite, mit relativer Breite. Editor-Felder nicht angesehen.

### Nachtrag 2026-10-11: Wallpaper beim ersten Start übernehmen (Version 2.8.0)

Wunsch: beim ersten Start von Editor oder exe das gesetzte Wallpaper auslesen und übernehmen. Zur
Laufzeit geschah das schon (`original.txt`); neu ist, dass es in die Konfiguration wandert: Kopie
nach `backgrounds\background_user.<Endung>` neben der Konfiguration (Name vom Nutzer vorgegeben),
relativer `wallpaper.path`, Herkunft als `wallpaper.originalPath` (nur Anzeige, im Editor unter
„Übernommen von (Originalpfad)“). Die exe legt dafür beim ersten echten Lauf ohne jede
Konfiguration eine `BGInfo-Reloaded.json` neben sich an – nicht bei `--preview`, nicht wenn der
Ordner schreibgeschützt ist. Übernommen wird das gerade gesetzte Bild; nur wenn das unser eigenes
ist, das gemerkte Original. Auf dem Entwicklungsrechner zeigt `original.txt` noch auf ein Bild aus
dem alten Ordner `BgInfoReloaded` (mit eingebranntem Panel) – Altlast, kein Fehler der Funktion.

## Vergleichstabelle (aus der finalen Antwort im Chat)

| | PowerBGInfo (PowerShell) | BGInfo-Reloaded (dieses Repo) |
|---|---|---|
| Laufzeit-Abhängigkeit | PowerShell 5.1/7 + Modul | keine (self-contained exe) |
| Anpassbarkeit | sehr hoch, PowerShell-Skript | mittel, C#-Quellcode |
| Update-Mechanismus | `Update-Module` | eingebaut: GitHub-Release oder Git-Quellcode |
| Wartungsaufwand | gering (fremdes, gepflegtes Projekt) | du pflegst es selbst |
| Governance | externes MIT-Projekt | vollständig in eigener Hand |

## Entwicklungsumgebung (falls relevant für Reproduzierbarkeit)

In der Session nachinstalliert (Ubuntu 24.04 Linux-Container):
- PowerShell 7 (`packages-microsoft-prod.deb` + `apt install powershell`)
- .NET 8 SDK (`apt install dotnet-sdk-8.0`, gleiche Microsoft-Paketquelle)
- Wine 9.0 (`apt install wine64 wine`, für den CLI-Smoketest)

Alle drei liefen über den normalen `apt`-Weg ohne Sonderkonfiguration.
