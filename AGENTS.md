# mish-mash-note — agent instructions

## Edit data.json

Always read the current `data.json` before editing it — it may have changed
through the live UI ("edit data.json" button) since you last saw it. Stale
reads overwrite real edits.

Before any write, back up the current file to a single `data.json.bak` (only
one backup file — overwrite it each time):

```bat
cp data.json data.json.bak
```

This rule is mandatory because the agent has already broken it a few times
and the user was not happy at all. The prior write is NOT a read. Always
re-read, always back up, no exceptions.

## Run after edits

After any edit to source (`Program.cs`, `mish-mash-note.csproj`, `index.html`,
`data.json`), rebuild and (re)start the server so changes are live:

```bat
dotnet publish -c Release -o publish
```

Then launch `run_server.bat` (it stops any running instance first, then starts
`publish\mish-mash-note.exe` on http://localhost:9341, no console window).
Use `stop_server.bat` to kill it.

Always leave the server running at the end of a task unless told otherwise.
