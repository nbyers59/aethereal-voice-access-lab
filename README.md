# Aethereal Voice Access Lab — v0.4.0-rc1

Aethereal Industries | Local-first experimental Windows accessibility application.

**Status:** Release candidate. Automated results are recorded separately. This revision has not received final human interaction testing or public release acceptance.

## Features
- **Practice:** Spoken navigation and selection in an in-memory list of five sample items.
- **Notepad pilot:** Cursor navigation (up, down, left, right, start, end) only in a named, user-approved sample text document open in Microsoft Notepad.
- Offline Windows speech recognition, with a locally installed en-US recognizer.
- Closed command vocabulary, explicit permission controls, target/focus checks, minimum speech confidence, activity log, emergency stop, and permission resets.
- **Stop controls:** ESC in the app, visible STOP / REVOKE, and Ctrl+Alt+Shift+F12 when the global hotkey is available.

## Voice commands
**Practice:** Access next, Access previous, Access select, Access read, Access help, Access stop.

**Notepad:** Access line down, Access line up, Access move left, Access move right, Access line start, Access line end, Access stop.

**Stop listening** also works.

## Restrictions
Does not offer general-purpose screen-wide button clicking, arbitrary application or browser automation, text entry, saving other documents, financial account actions, or network access.

## Build and launch
Requires Windows x64, the .NET Framework 4.x C# compiler, System.Speech, and Windows UI Automation assemblies.

Run `build.cmd` in the project directory, then double-click `Launch_Voice_Access_Lab.cmd`.

No cloud service, API account, or third-party downloaded source package is required for the build on a compatible Windows installation.

## Headless test commands
After building, launch the executable with each command below. Reports are written beside the executable in `bin/`.

```text
--self-test
--notepad-self-test
--ui-smoke
--notepad-ui-smoke
--v03-ui-smoke
```

These tests are noninteractive and do not prove end-to-end microphone operation, Notepad behavior on every Windows version, or human acceptance.

## Publishing
This independent, general-purpose release copy contains only the application source, build scripts, and project documentation. Historical internal materials and prebuilt binaries remain separately preserved.

**License:** Not yet selected by Aethereal Industries. Public visibility is not an open-source license by itself.
