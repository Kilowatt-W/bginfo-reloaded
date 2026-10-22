# Projektkontext für Claude Code

Lies zuerst `Übergabe.md` für die vollständige Entstehungsgeschichte (welche Alternativen verworfen
wurden, welche Bugs schon gefunden/gefixt wurden). Diese Datei hier ist die knappe Arbeitsanleitung
für Änderungen am Code.

## Was dieses Repo ist

**bginfo-reloaded** (Produktname „BGInfo-Reloaded“, bis Version 1.4.0 „BgInfoNative“): Ersatz für
Sysinternals BGInfo – Systeminfos auf dem Desktop-Wallpaper.

**Schreibweise:** nach außen überall „BGInfo-Reloaded“ mit Bindestrich (exe-Namen, `.csproj`,
`BGInfo-Reloaded.json`, Ordner unter `%LOCALAPPDATA%`/`%ProgramData%`, Aufgabennamen). Ausnahmen, die
so bleiben müssen: der C#-Namespace `BgInfoReloaded` (Bindestrich dort nicht erlaubt) und die
Umgebungsvariable `BGINFORELOADED_CONFIG`.

- **`src/`** – die verteilte Anwendung `BGInfo-Reloaded.exe` (.NET 8, fensterlos): Wallpaper rendern
  via System.Drawing/GDI+, Systeminfos via WMI, Scheduled Task via `schtasks.exe`, Selbst-Update
  via GitHub Releases oder `git clone`/`git pull` + Neubau.
- **`editor/`** – `BGInfo-Reloaded-Editor.exe`, grafischer Editor für die `BGInfo-Reloaded.json` (wird nicht
  verteilt).
- **`NOTICE.md`** – Herkunft des Quellcodes und der Vorbilder; bei neuen Fremdanteilen ergänzen.

- **`icons/`** – Programm-Icons, per `ApplicationIcon` in beide Projekte eingebettet; der Editor
  holt sein Fenster-Icon zur Laufzeit aus der eigenen exe.
- **`backgrounds/`** – Demo-Hintergründe je Auflösung; die Beispielkonfiguration verweist darauf.
  Ein `background.jpg` ohne Auflösung gibt es dort absichtlich nicht (siehe
  `WallpaperState.ResolveResolutionVariant`: dann gilt `background_1920x1080.jpg` als Standardbild,
  erst ohne dieses das am besten passende Bild).

Hinweise, die nur für den Entwicklungsrechner gelten (lokaler Projektordner, Dateisynchronisation,
Aufgabenverwaltung), stehen in `CLAUDE.local.md` – die Datei wird nicht eingecheckt. In dieses
Repository gehören **keine** Firmen-, Domänen-, Rechner- oder Benutzernamen und keine echten
Server- oder Sharepfade: Beispiele immer mit `\\fileserver.example.com\share\…`, `FIRMA\benutzer`,
`firma.local`.

Die frühere zweite Lösung (`powerbginfo/`, Setup-Skripte für EvotecIT/PowerBGInfo) ist nicht mehr
Teil des Repos. Rückwärtskompatibilität zu den alten Namen (Zustandsordner, Aufgaben,
Umgebungsvariable) gibt es bewusst nicht – der Nutzer hat sie ausdrücklich abgelehnt.

## Build & Test

```bash
# .NET SDK 8 wird gebraucht (auch zum Cross-Compilen von Linux/macOS aus moeglich):
cd src
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish_tmp
```

Ergebnis ist eine ~35 MB `BGInfo-Reloaded.exe` (PE32+, win-x64). **Cross-Kompilieren von Linux aus
funktioniert nachweislich** (wurde in der Entwicklungsumgebung so gemacht) – NuGet lädt automatisch
die win-x64-Runtime-Pakete, kein Windows-Host zum Bauen nötig.

Für PowerShell-Syntaxprüfung ohne echtes Windows (funktioniert unter Linux mit installiertem
`pwsh`):
```bash
pwsh -NoLogo -NoProfile -Command '
  $errors = $null; $tokens = $null
  [System.Management.Automation.Language.Parser]::ParseFile("pfad/zur/datei.ps1", [ref]$tokens, [ref]$errors)
  $errors
'
```

