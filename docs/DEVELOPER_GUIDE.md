# CalendarShot Developer Guide

This guide is for people who want to build, change or extend CalendarShot. For using the application, see the [User Manual](USER_MANUAL.md).

## Contents

1. [Overview](#overview)
2. [Repository layout](#repository-layout)
3. [Building and running](#building-and-running)
4. [Architecture](#architecture)
5. [How screenshot recognition works](#how-screenshot-recognition-works)
6. [Storage and export](#storage-and-export)
7. [Testing](#testing)
8. [The Inspect tool](#the-inspect-tool)
9. [Common changes](#common-changes)
10. [Coding conventions](#coding-conventions)
11. [Security guidelines for contributors](#security-guidelines-for-contributors)
12. [Continuous integration and releases](#continuous-integration-and-releases)

## Overview

CalendarShot is a Windows tray application written in C# on .NET 10 with Windows Forms. It has no third-party runtime dependencies. Text recognition uses `Windows.Media.Ocr` through the Windows SDK projection that ships with .NET.

The code is split so that everything except the Windows-specific shell is platform-independent and unit-tested:

- **`CalendarShot.Core`** (`net10.0`) holds the OCR data model, layout parsing, event merging, iCalendar writing, storage and settings. It has no UI and no Windows APIs.
- **`CalendarShot`** (`net10.0-windows10.0.19041.0`) holds the tray application: screen capture, Windows OCR, forms, reminder timer, startup registration and logging.

## Repository layout

```
CalendarShot.slnx                 Solution
Directory.Build.props             Shared build settings (nullable, analyzers, warnings as errors, version)
global.json                       Pins the .NET SDK feature band
.editorconfig                     Code style and analyzer configuration
build/publish.ps1                 Produces the release zip and checksum
docs/                             User manual and this guide
src/
  CalendarShot.Core/
    Calendar/                     CalendarEvent, merging, stable IDs, iCalendar writer
    Configuration/                AppSettings, validation, reminder policy
    Diagnostics/                  Logging interface
    Export/                       CalendarExporter (store + monthly .ics files)
    Parsing/                      Layout detection and event text parsing
    Recognition/                  Engine-independent OCR model, multi-pass merge
    Storage/                      Atomic file writes, JSON stores, folder layout
  CalendarShot/
    Capture/                      Screen capture, image import
    Forms/                        Settings, reminder, countdown and review windows
    Ocr/                          Windows OCR adapter
    Platform/                     Known folders, startup registration, single instance, file log
    Tray/                         TrayApplicationContext: the application workflow
    Program.cs                    Entry point
tests/
  CalendarShot.Core.Tests/        xUnit tests for the Core library
tools/
  CalendarShot.Inspect/           Command-line tool to debug recognition on an image
```

## Building and running

Prerequisites:

- Windows 10 2004+ or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Optional: Visual Studio 2022 17.14+ / 2026, or VS Code with the C# Dev Kit, or JetBrains Rider

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/CalendarShot
```

The build treats all warnings as errors and runs the .NET analyzers at the `latest-recommended` level, so fix warnings rather than suppressing them. If a rule really does not apply, disable it in `.editorconfig` with a comment explaining why.

The application and Core library have no NuGet dependencies. The test project's packages are locked in `tests/CalendarShot.Core.Tests/packages.lock.json`. After changing a package reference, run `dotnet restore --force-evaluate` and commit the updated lock file. CI restores in locked mode and fails if it is out of date.

To build a release package locally:

```powershell
./build/publish.ps1                 # uses the version from Directory.Build.props
./build/publish.ps1 -Version 1.1.0
```

This produces `artifacts/CalendarShot-<version>-win-x64.zip`, a self-contained single-file executable that does not need .NET installed, plus a `.sha256` checksum file.

### Running a development copy

A development build uses the same `Documents\CalendarShot` folder and the same startup registry value as an installed copy. If you also use CalendarShot day to day, either untick **Start CalendarShot when I sign in** in the development copy, or back up `Documents\CalendarShot` first. Only one copy can run at a time per Windows session.

## Architecture

```
            ┌─────────────────────────── CalendarShot (Windows app) ───────────────────────────┐
            │                                                                                   │
 Reminder ─►│ TrayApplicationContext ──► CountdownForm ──► ScreenCapture ──┐                    │
 timer      │        │                                   ImageSources ─────┤ Bitmap             │
            │        │                                                     ▼                    │
            │        │                                            WindowsOcrService             │
            │        │                                         (2 passes + OcrPassMerger)       │
            │        │                                                     │ OcrPage            │
            └────────┼─────────────────────────────────────────────────────┼────────────────────┘
                     │                                                     ▼
            ┌────────┼──────────────────── CalendarShot.Core ─────────────────────────────────────┐
            │        │                                  CalendarScreenshotParser                  │
            │        │                        MonthGridParser → TimeGridParser → AgendaParser     │
            │        │                                            │ RecognitionResult             │
            │        ▼                                            ▼                               │
            │   ReviewForm (app) ◄──────────────────────── events, date range, warnings           │
            │        │ ImportBatch                                                                │
            │        ▼                                                                            │
            │   CalendarExporter ──► MonthlyEventStore (data/YYYY-MM.json)                        │
            │        │          └──► EventMerger                                                  │
            │        └──────────────► IcsCalendarWriter ──► AtomicFile ──► CalendarShot-YYYY-MM.ics│
            └─────────────────────────────────────────────────────────────────────────────────────┘
```

Key points:

- **`TrayApplicationContext`** owns the whole flow. It runs on the UI thread; the only `async void` method is the menu entry point `StartCapture`, which catches and reports all exceptions.
- **`SingleInstance`** uses a session-local mutex. A second launch signals the running instance, which opens the settings window, and then exits.
- **Settings** (`settings.json`) and **state** (`state.json`) are separate. Settings are what the user configures; state is what the application remembers, such as the date of the last reminder.
- **`ReminderPolicy`** is a pure function checked every 30 seconds. Keeping it pure makes it easy to test and means a missed reminder (PC asleep) fires later the same day.

## How screenshot recognition works

Recognition turns an `OcrPage` (words with bounding boxes) into a `RecognitionResult`. All parsing lives in `src/CalendarShot.Core/Parsing` and uses only geometry and text, never the image.

### 1. OCR (app)

`WindowsOcrService` runs Windows OCR twice at different scales: 2× and 1× for screens up to 2560 px wide, 1× and 1.5× above that. Upscaling helps with small UI text, but each scale sometimes drops or garbles words that the other reads correctly. `OcrPassMerger` fills words missing from the primary pass and swaps in the secondary reading when it has more letters and digits without garbage characters. Coordinates are mapped back to the original image size.

Some applications (HCL Notes, for example) draw their calendar in very small type. When the median word height of the first pass is below 8.5 screen pixels, the image is read again at a scale that brings the text to about 32 pixels (up to 4.5×) and at an intermediate scale. The three passes are merged with the largest one as the primary, because recognition of tiny text is unstable and each scale misses different lines.

### 2. Pre-processing (`PageContext`)

- **Unit**: the median word height. Every tolerance in the parsers is a multiple of it, which keeps parsing independent of resolution and DPI.
- **Segments**: OCR lines split wherever the gap between words exceeds 1.2 units. OCR engines often merge text from neighbouring calendar cells into one line.
- **Caption**: `DateHints.FindMonthCaption` finds the most prominent "Month Year" text, such as "September 2026", which anchors date resolution.

### 3. Layout parsers

`CalendarScreenshotParser` tries the parsers in order and uses the first that succeeds.

**`MonthGridParser`**

1. Finds rows of seven evenly spaced weekday tokens (`Sun`, `Monday`, `S`…). The widest row is the main grid; narrower ones are usually mini calendars in a side bar.
2. Derives the eight column borders from the header. Columns need not be equally wide; some calendars draw Saturday and Sunday narrower. Labels may be left-aligned, centred or right-aligned, so the most evenly spaced edge is used for the first estimate. The borders are then refined against the page. Calendars clip text at cell borders and start event text (usually a time) just inside them, so a good border has no words crossing it, does not separate a time from its title, and has event text starting right after it.
   - First all borders are shifted together, which fixes the case where the label alignment cannot be told from positions alone.
   - Then each border is adjusted on its own, which fixes unequal widths.
   - Day numbers of different widths that share a right (or left) edge pin the neighbouring border just beyond that edge.
   - The outer borders barely move outwards, so side panels stay out of the grid.
   - No border moves more than 45% of a column in total, because a shift of a whole column describes the same grid one day off.
3. Splits all text below the header by column and finds day numbers (`14`, `Oct 1`).
4. Clusters day numbers into week rows. The row height comes from the smallest plausible gap, so a row whose numbers OCR missed does not shift the dates of the rows after it.
5. Solves for the grid's start date: every candidate start date within a few weeks of the caption month (or today) is scored by how many day numbers it explains. Numbers that carry a month (`Oct 1`) add a bonus. At least 60% of the numbers must match.
6. Assigns the remaining text to cells, joins pieces on the same visual line and parses each line with `EventTextParser`. "+N more" markers become a warning.

**`TimeGridParser`** (day, work week and week views)

1. Finds day headers: `Mon 22`, `Monday, September 22`, or a weekday label with a day number stacked below it.
2. Takes the largest row of headers as the columns. A single header counts only when there is an hour axis, since otherwise it is more likely an agenda.
3. Resolves dates from day number + weekday near the caption month, then walks forward column by column.
4. Fits a linear function from y-position to time of day using the hour labels on the left (`9 AM`, `13:00`, or 12-hour labels without AM/PM, which are unwrapped).
5. Groups text in each column into blocks and reads a title, an optional printed time, and notes. Blocks above the first hour label are all-day events. Blocks without a printed time get a start time estimated from the axis, rounded to 15 minutes, with a warning.

**`AgendaParser`** (lists and schedule views)

Reads rows top to bottom. A row that starts with a date (`Tue, Sep 22`, `22 SEP, TUE`, `Wednesday 23`, `Today`) sets the current date. The rest of that row and following rows are events. A time on its own line is attached to the title on the next line.

### 4. Event text (`EventTextParser`)

Recognises a leading or trailing time or time range in many formats (`9:30 AM`, `9am`, `9a`, `14:00-15:30`, `9 – 10am`, `11-1pm`, `Lunch, 12-1pm`, `(9:15am)`). It infers AM/PM across a range, handles overnight ranges, and removes "All day" markers, bullets and separators. Before matching it fixes common OCR confusions inside times only (`9:O0`, `I I AM`, `1 lam`, `IOam`, `gam`). A lone `I` is never changed, so "I am out" stays intact.

`IsNoise` filters calendar chrome such as "Today", "No events", lone numbers and "+2 more".

### 5. Review

Parsing is heuristic, so the result always goes through `ReviewForm`, where the user confirms or corrects it. Keep that in mind when changing the parsers: prefer returning a slightly incomplete result with a warning over guessing silently.

## Storage and export

- **`MonthlyEventStore`** keeps `Documents\CalendarShot\data\YYYY-MM.json` per month. The `.ics` files are always regenerated from the store, so users can move or delete exported files freely.
- **`EventMerger`** merges a batch into a month. If the batch has a replace range (by default the range visible in the screenshot), stored events in that range are dropped first, which is how cancellations propagate. Duplicates are identified by `EventIdentity.Key`: date, start time and normalised title.
- **`IcsCalendarWriter`** writes RFC 5545: CRLF line endings, folding at 75 octets without splitting UTF-8 sequences, TEXT escaping and removal of control characters. Timed events are written in UTC, converted from the local time zone (with handling for daylight-saving gaps). All-day events use `VALUE=DATE`. UIDs are a truncated SHA-256 of the identity key, so they are stable across exports.
- **`AtomicFile`** writes to a temporary file in the target directory and renames it over the destination, so a crash never leaves a truncated file.
- **`JsonFileStore<T>`** enforces a size limit, uses `System.Text.Json` without polymorphic type handling, and moves unreadable files aside as `*.corrupt-<timestamp>` instead of overwriting them.

## Testing

```powershell
dotnet test
dotnet test --filter "FullyQualifiedName~MonthGridParserTests"
dotnet test --collect:"XPlat Code Coverage"
```

Tests cover the Core library. Parser tests build synthetic OCR pages with `tests/CalendarShot.Core.Tests/Support/PageBuilder.cs`, which places words at exact coordinates:

```csharp
var page = new PageBuilder()
    .Text(20, 10, "September 2026", height: 24)
    .Text(26, 60, "Sun")                              // left edge, top, text
    .MergedLine(200, (166, "9am Planning"), (306, "Dentist"));  // one OCR line spanning two cells
```

When you fix a recognition bug, add a test that reproduces the geometry of the failing screenshot. The tests are the only protection against regressions, since real screenshots cannot be committed.

The Windows-specific project (`src/CalendarShot`) has no automated UI tests. Before a release, walk through the manual checklist:

- [ ] First run shows the welcome settings window; cancelling exits without creating a startup entry.
- [ ] Saving settings creates `Documents\CalendarShot\settings.json` and, if enabled, the `HKCU\...\Run\CalendarShot` value.
- [ ] Starting a second copy opens the settings window of the running copy.
- [ ] The reminder appears at the configured time, and Snooze, Skip and Capture behave as described.
- [ ] Capture, file import and clipboard import all reach the review window.
- [ ] Export writes the expected monthly files, and clicking the notification opens the folder.
- [ ] Turning off "Start with Windows" removes the registry value.

## The Inspect tool

`tools/CalendarShot.Inspect` runs the same OCR and parsing as the application on an image and prints the result. Use it to debug layouts without clicking through the UI.

```powershell
dotnet run --project tools/CalendarShot.Inspect -- screenshot.png --today 2026-09-23
dotnet run --project tools/CalendarShot.Inspect -- screenshot.png --show-ocr
dotnet run --project tools/CalendarShot.Inspect -- screenshot.png --save-ocr page.local.json
dotnet run --project tools/CalendarShot.Inspect -- --load-ocr page.local.json
```

`--today` fixes the reference date so that results are reproducible. `--save-ocr` writes the recognised words and positions as JSON. `--load-ocr` parses such a file without an image, which is handy for iterating on the parser.

### Reporting recognition problems

Real calendar screenshots contain private information and must not be attached to public issues. Instead:

1. Run the Inspect tool with `--save-ocr page.local.json`. Files ending in `.local.json` are ignored by git.
2. Open the JSON and replace private event titles with neutral text of similar length, for example `Meeting A`. Keep day numbers, weekday names, times and positions unchanged.
3. Check with `--load-ocr` that the anonymised file still shows the problem, then attach it to the issue.

## Common changes

### Supporting another calendar layout

1. Capture the layout and save an anonymised OCR dump as described above.
2. Check which parser handles it, or should, with the Inspect tool.
3. Add a failing test that reproduces the geometry with `PageBuilder`.
4. Adjust the parser, or add a new one in `Parsing/` and register it in `CalendarScreenshotParser` in the right order. More specific layouts come first.
5. Run the full test suite. Parsers interact, so a change in one can make another layout be detected differently.

### Supporting another language

Month and weekday names come from `DateVocabulary`, which always includes English and adds the names of the current culture, the UI culture and the configured OCR language. Many languages therefore work without code changes. For additional abbreviations or spellings that .NET cultures do not provide, extend the lists at the top of `DateVocabulary`. Words such as "more", "today" and "all day" are matched in `EventTextParser` (`NoisePattern`, `MorePattern`, `AllDayPattern`).

User interface text is currently English only. Localisation contributions are welcome. Please open an issue first to agree on the approach, which will probably be `.resx` resources.

### Adding a setting

1. Add the property with a safe default to `AppSettings` and to `AppSettings.Clone()`.
2. Validate it in `AppSettingsValidator` and add tests.
3. Add the control to `SettingsForm` (`LoadValues` and `Save`).
4. Document it in the settings table in the User Manual.

Settings files are forward compatible: unknown properties are ignored and missing ones take their defaults. Increase `AppSettings.CurrentSchemaVersion` only if an existing property changes meaning, and migrate old values when loading.

## Coding conventions

- Follow `.editorconfig`: file-scoped namespaces, `_camelCase` private fields, braces always, `System` usings first.
- Nullable reference types are enabled everywhere. Avoid the `!` operator unless the invariant is obvious from the surrounding code.
- Use `CultureInfo.InvariantCulture` for anything machine-readable (file names, ICS, JSON) and `CurrentCulture` only for text shown to the user.
- Prefer small, pure functions in Core; keep Windows APIs in the app project.
- Comments explain *why*, not *what*.
- Keep user-facing messages short and actionable, and say what the user can do next.
- Commit messages: imperative mood, short summary line, for example `Handle "+N more" markers in agenda views`.

## Security guidelines for contributors

CalendarShot handles personal data and processes untrusted input (screenshots and the text read from them). The design principles are summarised in [SECURITY.md](../SECURITY.md). When contributing:

- **No network access.** Do not add HTTP calls, telemetry, update checks or dependencies that make them. This is a core promise to users.
- **Treat OCR text as untrusted.** It is only ever used as data: escaped in ICS output, never used to build file paths, command lines, registry values or format strings.
- **File names are fixed.** Output file names are built only from the year and month. The export folder comes from settings and is validated (`AppSettingsValidator.IsValidFolderPath`).
- **Write files atomically** through `AtomicFile`, and read files through size-limited helpers.
- **No new deserialisation features.** Keep `System.Text.Json` without polymorphic or type-name handling.
- **No elevation.** The app runs `asInvoker` and writes only to the current user's profile and registry hive (`HKCU`).
- **Do not log calendar content.** Log counts and operations, never titles or notes.
- **Starting processes:** only hand directories that exist to the shell (see `TrayApplicationContext.OpenFolder`). Never start a process with text that comes from OCR or files.
- **Dependencies:** avoid new packages. If one is really needed, it must be widely used, actively maintained and pinned through the lock file, and the pull request should explain why.

CI runs CodeQL (`security-extended` queries), a vulnerable-package check and Dependabot updates.

## Continuous integration and releases

Workflows in `.github/workflows`:

| Workflow | Trigger | What it does |
| --- | --- | --- |
| `ci.yml` | Push and pull request to `main` | Locked restore, build with analyzers, tests, vulnerable package check, publish artifact |
| `codeql.yml` | Push, pull request, weekly | CodeQL static analysis for C# |
| `release.yml` | Tag `v*.*.*` | Tests, publishes the zip and checksum, creates a GitHub release |

Release steps:

1. Update `<Version>` in `Directory.Build.props` and move the `Unreleased` entries in `CHANGELOG.md` under the new version.
2. Merge to `main` and wait for CI to pass.
3. Tag and push: `git tag v1.1.0 && git push origin v1.1.0`.
4. Check the generated release notes and the checksum on the release page.

For wider distribution, sign `CalendarShot.exe` with a code-signing certificate before zipping. This removes the SmartScreen warning and lets users verify the publisher. Signing needs a certificate that is not stored in the repository, so it is left to the maintainers' release environment.
