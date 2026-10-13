# BGInfo-Reloaded – Howto

[Deutsch](HOWTO.md) | **English**

`BGInfo-Reloaded.exe` writes system information (host name, IP, operating system, hardware, drives, …)
as a panel onto the desktop wallpaper – an independent replacement for Sysinternals BGInfo. Operation
needs exactly two files:

| File | Purpose |
|---|---|
| `BGInfo-Reloaded.exe` | the program (one file, includes the .NET runtime, no installation required) |
| `BGInfo-Reloaded.json` | optional: what is shown and what it looks like |

In addition there is `BGInfo-Reloaded-Editor.exe` for admins, a graphical editor for
`BGInfo-Reloaded.json`. It is not distributed.

Technical background, license notes and the comparison with PowerBGInfo are in
[README.en.md](README.en.md).

> **Language:** The program itself currently speaks German – console output, log messages, the
> editor's user interface and a few values (`ja`/`nein`, `aktiv`/`aus`) are German. Command-line
> options, configuration keys and placeholders are English. Where this guide quotes program output
> or editor labels, the German original is given.

## Features at a glance

- **One exe, no dependencies** – no PowerShell, no installer, no .NET installation on the target
  machine.
- **Configuration through JSON** – content, position, colours and font of the panel, without
  rebuilding the exe.
- **Extensible through placeholders** – show arbitrary values from WMI, the registry, environment
  variables or files.
- **Central distribution** – configuration from a UNC share or an http(s) URL, with a local cache
  for machines without a network connection.
- **Whitelist/blacklist** – include or exclude the panel on machines by computer name pattern
  (`*`, `?`).
- **Autostart** – scheduled tasks at logon and at a fixed interval, without admin rights.
- **Windowless** – no console window appears when run as a task.
- **Log file** – every run is logged, errors can be traced.
- **The original wallpaper is preserved** – every run renders from the unmodified original; text is
  never stacked on text.
- **Pixel-exact for the screen** – the background image is brought to the current resolution (fill,
  fit, stretch, center, tile), the panel follows the Windows scaling.
- **Relative panel width** – set the panel to 1/3 to 1/8 of the screen width; the content is scaled
  to match and looks the same size on every monitor.
- **A separate background image per resolution** – e.g. `wallpaper_3440x1440.jpg`, otherwise the
  default image.
- **Multiple monitors** – one image per monitor in its resolution; the panel either on all monitors
  or only once on the primary one.
- **Reacts to resolution changes** – in watch mode (`--watch`) the wallpaper is re-rendered
  automatically when resolution or scaling changes.
- **Placeholder list** – `--list-tags` shows all available placeholders with their current value.
- **Preview** – render the result into an image file without touching the wallpaper.
- **Self-update** – from GitHub releases or directly from the Git source code.

## Graphical editor (`BGInfo-Reloaded-Editor.exe`)

`BGInfo-Reloaded.json` can be edited by hand or with the editor. The editor is a separate program
for admins and is **not** distributed to the target machines – only `BGInfo-Reloaded.exe` and
`BGInfo-Reloaded.json` run there.

```
BGInfo-Reloaded-Editor.exe                        open the BGInfo-Reloaded.json from the start directory (otherwise ask)
BGInfo-Reloaded-Editor.exe BGInfo-Reloaded.json   open an existing file
```

The tabs and buttons (German labels in parentheses):

- **Content (Inhalt):** add, remove and move the lines of the panel (heading, line, spacer,
  drives). Next to it is the list of all placeholders with their value on this machine; a
  double-click inserts the placeholder at the cursor.
  Below the list, the selected line shows “Bezeichnung” (label), “Value” (the text with
  placeholders as stored in the file) and “Aktueller Wert” (current value) – the result on this
  machine, display only. It also says when a line does not appear here at all (filter does not
  match, empty value with “omit”, hidden block). The table of custom fields on the tab “Datei und
  Felder” has the same column “Aktueller Wert”.
- **Appearance (Aussehen):** position, margins, width of the panel (by content, fixed, or 1/3 to 1/8
  of the screen width), split between label and value column (slider), distance measured from the
  taskbar, rounded corners, shadow, colours, opacity, font.
- **Background (Hintergrund):** image file, fit to the resolution, image per resolution, behaviour
  with multiple monitors.
- **Machines (Rechner):** whitelist and blacklist.
- **File and fields (Datei und Felder):** version and description of the file, central file
  (`include`), custom fields.
- **Autostart and tools (Autostart und Werkzeuge):** set up and remove the autostart tasks
  (interval, `--watch`), save the preview as an image file, open the log file and the data folder.
- **Title bar:** next to the file name, the detected resolution and Windows scaling of the primary
  monitor – the same values `BGInfo-Reloaded.exe` renders with. Above the preview they are shown for
  every monitor. If they change while the editor is open, display and preview are updated.
- **Status bar:** messages on the left; on the right the name of the loaded file (full path when
  the mouse points at it) and the program version.
- **Preview (Vorschau):** the whole screen at the top right, below it the panel in original size.
  Rendering uses the same code as `BGInfo-Reloaded.exe`, for the primary monitor of this machine.
- **Using placeholders (tab “Inhalt”):** a double-click on a placeholder inserts into the selected
  line, depending on where the caret is: in “Value” the placeholder (and, if the label is still
  empty, its meaning as the label), in “Bezeichnung” (label) the meaning (and, if “Value” is still
  empty, the placeholder). A new line from “+ Zeile” is thus filled completely with one
  double-click. For headings, blocks and “Nur wenn” only the placeholder is inserted; if the field
  is locked for the selected line (e.g. a spacer), a hint appears. Dragging onto the list of lines creates a new line where it is dropped (label = meaning,
  value = placeholder); dragging onto a text field inserts it there.
- **100 % (toggle in the toolbar):** draws the panel as it would look without Windows scaling. It
  switches off both “Größe mit der Windows-Skalierung vergrößern” (`panel.scaleWithDpi`) and
  “Relative Breite” (`panel.screenFraction`); switching it off restores the previous values. The
  background image itself is always rendered at the real pixel resolution of the monitor anyway.
