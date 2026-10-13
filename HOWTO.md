# BGInfo-Reloaded – Howto

**Deutsch** | [English](HOWTO.en.md)

`BGInfo-Reloaded.exe` schreibt Systeminformationen (Hostname, IP, Betriebssystem, Hardware, Laufwerke, …)
als Panel auf das Desktop-Wallpaper – ein eigenständiger Ersatz für Sysinternals BGInfo. Zum Betrieb
gehören genau zwei Dateien:

| Datei | Zweck |
|---|---|
| `BGInfo-Reloaded.exe` | das Programm (eine Datei, bringt die .NET-Runtime mit, keine Installation nötig) |
| `BGInfo-Reloaded.json` | optional: was angezeigt wird und wie es aussieht |

Dazu kommt für Admins `BGInfo-Reloaded-Editor.exe`, ein grafischer Editor für die `BGInfo-Reloaded.json`. Er wird nicht
verteilt.

Technische Hintergründe, Lizenzhinweise und der Vergleich zu PowerBGInfo stehen in
[README.md](README.md).

## Features im Überblick

- **Eine exe, keine Abhängigkeiten** – kein PowerShell, kein Installer, keine .NET-Installation auf
  dem Zielrechner.
- **Konfiguration per JSON** – Inhalt, Position, Farben und Schrift des Panels, ohne die exe neu zu
  bauen.
- **Erweiterbar über Platzhalter** – beliebige Werte aus WMI, Registry, Umgebungsvariablen oder
  Dateien anzeigen.
- **Zentral verteilbar** – Konfiguration von UNC-Share oder http(s)-URL, mit lokalem Cache für
  Rechner ohne Netzverbindung.
- **White-/Blacklist** – das Panel per Computernamen-Muster (`*`, `?`) auf Rechnern ein- oder
  ausschließen.
- **Autostart** – Scheduled Tasks bei Anmeldung und in festem Intervall, ohne Admin-Rechte.
- **Fensterlos** – im Task-Betrieb erscheint kein Konsolenfenster.
- **Logdatei** – jeder Lauf wird protokolliert, Fehler sind nachvollziehbar.
- **Original-Wallpaper bleibt erhalten** – jeder Lauf rendert vom unveränderten Original, es wird
  nie Text auf Text gestapelt.
- **Pixelgenau für den Bildschirm** – das Hintergrundbild wird auf die aktuelle Auflösung gebracht
  (Ausfüllen, Anpassen, Dehnen, Zentriert, Nebeneinander), das Panel folgt der Windows-Skalierung.
- **Relative Panelbreite** – das Panel auf 1/3 bis 1/8 der Bildschirmbreite festlegen; der Inhalt
  wird passend skaliert und wirkt auf jedem Monitor gleich groß.
- **Eigenes Hintergrundbild je Auflösung** – z. B. `wallpaper_3440x1440.jpg`, sonst das Standardbild.
- **Mehrere Monitore** – je Monitor ein eigenes Bild in dessen Auflösung; das Panel wahlweise auf
  allen Monitoren oder nur einmal auf dem primären.
- **Reagiert auf Auflösungswechsel** – im Überwachungsmodus (`--watch`) wird bei Änderung von
  Auflösung oder Skalierung automatisch neu gerendert.
- **Platzhalter-Liste** – `--list-tags` zeigt alle verfügbaren Platzhalter mit ihrem aktuellen Wert.
- **Vorschau** – Ergebnis in eine Bilddatei rendern, ohne das Wallpaper anzufassen.
- **Selbst-Update** – aus GitHub-Releases oder direkt aus dem Git-Quellcode.

## Grafischer Editor (`BGInfo-Reloaded-Editor.exe`)

Die `BGInfo-Reloaded.json` lässt sich von Hand oder mit dem Editor bearbeiten. Der Editor ist ein eigenes
Programm für Admins und wird **nicht** auf die Zielrechner verteilt – dort laufen nur
`BGInfo-Reloaded.exe` und die `BGInfo-Reloaded.json`.

```
BGInfo-Reloaded-Editor.exe                  die BGInfo-Reloaded.json aus dem Startverzeichnis öffnen (sonst nachfragen)
BGInfo-Reloaded-Editor.exe BGInfo-Reloaded.json      eine vorhandene Datei öffnen
```

- **Inhalt:** die Zeilen des Panels (Überschrift, Zeile, Leerraum, Laufwerke) hinzufügen, entfernen
  und verschieben. Daneben steht die Liste aller Platzhalter mit ihrem Wert auf diesem Rechner; ein
  Doppelklick fügt den Platzhalter an der Schreibmarke ein.
  Unter der Liste stehen für die gewählte Zeile „Bezeichnung“, „Value“ (der Text mit Platzhaltern,
  wie er in der Datei steht) und „Aktueller Wert“ – das Ergebnis auf diesem Rechner, nur zur Ansicht.
  Dort steht auch, wenn eine Zeile hier gar nicht erscheint (Filter trifft nicht zu, Wert leer bei
  „weglassen“, ausgeblendeter Block). Die Tabelle der eigenen Felder im Reiter „Datei und Felder“
  hat dieselbe Spalte „Aktueller Wert“.
- **Aussehen:** Position, Abstände, Breite des Panels (nach Inhalt, fest oder 1/3 bis 1/8 der
  Bildschirmbreite), Aufteilung in Bezeichnungs- und Wertspalte (Schieberegler), Abstand ab der
  Taskleiste, runde Ecken, Schatten, Farben, Deckkraft, Schrift.
- **Hintergrund:** Bilddatei, Anpassung an die Auflösung, Bild je Auflösung, Verhalten bei mehreren
  Monitoren.
- **Rechner:** White- und Blacklist.
- **Datei und Felder:** Version und Beschreibung der Datei, zentrale Datei (`include`), eigene Felder.
- **Autostart und Werkzeuge:** Autostart-Tasks einrichten und entfernen (Intervall, `--watch`),
  Vorschau als Bilddatei speichern, Logdatei und Datenordner öffnen.
- **Titelleiste:** neben dem Dateinamen die erkannte Auflösung und Windows-Skalierung des primären
  Monitors – dieselben Werte, mit denen `BGInfo-Reloaded.exe` rendert. Über der Vorschau stehen sie
  für jeden Monitor. Ändern sie sich bei geöffnetem Editor, werden Anzeige und Vorschau aktualisiert.
- **Fußzeile:** links Meldungen, rechts der Name der geladenen Datei (voller Pfad beim Zeigen mit
  der Maus) und die Programmversion.
- **Vorschau:** rechts oben der ganze Bildschirm, darunter das Panel in Originalgröße. Gerendert
  wird mit demselben Code wie in `BGInfo-Reloaded.exe`, für den primären Monitor dieses Rechners.
- **Platzhalter übernehmen (Reiter „Inhalt“):** Doppelklick auf einen Platzhalter fügt in die
  markierte Zeile ein, je nachdem, wo die Schreibmarke steht: in „Value“ den Platzhalter (und, wenn
  die Bezeichnung noch leer ist, dessen Bedeutung als Bezeichnung), in „Bezeichnung“ die Bedeutung
  (und, wenn „Value“ noch leer ist, den Platzhalter). Eine neue Zeile aus „+ Zeile“ wird so mit
  einem Doppelklick ganz gefüllt. Bei Überschriften, Blöcken und „Nur wenn“ kommt nur der
  Platzhalter; ist das Feld bei der markierten Zeile gesperrt (z. B. Leerraum), erscheint ein
  Hinweis. Ziehen auf die Zeilenliste legt an der Stelle, an der
  losgelassen wird, eine neue Zeile an (Bezeichnung = Bedeutung, Value = Platzhalter); Ziehen auf
  ein Textfeld fügt dort ein.
- **100 % (Schalter in der Leiste):** zeichnet das Panel so wie ohne Windows-Skalierung. Er schaltet
  dafür „Größe mit der Windows-Skalierung vergrößern“ (`panel.scaleWithDpi`) und „Relative Breite“
  (`panel.screenFraction`) zusammen aus; Ausschalten stellt die vorherigen Werte wieder her. Das
  Hintergrundbild selbst wird ohnehin immer in der echten Pixelauflösung des Monitors gerendert.