Für einen groben Laufzeit-Smoketest der .exe ohne echtes Windows: `wine BGInfo-Reloaded.exe --help`
funktioniert überraschend gut für CLI-Pfade (Argumentparsing, Fehlermeldungen), aber **nicht** für die
eigentliche WMI-/Wallpaper-Funktionalität (Wine hat dafür keine echte Implementierung) – dafür ist
echtes Windows nötig, das in dieser Entwicklungsumgebung nicht zur Verfügung stand.

## Wichtige Stolperfallen (bitte nicht versehentlich rückgängig machen)

- **Rendering läuft über `System.Drawing.Common` (GDI+, MIT), nicht mehr über SixLabors ImageSharp**
  (umgestellt am 2026-10-01). Nicht zu ImageSharp zurückwechseln: Es steht unter der "Six Labors
  Split License" (ab 1 Mio. USD Jahresumsatz kostenpflichtig), und ab `ImageSharp.Drawing` 3.x
  bricht der Build ohne Lizenzschlüssel mit "No Six Labors license found" ab. Seitdem gibt es keine
  bewusst gepinnten Pakete mehr – alle Abhängigkeiten dürfen auf den neuesten Stand.
- **GDI+-Eigenheiten in `WallpaperRenderer.cs`**: Schriftgröße in `GraphicsUnit.Pixel` (sonst hängt
  sie von der DPI-Angabe des Hintergrundbilds ab), Messen und Zeichnen mit demselben
  `StringFormat.GenericTypographic` (sonst stimmen die automatischen Spaltenbreiten nicht), Bild über
  Stream in eine eigene 32-Bit-Bitmap kopieren (sonst bleibt die Quelldatei gesperrt). GDI+ liest
  kein WebP – dann greift der einfarbige Hintergrund.
- **Panel-Layout (`WallpaperRenderer.Measure`/`PanelLayout`)**: alle Maße des Panels hängen an einem
  Skalierungsfaktor. Bei `panel.screenFraction` wird zuerst in normaler Größe gemessen und dann mit
  Zielbreite/Naturbreite skaliert (mit Nachverkleinerung, weil Textbreiten nicht exakt linear
  wachsen). Neue Maße deshalb in `Measure` aufnehmen und mit `scale` multiplizieren, nicht mit der
  DPI. `margin` wird absichtlich nur mit der DPI skaliert. Texte immer über `DrawTextIfAny` in ein
  Rechteck zeichnen – so werden sie mit „…“ gekürzt statt über das Panel hinauszulaufen. Mit
  `panel.textOverflow = Wrap` wird stattdessen umbrochen; die Höhe jeder Zeile steht dann in
  `rowHeights` (nicht mehr `layout.LineHeight` allein). Bei `screenFraction` wird nie umbrochen.
  Balken (`bar` einer Zeile, `InfoLine.Bar`, `DrawBar`): mit Text kommt die Höhe über
  `PanelLayout.BarBlock` zur Zeile dazu, ohne Text nicht – deshalb steckt sie in `LineHeight`.
  Rahmen (`border`, `DrawBorders`) liegen absichtlich im vorhandenen Innen- und Zeilenabstand und
  verändern die Maße des Panels nicht – sie richten sich nach den tatsächlich gezeichneten Zeilen.
- **TargetFramework ist `net8.0-windows`**, nicht `net8.0` – das schaltet den
  `SupportedOSPlatform("windows")`-Analyzer sauber frei für die WMI/Registry/P-Invoke-Aufrufe, ohne
  eine echte Windows-Desktop-SDK-Abhängigkeit (WPF/WinForms) zu brauchen. Nicht auf `net8.0` zurück
  ändern, sonst tauchen CA1416-Warnungen für jeden Windows-API-Aufruf auf.
- **Scheduled Tasks laufen bewusst NICHT als SYSTEM** (`AppTaskScheduler`/`TaskScheduler.cs` bzw.
  `03-Register-BGInfoTask.ps1`) – SYSTEM hat keine interaktive Desktop-Session und kann daher kein
  sichtbares Wallpaper setzen. Immer `LogonType Interactive` / `/IT /RU <User>` verwenden.
