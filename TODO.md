# TODO – bginfo-reloaded

Editierbare Quelle der offenen Punkte (UpNote ist nur das Journal). Hintergründe in `Übergabe.md`,
Teststand in `README.md`.

## Offen

### Editor von Hand prüfen

Per UI-Automation geprüft sind: Start, alle Reiter, Vorschau, Tippen in Wert- und Filterfeldern,
WMI-Dialog, Anzeige von Blöcken und Filtern. Noch von Hand durchzuklicken:

- [ ] Speichern (auch „Speichern unter“): kommentierte Ausgabe, Nachfrage bei eigenen Kommentaren,
      Datei mit `fields`/`include`/Filtern/Blöcken unverändert wieder einlesbar
- [ ] Zeilen verschieben/entfernen, Platzhalter per Doppelklick einfügen, „+ Block“, Farbdialog
- [ ] Feldtabelle im Reiter „Datei und Felder“ bearbeiten (neu, ändern, löschen, WMI einfügen)
- [ ] Reiter „Autostart und Werkzeuge“: Autostart einrichten/entfernen, Vorschau als Bilddatei
      speichern, Log und Datenordner öffnen
- [ ] „Jetzt anwenden“
- [ ] Reiter „Aussehen“: relative Breite wählen, Schieberegler für den Spaltenanteil ziehen,
      Spaltenabstand ändern – Vorschau folgt jeweils (bisher nur per `--preview` und Bildschirmfoto
      geprüft)
- [ ] Auflösung oder Skalierung bei offenem Editor ändern: Titelleiste und Vorschau ziehen nach

### Release

- [ ] Release-Lauf für `v2.3.0` auf GitHub prüfen (Actions grün, `BGInfo-Reloaded.exe` am Release)
- [ ] `--update` gegen das Release `v2.3.1` testen (von einer älteren exe aus)
- [ ] Selbst-Update aus einem Ordner (`update.path`) im echten Betrieb prüfen: UNC-Share, Lauf aus
      dem Autostart-Task mit `update.auto`, Programmordner ohne Schreibrechte für den Benutzer
- [ ] Editor, Reiter „Autostart und Werkzeuge“: Tooltips der vier Update-Kästchen und des
      Repository-Felds ansehen, Felder bedienen und speichern

- [ ] Editor, Reiter „Rechner“: „Importieren…“ (anhängen/ersetzen) und „Exportieren…“ bedienen,
      Listendatei über „…“ wählen
- [ ] `hosts.excludeFile`/`includeFile` per http(s)-URL testen
- [ ] Editor-Startabfrage: „Vorhandene Konfiguration laden…“ und „Ohne Datei beginnen“ bedienen;
      Verhalten bei nicht beschreibbarem Programmordner

### Auf anderer Hardware prüfen

- [ ] Echte Hardware (Notebook/Desktop): `{chassis}`, `{assettag}`, `{battery}`, `{gpu}`, `{virtual}`
      = nein und damit der Block „Blech“ – bisher nur auf einer VMware-VM gelaufen
- [ ] Windows Server: `{systemtype}`, `{antivirus}` (dort `n/a` erwartet)
- [ ] RDP-Sitzung: `{session}`, `{rdpclient}`
- [ ] Taskleiste links/rechts/oben und automatisch ausgeblendet (`panel.avoidTaskbar`) – bisher nur
      unten geprüft

### Sonstiges

- [ ] `publish_tmp\BGInfo-Reloaded.aus-bginfo_ps.json` sichten: die zuletzt in `bginfo_ps`
      bearbeitete Konfiguration (20:15 Uhr, neuer als die hiesige) weicht ab (Überschriftenfarbe,
      `{hostname}` statt `{fqdn}`, Überschriften in Großbuchstaben, `{allips}` und eigene Zeile für
      die Subnetzmaske) – übernehmen oder löschen
- [ ] Autostart-Tasks und Zustandsordner prüfen: Tasks, die noch auf `BgInfoReloaded.exe` in
      `bginfo_ps\publish_tmp` zeigen, neu einrichten; `%LOCALAPPDATA%\BgInfoReloaded` ist überholt