- **Excluded machine:** if the machine the editor runs on is on the blacklist (or not on the
  whitelist), the preview still shows the panel. To the right of “Vorschau – ganzer Bildschirm” an
  orange note then says that `BGInfo-Reloaded.exe` only sets the background here or changes nothing
  (full text when the mouse points at it).
- **Searching placeholders:** the field “Suchen” below “WMI-Abfrage…” filters the placeholder list
  while typing, across placeholder, meaning and value; several words must all occur. Esc or ✕ clears it.
- **Preview (Vorschau, toggle in the toolbar):** shows or hides the preview area on the right; while
  hidden nothing is rendered. The choice is remembered per user.
- **Update on start:** if the loaded configuration names an update folder (`update.path`), the editor
  checks it on start. With `update.auto` it fetches newer files like `BGInfo-Reloaded.exe` does
  (including itself – active from the next start – and a newer configuration, which it then
  reloads); without it, it only reports what is available. A message appears only if there is
  something to say (fetched, available, folder not reachable); after that the editor continues normally.
  If the editor itself was updated, the message offers “Jetzt neu starten” (restart now): the editor
  closes and starts again with the same file in the new version.
- **Show preview (Vorschau anzeigen):** shows the current result (unsaved changes included) full
  screen in the foreground – without frame, taskbar and desktop icons, on the screen the editor is
  on and rendered for its resolution. Esc, any key or a click closes it; the wallpaper stays untouched.
- **Apply now (Jetzt anwenden):** saves and runs the `BGInfo-Reloaded.exe` in the same folder with
  this file – the wallpaper is really set.
- **Apply and exit (Anwenden und beenden):** like “Jetzt anwenden”, then the editor closes. If the
  exe reports an error, it stays open and shows the message.

Please note:

- On every save the editor writes a fixed header and an explanatory default comment after every
  setting (so does `--export-config`). **Your own** comments in a hand-maintained file are lost on
  saving; the editor asks first. For your own remarks there is the field `"description"` – it is
  preserved.
- The editor writes out all settings, including those with their default value.
- If the selected background image is in the same folder as the configuration file, it is entered as
  a relative path (`.\image.jpg`).
- On saving, the editor checks whether there is a background image in a matching resolution for the
  monitors of this machine. If it is missing and the selected fit would cut off part of the
  substitute image (`None`, `Fill`, `Center`, `Tile` with a different aspect ratio), it names the
  image it looked for and the one that is used, and offers to switch to `Stretch` or `Fit` – then
  the whole image is visible. The check only covers this machine; on target machines with a
  different resolution the same note appears in the log.
- With `"fit": "None"` the preview shows the unscaled image. On the desktop, Windows then scales it
  itself and, depending on its own setting, cuts off part of it – including the panel. The status
  bar warns in this case.

## Quick start

```
BGInfo-Reloaded.exe --preview preview.png      1. see what it would look like
BGInfo-Reloaded.exe --export-config            2. create BGInfo-Reloaded.json as a template and adapt it
BGInfo-Reloaded.exe                            3. set the wallpaper now
BGInfo-Reloaded.exe --register-task            4. update automatically from now on
```

To try the demo backgrounds from the repository (folder `backgrounds/`):

```
BGInfo-Reloaded.exe --config BGInfo-Reloaded.sample.json --preview preview.png
```

## All commands

| Command | Effect |
|---|---|
| `BGInfo-Reloaded.exe` | update the wallpaper once |
| `--config <path\|url>` | load the configuration from a file, a UNC share or an http(s) URL |
| `--preview <image.png>` | only render into an image file and print the lines; the wallpaper stays unchanged |
| `--export-config [path]` | write the default configuration as a template (default: `BGInfo-Reloaded.json` in the current folder; overwrites nothing) |
| `--export-config <path> --config <path>` | rewrite an existing configuration in the current format (all options, default comments); source and target may be the same file, the old version is kept as `.bak` |
| `--export-config … --with-placeholders` | additionally write all placeholders of this program version into the file as a comment (this is how the sample configuration attached to every release is produced) |
| `--register-task` | set up autostart: at logon + every 30 minutes |
| `--register-task --interval-minutes <n>` | custom interval; `0` = at logon only |
| `--register-task --config <path\|url>` | autostart with a fixed configuration source |
| `--register-task --watch` | autostart that additionally reacts to resolution changes |
| `--unregister-task` | remove autostart again |
| `--watch` | render and stay active; re-render when resolution or scaling changes |
| `--list-tags` | print all placeholders with description and current value |
| `--list-tags wmi:<class>` | all properties of a WMI class as ready-to-use placeholders |
| `--list-tags reg:<key>` | all values of a registry key as ready-to-use placeholders |
| `--update --repo <org/repo>` | install a newer GitHub release |
| `--update` | without `--repo`: update from the folder in `update.path` of the configuration, otherwise from the remembered GitHub repository |
| `--update --from <folder>` | update once from this folder |
| `--update --check-only` | only check whether a newer version exists |
| `--update-source <git-url> [--branch main]` | fetch the source code and rebuild locally |
| `--version` | show the version |
| `--help` | show help |

`--config` can be combined with the normal run, `--preview`, `--watch` and `--register-task`.

### Behaviour in the console

The exe is windowless. Started from a console it writes its output there, but the shell does not
wait for the program to end – the prompt returns immediately and the output appears after it. So
that the console does not look as if it were hanging afterwards, the exe sends an Enter itself at
the end; the shell then shows a fresh prompt. Consequence: if you already type the next command
during the one to two seconds of runtime, this Enter submits it. This does not happen with `--watch`
(runs permanently) or with redirected output. If you need the exit code in scripts (`0` = success,
`1` = error):

```powershell
$p = Start-Process .\BGInfo-Reloaded.exe -ArgumentList '--config','\\fileserver.example.com\share\BGInfo-Reloaded.json' -Wait -PassThru
$p.ExitCode
```

```bat
start /wait BGInfo-Reloaded.exe
echo %ERRORLEVEL%
```

## Configuration: `BGInfo-Reloaded.json`

### Where the file is looked for

The first source found wins:

1. `--config <path|url>`
2. environment variable `BGINFORELOADED_CONFIG` (path or URL)
3. `BGInfo-Reloaded.json` in the start directory – the folder the program is called from (the
   current folder of the console, or “Start in” of a shortcut)