- **`WallpaperState.cs` merkt sich das Original-Wallpaper** (`%LOCALAPPDATA%\BGInfo-Reloaded\original.txt`),
  damit jeder Lauf vom sauberen Original aus rendert statt Text auf Text zu stapeln. Beim Ändern der
  Render-Logik darauf achten, dass dieser Mechanismus nicht umgangen wird.
- **Font-Fallback in `WallpaperRenderer.cs`** (`CreateFontSafe`) hat eine Kette
  (Consolas → Segoe UI → Tahoma → Arial → Verdana → erste verfügbare Systemschrift → klare
  Fehlermeldung). Das ist kein Overengineering, sondern ein real reproduzierter Bug-Fix: auf einem
  System ganz ohne installierte Schriftarten (z.B. schlankes Windows Server Core, unter Wine
  reproduziert) crashte die ursprüngliche Fassung mit einer kryptischen Fehlermeldung. Die Kette
  prüft ausdrücklich gegen `InstalledFontCollection`, weil GDI+ eine unbekannte Schrift sonst
  stillschweigend durch "Microsoft Sans Serif" ersetzt.
- **Selbst-Update aus einem Ordner (`FolderUpdater.cs`, `update.path`/`update.auto`)**: vergleicht
  die Dateiversion der exe im Ordner mit der eigenen, kopiert erst vollständig neben die eigene exe
  (`.new`) und tauscht dann über `ExecutableSwap` – nie direkt vom Share über die laufende Datei.
  Eingespielt wird eine höhere Version – oder dieselbe Version, wenn die Datei im Ordner jünger ist
  und eine andere Größe hat (neu gebaut ohne erhöhte Nummer); nie eine ältere. Der automatische Weg in `RunOnceLocked` läuft nicht bei
  `--preview` und darf den Lauf nie scheitern lassen (nur Warnung). `--update` ohne `--repo` nimmt
  zuerst `update.path`, dann `update.repo`, dann das gemerkte GitHub-Repository. Was aus dem Ordner
  geholt wird (verglichen wird mit den Dateien im Programmordner, nicht mit dem laufenden Prozess –
  `FolderUpdater.Run` läuft so auch im Editor, der es beim Start aufruft und das Ergebnis als
  Meldung zeigt; beide exe werden per Umbenennen getauscht, auch wenn sie gerade laufen), steuern
  `update.program`/`editor` (nach Dateiversion) und `update.config`/
  `backgrounds` (nach Änderungsdatum, `SyncFile`: nur neuere oder fehlende Dateien, nie löschen).
  `update.repo` wird nie automatisch abgefragt (Rate-Limit von GitHub je Anschluss). Die Dateinamen
  stehen als Konstanten in `FolderUpdater`; die Tooltips im Editor benutzen dieselben.
- **Update aus GitHub**: `update.repo` hat als Vorgabe die Adresse dieses Repositorys
  (`UpdateConfig.DefaultRepo`); leer muss als `""` gespeichert werden, sonst käme die Vorgabe zurück.
  Der Editor übergibt der exe die Kurzform (`GitHubUpdater.NormalizeRepo`), weil eine ältere exe die
  volle Adresse nicht versteht.
- **`--update` (GitHubUpdater.cs) erwartet ein Release-Asset, das exakt `BGInfo-Reloaded.exe` heißt.**
  Wird der Dateiname im Release-Workflow geändert, muss `AssetName` in `GitHubUpdater.cs`
  mitgezogen werden.

- **Konfiguration (`AppConfig.cs`, `ValueResolver.cs`)**: Panel-Inhalt und -Layout kommen aus
  `BGInfo-Reloaded.json` (Suchreihenfolge und Platzhalter siehe `HOWTO.md`; die Datei ohne Angabe
  bestimmt allein `AppConfig.FindDefaultFile()` – Startverzeichnis, dann exe-Ordner, dann
  `%ProgramData%` – und gilt für exe und Editor gleich). Die eingebauten
  Standardwerte in `AppConfig` müssen ohne Datei weiter ein vollständiges Panel ergeben. Den
  exe-Ordner über `Environment.ProcessPath` bestimmen, **nicht** `AppContext.BaseDirectory` – das
  zeigt bei Single-File mit Self-Extract ins Temp-Entpackverzeichnis.