- **Ausgeschlossener Rechner:** steht der Rechner, auf dem der Editor läuft, auf der Blacklist (oder
  nicht auf der Whitelist), zeigt die Vorschau das Panel trotzdem. Rechts neben „Vorschau – ganzer
  Bildschirm“ steht dann in Orange, dass `BGInfo-Reloaded.exe` hier nur den Hintergrund setzt bzw.
  nichts ändert (voller Text beim Zeigen mit der Maus).
- **Platzhalter suchen:** das Feld „Suchen“ unter „WMI-Abfrage…“ filtert die Platzhalterliste beim
  Tippen über Platzhalter, Bedeutung und Wert; mehrere Wörter müssen alle vorkommen. Esc oder ✕ leert es.
- **Vorschau (Schalter in der Leiste):** blendet den Vorschaubereich rechts ein oder aus; ausgeblendet
  wird auch nicht gerendert. Die Wahl bleibt je Benutzer gespeichert.
- **Update beim Start:** steht in der geladenen Konfiguration ein Update-Ordner (`update.path`), prüft
  ihn der Editor beim Start. Mit `update.auto` holt er Neueres wie `BGInfo-Reloaded.exe` (auch sich
  selbst – aktiv ab dem nächsten Start – und eine neuere Konfiguration, die er dann neu lädt); ohne
  meldet er nur, was bereitliegt. Eine Meldung erscheint nur, wenn es etwas zu sagen gibt (geholt,
  verfügbar, Ordner nicht erreichbar); danach geht es normal weiter. Wurde der Editor selbst
  aktualisiert, bietet die Meldung „Jetzt neu starten“ an: der Editor beendet sich und startet mit
  derselben Datei in der neuen Version.
- **Vorschau anzeigen:** zeigt die aktuelle Darstellung (auch ungespeichert) als Vollbild im
  Vordergrund – ohne Rahmen, Taskleiste und Desktop-Symbole, auf dem Bildschirm des Editors und für
  dessen Auflösung gerendert. Esc, eine Taste oder ein Klick schließt; das Wallpaper bleibt unberührt.
- **Jetzt anwenden:** speichert und führt die `BGInfo-Reloaded.exe` im selben Ordner mit dieser Datei
  aus – das Wallpaper wird dabei wirklich gesetzt.
- **Anwenden und beenden:** wie „Jetzt anwenden“, danach schließt sich der Editor. Meldet die exe
  einen Fehler, bleibt er mit der Meldung offen.

Zu beachten:

- Der Editor schreibt bei jedem Speichern einen festen Kopf und hinter jede Einstellung einen
  erläuternden Standardkommentar (ebenso `--export-config`). **Eigene** Kommentare in einer von Hand
  gepflegten Datei gehen beim Speichern verloren; der Editor fragt vorher nach. Für eigene
  Anmerkungen gibt es das Feld `"description"` – es bleibt erhalten.
- Der Editor schreibt alle Einstellungen aus, auch die mit Standardwert.
- Liegt das gewählte Hintergrundbild im selben Ordner wie die Konfigurationsdatei, wird es relativ
  eingetragen (`.\bild.jpg`).
- Beim Speichern prüft der Editor, ob es für die Monitore dieses Rechners ein Hintergrundbild in
  passender Auflösung gibt. Fehlt es und würde die gewählte Anpassung einen Teil des Ersatzbildes
  abschneiden (`None`, `Fill`, `Center`, `Tile` bei anderem Seitenverhältnis), nennt er das gesuchte
  und das verwendete Bild und bietet an, auf `Stretch` (Dehnen) oder `Fit` (Anpassen)
  umzustellen – damit ist das ganze Bild zu sehen. Die Prüfung gilt nur für diesen Rechner; auf
  Zielrechnern mit anderer Auflösung steht derselbe Hinweis im Log.
- Bei `"fit": "None"` zeigt die Vorschau das unskalierte Bild. Auf dem Desktop skaliert Windows es
  danach noch selbst und schneidet dabei je nach eigener Einstellung einen Teil ab – auch vom Panel.
  Die Statuszeile warnt in diesem Fall.

## Schnellstart

```
BGInfo-Reloaded.exe --preview vorschau.png     1. ansehen, wie es aussehen würde
BGInfo-Reloaded.exe --export-config            2. BGInfo-Reloaded.json als Vorlage erzeugen und anpassen
BGInfo-Reloaded.exe                            3. Wallpaper jetzt setzen
BGInfo-Reloaded.exe --register-task            4. künftig automatisch aktualisieren
```

## Alle Aufrufe

| Aufruf | Wirkung |
|---|---|
| `BGInfo-Reloaded.exe` | Wallpaper einmalig aktualisieren |
| `--config <pfad\|url>` | Konfiguration aus Datei, UNC-Share oder http(s)-URL laden |
| `--preview <bild.png>` | nur in eine Bilddatei rendern und die Zeilen ausgeben; Wallpaper bleibt unverändert |
| `--export-config [pfad]` | Standard-Konfiguration als Vorlage schreiben (Standard: `BGInfo-Reloaded.json` im aktuellen Ordner; überschreibt nichts) |
| `--export-config <pfad> --config <pfad>` | vorhandene Konfiguration im aktuellen Format neu schreiben (alle Optionen, Standardkommentare); Quelle und Ziel dürfen dieselbe Datei sein, die alte Fassung bleibt als `.bak` |
| `--export-config … --with-placeholders` | zusätzlich alle Platzhalter dieser Programmversion als Kommentar in die Datei schreiben (so entsteht die Beispielkonfiguration, die an jedem Release hängt) |
| `--register-task` | Autostart einrichten: bei Anmeldung + alle 30 Minuten |
| `--register-task --interval-minutes <n>` | eigenes Intervall; `0` = nur bei Anmeldung |
| `--register-task --config <pfad\|url>` | Autostart mit fester Konfigurationsquelle |
| `--register-task --watch` | Autostart, der zusätzlich auf Auflösungswechsel reagiert |
| `--unregister-task` | Autostart wieder entfernen |
| `--watch` | rendern und aktiv bleiben; bei Änderung von Auflösung oder Skalierung neu rendern |
| `--list-tags` | alle Platzhalter mit Beschreibung und aktuellem Wert ausgeben |
| `--list-tags wmi:<Klasse>` | alle Eigenschaften einer WMI-Klasse als fertige Platzhalter |
| `--list-tags reg:<Schlüssel>` | alle Werte eines Registry-Schlüssels als fertige Platzhalter |
| `--update --repo <org/repo>` | neueres GitHub-Release installieren |
| `--update` | ohne `--repo`: aus dem Ordner in `update.path` der Konfiguration aktualisieren, sonst aus dem gemerkten GitHub-Repository |
| `--update --from <ordner>` | einmalig aus diesem Ordner aktualisieren |
| `--update --check-only` | nur prüfen, ob eine neuere Version existiert |
| `--update-source <git-url> [--branch main]` | Quellcode holen und lokal neu bauen |
| `--version` | Version anzeigen |
| `--help` | Hilfe anzeigen |

`--config` lässt sich mit dem normalen Lauf, `--preview`, `--watch` und `--register-task` kombinieren.

### Verhalten in der Konsole

Die exe ist fensterlos. Aus einer Konsole gestartet schreibt sie ihre Ausgabe dorthin, die Shell
wartet aber nicht auf das Programmende – der Prompt kommt sofort zurück, die Ausgabe erscheint
dahinter. Damit die Konsole danach nicht aussieht, als hinge sie, schickt die exe am Ende selbst
ein Enter; die Shell zeigt daraufhin einen frischen Prompt. Folge: Wer während der etwa ein bis
zwei Sekunden Laufzeit schon den nächsten Befehl tippt, bekommt ihn durch dieses Enter abgeschickt.
Bei `--watch` (läuft dauerhaft) und bei umgeleiteter Ausgabe passiert das nicht. Wer in Skripten
den Exit-Code braucht (`0` = Erfolg, `1` = Fehler):

```powershell
$p = Start-Process .\BGInfo-Reloaded.exe -ArgumentList '--config','\\fileserver.example.com\share\BGInfo-Reloaded.json' -Wait -PassThru
$p.ExitCode
```

```bat
start /wait BGInfo-Reloaded.exe
echo %ERRORLEVEL%
```

## Konfiguration: `BGInfo-Reloaded.json`

### Wo die Datei gesucht wird

Die erste gefundene Quelle gewinnt:

1. `--config <pfad|url>`
2. Umgebungsvariable `BGINFORELOADED_CONFIG` (Pfad oder URL)
3. `BGInfo-Reloaded.json` im Startverzeichnis – dem Ordner, aus dem das Programm aufgerufen wird
   (aktueller Ordner der Konsole bzw. „Ausführen in“ einer Verknüpfung)
