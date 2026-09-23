# Security Policy

## Supported versions

Security fixes are made for the latest released version. Please update before reporting a problem.

## Reporting a vulnerability

Please **do not open a public issue** for security problems.

Report vulnerabilities privately through GitHub: open the repository's **Security** tab and choose **Report a vulnerability**. Include:

- the affected version,
- a description of the problem and its impact,
- steps to reproduce, with sample files if needed (do not include real calendar data).

You can expect an acknowledgement within five working days. We will keep you informed while we investigate and fix the issue, and credit you in the release notes unless you prefer otherwise. Please give us a reasonable amount of time to release a fix before disclosing the issue publicly.

## Security design

CalendarShot processes personal calendar data and untrusted input: screenshots, and the text recognised in them. The main measures are listed below. Contributors should also read the security guidelines in the [Developer Guide](docs/DEVELOPER_GUIDE.md#security-guidelines-for-contributors).

### Data stays local

- The application makes no network connections. There is no telemetry, update check or cloud OCR.
- Text recognition uses the Windows built-in OCR engine (`Windows.Media.Ocr`) on the local machine.
- Screenshots are held in memory and discarded after each capture unless the user turns on *Keep captured screenshots*.
- Log files record operations and counts only, never event titles or notes. They are deleted after 14 days.

### Least privilege

- The executable manifest requests `asInvoker`; administrator rights are never needed or requested.
- Automatic start uses the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value, containing the quoted path of the executable.
- All files are written inside the user's profile (`Documents\CalendarShot`) or the export folder the user chose.
- A session-local mutex (`Local\`) ensures a single instance without affecting other users.

### Input handling

- **Recognised text is data only.** It is never used to build file paths, command lines, registry values or format strings.
- **iCalendar output** is escaped per RFC 5545 (backslash, semicolon, comma, newlines), and control characters are removed, so text in a screenshot cannot inject properties or components. Lines are folded at 75 octets. Titles are limited to 200 characters.
- **Output file names** are fixed (`CalendarShot-YYYY-MM.ics`) and derived only from dates.
- **The export folder** must be an absolute path without wildcard or invalid characters, and device paths (`\\?\`, `\\.\`) are rejected.
- **Imported images** are limited to PNG, JPEG and BMP, 50 MB and 16,384 pixels per side, and are read into memory before decoding.
- **JSON files** (settings, state, event store) are read with size limits using `System.Text.Json` without polymorphic type handling. Unreadable files are moved aside rather than overwritten.
- **Writes are atomic** (temporary file plus rename), so an interrupted write cannot corrupt settings or exported files.
- **Opening folders** from the tray menu only passes existing directories to the Windows shell.

### Build and supply chain

- No third-party runtime dependencies. Test-only packages are pinned with NuGet lock files, and CI restores in locked mode.
- Builds are deterministic, compiler warnings are errors, and the .NET code analyzers run at the `latest-recommended` level.
- GitHub CodeQL (`security-extended`) runs on every change and weekly. CI fails if a vulnerable NuGet package is detected. Dependabot proposes dependency and workflow updates.
- Workflows run with read-only repository permissions unless a job needs more.
- Each release is published with a SHA-256 checksum. See the [User Manual](docs/USER_MANUAL.md#installing) for how to verify it.

### Known limitations

- Release binaries are not code-signed unless the maintainers add signing to their release environment, so Windows SmartScreen may warn on first start.
- Settings, event history and exported files are stored unencrypted, protected by the Windows account's file permissions like other documents. If `Documents` is synchronised (for example with OneDrive), these files are synchronised too.
- Anyone who can run programs as the same Windows user can read or change these files. This is the same trust boundary as the calendar application itself.