- [ ] `include` mit UNC-Pfad und http(s)-URL testen (bisher nur lokaler Pfad und Cache-Rückfall)
- [ ] Cross-Build unter Linux nach der Umstellung auf `System.Drawing.Common` erneut prüfen (nur
      relevant, falls ein Linux-Buildserver/CI genutzt wird)
- [ ] Merken des Benutzer-Originals (`original.txt`, `original_copy.jpg`) im echten Betrieb prüfen –
      bisher lief der echte Betrieb nur mit festem `wallpaper.path`
- [ ] Logon-Task durch eine echte An-/Abmeldung prüfen (bisher von Hand ausgelöst)
- [ ] Mehrmonitor-Betrieb real prüfen: Zuordnung Bild ↔ Monitor, `monitors.panel = Primary`,
      unterschiedliche Auflösungen/Skalierungen, Bild je Auflösung (Testrechner hat nur einen Monitor)
- [ ] `--watch` bei echtem Auflösungs- und Skalierungswechsel prüfen (Docking, RDP, Skalierung > 100 %)
- [ ] `--config` mit http(s)-URL testen
- [ ] `--update` end-to-end gegen `Kilowatt-W/bginfo-reloaded` testen (braucht das erste Release)
- [ ] `--update-source` gegen das GitHub-Repo testen
- [ ] Ergebnis des ersten Release-Laufs prüfen: Tag `v2.2.0` ist gepusht (2026-10-02); ob der
      Workflow durchlief und das Release das Asset `BGInfo-Reloaded.exe` trägt, ist ungeprüft
- [ ] Sichtbarkeit des GitHub-Repos klären: es ist derzeit privat (anonymer Abruf liefert 404) –
      `--update` und die Links in der README funktionieren für andere erst, wenn es öffentlich ist
- [ ] Programm selbst zweisprachig machen: Konsolenausgabe, Log, Editor-Oberfläche und Werte wie
      `ja`/`nein` sind nur deutsch, ebenso die Kommentare in den Beispielkonfigurationen
- [ ] Bildschirmfoto eines echten Panels und des Editors für die README (ohne interne Rechnerdaten)
- [ ] Sync-Client und Git im selben Ordner: klären, ob `.git` mitsynchronisiert werden soll (Gefahr
      von Konfliktkopien im Repository) – ggf. `.git` von der Synchronisation ausnehmen
- [ ] Verteilkonzept festlegen: exe + `BGInfo-Reloaded.json` per Softwareverteilung/GPO oder zentrale
      Konfiguration per Share/URL; `--register-task` muss je Benutzer laufen (z. B. Anmeldeskript)

## Erledigt

- [x] 2026-10-01 Konfigurationsdatei `BGInfo-Reloaded.json` (Inhalt, Position, Farben, Schrift, Hintergrundbild)
      mit Platzhaltern für WMI, Registry, Umgebungsvariablen und Dateien
- [x] 2026-10-01 Zentrale Konfiguration per `--config` (Datei, UNC, URL) bzw. `BGINFORELOADED_CONFIG`,
      mit lokalem Cache
- [x] 2026-10-01 `--preview` und `--export-config`
- [x] 2026-10-01 Fensterlose exe (`WinExe` + `AttachConsole`)
- [x] 2026-10-01 Logdatei `%LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log` mit Rotation
- [x] 2026-10-01 Erster Build und Teiltest auf echtem Windows 11 (Systemwerte, Rendering,
      Konfiguration, Log)
- [x] 2026-10-01 `HOWTO.md` geschrieben, beide READMEs aktualisiert
- [x] 2026-10-01 Abhängigkeiten geprüft, `System.Management` auf 10.0.12
- [x] 2026-10-01 Version 1.3.0: Rendern in Bildschirmauflösung (`fit`, `scaleWithDpi`), `--watch`
      für Auflösungswechsel, `--list-tags`, zusätzliche Platzhalter, Tasks per XML-Definition