4. `BGInfo-Reloaded.json` im selben Ordner wie die exe
5. `%ProgramData%\BGInfo-Reloaded\BGInfo-Reloaded.json`
6. eingebaute Standardwerte

Welche Quelle benutzt wurde, steht in der Ausgabe und im Log (`Konfiguration: …`).

Der Editor hält sich an dieselbe Reihenfolge: Ohne Dateiangabe öffnet er die Datei, die auch
`BGInfo-Reloaded.exe` benutzen würde (eine per Umgebungsvariable genannte URL überspringt er). Gibt
es keine, fragt er beim Start: **Standard-Konfiguration erstellen** (legt eine
`BGInfo-Reloaded.json` mit allen Einstellungen in ihren Standardwerten und leerer White- und
Blacklist im Ordner des Programms an und öffnet sie), **vorhandene Konfiguration laden** oder
**ohne Datei beginnen**. Ist der Programmordner nicht beschreibbar, lässt er den Speicherort wählen. In der Fußzeile steht der Dateiname; der volle Pfad
erscheint, wenn die Maus darauf zeigt.

Ein Autostart-Task startet in einem Systemordner – dort liegt keine `BGInfo-Reloaded.json`, es gilt
also die Datei neben der exe bzw. die mit `--config` eingerichtete.

### Erster Start: gesetztes Wallpaper übernehmen

Gibt es noch gar keine Konfiguration (keine Angabe, keine Umgebungsvariable, keine Datei an den
Orten oben), wird beim ersten Start das gerade gesetzte Wallpaper des Benutzers übernommen:

- Das Bild wird nach `backgrounds\background_user.<Endung>` neben die Konfiguration kopiert und als
  `wallpaper.path` relativ eingetragen (`.\backgrounds\background_user.jpg`). Der Ordner lässt sich
  so samt Bild verteilen. Eine vorhandene Datei wird nie überschrieben (dann `background_user-2` …).
- Woher das Bild stammt, steht als `wallpaper.originalPath` in der Datei (nur zur Information) und
  im Editor im Reiter „Hintergrund“ unter „Übernommen von (Originalpfad)“.
- `BGInfo-Reloaded.exe` legt dabei eine `BGInfo-Reloaded.json` mit den Standardwerten neben sich
  an; der Editor tut es bei **Standard-Konfiguration erstellen**. Mit `--preview` und bei
  **ohne Datei beginnen** wird nichts angelegt.
- Ist der Ordner nicht beschreibbar oder kein Wallpaper gesetzt, bleibt es beim bisherigen
  Verhalten: eingebaute Standardwerte, das Wallpaper des Benutzers wird zur Laufzeit gelesen.

### Aufbau

Kommentare (`//`) und abschließende Kommas sind erlaubt. Alles, was weggelassen wird, behält seinen
Standardwert – eine Datei, die nur `items` enthält, ist also gültig. Eine vollständige, kommentierte
Vorlage liegt in [BGInfo-Reloaded.sample.json](BGInfo-Reloaded.sample.json).

```jsonc
{
  "panel": {
    "position": "TopRight",      // TopLeft, TopCenter, TopRight, MiddleLeft, Center,
                                 // MiddleRight, BottomLeft, BottomCenter, BottomRight
    "margin": 40,                // Abstand zum Rand in Pixeln
    "avoidTaskbar": true,        // Abstand ab dem Arbeitsbereich messen: das Panel beginnt über bzw.
                                 // neben der Taskleiste statt dahinter (false = ab der Monitorkante)
    "padding": 22,               // Innenabstand des Panels
    "screenFraction": 0,         // Panelbreite als Bruchteil der Bildschirmbreite: 3 bis 8 = 1/3 bis 1/8,
                                 // der Inhalt wird passend skaliert; 0 = aus
    "width": 0,                  // feste Panelbreite in Pixeln; 0 = nach Inhalt
    "labelShare": 0,             // Anteil der Bezeichnungsspalte in Prozent (10 bis 90); 0 = automatisch
    "labelWidth": 0,             // Breite der Bezeichnungsspalte in Pixeln; 0 = automatisch
    "columnGap": 24,             // Abstand zwischen Bezeichnung und Wert bei automatischer Spalte
    "textOverflow": "Truncate",  // zu lange Texte: Truncate = mit … abschneiden, Wrap = umbrechen
    "background": "#0C0E12A5",   // #RRGGBBAA, AA = Deckkraft
    "cornerRadius": 0,           // abgerundete Ecken des Panel-Hintergrunds; 0 = eckig
    "shadowSize": 0,             // Schlagschatten: so weit schaut er rechts unten hervor; 0 = ohne
    "shadowColor": "#00000055",  // Schattenfarbe, läuft am Rand weich aus
    "scaleWithDpi": true         // Maße und Schrift mit der Windows-Skalierung vergrößern
  },
  "font":   { "family": "Consolas", "size": 15, "headerSize": 19 },
  "colors": { "header": "#FFD700", "label": "#FFFFFF", "value": "#82DCD2" },
  "wallpaper": {
    "path": "\\\\fileserver.example.com\\share\\wallpaper.jpg",  // optional: festes Hintergrundbild
    "fit": "Fill",                               // Fill, Fit, Stretch, Center, Tile oder None
    "resolutionVariants": true,                  // Bild mit angehängter Auflösung bevorzugen
    "backgroundColor": "#101218"                 // Farbe für Ränder und falls kein Bild vorhanden ist
  },
  "monitors": {
    "mode": "PerMonitor",                        // PerMonitor = je Monitor ein Bild, Single = eines für alle
    "panel": "All"                               // All = Panel auf jedem Monitor, Primary = nur einmal
  },
  "hosts": {
    "include": [],                               // Whitelist von Computernamen, * und ? erlaubt
    "exclude": ["KIOSK-*", "PC-0??"],            // Blacklist, hat Vorrang
    "onExcluded": "BackgroundOnly"               // oder "Skip"
  },
  "update": {
    "path": "\\\\fileserver.example.com\\share\\BGInfo-Reloaded", // optional: Ordner mit der aktuellen exe für das Selbst-Update
    "auto": false                                // true = bei jedem Lauf prüfen und selbst aktualisieren
  },
  "items": [
    { "section": "SYSTEM" },
    { "label": "Hostname", "value": "{hostname}" },
    { "spacer": true },
    { "drives": true }
  ]
}
```

### Breite des Panels und der Spalten

**Panelbreite** – drei Möglichkeiten, in dieser Rangfolge:

| Einstellung | Wirkung |
|---|---|
| `"screenFraction": 3` … `8` | Das Panel ist 1/3 … 1/8 der Bildschirmbreite breit. Schrift, Innenabstand, Spaltenabstand, Ecken und Schatten werden so skaliert, dass der Inhalt diese Breite gerade füllt. Das Panel wirkt damit auf jedem Monitor gleich groß, unabhängig von Auflösung und Windows-Skalierung. |
| `"width": 600` | Feste Breite in Pixeln. Der Inhalt wird nicht skaliert; was nicht hineinpasst, wird je nach `textOverflow` abgeschnitten oder umbrochen. |
| beides `0` (Standard) | Breite nach Inhalt: so breit wie die längste Zeile. |

Bei `screenFraction` bestimmen `font.size` und `font.headerSize` nur noch das Verhältnis von
Überschrift zu Zeile, nicht mehr die absolute Größe. Wäre das Panel bei der gewünschten Breite höher
als der Bildschirm, wird der Inhalt so weit verkleinert, dass er in der Höhe passt. Der Abstand zum
Rand (`margin`) wird nicht mitskaliert. Bei mehreren Monitoren gilt der Bruchteil je Monitor.

**Spalten** – wie sich die Breite auf Bezeichnung und Wert verteilt, ebenfalls in dieser Rangfolge:

| Einstellung | Wirkung |
|---|---|
| `"labelShare": 35` | Die Bezeichnungsspalte bekommt 35 % der Innenbreite, die Werte den Rest (erlaubt: 10 bis 90). |
| `"labelWidth": 180` | Bezeichnungsspalte mit fester Breite in Pixeln. |
| beides `0` (Standard) | Automatisch: so breit wie die längste Bezeichnung plus `columnGap`. |

Mit `columnGap` lässt sich bei automatischer Spalte der Abstand zwischen Bezeichnung und Wert
einstellen.