4. `BGInfo-Reloaded.json` in the same folder as the exe
5. `%ProgramData%\BGInfo-Reloaded\BGInfo-Reloaded.json`
6. built-in default values

Which source was used is shown in the output and in the log (`Konfiguration: …`).

The editor follows the same order: without a file argument it opens the file `BGInfo-Reloaded.exe`
would use (it skips a URL given through the environment variable). If there is none, it asks on
start: **create a default configuration** (writes a `BGInfo-Reloaded.json` with all settings at
their default values and empty whitelist and blacklist into the program folder and opens it),
**load an existing configuration** or **start without a file**. If the program folder is not
writable, it lets you choose where to save. The status bar shows the file name; the full path appears when the mouse points
at it.

An autostart task starts in a system folder – there is no `BGInfo-Reloaded.json` there, so the file
next to the exe or the one set up with `--config` applies.

### First start: adopting the current wallpaper

If there is no configuration at all yet (no argument, no environment variable, no file in the
places above), the first start adopts the wallpaper the user currently has set:

- The image is copied to `backgrounds\background_user.<extension>` next to the configuration and
  entered as a relative `wallpaper.path` (`.\backgrounds\background_user.jpg`). The folder can then
  be distributed together with the image. An existing file is never overwritten (then
  `background_user-2` …).
- Where the image came from is stored as `wallpaper.originalPath` in the file (for information
  only) and shown in the editor on the “Hintergrund” tab under “Übernommen von (Originalpfad)”.
- `BGInfo-Reloaded.exe` creates a `BGInfo-Reloaded.json` with the default values next to itself;
  the editor does so on **create a default configuration**. With `--preview` and on **start
  without a file** nothing is created.
- If the folder is not writable or no wallpaper is set, the previous behaviour applies: built-in
  default values, the user's wallpaper is read at run time.

### Structure

Comments (`//`) and trailing commas are allowed. Everything that is left out keeps its default
value – so a file that only contains `items` is valid. A complete, commented template is in
[BGInfo-Reloaded.sample.json](BGInfo-Reloaded.sample.json) (comments in German).

```jsonc
{
  "panel": {
    "position": "TopRight",      // TopLeft, TopCenter, TopRight, MiddleLeft, Center,
                                 // MiddleRight, BottomLeft, BottomCenter, BottomRight
    "margin": 40,                // distance to the edge in pixels
    "avoidTaskbar": true,        // measure the distance from the work area: the panel starts above or
                                 // next to the taskbar instead of behind it (false = from the monitor edge)
    "padding": 22,               // inner padding of the panel
    "screenFraction": 0,         // panel width as a fraction of the screen width: 3 to 8 = 1/3 to 1/8,
                                 // the content is scaled to match; 0 = off
    "width": 0,                  // fixed panel width in pixels; 0 = by content
    "labelShare": 0,             // share of the label column in percent (10 to 90); 0 = automatic
    "labelWidth": 0,             // width of the label column in pixels; 0 = automatic
    "columnGap": 24,             // gap between label and value when the column is automatic
    "textOverflow": "Truncate",  // overlong texts: Truncate = cut off with …, Wrap = wrap
    "background": "#0C0E12A5",   // #RRGGBBAA, AA = opacity
    "cornerRadius": 0,           // rounded corners of the panel background; 0 = square
    "shadowSize": 0,             // drop shadow: how far it shows at the bottom right; 0 = none
    "shadowColor": "#00000055",  // shadow colour, fades out softly at the edge
    "scaleWithDpi": true         // enlarge measurements and font with the Windows scaling
  },
  "font":   { "family": "Consolas", "size": 15, "headerSize": 19 },
  "colors": { "header": "#FFD700", "label": "#FFFFFF", "value": "#82DCD2" },
  "wallpaper": {
    "path": "\\\\fileserver.example.com\\share\\wallpaper.jpg",  // optional: fixed background image
    "fit": "Fill",                               // Fill, Fit, Stretch, Center, Tile or None
    "resolutionVariants": true,                  // prefer an image with the resolution appended
    "backgroundColor": "#101218"                 // colour for borders and if there is no image
  },
  "monitors": {
    "mode": "PerMonitor",                        // PerMonitor = one image per monitor, Single = one for all
    "panel": "All"                               // All = panel on every monitor, Primary = only once
  },
  "hosts": {
    "include": [],                               // whitelist of computer names, * and ? allowed
    "exclude": ["KIOSK-*", "PC-0??"],            // blacklist, takes precedence
    "onExcluded": "BackgroundOnly"               // or "Skip"
  },
  "update": {
    "path": "\\\\fileserver.example.com\\share\\BGInfo-Reloaded", // optional: folder with the current exe for the self-update
    "auto": false                                // true = check on every run and update automatically
  },
  "items": [
    { "section": "SYSTEM" },
    { "label": "Hostname", "value": "{hostname}" },
    { "spacer": true },
    { "drives": true }
  ]
}
```

### Width of the panel and of the columns

**Panel width** – three options, in this order of precedence:

| Setting | Effect |
|---|---|
| `"screenFraction": 3` … `8` | The panel is 1/3 … 1/8 of the screen width wide. Font, padding, column gap, corners and shadow are scaled so that the content just fills this width. The panel therefore looks the same size on every monitor, regardless of resolution and Windows scaling. |
| `"width": 600` | Fixed width in pixels. The content is not scaled; whatever does not fit is truncated or wrapped, depending on `textOverflow`. |
| both `0` (default) | Width by content: as wide as the longest line. |

With `screenFraction`, `font.size` and `font.headerSize` only determine the ratio of heading to
line, no longer the absolute size. If the panel would be taller than the screen at the requested
width, the content is reduced until it fits vertically. The distance to the edge (`margin`) is not
scaled. With multiple monitors the fraction applies per monitor.

**Columns** – how the width is divided between label and value, again in this order of precedence:

| Setting | Effect |
|---|---|
| `"labelShare": 35` | The label column gets 35 % of the inner width, the values the rest (allowed: 10 to 90). |
| `"labelWidth": 180` | Label column with a fixed width in pixels. |
| both `0` (default) | Automatic: as wide as the longest label plus `columnGap`. |