- **Erster Start (`AppConfig.TryCreateInitialFile`/`CreateInitial`, `WallpaperState.AdoptCurrentWallpaper`)**:
  gibt es gar keine Konfiguration, kopiert die exe (nicht bei `--preview`) bzw. der Editor (bei
  „Standard-Konfiguration erstellen“) das gesetzte Wallpaper nach `backgrounds\background_user.*`
  neben die Konfiguration und trägt es relativ ein; `wallpaper.originalPath` ist reine Anzeige.
  Der Name `background_user` ist vom Nutzer so gewünscht. Nie vorhandene Dateien überschreiben,
  und ein Fehlschlag (Ordner schreibgeschützt) darf den Lauf nicht verhindern.
- **Datums- und Zahlenformat (`ValueFormat.cs`, `format` in der Konfiguration)**: beide Programme
  rufen als Erstes `ValueFormat.PinInvariantCulture()` auf – `InvariantGlobalization` ist seit 2.10.0
  aus (Kulturdaten für `format.culture`), ohne das Festlegen würde alles plötzlich der Kultur von
  Windows folgen (Komma statt Punkt). Datums- und Zahlenwerte von Platzhaltern immer über
  `ValueFormat.FormatDate/FormatDateTime/FormatNumber` ausgeben, nie mit festem Format. Die
  Einstellungen gelten je Thread; der `ValueResolver` setzt sie vor jedem `Expand`.
- **`src/nuget.config`** bindet nuget.org projektlokal ein, weil auf dem
  Windows-Entwicklungsrechner global nur die "Visual Studio Offline Packages" konfiguriert sind.
- **`OutputType` ist `WinExe`**, nicht `Exe` – sonst blitzt bei jedem Lauf des Scheduled Tasks ein
  Konsolenfenster auf. `Program.cs` ruft dafür `AttachConsole` auf, aber nur wenn stdout nicht
  bereits umgeleitet ist (sonst geht die Ausgabe von `> datei` verloren). Die Shell wartet nicht auf
  eine GUI-exe: in Skripten `Start-Process -Wait -PassThru` bzw. `start /wait` für den Exit-Code.
  Weil der Prompt der Shell schon vor unserer Ausgabe steht, legt `ReleaseParentConsole()` am Ende
  ein Enter in den Eingabepuffer der Konsole (`WriteConsoleInput` auf `CONIN$`) – nur wenn
  `AttachConsole` gelungen ist. Nicht entfernen, sonst wirkt die Konsole wieder wie eingefroren.
  Umgeleitete Ausgabe wird über eigene UTF-8-Writer geschrieben (sonst ANSI-Codepage, Umlaute
  kaputt); nicht über `Console.OutputEncoding` lösen – das will die Codepage einer Konsole umstellen.
  Texte für Benutzer dürfen Umlaute enthalten.
- **`RunLog.cs`** hängt sich per `Console.SetOut/SetError` an die Konsolenausgabe und schreibt sie
  nach `%LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log` (einzige Fehleranzeige im Task-Betrieb). Meldungen
  deshalb weiter über `Console.WriteLine`/`Console.Error.WriteLine` ausgeben, dann landen sie
  automatisch im Log.
- **Auflösung/DPI**: `WallpaperInterop.EnableDpiAwareness()` muss in `Main` vor jeder Abfrage von
  Auflösung oder DPI laufen, sonst meldet Windows bei Skalierung virtuelle Pixel. DPI immer über
  `GetPrimaryScreenDpi()` (pro Aufruf frisch) lesen, nicht über `GetDpiForSystem` – der Systemwert
  ändert sich für eine laufende `--watch`-Instanz nicht.