**Texte, die nicht in ihre Spalte passen** (`textOverflow`) – das betrifft eine feste Panelbreite,
einen festen Spaltenanteil und ein Panel, das an den Bildschirmrand stößt:

| Einstellung | Wirkung |
|---|---|
| `"textOverflow": "Truncate"` (Standard) | Der Text wird abgeschnitten und endet mit „…“. Jede Zeile bleibt einzeilig. |
| `"textOverflow": "Wrap"` | Der Text wird in seiner Spalte umbrochen; die Zeile des Panels wird entsprechend höher. Gilt für Überschriften, Bezeichnungen und Werte. |

Bei relativer Breite (`screenFraction`) wird nie umbrochen – dort ist der Inhalt bereits so
skaliert, dass er passt. `screenFraction` hat außerdem Vorrang vor `width`: ist beides gesetzt, gilt
die relative Breite, und das Panel richtet sich wieder nach dem Inhalt. Reicht eine feste Breite
nicht für beide Spalten, bekommt die automatisch bemessene Bezeichnungsspalte höchstens 45 % davon.

### Rahmen (`border`)

```jsonc
"border": {
  "width": 2,            // Strichstärke in Pixeln, 0 = kein Rahmen (Standard)
  "style": "Solid",      // Solid = Linie, Dotted = Punkte, Dashed = strichliert
  "scope": "Panel",      // Panel = um das ganze Panel, Sections = je Abschnitt ein Kasten, Rows = Gitter um jede Zeile
  "color": "#FFFFFF80"   // #RRGGBBAA
}
```

- `Sections`: ein Kasten je Überschrift mit ihren Zeilen; ein Leerraum (`spacer`) trennt die Kästen
  sichtbar, ohne Leerraum stoßen sie aneinander.
- `Rows`: Gitter um jede Zeile mit Bezeichnung und Wert, mit Trennlinie zwischen den Spalten;
  Überschriften bleiben frei.
- Der Rahmen liegt im vorhandenen Innen- und Zeilenabstand: Größe und Lage des Panels ändern sich
  nicht. Die Strichstärke wird wie das Panel skaliert.
- Im Editor: Reiter „Aussehen“, Abschnitt „Rahmen“.

### Datums- und Zahlenformat (`format`)

```jsonc
"format": {
  "date": "dd.MM.yyyy",            // reines Datum: {biosdate}, {lastupdate}
  "dateTime": "dd.MM.yyyy HH:mm",  // Datum mit Uhrzeit: {boot}, {now}, {installdate}, {filetime:…}, WMI-Datumswerte
  "decimals": 1,                   // höchstens so viele Nachkommastellen: {ram}, {ramfree}, {netspeed}, Laufwerke
  "culture": "de-DE"               // Trennzeichen, Monats- und Tagesnamen
}
```

- Formatangaben wie in .NET: `d`/`dd` Tag, `M`/`MM` Monat, `MMMM` Monatsname, `yy`/`yyyy` Jahr,
  `ddd`/`dddd` Wochentag, `HH` Stunde (24 h), `hh` mit `tt` (12 h), `mm` Minute, `ss` Sekunde.
- `culture`: weggelassen = neutral (Punkt als Dezimaltrenner, englische Namen) – so sah das Panel
  bisher aus; `system` = wie das jeweilige Windows; sonst ein Kulturname wie `de-DE`, `de-AT`, `en-US`.
- Ein eigenes Format in einer Zeile (`{now:dd.MM.yyyy}`) hat Vorrang.
- Die Laufzeit (`{uptime}`) richtet sich nach `culture`: deutsch „31 Tage 21 Std.“, englisch
  „31 days 21 hours“ (unter einem Tag Stunden und Minuten); ohne Angabe und für andere Sprachen die
  Kurzform „31 d 21 h“. Eigenes Format: `{uptime:d' Tage und 'h' Stunden'}` (`d` Tage, `h` Stunden,
  `m` Minuten, fester Text in einfachen Anführungszeichen).
- Ein ungültiges Format oder eine unbekannte Kultur fällt auf den Standard zurück.
- Im Editor: Reiter „Aussehen“, Abschnitt „Datum und Zahlen“, mit Beispielzeile.

### Wert auf mehrere Zeilen teilen (`split`)

Liefert ein Platzhalter mehrere Angaben in einem Text – mehrere Grafikkarten, DNS-Server,
Datenträger –, teilt `split` den Wert an den angegebenen Trennzeichen. Der erste Teil steht neben
der Bezeichnung, jeder weitere in einer eigenen Zeile darunter:

```jsonc
{ "label": "Grafik", "value": "{gpu}", "split": ", " }
{ "label": "DNS-Server", "value": "{dns}", "split": ", |;" }   // mehrere Trennzeichen mit | getrennt
```

Leerraum um die Teile und leere Teile entfallen. Geht bei normalen Zeilen und bei `drives`. Im
Editor: Feld „Trennen bei“ unter „Value“; „Aktueller Wert“ zeigt die Teile mit ⏎ dazwischen.

### Pfad des Hintergrundbilds (`wallpaper.path`)

Leer bedeutet: das Wallpaper des jeweiligen Benutzers. Sonst ein Pfad, der auf allen Rechnern gilt –
ein UNC-Share, ein Pfad mit Umgebungsvariablen (`%ProgramData%\Firma\wallpaper.jpg`) oder ein
relativer Pfad. Relative Pfade wie `.\\background.jpg` gelten relativ zum Ordner der
Konfigurationsdatei (bei einer Konfiguration per URL: zum Ordner der exe), nicht zum
Arbeitsverzeichnis. So lassen sich `BGInfo-Reloaded.json` und Bilder gemeinsam in einen beliebigen Ordner
verteilen.

### Hintergrundbild an die Auflösung anpassen (`wallpaper.fit`)

Das Hintergrundbild wird vor dem Beschriften auf die Auflösung des jeweiligen Monitors gebracht.
Wie, bestimmt `fit` – die Werte entsprechen den Windows-Einstellungen:

| `fit` | Windows-Name | Wirkung |
|---|---|---|
| `Fill` (Standard) | Ausfüllen | füllt den Bildschirm, Seitenverhältnis bleibt, Überstand wird mittig abgeschnitten |
| `Fit` | Anpassen | ganzes Bild sichtbar, Seitenverhältnis bleibt, Ränder in `backgroundColor` |
| `Stretch` | Dehnen | füllt den Bildschirm, Bild wird verzerrt, nichts abgeschnitten |
| `Center` | Zentriert | unskaliert in der Mitte, Ränder in `backgroundColor` |
| `Tile` | Nebeneinander | unskaliert gekachelt |
| `None` | – | Bild in Originalgröße beschriften; Windows skaliert Bild und Panel gemeinsam |

Bei allen Werten außer `None` ist das Panel pixelgenau, und mit `"scaleWithDpi": true` wachsen
Schrift und Abstände mit der Windows-Skalierung des Monitors (bei 150 % um den Faktor 1,5). Hat ein
Firmen-Wallpaper ein anderes Seitenverhältnis als der Bildschirm, schneidet `Fill` Logo oder Text am
Rand ab – dann `Fit` mit passender `backgroundColor`, `Stretch` oder ein eigenes Bild je Auflösung
verwenden.

### Eigenes Bild je Auflösung (`wallpaper.resolutionVariants`)

Je Monitor wird zuerst eine Datei mit angehängter Auflösung gesucht, im selben Ordner und mit
derselben Endung. Das ist standardmäßig eingeschaltet (`"resolutionVariants": true`) und lässt sich
mit `false` abschalten:

```
wallpaper.jpg              Ausgangsbild (wallpaper.path bzw. das Wallpaper des Benutzers)
wallpaper_3440x1440.jpg    wird auf einem Monitor mit 3440 x 1440 verwendet
wallpaper_1920x1080.jpg    wird auf einem Monitor mit 1920 x 1080 verwendet
```

Erkannt werden auch `wallpaper-3440x1440.jpg` und `wallpaper3440x1440.jpg`. Gibt es für eine
Auflösung keine Datei, gilt das Ausgangsbild. Das Ausgangsbild selbst darf auch fehlen: Liegen nur
Dateien je Auflösung im Ordner (wie bei den mitgelieferten Demo-Hintergründen in `backgrounds/`),
wird für eine nicht vorhandene Auflösung das am besten passende Bild genommen – das mit dem
ähnlichsten Seitenverhältnis, möglichst nicht kleiner als der Monitor. Maßgeblich ist die echte Auflösung in Pixeln, nicht
die skalierte; welche das ist, zeigt `{resolution}` bzw. das Log (`Rendere Monitor …`). Welches Bild
verwendet wurde, steht ebenfalls im Log.

