# mish-mash-note

A tiny single-page note dashboard. A .NET (ASP.NET Core minimal API) process
serves an HTML UI over a local HTTP server; file groups scan a folder and
open notes in an in-browser editor.

## Run (Windows)

Double-click `run_server.bat` (calls `stop_server.bat` first, then launches
`publish\mish-mash-note.exe` — no console window). Open http://localhost:9341.

Rebuild after editing source: `dotnet publish -c Release -o publish`

## What it does

- **Pages** — tabs across the top, defined in `data.json`.
- **Groups** — sections on a page. Two kinds:
  - `link` — curated list of URLs (`g.links`).
  - `file` — scans `g.dir` and lists every file by name. Click a filename to
    edit it in a modal; Shift-click to open it in the OS. Click the group
    title to open the folder in Explorer. `+ add file` creates a new
    `.md`/`.txt` there.
- **Editor** — Ctrl+S saves, Esc cancels, `delete` removes the file.
- **data.json** — "edit data.json" button in the header edits the config
  live; the server reloads allowed roots on save.

## Files

| file                      | role                                    |
|---------------------------|-----------------------------------------|
| `Program.cs`              | HTTP server: list/read/write/delete files, serve UI and data.json |
| `mish-mash-note.csproj`   | .NET project (Web SDK, WinExe = no console window) |
| `index.html`              | the whole UI (HTML+CSS+JS, no deps)     |
| `data.json`               | pages, groups, and link/folder config   |
| `run_server.bat`          | stop any running instance, then start  |
| `stop_server.bat`         | kill the running server by process name |

## data.json shape

```jsonc
{
  "pages": [{ "id": "home", "title": "Home", "groups": ["notes"] }],
  "groups": {
    "notes": {
      "title": "Local notes",
      "kind": "file",          // "file" (scan dir) or "link" (use links[])
      "dir": "C:/me/notes",    // file groups only
      "max": 5,                // items per page, 0 = no paging
      "sort": "manual",        // "manual" (order field) or "alpha"
      "links": []              // link groups only
    }
  }
}
```

Single-user, localhost:9341. The path guard in `Program.cs` only prevents
accidents, not a real trust boundary.