`columnGap` sets the gap between label and value when the column is automatic.

**Texts that do not fit into their column** (`textOverflow`) – this concerns a fixed panel width, a
fixed column share and a panel that reaches the edge of the screen:

| Setting | Effect |
|---|---|
| `"textOverflow": "Truncate"` (default) | The text is cut off and ends with “…”. Every line stays on one line. |
| `"textOverflow": "Wrap"` | The text wraps inside its column; the line of the panel becomes taller accordingly. Applies to headings, labels and values. |

With a relative width (`screenFraction`) nothing is ever wrapped – there the content is already
scaled to fit. `screenFraction` also takes precedence over `width`: if both are set, the relative
width applies and the panel follows the content again. If a fixed width is not enough for both
columns, the automatically sized label column gets at most 45 % of it.

### Border (`border`)

```jsonc
"border": {
  "width": 2,            // line width in pixels, 0 = no border (default)
  "style": "Solid",      // Solid = line, Dotted = dots, Dashed = dashes
  "scope": "Panel",      // Panel = around the whole panel, Sections = one box per section, Rows = grid around every line
  "color": "#FFFFFF80"   // #RRGGBBAA
}
```

- `Sections`: one box per heading with its lines; a spacer separates the boxes visibly, without a
  spacer they touch.
- `Rows`: a grid around every line with label and value, with a divider between the columns;
  headings stay free.
- The border lies within the existing padding and line spacing: size and position of the panel do
  not change. The line width is scaled like the panel.
- In the editor: tab “Aussehen”, section “Rahmen”.

### Date and number format (`format`)

```jsonc
"format": {
  "date": "dd.MM.yyyy",            // date only: {biosdate}, {lastupdate}
  "dateTime": "dd.MM.yyyy HH:mm",  // date with time: {boot}, {now}, {installdate}, {filetime:…}, WMI date values
  "decimals": 1,                   // at most this many decimal places: {ram}, {ramfree}, {netspeed}, drives
  "culture": "en-GB"               // separators, month and day names
}
```

- Format strings as in .NET: `d`/`dd` day, `M`/`MM` month, `MMMM` month name, `yy`/`yyyy` year,
  `ddd`/`dddd` weekday, `HH` hour (24 h), `hh` with `tt` (12 h), `mm` minute, `ss` second.
- `culture`: omitted = neutral (dot as decimal separator, English names) – this is how the panel
  looked before; `system` = like the Windows of the machine; otherwise a culture name such as
  `de-DE`, `en-GB`, `en-US`.
- A format given in a line (`{now:dd.MM.yyyy}`) takes precedence.
- The uptime (`{uptime}`) follows `culture`: German “31 Tage 21 Std.”, English “31 days 21 hours”
  (below one day hours and minutes); without a culture and for other languages the short form
  “31 d 21 h”. Custom format: `{uptime:d' days and 'h' hours'}` (`d` days, `h` hours, `m` minutes,
  fixed text in single quotes).
- An invalid format or an unknown culture falls back to the default.
- In the editor: tab “Aussehen”, section “Datum und Zahlen”, with an example line.

### Splitting a value into several lines (`split`)

If a placeholder returns several entries in one text – several graphics adapters, DNS servers,
disks – `split` divides the value at the given separators. The first part stands next to the
label, every further part in a line of its own below:

```jsonc
{ "label": "Graphics", "value": "{gpu}", "split": ", " }
{ "label": "DNS servers", "value": "{dns}", "split": ", |;" }   // several separators divided by |
```

Whitespace around the parts and empty parts are dropped. Works for normal lines and for `drives`.
In the editor: field “Trennen bei” below “Value”; “Aktueller Wert” shows the parts with ⏎ between them.

### Path of the background image (`wallpaper.path`)

Empty means: the wallpaper of the respective user. Otherwise a path that is valid on all machines –
a UNC share, a path with environment variables (`%ProgramData%\Company\wallpaper.jpg`) or a relative
path. Relative paths such as `.\\background.jpg` are relative to the folder of the configuration
file (for a configuration loaded from a URL: to the folder of the exe), not to the working
directory. This way `BGInfo-Reloaded.json` and images can be distributed together into any folder.

### Fitting the background image to the resolution (`wallpaper.fit`)

Before the text is drawn, the background image is brought to the resolution of the respective
monitor. How this is done is determined by `fit` – the values correspond to the Windows settings:

| `fit` | Windows name | Effect |
|---|---|---|
| `Fill` (default) | Fill | fills the screen, aspect ratio is kept, the excess is cropped centrally |
| `Fit` | Fit | the whole image is visible, aspect ratio is kept, borders in `backgroundColor` |
| `Stretch` | Stretch | fills the screen, the image is distorted, nothing is cropped |
| `Center` | Center | unscaled in the middle, borders in `backgroundColor` |
| `Tile` | Tile | unscaled, tiled |
| `None` | – | draw on the image in its original size; Windows scales image and panel together |

With all values except `None` the panel is pixel-exact, and with `"scaleWithDpi": true` font and
spacing grow with the Windows scaling of the monitor (by a factor of 1.5 at 150 %). If a company
wallpaper has a different aspect ratio than the screen, `Fill` crops logo or text at the edge – in
that case use `Fit` with a matching `backgroundColor`, `Stretch` or a separate image per resolution.

### A separate image per resolution (`wallpaper.resolutionVariants`)

For each monitor a file with the resolution appended is looked for first, in the same folder and
with the same extension. This is switched on by default (`"resolutionVariants": true`) and can be
switched off with `false`:

```
wallpaper.jpg              base image (wallpaper.path or the user's wallpaper)
wallpaper_3440x1440.jpg    used on a monitor with 3440 x 1440
wallpaper_1920x1080.jpg    used on a monitor with 1920 x 1080
```

`wallpaper-3440x1440.jpg` and `wallpaper3440x1440.jpg` are recognized as well. If there is no file
for a resolution, the base image is used. The base image itself may be missing: if only files per
resolution are in the folder (as with the demo backgrounds supplied in `backgrounds/`), the best
matching image is taken for a resolution that does not exist – the one with the most similar aspect
ratio, preferably not smaller than the monitor. What counts is the real resolution in pixels, not
the scaled one; `{resolution}` or the log (`Rendere Monitor …`) shows which one that is. The log
also shows which image was used.