#### Eigene Zuordnung je Auflösung (`wallpaper.byResolution`)

Für Auflösungen, zu denen es im Ordner kein passendes Bild gibt, lässt sich ein beliebiges Bild
hinterlegen:

```jsonc
"wallpaper": {
  "path": ".\\backgrounds\\Wallpaper.jpg",
  "byResolution": {
    "3440x1440": ".\\backgrounds\\breit.jpg",
    "2560x1600": "\\\\fileserver.example.com\\share\\bginfo\\notebook.jpg"
  }
}
```

Reihenfolge je Monitor: eigene Zuordnung für genau diese Auflösung, dann die Datei mit dieser
Auflösung im Namen, dann das Bild ohne Zusatz, dann die am besten passende andere Fassung. Das gilt
bei jedem Lauf – auch wenn `--watch` nach einem Wechsel der Auflösung neu rendert. Fehlt die Datei
einer Zuordnung, gibt es eine Warnung im Log und es geht mit dem nächsten Schritt weiter.

Im Editor (Reiter „Hintergrund“, „Bilder je Auflösung“) zeigt eine Liste das Standardbild mit
seiner tatsächlichen Auflösung, die eigenen Zuordnungen,
die im Ordner gefundenen Fassungen und für jeden angeschlossenen Monitor, welches Bild er bekommt.
Zuordnungen lassen sich dort hinzufügen, ändern und entfernen. Wird ein Bild gewählt, das schon eine
Auflösung im Namen trägt (`Wallpaper_1920x1080.jpg`), trägt der Editor `Wallpaper.jpg` ein – unter
diesem Namen werden alle Fassungen gefunden; die Datei selbst muss es nicht geben.

### Mehrere Monitore (`monitors`)

| Einstellung | Wert | Wirkung |
|---|---|---|
| `mode` | `PerMonitor` (Standard) | je Monitor ein eigenes Bild in dessen Auflösung und Skalierung, inklusive eigenem Bild je Auflösung |
| | `Single` | ein Bild für den primären Monitor; Windows zeigt dasselbe Bild auf allen Monitoren |
| `panel` | `All` (Standard) | Info-Panel auf jedem Monitor |
| | `Primary` | Info-Panel nur einmal, auf dem primären Monitor; die übrigen bekommen nur den Hintergrund |

`panel` wirkt nur bei `PerMonitor` – bei `Single` erscheint das eine Bild samt Panel zwangsläufig auf
allen Monitoren. Steht Windows auf „Übergreifend“, wird bei `PerMonitor` auf „Ausfüllen“ umgestellt,
weil sich ein übergreifendes Bild nicht mit Bildern je Monitor verträgt. Ist die dafür nötige
Windows-Schnittstelle nicht verfügbar (z. B. Server Core), wird automatisch wie bei `Single`
gearbeitet; im Log steht dann eine Warnung. Mit `--preview bild.png` entsteht je Monitor eine Datei
(`bild.png` für den primären, `bild_m2.png` usw. für die übrigen).

Als Hintergrundbild funktionieren JPG, PNG, BMP, GIF und TIFF. WebP wird nicht gelesen – dann
erscheint das Panel auf der einfarbigen Fläche aus `backgroundColor`.

Ist die konfigurierte Schrift nicht installiert, wird automatisch auf Consolas, Segoe UI, Tahoma,
Arial oder Verdana ausgewichen. Backslashes müssen in JSON verdoppelt werden (`\\`).

### Rechner ein- oder ausschließen (`hosts`)

Mit einer zentralen Konfiguration lässt sich steuern, auf welchen Rechnern das Info-Panel erscheint.

| Einstellung | Wirkung |
|---|---|
| `include` | Whitelist. Ist sie nicht leer, erscheint das Panel nur auf Rechnern, auf die einer der Einträge passt. |
| `exclude` | Blacklist. Auf passenden Rechnern erscheint das Panel nicht. Hat Vorrang vor `include`. |
| `onExcluded` | Was auf ausgeschlossenen Rechnern passiert: `BackgroundOnly` (Standard) setzt den Hintergrund wie konfiguriert, nur ohne Panel – das entfernt auch ein früher gesetztes Panel. `Skip` lässt das Wallpaper völlig unangetastet. |

Ein Eintrag ist entweder ein Rechnername oder eine IP-Angabe; beides gilt in beiden Listen:

| Eintrag | Beispiel | Geprüft gegen |
|---|---|---|
| Name, auch mit `*` (beliebig viele Zeichen) und `?` (genau ein Zeichen) | `SRV-*`, `PC-0??`, `*.dmz.firma.local` | Rechnername und voll qualifizierter Name; Groß-/Kleinschreibung egal |
| einzelne IP-Adresse | `10.10.1.5` | alle IPv4-Adressen der aktiven Netzwerkadapter |
| Netz | `10.10.0.0/16` | ebenso |
| Bereich | `10.10.1.10-10.10.1.50`, kurz `10.10.1.10-50` | ebenso |

Hat ein Rechner mehrere Adressen, genügt ein Treffer. Unterstützt wird IPv4. Ein Namensmuster wird
zusätzlich gegen die Adressen als Text geprüft, `10.10.*` funktioniert also auch.

```jsonc
"hosts": { "exclude": ["KIOSK-*", "PC-0??"] }                    // überall außer auf diesen
"hosts": { "include": ["SRV-*"], "exclude": ["SRV-TEST*"] }       // nur Server, aber keine Testserver
"hosts": { "include": ["10.10.0.0/16"], "exclude": ["10.10.99.1-10.10.99.254"] }   // nach Netzen
```

**Import und Export:** Im Editor hat jede Liste die Knöpfe „Importieren…“ und „Exportieren…“. Der
Import liest eine Textdatei mit einem Eintrag je Zeile (auch durch Komma oder Semikolon getrennt);
Leerzeilen und Zeilen, die mit `#` oder `//` beginnen, werden übergangen, Doppelte entfernt. Hat die
Liste schon Einträge, fragt der Editor, ob angehängt oder ersetzt werden soll. Der Export schreibt
dieselbe Form. Die Einträge stehen danach in der `BGInfo-Reloaded.json` – eine später geänderte
Textdatei muss neu importiert werden. Doppelte Einträge (auch bei abweichender
Groß-/Kleinschreibung) werden beim Import, beim Verlassen des Eingabefelds und beim Speichern
entfernt.

**Liste in einer eigenen Datei:** Statt (oder zusätzlich zu) den Listen in der Konfiguration lässt
sich je Liste eine Textdatei angeben, die bei jedem Lauf frisch gelesen wird:

```jsonc
"hosts": {
  "exclude": ["KIOSK-*"],                                             // direkt in der Konfiguration
  "excludeFile": "\\\\fileserver.example.com\\share\\bginfo\\disallowed_hosts.txt",   // zusätzlich aus einer Datei
  "includeFile": ".\\allowed_hosts.txt"                               // relativ zur Konfigurationsdatei
}
```

- Die Datei hat dieselbe Form wie beim Import: ein Eintrag je Zeile, Namen oder IP-Angaben,
  Kommentarzeilen mit `#` oder `//`.
- Es gilt die Summe aus Liste und Datei. Pfad, UNC-Share oder http(s)-URL sind möglich.
- Ist die Datei nicht erreichbar, gilt die zuletzt gelesene Fassung aus einem lokalen Cache
  (`%LOCALAPPDATA%\BGInfo-Reloaded\hosts.….cache.txt`); gibt es noch keinen, fehlen ihre Einträge,
  und im Log steht eine Warnung.
- Im Log steht bei jedem Lauf, welche Datei mit wie vielen Einträgen gelesen wurde.

Wann welcher Weg passt: Eine eigene Datei lohnt sich, wenn die Liste oft geändert wird oder von
anderen Personen gepflegt wird als die Konfiguration – eine Änderung wirkt dann beim nächsten Lauf
auf allen Rechnern, ohne die `BGInfo-Reloaded.json` neu zu verteilen. Wenige, selten geänderte
Einträge sind in der Konfiguration selbst besser aufgehoben; sie funktionieren auch ohne Netz.

