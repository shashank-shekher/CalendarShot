# Contributing to CalendarShot

Thanks for your interest in improving CalendarShot. Bug reports, recognition improvements for more calendar layouts, documentation fixes and translations are all welcome.

By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- **Bugs:** search the existing issues first. If it is new, open an issue using the bug template.
- **Features and larger changes:** open an issue to discuss the idea before writing code, so that effort is not wasted on something that does not fit the project.
- **Security issues:** do not open a public issue. Follow [SECURITY.md](SECURITY.md).
- **Privacy:** never attach screenshots of a real calendar. See [Reporting recognition problems](docs/DEVELOPER_GUIDE.md#reporting-recognition-problems) for how to share an anonymised OCR dump instead.

## Development workflow

1. Fork the repository and create a branch from `main`, for example `fix/agenda-relative-dates`.
2. Set up your environment as described in the [Developer Guide](docs/DEVELOPER_GUIDE.md#building-and-running).
3. Make your change in small, focused commits.
4. Add or update tests. Parser fixes need a test that reproduces the problem.
5. Run the full build and tests locally:

   ```powershell
   dotnet build
   dotnet test
   ```

6. Update `CHANGELOG.md` under **Unreleased** and, for user-visible changes, `docs/USER_MANUAL.md`.
7. Open a pull request and fill in the template.

## Pull request expectations

- The build passes with no warnings. Warnings are treated as errors.
- All tests pass, and new behaviour has tests.
- The change follows the [coding conventions](docs/DEVELOPER_GUIDE.md#coding-conventions) and the [security guidelines](docs/DEVELOPER_GUIDE.md#security-guidelines-for-contributors). In particular, no network access and no logging of calendar content.
- One logical change per pull request. Refactoring and behaviour changes are easier to review separately.
- New dependencies need a clear justification in the pull request description.

A maintainer will review your pull request, usually within a week. Please respond to review comments by pushing new commits; they will be squashed when merging.

## Licensing

By contributing, you agree that your contributions are licensed under the project's [MIT License](LICENSE).
