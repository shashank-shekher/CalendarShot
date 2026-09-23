# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-09-23

### Fixed

- Month views whose columns have different widths (for example narrower weekends in HCL Notes) no longer shift events and times into neighbouring days.
- Text from side panels next to the month grid, such as "Day at a glance" or a mini calendar, is no longer imported as events.
- Clipped titles that touch the next day's time no longer take that time with them.
- Times read without a colon ("1000 AM", "04 30 PM") or with the event's coloured edge read as a leading "1" are now recognised.
- Leftover fragments such as "00 AM" are removed from titles when the hour could not be read.

### Changed

- Calendars drawn in very small text are read at a higher resolution with an extra pass, which improves accuracy noticeably.

## [1.0.0] - 2026-09-23

### Added

- Tray application that starts with Windows and reminds the user to capture their calendar at a configurable time and on selected weekdays, with snooze and skip.
- Screen capture of the monitor under the mouse pointer after a countdown, plus import from image files and the clipboard.
- Offline text recognition with the Windows OCR engine, using two passes at different scales for better accuracy.
- Recognition of month grids, day and week views with an hour axis, and agenda or schedule lists.
- Review window to correct, add or remove events before export.
- Cumulative monthly iCalendar files with stable event IDs, and replacement of the captured date range so that removed events disappear.
- Settings, history and logs stored in `Documents\CalendarShot`.
- `CalendarShot.Inspect` developer tool for debugging recognition.
- User manual, developer guide, security policy and contribution guidelines.