#### Own assignment per resolution (`wallpaper.byResolution`)

For resolutions without a matching image in the folder, any image can be assigned:

```jsonc
"wallpaper": {
  "path": ".\\backgrounds\\Wallpaper.jpg",
  "byResolution": {
    "3440x1440": ".\\backgrounds\\wide.jpg",
    "2560x1600": "\\\\fileserver.example.com\\share\\bginfo\\notebook.jpg"
  }
}
```

Order per monitor: the own assignment for exactly this resolution, then the file with this
resolution in its name, then the image without suffix, then the best fitting other version. This
applies on every run – also when `--watch` renders again after a resolution change. If the file of
an assignment is missing, the log gets a warning and the next step applies.

In the editor (tab “Hintergrund”, “Bilder je Auflösung”) a list shows the own assignments, the
versions found in the folder and, for every connected monitor, which image it gets. Assignments can
be added, changed and removed there. If an image is chosen that already carries a resolution in its
name (`Wallpaper_1920x1080.jpg`), the editor enters `Wallpaper.jpg` – all versions are found under
that name; the file itself need not exist.

### Multiple monitors (`monitors`)

| Setting | Value | Effect |
|---|---|---|
| `mode` | `PerMonitor` (default) | one image per monitor in its resolution and scaling, including a separate image per resolution |
| | `Single` | one image for the primary monitor; Windows shows the same image on all monitors |
| `panel` | `All` (default) | info panel on every monitor |
| | `Primary` | info panel only once, on the primary monitor; the others only get the background |

`panel` only has an effect with `PerMonitor` – with `Single` the one image including the panel
necessarily appears on all monitors. If Windows is set to "Span", `PerMonitor` switches to "Fill",
because a spanning image is not compatible with images per monitor. If the Windows interface needed
for this is not available (e.g. Server Core), the program automatically works as with `Single`; the
log then contains a warning. With `--preview image.png` one file per monitor is created
(`image.png` for the primary one, `image_m2.png` etc. for the others).

JPG, PNG, BMP, GIF and TIFF work as background images. WebP is not read – the panel then appears on
the plain area in `backgroundColor`.

If the configured font is not installed, the program automatically falls back to Consolas, Segoe UI,
Tahoma, Arial or Verdana. Backslashes have to be doubled in JSON (`\\`).

### Including or excluding machines (`hosts`)

With a central configuration you can control on which machines the info panel appears.

| Setting | Effect |
|---|---|
| `include` | Whitelist. If it is not empty, the panel only appears on machines that one of the entries matches. |
| `exclude` | Blacklist. The panel does not appear on matching machines. Takes precedence over `include`. |
| `onExcluded` | What happens on excluded machines: `BackgroundOnly` (default) sets the background as configured, just without the panel – this also removes a panel set earlier. `Skip` leaves the wallpaper completely untouched. |

An entry is either a computer name or an IP specification; both are valid in both lists:

| Entry | Example | Checked against |
|---|---|---|
| name, optionally with `*` (any number of characters) and `?` (exactly one character) | `SRV-*`, `PC-0??`, `*.dmz.company.local` | computer name and fully qualified name; case does not matter |
| single IP address | `10.10.1.5` | all IPv4 addresses of the active network adapters |
| network | `10.10.0.0/16` | the same |
| range | `10.10.1.10-10.10.1.50`, short `10.10.1.10-50` | the same |

If a machine has several addresses, one match is enough. IPv4 is supported. A name pattern is
additionally checked against the addresses as text, so `10.10.*` works as well.

```jsonc
"hosts": { "exclude": ["KIOSK-*", "PC-0??"] }                    // everywhere except on these
"hosts": { "include": ["SRV-*"], "exclude": ["SRV-TEST*"] }       // servers only, but no test servers
"hosts": { "include": ["10.10.0.0/16"], "exclude": ["10.10.99.1-10.10.99.254"] }   // by network
```

**Import and export:** in the editor each list has the buttons “Importieren…” (import) and
“Exportieren…” (export). The import reads a text file with one entry per line (also separated by
comma or semicolon); empty lines and lines starting with `#` or `//` are skipped, duplicates
removed. If the list already has entries, the editor asks whether to append or replace. The export
writes the same form. Afterwards the entries are stored in `BGInfo-Reloaded.json` – a text file
changed later has to be imported again. Duplicate entries (also with different upper/lower case)
are removed on import, when leaving the input field and on saving.

**List in a separate file:** instead of (or in addition to) the lists in the configuration, a text
file can be given per list; it is read afresh on every run:

```jsonc
"hosts": {
  "exclude": ["KIOSK-*"],                                             // directly in the configuration
  "excludeFile": "\\\\fileserver.example.com\\share\\bginfo\\disallowed_hosts.txt",   // additionally from a file
  "includeFile": ".\\allowed_hosts.txt"                               // relative to the configuration file
}
```

- The file has the same form as for the import: one entry per line, names or IP specifications,
  comment lines starting with `#` or `//`.
- The list and the file are combined. A path, a UNC share or an http(s) URL are possible.
- If the file is unreachable, the version read last from a local cache is used
  (`%LOCALAPPDATA%\BGInfo-Reloaded\hosts.….cache.txt`); if there is none yet, its entries are
  missing and the log contains a warning.
- On every run the log states which file was read with how many entries.

Which way fits when: a separate file pays off if the list changes often or is maintained by other
people than the configuration – a change then takes effect on all machines on the next run without
redistributing `BGInfo-Reloaded.json`. A few rarely changed entries are better kept in the
configuration itself; they also work without a network.

Whether and why a machine is excluded is shown in the output and in the log
(`Info-Panel auf diesem Rechner abgeschaltet: …`). With `Skip` a panel set earlier stays until the
wallpaper is changed in some other way.

### Lines (`items`)

| Entry | Result |
|---|---|
| `{ "section": "TEXT" }` | heading |
| `{ "spacer": true }` | spacer |
| `{ "label": "…", "value": "…" }` | normal line |
| `{ "label": "…", "value": "…", "hideIfEmpty": true }` | the line is omitted if the value is empty or `n/a` |
| `{ "drives": true }` | one line per hard disk |
| `{ "drives": true, "label": "…", "value": "…" }` | drive lines with custom text |
| `{ "includeItems": true }` | the lines of the central file included through `include` |
| `{ "block": "Name", "when": "…" }` … `{ "endBlock": true }` | block: the content in between only appears if the filter at the start of the block matches |