- **Mehrmonitor** (`monitors.mode = PerMonitor`, Standard): gesetzt wird über `IDesktopWallpaper`
  (COM), mit Rückfall auf `SystemParametersInfo`. Dabei gilt: (1) die Ausgabedatei braucht je Lauf
  einen neuen Namen (`WallpaperState.NewMonitorOutputPath`), sonst lädt die Shell nicht neu;
  (2) danach steht in der Registry nur `Themes\TranscodedWallpaper` – den aktuellen Pfad deshalb
  über `WallpaperInterop.GetCurrentWallpaperPath()` (fragt zuerst die Shell) holen und eigene
  Ausgaben über `IsOwnOutput` ausschließen, sonst wird das eigene Bild als "Original" gemerkt.
  Der Entwicklungsrechner hat nur einen Monitor: echte Mehrmonitor-Zuordnung ist ungetestet.
- **`--unregister-task`** beendet den Task (`/End`) vor dem Löschen – `/Delete` allein lässt eine
  laufende `--watch`-Instanz weiterlaufen (real beobachtet).
- **`--watch`** darf nicht auf `SystemEvents.UserPreferenceChanged` reagieren: das eigene Setzen des
  Wallpapers löst dieses Ereignis aus und ergäbe eine Endlosschleife. Nur `DisplaySettingsChanged`
  plus Zustandsvergleich (`WallpaperInterop.GetDisplayState()`, über alle Monitore).
- **Scheduled Tasks werden per XML angelegt** (`TaskScheduler.cs`), nicht mit `/SC`-Schaltern – nur
  so lassen sich Akkubetrieb-Sperre und 72-Stunden-Limit abschalten. Reihenfolge der XML-Elemente
  folgt dem Task-Schema, nicht umsortieren.
- **Editor (`editor/`)**: eigenes WinForms-Projekt, bindet `..\src\*.cs` außer
  `Program.cs` ein – Änderungen in `src/` müssen deshalb auch mit `UseWindowsForms` kompilieren
  (implizite `using System.Windows.Forms`/`System.Drawing`: keine mehrdeutigen Typnamen wie `Timer`
  einführen). Nach Änderungen an `AppConfig` beide Projekte bauen und neue Optionen in `MainForm.cs`
  ergänzen. Der Editor wird nicht verteilt.
- **Serialisierung (`AppConfig.JsonOptions`)**: `WhenWritingNull`, nicht `WhenWritingDefault` –
  mehrere Optionen haben Standardwerte ungleich `false`/0/erster Enum-Wert; mit `WhenWritingDefault`
  ginge z.B. `scaleWithDpi: false` oder `fit: None` beim Speichern verloren.
- **Welches Bild ein Monitor bekommt**, entscheidet allein `AppConfig.ResolveWallpaperFor` (eigene
  Zuordnung aus `wallpaper.byResolution`, dann Datei mit Auflösung im Namen, dann Standardbild (die
  eingetragene Datei selbst – auch mit Auflösung im Namen; sonst die Datei ohne Auflösung, sonst die
  Fassung mit 1920x1080 – `WallpaperState.FindDefaultImage`; gesucht wird immer unter dem Namen ohne
  Auflösung, `StripResolutionSuffix`), dann
  am besten passende Fassung) – exe, Editor-Vorschau und die Liste im Editor fragen dieselbe Stelle.
  Gefundene Dateien werden nie in die JSON geschrieben, nur die eigenen Zuordnungen.
- **Relative Pfade** in der Konfiguration (`wallpaper.path`) gelten relativ zu
  `AppConfig.BaseDirectory` (Ordner der Konfigurationsdatei) – immer über
  `ResolveWallpaperPath()` auflösen, nie über das Arbeitsverzeichnis.
