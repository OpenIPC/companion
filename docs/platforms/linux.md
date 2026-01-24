# Linux

## Install required dependencies
```bash
sudo apt-get install -y dotnet-sdk-8.0 dotnet-runtime-8.0
```

## Build and run
```bash
dotnet build Companion.Desktop/Companion.Desktop.csproj -c Release
dotnet run --project Companion.Desktop/Companion.Desktop.csproj
```
