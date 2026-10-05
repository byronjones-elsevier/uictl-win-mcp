# uictl

- [uictl-mac-mcp](https://github.com/ByronJones-Elsevier/uictl-mac-mcp) MacOs version
- [uictl-win-mcp](https://github.com/ByronJones-Elsevier/uictl-win-mcp) Windows version

A Windows command-line tool (and MCP server) for finding, inspecting, and
driving running GUI applications — built so a coding agent (Claude Code or
otherwise) can locate a window, screenshot it, read its UI structure, and
click/type into it without a human at the keyboard.

This is the Windows counterpart to
[`uictl-mac-mcp`](https://github.com/byronjones-elsevier/uictl-mac-mcp) (macOS). Both
implementations expose the **same CLI verbs and MCP tool names**, so an
agent's workflow doesn't change based on which OS it's driving. The exact,
binding contract both commit to is `MCP_INTERFACE.md` — read that before
implementing or extending either one.

## Status

**Feature-complete against `MCP_INTERFACE.md`, but never run.** Every layer
(`UICtl.Core`, `UICtl.Ipc`, `UICtl.Mcp`, `UICtl.Cli`) is implemented and the
solution builds clean, but all of it was written on macOS and verified only
by cross-compiling against the Windows TFM — nothing that touches Win32/COM/
WinRT has ever executed. **See `TESTING.md` before trusting any of this** —
it's the checklist for the first real Windows verification pass, including
the specific spots flagged as highest-risk while writing this blind.

## What it does

- **Find & activate** — list running processes and their windows, bring one
  to the front.
- **Screenshot** — capture a window or a whole display, optionally with a
  numbered "set-of-marks" overlay on every UI Automation element so a
  vision-capable model can say "click element 7" instead of guessing pixel
  coordinates.
- **Inspect** — walk a window's UI Automation tree: every element's control
  type, name, value, and on-screen bounding rectangle.
- **Act** — click (by coordinate or by element id), move, scroll, type
  (direct value-set or synthesized keystrokes via `SendInput`), send key
  combos.
- **Wait** — block until an element matching a role/title appears.
- **Read** — OCR a window or image region, sample a pixel's color,
  read/write the clipboard.
- **MCP server** — every capability above also exposed as an MCP tool over
  stdio, so an MCP-aware client can call `uictl_screenshot`, `uictl_click`,
  etc. directly instead of shelling out and parsing JSON.

Every CLI command prints one JSON object to stdout and exits `0`/non-zero on
success/failure, so it's easy to script or parse — identical contract to the
macOS tool.

## Build

Requires the .NET 10 SDK on Windows 10/11. From this directory:

```powershell
dotnet build -c Release
```

The binary lands at `src/UICtl.Cli/bin/Release/net10.0/uictl.exe`. Either
invoke it by that full path, or put it on `PATH`.

If you have GNU Make (e.g. via Git Bash, MSYS2, or `choco install make`), the
`Makefile` wraps the common tasks; run `make` to list them (`make build`,
`make test`, `make publish`, `make publish/win-arm64`, `make install`, `make daemon/stop`).

## Permissions model — how this differs from macOS

macOS gates Accessibility/Screen Recording behind an OS consent prompt (TCC).
Windows has no equivalent prompt; the relevant constraints instead are:

- **UIPI (User Interface Privilege Isolation)**: UI Automation calls and
  synthesized input (`SendInput`) into another process only work if that
  process isn't running at a *higher* integrity level than `uictl` itself. An
  unelevated `uictl` cannot automate an elevated app (e.g. an installer that
  triggered a UAC prompt) — run `uictl` elevated too if you need to reach
  those.
- **Screen capture** requires no explicit user consent on desktop Windows.
- `uictl permissions` reports elevation status (its own and, if a target was
  given, the target process's) rather than triggering anything — see
  `MCP_INTERFACE.md`'s `uictl_permissions` section for the exact shape.

## Architecture in one paragraph

Same shape as the macOS tool: a thin CLI/MCP front end over a small
background daemon. The first command you run auto-spawns the daemon (a
detached process listening on a named pipe, `\\.\pipe\uictl`); every
subsequent command — CLI or MCP — is a client that sends one JSON request and
gets one JSON response back. See `ENGINEERING.md` for the details, including
where this diverges from macOS (named pipe vs. Unix socket, direct
`HWND → AutomationElement` resolution vs. macOS's title+frame heuristic).

## CLI quick reference

Identical verbs to macOS — see `MCP_INTERFACE.md` for the full table:

```powershell
uictl apps                                   # list running processes
uictl displays                               # list monitors + DPI scale
uictl windows --app notepad                  # list an app's windows
uictl activate --app notepad                 # bring it to front
uictl focus hold --app notepad               # pin focus for a sequence of actions
uictl screenshot --app notepad --annotate    # numbered element overlay + legend
uictl elements --app notepad --role Button   # just the buttons
uictl click --element 12345-7                # click element 7 from that legend
uictl type --element 12345-9 "hello"         # type into a specific field
uictl type "hello"                           # type into whatever's focused
uictl key "ctrl+shift+esc"                   # keyboard shortcut
uictl wait-for --app notepad --title "Done"  # poll for an element
uictl ocr --app notepad                      # read on-screen text
uictl pixel --at 100,200                     # sample a pixel's color
uictl clipboard get / set "text"
uictl log show                               # open the live activity log window
uictl log export                             # export it as JSON
uictl feedback create --category issue --title "..." --body "..."
uictl feedback submit <id>                   # checks for duplicates, then opens a pre-filled GitHub issue
uictl daemon status / stop
```

Run `uictl --help` (also `-h`, `-H`, `--HELP`, `-?`) or `uictl <subcommand>
--help` for full option lists. HTML help (`docs/help.html`) is also
included; no man page, since a troff man page isn't idiomatic on native
Windows.

## Using it as an MCP server

`uictl mcp` will run as an MCP server over stdio — no separate flags or
config file of its own; whatever launches it just needs to point at the
binary.

**Claude Code:**

```powershell
claude mcp add uictl -- C:\path\to\uictl.exe mcp
```

**Claude Desktop / other MCP clients** that take a JSON config:

```json
{
  "mcpServers": {
    "uictl": {
      "command": "C:\\path\\to\\uictl.exe",
      "args": ["mcp"]
    }
  }
}
```

Registers the same `uictl_*` tools as the macOS implementation — see
`MCP_INTERFACE.md` for the current list.

## Recommended agent workflow

1. `apps` / `windows --app <name>` to find the target window.
2. `activate --app <name>` to bring it to front.
3. `screenshot --app <name> --annotate` — get an image plus a legend mapping
   numbers to element ids/roles/titles.
4. `click --element <id>` / `type --element <id> "..."` to act.
5. Re-run `elements` or `screenshot --annotate` to confirm the result, or
   `wait-for` if the UI update isn't instant.

See `AGENTS.md` for a more detailed walkthrough and Windows-specific
gotchas.

## See also

- `AGENTS.md` — the agent playbook.
- `ENGINEERING.md` — planned architecture and implementation notes.
- `MCP_INTERFACE.md` — the exact, binding tool/parameter contract shared
  with the macOS implementation.
- `TESTING.md` — the checklist for verifying this on a real Windows machine
  for the first time, since everything so far was written blind on macOS.