Every entry can additionally carry filters (`onlyOn`, `notOn`, `when`) – see "Filtering lines".

In drive lines there are additionally `{drive}` (e.g. `C:`), `{volume}` (volume label), `{used}`,
`{total}`, `{free}` (each in GB) and `{percent}` (used, in percent).

### Placeholders

Placeholders work in `section`, `label` and `value` and can be mixed with free text, e.g.
`"{os} {displayversion} (Build {build})"`.

The complete, always current list with the values of the respective machine is printed by:

```
BGInfo-Reloaded.exe --list-tags
BGInfo-Reloaded.exe --list-tags wmi:Win32_ComputerSystem
BGInfo-Reloaded.exe --list-tags "reg:HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
```

Without an argument the built-in placeholders, the drive placeholders, the generic sources and all
environment variables are printed. With `wmi:<class>` or `reg:<key>` every property or value appears
as a ready-to-use placeholder to copy into `BGInfo-Reloaded.json`. The output contains all
environment variables in plain text – do not pass it on unchecked if credentials are stored there.

**Built-in**

| Placeholder | Content |
|---|---|
| `{hostname}`, `{fqdn}` | computer name, fully qualified name |
| `{user}`, `{username}`, `{domain}` | `DOMAIN\user`, user only, domain only |
| `{machinedomain}`, `{logonserver}` | domain or workgroup of the machine, logon server |
| `{systemtype}` | role of the machine: workstation, server or domain controller |
| `{ipv4}`, `{ipv6}` | first IPv4 or IPv6 address of an active network adapter |
| `{subnet}` | subnet mask of the IPv4 address (e.g. `255.255.255.0`) |
| `{mac}`, `{gateway}`, `{dns}`, `{dhcp}` | MAC address, default gateway, DNS servers, DHCP server |
| `{nic}`, `{netspeed}`, `{nettype}` | name, speed and type (Ethernet/WLAN) of the active network adapter |
| `{os}`, `{osversion}`, `{displayversion}`, `{build}`, `{arch}` | Windows name, version, release (e.g. `24H2`), build, architecture |
| `{boot}`, `{uptime}` | last system start, uptime since then |
| `{timezone}` | time zone |
| `{resolution}`, `{scaling}` | resolution and Windows scaling of the primary monitor |
| `{cpu}`, `{cores}` | processor name, logical cores |
| `{ram}`, `{ramfree}`, `{ramspeed}` | memory, free memory, clock speed |
| `{bios}`, `{serial}` | BIOS version, serial number |
| `{manufacturer}`, `{model}` | manufacturer, model |
| `{allips}` | all IPv4 addresses of the active adapters |
| `{adsite}`, `{ou}` | Active Directory site and OU path of the machine (e.g. `Berlin/Clients`) |
| `{session}`, `{rdpclient}` | type of session (console/RDP), name of the RDP client |
| `{language}`, `{installdate}` | display language and installation date of Windows |
| `{lastupdate}`, `{pendingreboot}` | most recently installed Windows update with KB number; whether a restart is pending (`ja`/`nein`) |
| `{secureboot}`, `{antivirus}` | Secure Boot (`aktiv`/`aus`), registered antivirus (client Windows only) |
| `{virtual}`, `{chassis}` | virtual machine (`ja (VMware)`/`nein`), form factor (desktop, notebook, server …) |
| `{assettag}`, `{uuid}`, `{biosdate}` | asset tag, UUID and BIOS date |
| `{gpu}`, `{disk}` | graphics card, physical disks with size |
| `{rampercent}`, `{battery}` | used memory and battery level in percent |
| `{monitors}` | number and resolution of all monitors |
| `{now}` | current date and time |
| `{appversion}` | version of BGInfo-Reloaded |
| `{configversion}` | version of the configuration file – the value of `"version"` at the very top of the file |

**Generic – for extending without a rebuild**

| Placeholder | Example |
|---|---|
| `{env:NAME}` | `{env:LOGONSERVER}` |
| `{reg:HIVE\path\valuename}` | `{reg:HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\RegisteredOwner}` |
| `{wmi:Class.Property}` | `{wmi:Win32_TimeZone.Caption}` |
| `{wmi:Class.Property where condition}` | `{wmi:Win32_NetworkAdapterConfiguration.DNSServerSearchOrder where IPEnabled=true}` |
| `{wmi:SELECT … FROM …}` | `{wmi:SELECT Caption FROM Win32_OperatingSystem}` – a whole WQL query as in the WMI Query field of BGInfo; the first property with a value counts |
| `{filever:path}` | `{filever:%WINDIR%\\explorer.exe}` – file version |
| `{filetime:path}` | `{filetime:%ProgramData%\\Company\\asset.txt}` – modification date of the file |
| `{file:path}` | `{file:%ProgramData%\\Company\\asset.txt}` – first non-empty line of the file |
| `{now:format}` | `{now:dd.MM.yyyy HH:mm}` |

**Truncating** – works with every placeholder, including custom fields:

| Notation | Result |
|---|---|
| `{cpu,20}` | at most the first 20 characters |
| `{cpu,20...}` | at most 20 characters, with `…` at the end if truncated |
| `{serial,-8}` | only the last 8 characters (`{serial,-8...}` with `…` at the front) |

Shorter values and `n/a` stay unchanged.

Notes:

- Registry hives: `HKLM`, `HKCU`, `HKCR`, `HKU`. The last segment is the value name.
- If WMI returns several values, they are joined with commas; with several matches the first counts.
- Values that cannot be determined yield `n/a`; the run does not abort.
- Unknown placeholders stay as literal text – this way typos show up on the wallpaper.

### Custom fields (`fields`)

A WMI query (or any other source) normally stands directly in the `value` of a line. If it is to be
used several times, it gets a name under `fields` and is then available everywhere as `{name}` – the
counterpart to the "Custom" fields of BGInfo:

```json
"fields": {
  "model": "{wmi:SELECT Model FROM Win32_ComputerSystem}",
  "operatingsystem": "{os} {displayversion} (Build {build})"
},
"items": [
  { "label": "Model", "value": "{model}" },
  { "label": "System", "value": "{operatingsystem}" }
]
```

- A definition may contain placeholders, other custom fields and free text.
- Custom fields take precedence over built-in names. Within its own definition the same name refers
  to the built-in value again: `"ipv4": "{ipv4} / {subnet}"` extends `{ipv4}`.
- `BGInfo-Reloaded.exe --list-tags [--config file]` shows the custom fields with their values.

### Including a central file (`include`)

```json
"include": "\\\\fileserver.example.com\\share\\BGInfo-Reloaded.central.json"
```

The file named (local path, UNC or http(s) URL; relative to the folder of the configuration file)
provides shared `fields` and `items` – template: `BGInfo-Reloaded.central.sample.json`.

- Its **fields** apply in addition to your own; with the same name your own definition wins.
- Its **lines** appear where the entry `{ "includeItems": true }` stands in your own `items` (also
  several times or not at all – then only the fields are used).
- Everything else (appearance, background, `hosts`) remains the business of the including file. If
  the whole configuration is to be kept centrally, keep using `--config` (see "Central
  distribution").
- The file is read on every run and cached in `%LOCALAPPDATA%\BGInfo-Reloaded\include.cache.json`.
  If it cannot be reached, the cache applies; the log states what was used.

### Filtering lines (`onlyOn`, `notOn`, `when`)

Every entry in `items` – including heading, spacer, drives and `includeItems` – can carry filters.
If several are given, all of them have to match.

| Filter | Meaning |
|---|---|
| `"onlyOn": ["SRV-*", "*-DC??"]` | only on machines whose name (or FQDN) matches one of the patterns |
| `"notOn": ["KIOSK-*"]` | not on matching machines |
| `"when": "{systemtype} = Server"` | only if the comparison is true; `*` and `?` are allowed on the right, case does not matter |
| `"when": "{env:SITE} != Berlin"` | only if the value does not match |
| `"when": "{env:SITE}"` | without a comparison: only if the text yields a value (not empty, not `n/a`) |

**Blocks** group several entries under one condition – e.g. lines that only make sense on physical
hardware (`{virtual}` yields the German values `nein` or `ja (…)`):

```json
{ "block": "Physical", "when": "{virtual} = nein" },
  { "label": "Form factor", "value": "{chassis}" },
  { "label": "BIOS date", "value": "{biosdate}" },
{ "endBlock": true },
{ "block": "VMs only", "when": "{virtual} = ja*" },
  { "label": "Hypervisor", "value": "{virtual}" },
{ "endBlock": true }
```

The text after `"block"` is only a name for orientation. At the start of a block the same filters
apply as on every line (`onlyOn`, `notOn`, `when`); blocks may be nested, and lines inside a block
can additionally carry their own filters.

This way a single central file can contain lines that only appear on servers, only at one site or
only on certain machines. `hosts.include`/`hosts.exclude`, in contrast, switch off the whole panel.

### Testing the configuration

```
BGInfo-Reloaded.exe --config my.json --preview preview.png
```

Prints all lines with their resolved values and saves the image without changing the wallpaper. A
faulty file (JSON syntax, unknown position, invalid colour) leads to a clear error message and exit
code `1`.

## Central distribution

There are two ways, which can also be combined.

**Distribute the file along with the exe** – copy `BGInfo-Reloaded.json` through software
distribution/GPO next to the exe or to `%ProgramData%\BGInfo-Reloaded\`. Works without a network
connection; changes have to be distributed again.

**Reference a central source** – the file is on a share or web server; changes take effect on all
machines with the next run:

```
BGInfo-Reloaded.exe --register-task --config \\fileserver.example.com\share\BGInfo-Reloaded.json
BGInfo-Reloaded.exe --register-task --config https://intranet.example/BGInfo-Reloaded.json
```

Alternatively set the environment variable `BGINFORELOADED_CONFIG` (e.g. through GPO); the call then
needs no `--config`.

After every successful load a central source is copied to
`%LOCALAPPDATA%\BGInfo-Reloaded\config.cache.json`. If it cannot be reached (notebook on the road,
server down), this cache is used; if there is none yet, the default values apply. In both cases the
run does not fail; the log contains a warning.

## Autostart

```
BGInfo-Reloaded.exe --register-task
```

creates two scheduled tasks for the currently logged-on user:

| Task | Trigger |
|---|---|
| `BGInfo-Reloaded - Logon` | at every logon |
| `BGInfo-Reloaded - Refresh` | every 30 minutes (or `--interval-minutes`) |

The tasks also run on battery power and have no time limit.

### Reacting to resolution changes

```
BGInfo-Reloaded.exe --register-task --watch
```

The logon task then starts the exe with `--watch`: it renders at logon and stays active invisibly in
the background until logoff. If resolution, scaling, number or arrangement of the monitors changes
(docking, RDP session with a different window size, switching monitors), the wallpaper is
re-rendered after about two seconds; a pure scaling change is detected after 15 seconds at the
latest. At most one such instance runs per session; the refresh task keeps working in parallel.
With `"fit": "None"` this is of no use because the result then does not depend on the resolution.
To try it without a task, `BGInfo-Reloaded.exe --watch` is enough; the instance is ended through
Task Manager or by logging off.

- Admin rights are not required; no password is stored.
- The tasks deliberately run in the user context, not as SYSTEM – SYSTEM has no visible desktop and
  cannot set a wallpaper. The call therefore has to be made per user (e.g. through a logon script).
- The tasks point to the path where the exe was when registering. If it is moved, run
  `--register-task` again.
- Registering again overwrites the existing tasks, e.g. to change the interval or `--config`.

## Log file and troubleshooting

Every run (except `--help`, `--version` and `--list-tags`) is logged to
`%LOCALAPPDATA%\BGInfo-Reloaded\bginfo.log` – when run as a task this is the only error display.
The messages are German (`FEHLER` = error, `Ende` = end):

```
2026-10-01 15:23:00 INFO   --- Start COMPANY\user: --config \\fileserver.example.com\share\BGInfo-Reloaded.json
2026-10-01 15:23:00 INFO   Konfiguration: \\fileserver.example.com\share\BGInfo-Reloaded.json
2026-10-01 15:23:00 FEHLER Fehler: Konfiguration '…' ist kein gueltiges JSON: …
2026-10-01 15:23:00 FEHLER --- Ende, Exit-Code 1
```

From 256 KB on the file becomes `bginfo.log.old`; exactly one predecessor file is kept.

| Symptom | Cause / remedy |
|---|---|
| The wallpaper does not change | check the log; if it says `FEHLER`, the configuration is usually invalid |
| `{something}` appears literally on the wallpaper | the placeholder is misspelled |
| The value is `n/a` | the source returns nothing (check the WMI class/property, registry path or file) – test with `--preview` |
| An old configuration is used | the central source cannot be reached, the cache is used (warning in the log) |
| A different background image is wanted | set `wallpaper.path`, or delete `original.txt` (see below) and choose the wallpaper again in Windows |

### Files in `%LOCALAPPDATA%\BGInfo-Reloaded\`

| File | Content |
|---|---|
| `wallpaper_bginfo_m1_….jpg` etc. | the generated wallpapers per monitor (the name changes on every run, old ones are removed); with `Single` the file is called `wallpaper_bginfo.jpg` |
| `original.txt` | path of the unmodified original wallpaper |
| `original_copy.jpg` | saved copy of the original in case Windows only knew its own working copy |
| `config.cache.json` | the last central configuration that was loaded successfully |
| `include.cache.json` | the last central `include` file that was loaded successfully |
| `bginfo.log`, `bginfo.log.old` | log |
| `update.json` | remembered repository for `--update` |

For a complete reset run `--unregister-task`, delete this folder and choose the wallpaper again in
the Windows settings.

## Updating

**From a folder** (local or UNC share – for machines without access to GitHub):

```jsonc
"update": {
  "path": "\\\\fileserver.example.com\\share\\BGInfo-Reloaded",   // folder with the current files
  "auto": true,                                   // check on every run and update automatically
  "program": true,                                // BGInfo-Reloaded.exe (default: on)
  "editor": true,                                 // BGInfo-Reloaded-Editor.exe, where present (default: on)
  "config": false,                                // BGInfo-Reloaded.json (default: off)
  "backgrounds": false                            // background*.* and the subfolder backgrounds\ (default: off)
}
```

What is fetched from the folder:

| Switch | Files | When | Default |
|---|---|---|---|
| `program` | `BGInfo-Reloaded.exe` | if the one in the folder has a higher version | on |
| `editor` | `BGInfo-Reloaded-Editor.exe` | if it has a higher version – and only where the editor is already next to the exe | on |
| `config` | `BGInfo-Reloaded.json` | if the one in the folder is newer (modification date); the previous one is kept as `.bak` | off |
| `backgrounds` | `background*.*` and all files in the subfolder `backgrounds\` | whatever is newer or missing locally | off |

All files end up next to the exe (those from `backgrounds\` in the subfolder of the same name).
Locally newer files are not overwritten, and nothing is deleted. A fetched configuration applies
from the next run – the file in the folder should contain the same `update` section, otherwise the
synchronisation ends with it. In the editor the switches are on the tab “Autostart und Werkzeuge”;
every checkbox shows the affected files as a tooltip.

```
BGInfo-Reloaded.exe --update                    update from the folder in "update.path"
BGInfo-Reloaded.exe --update --check-only       only check
BGInfo-Reloaded.exe --update --from <folder>    once from a different folder
```

If the folder contains a `BGInfo-Reloaded.exe` with a higher file version than the running one, it
is copied next to the program's own exe and then swapped in; it becomes active on the next start.
Until then the previous version remains as `BGInfo-Reloaded.exe.old`. An older or equal version in
the folder is never installed. If the editor is also present next to the exe, it is updated from
the same folder.

- With `"auto": true` every normal run (including the autostart task) checks the folder by itself.
  If there is nothing to do, nothing is written to the log about it. If the folder is unreachable
  or the program folder is not writable, there is a warning; the run itself continues.
- With `"auto": false` (default) this only happens when `--update` is called.
- A relative path is relative to the folder of the configuration file. URLs are not supported here.
- To roll out a new version it is enough to put the new files into the folder.
- **Security:** whoever may write to this folder decides which program runs on the machines.
  Restrict write access there to the admins; users only need read access. The program folder on the
  machines must be writable for the user, otherwise the exe cannot replace itself.

**From GitHub releases** (no SDK needed on the target machine):

```
BGInfo-Reloaded.exe --update --repo Kilowatt-W/bginfo-reloaded
BGInfo-Reloaded.exe --update                    (the repository is remembered after the first call)
BGInfo-Reloaded.exe --update --check-only
```

Compares the tag of the latest release (e.g. `v1.3.0`) with its own version and downloads the
release asset named exactly `BGInfo-Reloaded.exe`. The new version becomes active on the next start.

Instead of `--repo`, the repository can also be entered centrally in the configuration:

```jsonc
"update": { "repo": "Kilowatt-W/bginfo-reloaded" }
```

It is only used when `--update` is called, never automatically (GitHub limits anonymous requests per
internet connection), and only `BGInfo-Reloaded.exe` is fetched. A repository given with `--repo` is
still remembered per user in `%LOCALAPPDATA%\BGInfo-Reloaded\update.json`. A private repository
cannot be reached without signing in.

**From source** (requires `git.exe` and the .NET SDK on the machine):

```
BGInfo-Reloaded.exe --update-source https://github.com/Kilowatt-W/bginfo-reloaded.git --branch main
```

Without `--repo` and without `--from`: first the folder from `update.path`, otherwise `update.repo`,
otherwise the remembered GitHub repository. The two paths through GitHub and the source code have not been tested against a
real repository yet; the update from a folder has been tested.

## Building it yourself

```powershell
.\Build.ps1
```

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer) and puts
`BGInfo-Reloaded.exe` and `BGInfo-Reloaded-Editor.exe` into the folder `publish_tmp`. New built-in
placeholders are added in `src/ValueResolver.cs`; the version number is in
`src/BGInfo-Reloaded.csproj`.