- [x] 2026-10-01 `--register-task --watch`, Lauf beider Tasks und `--unregister-task` real geprüft
      (ohne Admin-Rechte); echtes Setzen des Wallpapers geprüft
- [x] 2026-10-01 Anpassungsarten `Fill`/`Fit`/`Stretch`/`Center`/`Tile`/`None`, eigenes Bild je
      Auflösung (`resolutionVariants`), Mehrmonitor-Optionen (`monitors.mode`, `monitors.panel`)
- [x] 2026-10-02 Version 1.4.0: grafischer Editor `BGInfo-Reloaded-Editor.exe` (eigenes Programm, wird nicht
      verteilt); relativer `wallpaper.path` gilt relativ zur Konfigurationsdatei; Bild je Auflösung
      standardmäßig an
- [x] 2026-10-02 Platzhalter-Beschreibungen mit Umlauten; umgeleitete Ausgabe als UTF-8; Ausgabe in
      einer interaktiven Konsole geprüft
- [x] 2026-10-02 Version 2.0.0: Umbenennung in bginfo-reloaded (`BGInfo-Reloaded.exe`,
      `BGInfo-Reloaded-Editor.exe`), flache Ordnerstruktur, `NOTICE.md` mit Herkunft und Quellen,
      Git-Repository mit erstem Commit; `powerbginfo/` nicht mehr Teil des Projekts
