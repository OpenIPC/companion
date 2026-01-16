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

Use semantic versioning for tags:

- `v1.0.0`: initial release
- `v1.0.1`: patch release (bug fixes)
- `v1.1.0`: minor release (backwards compatible features)
- `v2.0.0`: major release (breaking changes)

## iOS guide

https://docs.avaloniaui.net/docs/guides/platforms/ios/build-and-run-your-application-on-your-iphone-or-ipad
