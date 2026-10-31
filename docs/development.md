# Development and release workflow

## Build

```bash
dotnet build Companion.Desktop/Companion.Desktop.csproj -c Release
```

## Run

```bash
dotnet run --project Companion.Desktop/Companion.Desktop.csproj
```

## Tests

```bash
dotnet test
```

## Code coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"TestResults/**/*.xml" -targetdir:coverage-report -reporttypes:Html
```

## Release versioning

Versioning is managed by Nerdbank.GitVersioning (`version.json`). Tags remain the source of truth for release versions.

Use semantic versioning for tags:

- `v1.0.0`: initial release
- `v1.0.1`: patch release (bug fixes)
- `v1.1.0`: minor release (backwards compatible features)
- `v2.0.0`: major release (breaking changes)

Local builds without a release tag will include git metadata in the informational version (e.g., `0.0.1+githash`).
CI builds of a branch (manual runs of `build_and_release_all.yml`) report the latest `release-v*` tag plus a dev
suffix, for example `0.9.6-dev.13+577c053`, so the in-app updater does not offer that release as an update.

## Release notes

Pushing a `release-vX.Y.Z` tag builds the release and creates a draft GitHub release. Before tagging, add curated
notes at `docs/release-notes/vX.Y.Z.md`. The workflow uses that file as the release body (followed by the download and
install instructions) and converts it to plain text for `latest.json`, which the in-app update dialog shows. Without
the file, GitHub's generated notes are used instead.

Write the notes for users: what changed and why it matters, not internal file names. Keep markdown simple (headings,
bullets, inline code), since the update dialog shows it as plain text.

## iOS guide

https://docs.avaloniaui.net/docs/guides/platforms/ios/build-and-run-your-application-on-your-iphone-or-ipad
