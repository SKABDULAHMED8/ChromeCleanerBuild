# Chrome Cache Cleaner

Offline Windows GUI utility.

## Build

Open PowerShell in this folder:

```powershell
.\build.ps1
```

The EXE will be:

`publish\ChromeCacheCleaner.exe`

## Safety

- Detects running Chrome.
- Does not force-close Chrome.
- Cache cleanup targets cache directories only.
- Local Storage is a separate optional action and is OFF by default.
- Does not intentionally target bookmarks, history, passwords, cookies, extensions, or Chrome preferences.
- The program does not require an Internet connection.

## Important

Close Chrome before cleaning. If Chrome is running, the Clean button remains disabled until Chrome is closed.