Ob und warum ein Rechner ausgeschlossen ist, steht in der Ausgabe und im Log
(`Info-Panel auf diesem Rechner abgeschaltet: …`). Bei `Skip` bleibt ein früher gesetztes Panel
stehen, bis das Wallpaper anderweitig geändert wird.

### Zeilen (`items`)

| Eintrag | Ergebnis |
|---|---|
| `{ "section": "TEXT" }` | Überschrift |
| `{ "spacer": true }` | Leerraum |
| `{ "label": "…", "value": "…" }` | normale Zeile |
| `{ "label": "…", "value": "…", "hideIfEmpty": true }` | Zeile entfällt, wenn der Wert leer oder `n/a` ist |
| `{ "drives": true }` | eine Zeile je Festplatte |
| `{ "drives": true, "label": "…", "value": "…" }` | Laufwerkszeilen mit eigenem Text |
| `{ "includeItems": true }` | die Zeilen der per `include` eingebundenen zentralen Datei |
| `{ "block": "Name", "when": "…" }` … `{ "endBlock": true }` | Block: der Inhalt dazwischen erscheint nur, wenn der Filter des Blockanfangs zutrifft |

Jeder Eintrag kann zusätzlich Filter tragen (`onlyOn`, `notOn`, `when`) – siehe „Zeilen filtern“.

In Laufwerkszeilen gibt es zusätzlich `{drive}` (z. B. `C:`), `{volume}` (Datenträgername),
`{used}`, `{total}`, `{free}` (jeweils GB) und `{percent}` (belegt in Prozent).

### Platzhalter

Platzhalter funktionieren in `section`, `label` und `value` und lassen sich mit freiem Text mischen,
z. B. `"{os} {displayversion} (Build {build})"`.

Die vollständige, immer aktuelle Liste mit den Werten des jeweiligen Rechners liefert:

```
BGInfo-Reloaded.exe --list-tags
BGInfo-Reloaded.exe --list-tags wmi:Win32_ComputerSystem
BGInfo-Reloaded.exe --list-tags "reg:HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
```

Ohne Angabe werden die eingebauten Platzhalter, die Laufwerks-Platzhalter, die generischen Quellen
und alle Umgebungsvariablen ausgegeben. Mit `wmi:<Klasse>` bzw. `reg:<Schlüssel>` erscheint jede
Eigenschaft bzw. jeder Wert als fertiger Platzhalter zum Kopieren in die `BGInfo-Reloaded.json`. Die Ausgabe
enthält alle Umgebungsvariablen im Klartext – nicht ungeprüft weitergeben, falls dort Zugangsdaten
stehen.

**Eingebaut**

| Platzhalter | Inhalt |
|---|---|
| `{hostname}`, `{fqdn}` | Rechnername, voll qualifizierter Name |
| `{user}`, `{username}`, `{domain}` | `DOMÄNE\Benutzer`, nur Benutzer, nur Domäne |
| `{machinedomain}`, `{logonserver}` | Domäne bzw. Arbeitsgruppe des Rechners, Anmeldeserver |
| `{systemtype}` | Rolle des Rechners: Workstation, Server oder Domänencontroller |
| `{ipv4}`, `{ipv6}` | erste IPv4- bzw. IPv6-Adresse eines aktiven Netzwerkadapters |
| `{subnet}` | Subnetzmaske zur IPv4-Adresse (z. B. `255.255.255.0`) |
| `{mac}`, `{gateway}`, `{dns}`, `{dhcp}` | MAC-Adresse, Standardgateway, DNS-Server, DHCP-Server |
| `{nic}`, `{netspeed}`, `{nettype}` | Name, Geschwindigkeit und Art (Ethernet/WLAN) des aktiven Netzwerkadapters |
| `{os}`, `{osversion}`, `{displayversion}`, `{build}`, `{arch}` | Windows-Name, Version, Release (z. B. `24H2`), Build, Architektur |
| `{boot}`, `{uptime}` | letzter Systemstart, Laufzeit seitdem |
| `{timezone}` | Zeitzone |
| `{resolution}`, `{scaling}` | Auflösung und Windows-Skalierung des primären Monitors |
| `{cpu}`, `{cores}` | Prozessorname, logische Kerne |
| `{ram}`, `{ramfree}`, `{ramspeed}` | Arbeitsspeicher, davon frei, Takt |
| `{bios}`, `{serial}` | BIOS-Version, Seriennummer |
| `{manufacturer}`, `{model}` | Hersteller, Modell |
| `{allips}` | alle IPv4-Adressen der aktiven Adapter |
| `{adsite}`, `{ou}` | Active-Directory-Standort und OU-Pfad des Rechners (z. B. `Berlin/Clients`) |
| `{session}`, `{rdpclient}` | Art der Sitzung (Konsole/RDP), Name des RDP-Clients |
| `{language}`, `{installdate}` | Anzeigesprache und Installationsdatum von Windows |
| `{lastupdate}`, `{pendingreboot}` | zuletzt installiertes Windows-Update mit KB-Nummer; steht ein Neustart aus (`ja`/`nein`) |
| `{secureboot}`, `{antivirus}` | Secure Boot (`aktiv`/`aus`), registrierter Virenschutz (nur Client-Windows) |
| `{virtual}`, `{chassis}` | virtuelle Maschine (`ja (VMware)`/`nein`), Bauform (Desktop, Notebook, Server …) |
| `{assettag}`, `{uuid}`, `{biosdate}` | Asset-Tag, UUID und BIOS-Datum |
| `{gpu}`, `{disk}` | Grafikkarte, physische Datenträger mit Größe |
| `{rampercent}`, `{battery}` | belegter Arbeitsspeicher und Akkustand in Prozent |
| `{monitors}` | Anzahl und Auflösung aller Monitore |
| `{now}` | aktuelles Datum und Uhrzeit |
| `{appversion}` | Version von BGInfo-Reloaded |
| `{configversion}` | Version der Konfigurationsdatei – der Wert von `"version"` ganz oben in der Datei |

**Generisch – zum Erweitern ohne Neubau**

| Platzhalter | Beispiel |
|---|---|
| `{env:NAME}` | `{env:LOGONSERVER}` |
| `{reg:HIVE\Pfad\Wertname}` | `{reg:HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\RegisteredOwner}` |
| `{wmi:Klasse.Eigenschaft}` | `{wmi:Win32_TimeZone.Caption}` |
| `{wmi:Klasse.Eigenschaft where Bedingung}` | `{wmi:Win32_NetworkAdapterConfiguration.DNSServerSearchOrder where IPEnabled=true}` |
| `{wmi:SELECT … FROM …}` | `{wmi:SELECT Caption FROM Win32_OperatingSystem}` – ganze WQL-Abfrage wie im Feld WMI Query von BGInfo; es zählt die erste Eigenschaft mit Wert |
| `{filever:Pfad}` | `{filever:%WINDIR%\\explorer.exe}` – Dateiversion |
| `{filetime:Pfad}` | `{filetime:%ProgramData%\\Firma\\asset.txt}` – Änderungsdatum der Datei |
| `{file:Pfad}` | `{file:%ProgramData%\\Firma\\asset.txt}` – erste nicht-leere Zeile der Datei |
| `{now:Format}` | `{now:dd.MM.yyyy HH:mm}` |

**Kürzen** – funktioniert mit jedem Platzhalter, auch mit eigenen Feldern:

| Schreibweise | Ergebnis |
|---|---|
| `{cpu,20}` | höchstens die ersten 20 Zeichen |
| `{cpu,20...}` | höchstens 20 Zeichen, bei Kürzung mit `…` am Ende |
| `{serial,-8}` | nur die letzten 8 Zeichen (`{serial,-8...}` mit `…` vorn) |

Kürzere Werte und `n/a` bleiben unverändert.

Hinweise:

- Registry-Hives: `HKLM`, `HKCU`, `HKCR`, `HKU`. Das letzte Segment ist der Wertname.
- Liefert WMI mehrere Werte, werden sie mit Komma verbunden; bei mehreren Treffern zählt der erste.
- Nicht ermittelbare Werte ergeben `n/a`, der Lauf bricht nicht ab.
- Unbekannte Platzhalter bleiben wörtlich stehen – so fallen Tippfehler auf dem Wallpaper auf.

### Eigene Felder (`fields`)

Eine WMI-Abfrage (oder jede andere Quelle) steht normalerweise direkt im `value` einer Zeile. Soll
sie mehrfach benutzt werden, bekommt sie unter `fields` einen Namen und ist dann überall als
`{name}` verfügbar – das Gegenstück zu den „Custom“-Feldern von BGInfo:

