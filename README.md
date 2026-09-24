# CalendarShot

[![CI](https://github.com/shashank-shekher/CalendarShot/actions/workflows/ci.yml/badge.svg)](https://github.com/shashank-shekher/CalendarShot/actions/workflows/ci.yml)
[![CodeQL](https://github.com/shashank-shekher/CalendarShot/actions/workflows/codeql.yml/badge.svg)](https://github.com/shashank-shekher/CalendarShot/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

CalendarShot turns screenshots of a calendar into standard `.ics` files.

Some workplaces block calendar export, sharing and subscriptions. You can still see your calendar, though. CalendarShot reminds you at a fixed time each day to capture it, reads the dates and events from the screenshot, lets you check the result, and keeps one cumulative `.ics` file per month in a folder you choose. You then import those files wherever you need them.

Everything runs locally on your PC. CalendarShot makes no network connections and uses the text recognition engine that is built into Windows.

## Features

- **Daily reminder** at a time and on the weekdays you choose. If the PC was off or asleep, the reminder appears when you are back on the same day.
- **Capture whenever you want** with **Capture now** in the CalendarShot window (click the tray icon), not only at reminder time. A short countdown lets you bring the calendar to the front. You can also import an existing screenshot from a file or the clipboard.
- **Multi-monitor aware:** click the screen to capture on a numbered overlay, or set a fixed screen or the one under the mouse pointer.
- **Recognises common calendar views:** month grid, day and week views with an hour axis, and agenda or schedule lists (Outlook, Outlook on the web, Google Calendar and similar).
- **Review before export:** the screenshot and the recognised events are shown side by side so you can fix or untick anything that was misread.
- **Cumulative monthly files:** `CalendarShot-2026-09.ics`, `CalendarShot-2026-10.ics` and so on. Each capture updates the months it covers. Events that disappear from your calendar are removed from the file too.
- **Stable event IDs:** re-importing an updated file updates events instead of duplicating them in most calendar apps.
- **Starts with Windows** and runs quietly in the notification area.
- **Settings are kept in `Documents\CalendarShot`**, so they are easy to find, back up and move.

## Getting started

1. Download the latest `CalendarShot-<version>-win-x64.zip` from the [Releases](https://github.com/shashank-shekher/CalendarShot/releases) page, and check the file against the `.sha256` checksum published next to it.
2. Extract it to a permanent folder, for example `%LOCALAPPDATA%\Programs\CalendarShot`.
3. Run `CalendarShot.exe`, review the settings, and click **Save and start**.

The full walkthrough is in the [User Manual](docs/USER_MANUAL.md).

**Requirements:** Windows 10 version 2004 or later, or Windows 11 (64-bit), with at least one language installed that supports Windows text recognition. English is installed by default on most systems.

## Documentation

| Document | For |
| --- | --- |
| [User Manual](docs/USER_MANUAL.md) | Installing, configuring and using CalendarShot |
| [Developer Guide](docs/DEVELOPER_GUIDE.md) | Architecture, building, testing and extending the parser |
| [Contributing](CONTRIBUTING.md) | How to propose changes |
| [Security Policy](SECURITY.md) | Security design and how to report vulnerabilities |
| [Changelog](CHANGELOG.md) | Release history |

## Building from source

```powershell
git clone https://github.com/shashank-shekher/CalendarShot.git
cd CalendarShot
dotnet test
dotnet run --project src/CalendarShot
```

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). See the [Developer Guide](docs/DEVELOPER_GUIDE.md) for details.

## Limitations

Screenshot recognition is never perfect, which is why every import goes through the review screen. Only what is visible is captured. Events hidden behind "+2 more", truncated titles and details that only appear when you open an event are not included. The [User Manual](docs/USER_MANUAL.md#getting-the-best-results) has tips for getting the best results.

## License

CalendarShot is released under the [MIT License](LICENSE).

Please make sure that using CalendarShot is consistent with your organisation's policies on handling calendar information.
