# ScreenDarkFilter

Windows x64 desktop color filter using the native Magnification API, WPF and Windows Forms.
Supports desktop/monitor filtering and foreground filtering for 1C applications.
Source includes the local icon generator under `tools/`.

## Build

Windows x64 and .NET 8 SDK:

```powershell
dotnet build ScreenDarkFilter.csproj -c Release --nologo -p:PlatformTarget=x64
```

Launch the resulting application manually and use its tray controls. Startup is opt-in.
Settings are stored under `%APPDATA%/ScreenDarkFilter/` outside the repository.
The filter can temporarily adjust font smoothing; normal shutdown restores changes.
The `--reset` option is the recovery path for clearing the native filter.

## Verification

See VALIDATION.md for build results. Native filtering, restoration on exit, multi-monitor placement,
hotkeys and application compatibility have not been tested for this publication.
No automated test suite exists; compilation does not establish visual correctness.