```json
"fields": {
  "modell": "{wmi:SELECT Model FROM Win32_ComputerSystem}",
  "betriebssystem": "{os} {displayversion} (Build {build})"
},
"items": [
  { "label": "Modell", "value": "{modell}" },
  { "label": "System", "value": "{betriebssystem}" }
]
```

- Eine Definition darf Platzhalter, andere eigene Felder und freien Text enthalten.
- Eigene Felder haben Vorrang vor eingebauten Namen. Innerhalb der eigenen Definition meint
  derselbe Name wieder den eingebauten Wert: `"ipv4": "{ipv4} / {subnet}"` erweitert `{ipv4}`.
- `BGInfo-Reloaded.exe --list-tags [--config datei]` zeigt die eigenen Felder mit ihren Werten.

### Zentrale Datei einbinden (`include`)

```json
"include": "\\\\fileserver.example.com\\share\\BGInfo-Reloaded.central.json"
```

Die genannte Datei (lokaler Pfad, UNC oder http(s)-URL; relativ zum Ordner der Konfigurationsdatei)
liefert gemeinsam genutzte `fields` und `items` – Vorlage: `BGInfo-Reloaded.central.sample.json`.

- Ihre **Felder** gelten zusätzlich zu den eigenen; bei gleichem Namen gewinnt die eigene Definition.
- Ihre **Zeilen** erscheinen dort, wo in den eigenen `items` der Eintrag `{ "includeItems": true }`
  steht (auch mehrfach oder gar nicht – dann werden nur die Felder genutzt).
- Alles andere (Aussehen, Hintergrund, `hosts`) bleibt Sache der einbindenden Datei. Soll die ganze
  Konfiguration zentral liegen, weiterhin `--config` verwenden (siehe „Zentrale Verteilung“).
- Die Datei wird bei jedem Lauf gelesen und nach
  `%LOCALAPPDATA%\BGInfo-Reloaded\include.cache.json` zwischengespeichert. Ist sie nicht erreichbar,
  gilt der Cache; das Log nennt, was verwendet wurde.

### Zeilen filtern (`onlyOn`, `notOn`, `when`)

Jeder Eintrag in `items` – auch Überschrift, Leerraum, Laufwerke und `includeItems` – kann Filter
tragen. Sind mehrere angegeben, müssen alle zutreffen.

| Filter | Bedeutung |
|---|---|
| `"onlyOn": ["SRV-*", "*-DC??"]` | nur auf Rechnern, deren Name (oder FQDN) auf eines der Muster passt |
| `"notOn": ["KIOSK-*"]` | nicht auf passenden Rechnern |
| `"when": "{systemtype} = Server"` | nur wenn der Vergleich zutrifft; rechts sind `*` und `?` erlaubt, Groß-/Kleinschreibung ist egal |
| `"when": "{env:STANDORT} != Berlin"` | nur wenn der Wert nicht passt |
| `"when": "{env:STANDORT}"` | ohne Vergleich: nur wenn der Text einen Wert liefert (nicht leer, nicht `n/a`) |

**Blöcke** fassen mehrere Einträge unter einer Bedingung zusammen – z. B. Zeilen, die nur auf echter
Hardware sinnvoll sind:

```json
{ "block": "Blech", "when": "{virtual} = nein" },
  { "label": "Bauform", "value": "{chassis}" },
  { "label": "BIOS-Datum", "value": "{biosdate}" },
{ "endBlock": true },
{ "block": "Nur VMs", "when": "{virtual} = ja*" },
  { "label": "Hypervisor", "value": "{virtual}" },
{ "endBlock": true }
```

Der Text hinter `"block"` ist nur ein Name zur Orientierung. Am Blockanfang gelten dieselben Filter
wie an jeder Zeile (`onlyOn`, `notOn`, `when`); Blöcke dürfen verschachtelt werden, und Zeilen im
Block können zusätzlich eigene Filter tragen.

So kann eine einzige zentrale Datei Zeilen enthalten, die nur auf Servern, nur an einem Standort
oder nur auf bestimmten Rechnern erscheinen. `hosts.include`/`hosts.exclude` schalten dagegen das
ganze Panel ab.

### Konfiguration testen

```
BGInfo-Reloaded.exe --config meine.json --preview vorschau.png
```

Gibt alle Zeilen mit ihren aufgelösten Werten aus und speichert das Bild, ohne das Wallpaper zu
ändern. Eine fehlerhafte Datei (JSON-Syntax, unbekannte Position, ungültige Farbe) führt zu einer
klaren Fehlermeldung und Exit-Code `1`.

## Zentrale Verteilung

Es gibt zwei Wege, die sich auch kombinieren lassen.