- **Rechnergruppen (`HostsConfig.Groups`, seit 2.15.0)**: die Gruppen werden von oben nach unten
  geprüft, die erste passende entscheidet – Profil (`profile`), eigene Datei (`config`), sonst das
  Standardprofil; kein Panel nur mit `mode: Exclude` (`HostGroup.Excludes`). `AppConfig.Load` leitet über
  `ResolveGroupConfig` auf die Gruppendatei um (Cache je Quelle, keine Verschachtelung: `hosts` der
  Gruppendatei wird verworfen), sonst über `ResolveGroupProfile` auf das Profil; der Editor öffnet
  Dateien direkt und leitet nie um. Das frühere
  Format (`include`/`exclude` samt Listendateien) wird beim Laden über `MigrateLegacyLists` in
  Gruppen umgewandelt – die alten Eigenschaften sind danach `null` und werden nicht mehr geschrieben.
- **Profile (`AppConfig.Profiles`, seit 2.19.0)**: je Profil ein `JsonObject` nur mit den Abweichungen
  vom Standardprofil (der Datei ohne `profiles`). `WithProfile` setzt zusammen (`MergeInto`: Blöcke
  der obersten Ebene Wert für Wert, alles darunter und Listen als Ganzes), `StoreProfile`/`Diff`
  rechnen zurück – beide genau eine Ebene tief halten, sonst gehen z. B. Feldbezeichnungen verloren.
  Eigene Felder (`IsInheritedBlock`) gelten in jedem Profil: `Diff` schreibt dort kein `null`, und
  `MergeInto` übergeht `null` (Dateien von 2.19.0 enthalten solche Einträge). Zeilen mit
  `ItemConfig.AllProfiles` (nur im Standardprofil sinnvoll) setzt `WithProfile` vor bzw. hinter die
  eigenen Zeilen eines Profils; `StoreProfile` nimmt sie dafür vor dem Vergleich heraus. Kopien von
  Zeilen verlieren die Markierung (sonst stünden sie doppelt). Die Zeilenliste im Editor ist
  `MultiExtended` und selbst gezeichnet (`SetUpSharedItems`); `SelectedIndex` ist dort die erste
  markierte Zeile, und das Ersetzen eines Eintrags hebt seine Markierung auf (`SetSelected` danach).
  `configFormat`, `hosts`, `profiles` (`FileOnlyKeys`) gelten nur für die Datei. Fehlt
  `configFormat` beim Lesen (Datei vor 2.19), macht `UpgradeFromFormat1` aus Gruppen ohne
  `config` `mode: Exclude` – früher hieß das „kein Panel“. Der Editor schreibt für Profil und
  Standardprofil `mode: ThisConfig`, damit ältere exe wenigstens das Standardprofil zeigen. Im Editor
  ist `_root` die Datei, `_config` das gewählte Profil (im Standardprofil dasselbe Objekt; `Hosts`
  ist immer das Objekt von `_root`); vor Speichern und Profilwechsel `CommitProfile()`, nach
  Änderungen am Standardprofil aus einem Profil heraus `RebuildProfile()`. Profilcode steht in
  `editor/MainForm.Profiles.cs`.
- **Einträge der Gruppen**: ein Eintrag ist Namensmuster oder IPv4-Angabe (Adresse, CIDR,
  Bereich). `TryParseAddressRange` ist absichtlich streng (vier Zahlen mit Punkten), damit Namen wie
  `pc-10` Namensmuster bleiben. Wer prüft, ob ein Eintrag auf den Rechner passt, nimmt
  `MatchesMachine` (nicht `Matches`) – so gelten IP-Angaben auch in `onlyOn`/`notOn`. Wird ein
  Standardkommentar in `ConfigWriter` umformuliert, den alten Text in `FormerComments` eintragen.
  Listen aus `matchFile` einer Gruppe liegen nur zur Laufzeit in `MatchFromFile`
  (`[JsonIgnore]`) – wie bei `include` ruft `AppConfig.Load` das Einlesen
  (`ResolveHostFiles`) selbst auf, der Editor nur auf der Vorschau-Kopie. Der Cache je Listendatei
  trägt eine Kennung der Quelle im Namen, damit nie der Inhalt einer anderen Datei als Ersatz dient.
  Listen immer über `HostsConfig.Normalize` bereinigen (Doppelte, Leerzeilen).