- [x] 2026-10-02 Schreibweise „BGInfo-Reloaded“ mit Bindestrich für Programme, Konfigurationsdatei,
      Ordner und Aufgaben (Ausnahmen: C#-Namespace, Umgebungsvariable `BGINFORELOADED_CONFIG`)
- [x] 2026-10-02 Umzug in den Projektordner `bginfo_reloaded`; Icons in beide Programme eingebettet;
      Demo-Hintergründe in `backgrounds/` angebunden (Bild ohne Auflösungszusatz darf fehlen, sonst
      am besten passendes Bild)
- [x] 2026-10-02 Konfigurationsdatei heißt standardmäßig `BGInfo-Reloaded.json` (vorher
      `bginfo.json`), Vorlage `BGInfo-Reloaded.sample.json`; ohne Rückfall auf den alten Namen
- [x] 2026-10-02 Konsole wirkt nach dem Lauf nicht mehr eingefroren (exe schickt am Ende selbst ein
      Enter, die Shell zeigt einen frischen Prompt) – in einem echten Terminal geprüft
- [x] 2026-10-02 Log nennt je Monitor das tatsächlich verwendete Hintergrundbild samt Grund; echter
      Lauf mit `background_5120x1440.jpg` geprüft
- [x] 2026-10-02 White-/Blacklist von Computernamen (`hosts.include`, `hosts.exclude`, `onExcluded`)
- [x] 2026-10-02 Platzhalter `{subnet}`; Standardpanel zeigt Subnetzmaske und Laufzeit; Editor:
      Bezeichnungs-/Wertfeld verlor nach dem ersten Tastendruck den Fokus (per UI-Automation
      nachgestellt und behoben)
- [x] 2026-10-02 Version 2.1.0: Abgleich mit der Feldliste von BGInfo – neue Platzhalter
      `{dhcp}`, `{nic}`, `{netspeed}`, `{nettype}`, `{machinedomain}`, `{logonserver}`,
      `{systemtype}`, `{ramfree}`, `{filever:}`, `{filetime:}`; ganze WQL-Abfragen
      (`{wmi:SELECT … FROM …}`); Editor mit Dialog „WMI-Abfrage…“ (Klasse, Eigenschaft, Bedingung)
- [x] 2026-10-02 Eigene, benannte Felder (`fields`), zentrale Datei per `include` (Felder und
      Zeilen, mit lokalem Cache), Zeilenfilter `onlyOn`/`notOn`/`when`; Editor: Reiter „Eigene
      Felder“, Filterfelder je Zeile, Eintrag „Zentrale Zeilen“; `--list-tags` zeigt eigene Felder
- [x] 2026-10-02 Panel mit abgerundeten Ecken (`panel.cornerRadius`) und Platzierung über bzw.
      neben der Taskleiste (`panel.avoidTaskbar`, Standard an), weicher Schatten
      (`panel.shadowSize`, `panel.shadowColor`); alles im Editor einstellbar
- [x] 2026-10-02 Weitere Platzhalter: `{allips}`, `{adsite}`, `{ou}`, `{session}`, `{rdpclient}`,
      `{language}`, `{installdate}`, `{lastupdate}`, `{pendingreboot}`, `{secureboot}`,
      `{antivirus}`, `{virtual}`, `{chassis}`, `{assettag}`, `{uuid}`, `{biosdate}`, `{gpu}`,
      `{disk}`, `{rampercent}`, `{battery}`, `{monitors}`
- [x] 2026-10-02 Platzhalter kürzen (`{name,20}`, `{name,20...}`, `{name,-8}`); `"version"` und
      `"description"` in der Konfiguration, `{configversion}`; gespeicherte Dateien bekommen
      Standardkommentare (`ConfigWriter.cs`), Nachfrage nur noch bei eigenen Kommentaren
- [x] 2026-10-02 Editor: Symbolleiste mit Symbolen, Fußzeile mit Dateiname und Programmversion,
      Reiter „Datei und Felder“ und „Autostart und Werkzeuge“, Aussehen-Reiter neu gegliedert
- [x] 2026-10-02 Bedingte Blöcke (`"block"` … `"endBlock"`): mehrere Einträge unter einem Filter,
      z. B. nur auf echter Hardware (`{virtual} = nein`); im Editor „+ Block“ mit Einrückung
- [x] 2026-10-02 Restordner `bginfonative\` ist entfernt
- [x] 2026-10-02 `--export-config <datei> --config <datei>` schreibt eine vorhandene Konfiguration
      im aktuellen Format neu (alte Fassung als `.bak`)
- [x] 2026-10-02 Version 2.2.0: Arbeitsstand aus `bginfo_ps` hierher zusammengeführt und auf die
      Schreibweise `BGInfo-Reloaded` gebracht; `README.md` und `Übergabe.md` nachgezogen
- [x] 2026-10-02 Vor der Veröffentlichung geprüft: `bginfo_ps` und `bginfo_reloaded` enthalten
      keine unzusammengeführten Commits, Branches oder Stashes; beide Projekte bauen ohne Warnungen;
      keine Zugangsdaten oder internen Namen in den versionierten Dateien
- [x] 2026-10-02 CI-Workflow `.github/workflows/release.yml`: baut bei einem Tag `v*` und hängt
      `BGInfo-Reloaded.exe` an ein GitHub-Release (prüft Tag gegen `<Version>`)
- [x] 2026-10-02 Lizenz festgelegt: MIT (`LICENSE`); `README.md` bleibt das Haupt-README, das
      englische Gerüst `README_BGInfo-Reloaded.md` ist entfernt
- [x] 2026-10-02 Demo-Motiv (Fenster mit vier Scheiben) gesichtet: es ähnelt dem Windows-Logo;
      Entscheidung des Nutzers: wird trotzdem mitveröffentlicht
- [x] 2026-10-02 Veröffentlicht auf GitHub: `https://github.com/Kilowatt-W/bginfo-reloaded`
      (Remote `origin`, Branch `main`; der dortige Erstcommit mit LICENSE und README-Platzhalter ist
      eingemischt)
- [x] 2026-10-02 Dokumentation zweisprachig: `README.en.md`, `HOWTO.en.md`, `NOTICE.en.md` neben den
      deutschen Fassungen; README zeigt und verlinkt die Demo-Hintergründe; `Übergabe.md`, `TODO.md`
      und `CLAUDE.md` bleiben als Arbeitsunterlagen deutsch
- [x] 2026-10-02 Alter Ordner `bginfo_ps` in den Papierkorb verschoben (Stand war
      vollständig übernommen; die dort zuletzt geänderte Konfiguration vorher nach `publish_tmp`
      kopiert). Commits tragen jetzt die GitHub-noreply-Adresse, Historie auf GitHub überschrieben
- [x] 2026-10-11 Version 2.3.0 (Tag `v2.3.0` am 2026-10-11 gepusht, löst den Release-Workflow aus): relative Panelbreite
      (`panel.screenFraction`, 1/3 bis 1/8 mit skaliertem Inhalt), Spaltenanteil
      (`panel.labelShare`) und Spaltenabstand (`panel.columnGap`), Kürzen zu langer Texte mit „…“;
      Editor zeigt Auflösung und Skalierung in der Titelleiste und über der Vorschau
- [x] 2026-10-11 `BGInfo-Reloaded.json` wird ohne Angabe zuerst im Startverzeichnis gesucht (exe und
      Editor gleich); Editor öffnet sie beim Start von selbst; Fußzeile zeigt den Dateinamen, der
      Pfad steht im Tooltip
- [x] 2026-10-11 Release-Workflow hängt die Beispielkonfiguration als zweites Asset an, von der exe
      neu geschrieben mit allen Optionen und einer Kommentarliste aller Platzhalter
      (`--with-placeholders`); Editor fragte fälschlich bei jeder Datei nach eigenen Kommentaren –
      behoben
- [x] 2026-10-11 Fehlendes Hintergrundbild für die Auflösung: Editor weist beim Speichern darauf hin
      und stellt die Anpassung auf Dehnen bzw. Anpassen um; Hinweis auch im Log der exe und in der
      Statuszeile der Vorschau (Anlass: abgeschnittenes Wallpaper bei `fit = None` auf 2454×960)
- [x] 2026-10-11 Release `v2.3.1` veröffentlicht: Lauf auf GitHub grün, beide Assets vorhanden
      (`BGInfo-Reloaded.exe`, `BGInfo-Reloaded.sample.json`) – vom Nutzer bestätigt
- [x] 2026-10-11 Version 2.4.0 (Tag `v2.4.0` gepusht): Selbst-Update aus einem Ordner bzw. UNC-Share
      (`update.path`, `update.auto`, `--update --from`), aktualisiert auch einen vorhandenen Editor;
      mit lokalen Ordnern von 2.3.9 auf 2.4.0 durchgetestet
- [x] 2026-10-11 Version 2.5.0 (Tag `v2.5.0` gepusht): beim Ordner-Update wählbar, was
      geholt wird (`update.program`, `editor`, `config`, `backgrounds`), GitHub-Repository in der
      Konfiguration (`update.repo`); im Editor mit Kästchen und Tooltips zu den betroffenen Dateien
- [x] 2026-10-11 Version 2.6.0 (Tag `v2.6.0` gepusht): Import/Export der White- und
      Blacklist als Textdatei im Editor; IP-Adressen, Netze und Bereiche in beiden Listen und in
      `onlyOn`/`notOn`; Release-Builds ohne `.pdb`; Doppelte in den Listen werden entfernt;
      Listen wahlweise aus einer zentralen Textdatei (`hosts.includeFile`, `hosts.excludeFile`);
      Editor: Feld „Value“ mit „Aktueller Wert“ darunter (auch für eigene Felder), Startabfrage,
      wenn keine Konfiguration gefunden wird (Standard-Konfiguration kommt in den Programmordner)
- [x] 2026-10-11 Version 2.7.0 (Tag `v2.7.0` gepusht): `panel.textOverflow` – zu lange
      Texte bei fester Breite abschneiden oder umbrechen; Editor sperrt die feste Breite, solange
      eine relative Breite gewählt ist
- [x] 2026-10-11 Version 2.7.1 (Tag `v2.7.1` gepusht): Logo `icons/BGInfo-Reloaded_logo.jpg` in
      beiden READMEs; Programm unverändert gegenüber 2.7.0
- [x] 2026-10-11 Version 2.7.2 (Tag `v2.7.2` gepusht): Release-Workflow repariert – die
      Versionsprüfung las seit der zweiten `PropertyGroup` (pdb abgeschaltet) „2.x.y “ mit
      Leerzeichen und brach ab. Läufe `v2.3.0` bis `v2.5.0` waren grün (per `gh` geprüft)
- [x] 2026-10-11 Tags `v2.6.0`, `v2.7.0` und `v2.7.1` gelöscht (lokal und auf GitHub): für sie gab es
      nie ein Release; ihre Änderungen stecken in `v2.7.2`. Release-Lauf `v2.7.2` grün, beide Assets da
- [x] 2026-10-11 Version 2.8.0: erster Start ohne Konfiguration übernimmt das gesetzte Wallpaper
      (Kopie nach `backgrounds\background_user.*`, relativer `wallpaper.path`, Herkunft in
      `wallpaper.originalPath` und im Editor unter „Übernommen von (Originalpfad)“)
- [x] 2026-10-12 Version 2.9.0: Editor-Knopf „Vorschau anzeigen“ – aktuelle Darstellung als
      randloses Vollbild im Vordergrund (per UI-Automation geprüft: Fenster in Bildschirmgröße, Esc schließt)
- [x] 2026-10-12 Version 2.9.1: Firmen-, Domänen- und Pfadbezüge aus Repository und Doku entfernt,
      Beispielpfade auf `\\fileserver.example.com\share\…`, lokale Hinweise in `CLAUDE.local.md`
      (nicht eingecheckt), Git-Historie neu begonnen
- [x] 2026-10-12 Version 2.9.1: Update aus dem Ordner läuft auch im Editor (Prüfung beim Start mit
      Meldung) und ersetzt laufende Programme per Umbenennen; Schalter „Vorschau“ blendet den
      Vorschaubereich aus (getestet mit Update-Ordner 9.9.9 gegen 2.9.1: Editor und exe, jeweils
      während das andere Programm lief)
- [x] 2026-10-12 Version 2.10.0: Platzhalter per Drag & Drop als neue Zeile an der Ablagestelle bzw. in
      ein Textfeld; Doppelklick füllt je nach Schreibmarke Bezeichnung und/oder Value; Hinweis, wenn er auf ein gesperrtes Feld trifft (mit echter
      Mauseingabe geprüft: Ablegen auf Liste und auf „Value“, Doppelklick bei Leerraum)
- [x] 2026-10-12 Version 2.10.0: Datums- und Zahlenformat einstellbar (`format.date`, `dateTime`,
      `decimals`, `culture`; Editor: Reiter „Aussehen“). Geprüft mit der exe: Standard unverändert,
      `de-DE`, `system` (de-AT), ungültige Angaben fallen auf den Standard zurück
- [x] 2026-10-12 Version 2.11.0: `split` teilt einen Wert an Trennzeichen auf mehrere Zeilen; Laufzeit nach
      Sprache und `{uptime:Format}`; Editor: Suche in der Platzhalterliste, kurzer Titel mit Hinweiszeile,
      hervorgehobene Spaltenköpfe (exe: neutral/de/en/fr und Teilen geprüft; Editor per Bildschirmfoto)
- [x] 2026-10-12 Version 2.12.0: Rahmen (`border`: Farbe, Stärke, Linie/Punkte/strichliert) um Panel,
      Abschnitte oder Zeilen; Editor: Reiter „Aussehen“ (alle drei Arten gerendert und angesehen)
- [x] 2026-10-12 Version 2.13.0: Editor rendert die Vorschau auch auf einem per White-/Blacklist
      ausgeschlossenen Rechner und weist in Orange darauf hin (Ursache der „leeren unteren Vorschau“)
- [x] 2026-10-12 Version 2.13.0: Editor-Schalter „100 %“ – Panel ohne Windows-Skalierung und ohne
      relative Breite zeichnen (schaltet `panel.scaleWithDpi` und `panel.screenFraction` zusammen)
- [x] 2026-10-12 Version 2.13.1: Update-Meldung des Editors mit „Jetzt neu starten“, wenn er sich selbst
      aktualisiert hat (geprüft: alter Prozess endet, neuer startet mit derselben Datei)
- [x] 2026-10-12 Version 2.13.2: Editor-Knopf „Anwenden und beenden“ (geprüft: exe läuft mit Exit 0, Editor schließt sich)
- [x] 2026-10-13 Version 2.14.0: Bilder je Auflösung – Liste im Editor (gefundene Fassungen, eigene Zuordnungen,
      Verwendung je Monitor), `wallpaper.byResolution`, Auflösung im Namen des gewählten Bildes wird entfernt
      (exe: Ordnerfassungen, Zuordnung, fehlende Datei geprüft; Editor: Liste per Bildschirmfoto)
- [x] 2026-10-13 Version 2.14.1: die Liste „Bilder je Auflösung“ führt auch das Standardbild mit seiner
      tatsächlichen Auflösung an und vermerkt je Monitor das verwendete Bild; ein Bild für 1920 × 1080 steht fest direkt darunter
- [x] 2026-10-13 Version 2.14.2: der Hinweis „Rechner ausgeschlossen“ steht in einer eigenen orangen Zeile
      links unten über der Fußzeile (vorher rechts neben der Vorschau-Überschrift, dort verdrängt)
- [x] 2026-10-13 Version 2.15.0: Rechnergruppen (`hosts.groups`) – je Gruppe eigene Konfigurationsdatei, diese
      Konfiguration oder kein Panel; ersetzt White- und Blacklist (altes Format wird gelesen und umgewandelt);
      Editor: Gruppentabelle im Reiter „Rechner“ (exe: neun Fälle geprüft, Editor: Tabelle per Bildschirmfoto)
- [x] 2026-10-13 Version 2.15.0: fehlt das Bild ohne Auflösung im Namen, gilt die Fassung mit 1920x1080 im Namen
      als Standardbild; der Editor sagt bei der Auswahl, welche Datei als Standard gilt
- [x] 2026-10-13 Version 2.16.0: Balken für Prozentwerte (`bar` je Zeile, `colors.barUsed`/`barFree`,
      `panel.barHeight`) – Laufwerke, RAM, Akku; mit Text darunter, ohne Text statt des Werts (drei Fälle gerendert)
- [x] 2026-10-13 Version 2.17.0: Update aus GitHub im Editor (Sync-Knopf neben dem Repository-Feld), Adresse des
      Repositorys als Vorgabe, volle Adresse wird verstanden, Editor als drittes Release-Asset (exe: aktuell und
      alter Stand 2.0.0 gegen v2.16.2 geprüft; Editor-Update erst nach dem ersten Release mit Editor-Asset prüfbar)
- [x] 2026-10-13 Version 2.16.2: „Aktualisieren“ ist grau, solange nichts Neueres bereitliegt; als neuer gilt auch
      dieselbe Version mit neuerem Datum und anderer Größe; Editor startet auch, wenn der Teiler nicht gezeichnet
      werden kann (GDI+-Fehler bei nicht benutzbarem Bildschirm)
- [x] 2026-10-13 Version 2.16.1: Editor-Knopf „Aktualisieren“ – Update-Ordner sofort prüfen und holen, mit Meldung
      und Neustart-Angebot; ein schon getauschtes, noch laufendes Programm wird nicht erneut ersetzt
- [x] 2026-10-13 Version 2.16.0: die ausgewählte Bilddatei gilt immer als Standardbild, auch mit Auflösung im
      Namen; der Editor trägt sie unverändert ein (vorher: Name ohne Auflösung, in der Liste rot „fehlt“)
- [x] 2026-10-01 Rendering von SixLabors ImageSharp auf `System.Drawing.Common` (GDI+, MIT)
      umgestellt – Entscheidung Rendering-Bibliothek damit erledigt, kein Lizenzthema mehr