**Datei mitverteilen** – `BGInfo-Reloaded.json` per Softwareverteilung/GPO neben die exe oder nach
`%ProgramData%\BGInfo-Reloaded\` kopieren. Funktioniert ohne Netzverbindung; Änderungen müssen neu
verteilt werden.

**Zentrale Quelle referenzieren** – die Datei liegt auf einem Share oder Webserver, Änderungen
wirken beim nächsten Lauf auf allen Rechnern:

```
BGInfo-Reloaded.exe --register-task --config \\fileserver.example.com\share\BGInfo-Reloaded.json
BGInfo-Reloaded.exe --register-task --config https://intranet.example/BGInfo-Reloaded.json
```

Alternativ die Umgebungsvariable `BGINFORELOADED_CONFIG` setzen (z. B. per GPO), dann braucht der
Aufruf kein `--config`.

Eine zentrale Quelle wird nach jedem erfolgreichen Laden nach
`%LOCALAPPDATA%\BGInfo-Reloaded\config.cache.json` kopiert. Ist sie nicht erreichbar (Notebook
unterwegs, Server down), wird dieser Cache benutzt; gibt es noch keinen, gelten die Standardwerte.
Der Lauf schlägt in beiden Fällen nicht fehl, im Log steht eine Warnung.

## Autostart

```
BGInfo-Reloaded.exe --register-task
```

legt zwei Scheduled Tasks für den aktuell angemeldeten Benutzer an:

| Task | Auslöser |
|---|---|
| `BGInfo-Reloaded - Logon` | bei jeder Anmeldung |
| `BGInfo-Reloaded - Refresh` | alle 30 Minuten (bzw. `--interval-minutes`) |

Die Tasks laufen auch im Akkubetrieb und haben kein Zeitlimit.

### Auf Auflösungswechsel reagieren

```
BGInfo-Reloaded.exe --register-task --watch
```

Der Logon-Task startet die exe dann mit `--watch`: Sie rendert bei der Anmeldung und bleibt bis zur
Abmeldung unsichtbar im Hintergrund aktiv. Ändert sich Auflösung, Skalierung, Anzahl oder Anordnung
der Monitore (Docking, RDP-Sitzung mit anderer Fenstergröße, Monitorwechsel), wird nach etwa
zwei Sekunden neu gerendert; eine reine Skalierungsänderung wird spätestens nach 15 Sekunden
erkannt. Je Sitzung läuft höchstens eine solche Instanz, der Refresh-Task arbeitet parallel weiter.
Bei `"fit": "None"` bringt das nichts, weil das Ergebnis dann nicht von der Auflösung abhängt. Zum Ausprobieren ohne Task genügt `BGInfo-Reloaded.exe --watch`; beendet wird die Instanz über den
Task-Manager oder durch Abmelden.

- Admin-Rechte sind nicht nötig, es wird kein Passwort gespeichert.
- Die Tasks laufen bewusst im Benutzerkontext, nicht als SYSTEM – SYSTEM hat keinen sichtbaren
  Desktop und kann kein Wallpaper setzen. Der Aufruf muss daher je Benutzer erfolgen (z. B. per
  Anmeldeskript).
- Die Tasks verweisen auf den Pfad, an dem die exe beim Registrieren lag. Wird sie verschoben,
  `--register-task` erneut ausführen.
- Erneutes Registrieren überschreibt die bestehenden Tasks, z. B. um Intervall oder `--config` zu
  ändern.

## Logdatei und Fehlersuche

Jeder Lauf (außer `--help`, `--version` und `--list-tags`) wird nach `%LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log`
protokolliert – im Task-Betrieb ist das die einzige Fehleranzeige.

```
2026-10-01 15:23:00 INFO   --- Start FIRMA\benutzer: --config \\fileserver.example.com\share\BGInfo-Reloaded.json
2026-10-01 15:23:00 INFO   Konfiguration: \\fileserver.example.com\share\BGInfo-Reloaded.json
2026-10-01 15:23:00 FEHLER Fehler: Konfiguration '…' ist kein gueltiges JSON: …
2026-10-01 15:23:00 FEHLER --- Ende, Exit-Code 1
```

Ab 256 KB wird die Datei zu `bginfo.log.old`; es bleibt genau eine Vorgängerdatei.

| Symptom | Ursache / Abhilfe |
|---|---|
| Wallpaper ändert sich nicht | Log prüfen; steht dort `FEHLER`, ist meist die Konfiguration ungültig |
| `{irgendwas}` steht wörtlich auf dem Wallpaper | Platzhalter falsch geschrieben |
| Wert ist `n/a` | Quelle liefert nichts (WMI-Klasse/-Eigenschaft, Registry-Pfad oder Datei prüfen) – mit `--preview` testen |
| Alte Konfiguration wird verwendet | zentrale Quelle nicht erreichbar, es greift der Cache (Warnung im Log) |
| Anderes Hintergrundbild gewünscht | `wallpaper.path` setzen, oder `original.txt` (siehe unten) löschen und das Wallpaper in Windows neu wählen |

### Dateien unter `%LOCALAPPDATA%\BGInfo-Reloaded\`

| Datei | Inhalt |
|---|---|
| `wallpaper_bginfo_m1_….jpg` usw. | die erzeugten Wallpaper je Monitor (Name wechselt bei jedem Lauf, alte werden entfernt); bei `Single` heißt die Datei `wallpaper_bginfo.jpg` |
| `original.txt` | Pfad des unveränderten Original-Wallpapers |
| `original_copy.jpg` | gesicherte Kopie des Originals, falls Windows nur eine eigene Arbeitskopie kannte |
| `config.cache.json` | letzte erfolgreich geladene zentrale Konfiguration |
| `include.cache.json` | letzte erfolgreich geladene zentrale `include`-Datei |
| `bginfo.log`, `bginfo.log.old` | Protokoll |
| `update.json` | gemerktes Repository für `--update` |

Zum vollständigen Zurücksetzen `--unregister-task` ausführen, diesen Ordner löschen und das
Wallpaper in den Windows-Einstellungen neu wählen.

## Aktualisieren

**Aus einem Ordner** (lokal oder UNC-Share – für Rechner ohne Zugriff auf GitHub):

```jsonc
"update": {
  "path": "\\\\fileserver.example.com\\share\\BGInfo-Reloaded",   // Ordner mit den aktuellen Dateien
  "auto": true,                                   // bei jedem Lauf prüfen und selbst aktualisieren
  "program": true,                                // BGInfo-Reloaded.exe (Standard: an)
  "editor": true,                                 // BGInfo-Reloaded-Editor.exe, wo vorhanden (Standard: an)
  "config": false,                                // BGInfo-Reloaded.json (Standard: aus)
  "backgrounds": false                            // background*.* und Unterordner backgrounds\ (Standard: aus)
}
```

Was aus dem Ordner geholt wird:

| Schalter | Dateien | Wann | Standard |
|---|---|---|---|
| `program` | `BGInfo-Reloaded.exe` | wenn die im Ordner eine höhere Version hat | an |
| `editor` | `BGInfo-Reloaded-Editor.exe` | wenn höhere Version – und nur dort, wo der Editor schon neben der exe liegt | an |
| `config` | `BGInfo-Reloaded.json` | wenn die im Ordner neuer ist (Änderungsdatum); die vorherige bleibt als `.bak` | aus |
| `backgrounds` | `background*.*` und alle Dateien im Unterordner `backgrounds\` | was neuer ist oder lokal fehlt | aus |

Alle Dateien landen neben der exe (die aus `backgrounds\` im gleichnamigen Unterordner). Lokal
neuere Dateien werden nicht überschrieben, und nichts wird gelöscht. Eine geholte Konfiguration gilt
ab dem nächsten Lauf – die Datei im Ordner sollte denselben `update`-Abschnitt enthalten, sonst
endet der Abgleich mit ihr. Im Editor stehen die Schalter im Reiter „Autostart und Werkzeuge“; die
betroffenen Dateien zeigt jedes Kästchen als Tooltip.

```
BGInfo-Reloaded.exe --update                    aus dem Ordner in "update.path" aktualisieren
BGInfo-Reloaded.exe --update --check-only       nur prüfen
BGInfo-Reloaded.exe --update --from <ordner>    einmalig aus einem anderen Ordner
```

Liegt im Ordner eine `BGInfo-Reloaded.exe` mit höherer Dateiversion als die laufende, wird sie
neben die eigene exe kopiert und dann getauscht; aktiv wird sie beim nächsten Start. Die vorherige
Fassung bleibt bis dahin als `BGInfo-Reloaded.exe.old` liegen. Eine ältere oder gleiche Version im
Ordner wird nie eingespielt. Liegt neben der eigenen exe auch der Editor, wird er aus demselben
Ordner mit aktualisiert.

- Mit `"auto": true` prüft jeder normale Lauf (also auch der Autostart-Task) den Ordner von selbst.
  Ist nichts zu tun, steht dazu nichts im Log. Ist der Ordner nicht erreichbar oder fehlen
  Schreibrechte im Programmordner, gibt es eine Warnung; der Lauf selbst geht weiter.
- Mit `"auto": false` (Standard) geschieht das nur auf Aufruf von `--update`.
- Ein relativer Pfad gilt relativ zum Ordner der Konfigurationsdatei. URLs werden hier nicht
  unterstützt.
- Zum Verteilen einer neuen Version genügt es, die neuen Dateien in den Ordner zu legen.
- **Sicherheit:** Wer in diesen Ordner schreiben darf, bestimmt, welches Programm auf den Rechnern
  läuft. Schreibrechte dort auf die Admins beschränken; die Benutzer brauchen nur Leserechte. Der
  Programmordner auf den Rechnern muss für den Benutzer beschreibbar sein, sonst kann sich die exe
  nicht selbst ersetzen.

**Aus GitHub-Releases** (kein SDK auf dem Zielrechner nötig):

```
BGInfo-Reloaded.exe --update --repo Kilowatt-W/bginfo-reloaded
BGInfo-Reloaded.exe --update                    (Repository ist nach dem ersten Aufruf gemerkt)
BGInfo-Reloaded.exe --update --check-only
```

Vergleicht den Tag des neuesten Releases (z. B. `v1.3.0`) mit der eigenen Version und lädt das
Release-Asset, das exakt `BGInfo-Reloaded.exe` heißt. Aktiv wird die neue Version beim nächsten Start.

Das Repository lässt sich statt per `--repo` auch zentral in der Konfiguration eintragen:

```jsonc
"update": { "repo": "Kilowatt-W/bginfo-reloaded" }
```

Es wird nur auf Aufruf von `--update` benutzt, nie automatisch (GitHub begrenzt anonyme Abfragen je
Internet-Anschluss), und geholt wird ausschließlich `BGInfo-Reloaded.exe`. Ein mit `--repo`
angegebenes Repository merkt sich das Programm weiterhin je Benutzer in
`%LOCALAPPDATA%\BGInfo-Reloaded\update.json`. Ein privates Repository ist ohne Anmeldung nicht
erreichbar.

**Aus dem Quellcode** (braucht `git.exe` und das .NET SDK auf dem Rechner):

```
BGInfo-Reloaded.exe --update-source https://github.com/Kilowatt-W/bginfo-reloaded.git --branch main
```

Ohne `--repo` und ohne `--from` gilt: zuerst der Ordner aus `update.path`, sonst `update.repo`,
sonst das gemerkte GitHub-Repository. Die beiden Wege über GitHub und den Quellcode sind bisher nicht gegen ein echtes
Repository getestet; das Update aus einem Ordner ist getestet.

## Selbst bauen

```powershell
.\Build.ps1
```

Braucht das [.NET SDK](https://dotnet.microsoft.com/download) (8 oder neuer) und legt
`BGInfo-Reloaded.exe` und `BGInfo-Reloaded-Editor.exe` in den Ordner `publish_tmp`. Neue eingebaute Platzhalter werden in `src/ValueResolver.cs`
ergänzt, die Versionsnummer steht in `src/BGInfo-Reloaded.csproj`.