- **Eigene Felder (`FieldMap`)**: `AppConfig.Fields` ist ein `Dictionary<string,string>` mit
  zusätzlichen `Labels`; `FieldMapConverter` liest die Kurzform `"name": "{…}"` und die Form
  `"name": { "value": …, "label": … }` und schreibt die Objektform nur, wenn eine Bezeichnung da ist.
  Wer Felder neu zusammenstellt, nimmt `new FieldMap()` und überträgt die Labels.
- **Editor-Teiler (`SplitContainer`)**: `Panel1MinSize`/`Panel2MinSize` nie im Initialisierer setzen –
  vor dem ersten Layout ist der Container winzig, Windows Forms wirft dann beim Start (`SetMinSizes`
  im Load-Handler). Teilerstellungen und Vorschau-Schalter merkt sich der Editor in
  `%LOCALAPPDATA%\BGInfo-Reloaded\editor-layout.txt`.
- Eingebaute Platzhalter stehen nur in `ValueResolver.BuiltIns`; `--list-tags` (`TagList.cs`) liest
  dieselbe Liste.
- **Eigene Felder und zentrale Datei**: `fields` (benannte Platzhalter) gehen über den Konstruktor in
  den `ValueResolver` – wer Texte der Konfiguration auflöst, muss `new ValueResolver(config)`
  benutzen. `AppConfig.ResolveInclude()` mischt Felder und Zeilen der `include`-Datei in das
  Laufzeitobjekt; `AppConfig.Load` ruft das selbst auf. Im Editor nur auf der Vorschau-Kopie
  aufrufen, nie auf `_config` – sonst landen die zentralen Felder beim Speichern in der lokalen Datei.
  Zeilenfilter (`onlyOn`, `notOn`, `when`) wertet `SystemInfoCollector.Applies` aus.
- Test ohne Wallpaper-Änderung (geht auch mit `--watch`): `BGInfo-Reloaded.exe --preview bild.png [--config datei]`.

## Offene Punkte / nächste Schritte

Die offenen Punkte stehen in `TODO.md` (Quelle), der Teststand in `README.md` → „Teststand“.
Kurzfassung: ungetestet sind echter Mehrmonitor-Betrieb, `--watch` bei echtem Auflösungswechsel,
beide Update-Wege und der CI-Workflow für Releases (`.github/workflows/release.yml`).

## Lokal testen

Getestet wird auf dem Entwicklungsrechner selbst: `.\Build.ps1`, dann
`publish_tmp\BGInfo-Reloaded.exe --preview bild.png` bzw. der Editor aus `publish_tmp`.

## Veröffentlichung und Dokumentation

- Öffentliches Repository: `https://github.com/Kilowatt-W/bginfo-reloaded` (Remote `origin`, Branch
  `main`, Lizenz MIT).
- **Dokumentation ist zweisprachig**: `README.md`/`README.en.md`, `HOWTO.md`/`HOWTO.en.md`,
  `NOTICE.md`/`NOTICE.en.md`. Jede inhaltliche Änderung in beiden Sprachen nachziehen. `Übergabe.md`,
  `TODO.md` und diese Datei bleiben deutsch.
- Ein Release entsteht durch einen Tag `v<Version>`, der zur `<Version>` in
  `src/BGInfo-Reloaded.csproj` passen muss. Der Workflow hängt drei Assets an: `BGInfo-Reloaded.exe`,
  `BGInfo-Reloaded-Editor.exe` (für das Update des Editors aus GitHub – der Name muss so bleiben,
  `FolderUpdater.EditorExe`) und eine `BGInfo-Reloaded.sample.json`, die die frisch gebaute exe aus der Datei im Repository
  neu schreibt (`--export-config … --with-placeholders`). Alle Optionen kommen dabei aus
  `AppConfig`/`ConfigWriter.Trailing`, alle Platzhalter aus `ValueResolver.BuiltIns`, `DriveTags`
  und `GenericTags` – neue Optionen und Platzhalter dort eintragen, dann stimmt die Beispieldatei
  von selbst. Kommentare, die `ConfigWriter` schreibt, müssen in `HasForeignComments` als Standard
  gelten, sonst fragt der Editor bei jeder Datei nach.
