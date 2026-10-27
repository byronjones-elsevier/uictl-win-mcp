using System.CommandLine;
using System.CommandLine.Help;
using UICtl.Cli.Commands;
using UICtl.Core;

// Defense in depth alongside DaemonServer's own call: harmless if this process
// never calls a window/monitor API directly (today it always forwards to the
// daemon), but cheap insurance if that ever changes.
DpiAwareness.EnsurePerMonitorAware();

var root = new RootCommand(
    "Find, inspect, and drive running Windows GUI apps from the command line or from an MCP client. " +
    "Every command prints one JSON object to stdout and exits 0 on success, non-zero on failure. " +
    "A small daemon (auto-started on first use) holds the UI Automation engine and the element-id cache across invocations - see `uictl daemon status`.");

// Global CLI convention (see ~/.claude/agents/*.md's "CLI and TUI Applications"
// rule): -h, -H, --help, --HELP, -? should all show help. System.CommandLine's
// built-in HelpOption already covers -h/--help/-?/?; add the uppercase aliases.
var help = root.Options.OfType<HelpOption>().First();
help.Aliases.Add("-H");
help.Aliases.Add("--HELP");

root.Options.Add(LibsVersionOption.Build());

root.Add(QueryCommands.Permissions());
root.Add(QueryCommands.Apps());
root.Add(QueryCommands.Windows());
root.Add(QueryCommands.Displays());
root.Add(QueryCommands.Activate());
root.Add(FocusCommands.Focus());
root.Add(CaptureCommands.Screenshot());
root.Add(CaptureCommands.Elements());
root.Add(CaptureCommands.Ocr());
root.Add(CaptureCommands.Pixel());
root.Add(InputCommands.Click());
root.Add(InputCommands.Move());
root.Add(InputCommands.Scroll());
root.Add(InputCommands.Type());
root.Add(InputCommands.Key());
root.Add(MiscCommands.WaitFor());
root.Add(MiscCommands.Clipboard());
root.Add(DaemonCommands.Daemon());
root.Add(FeedbackCommands.Feedback());
root.Add(LogCommands.Log());
root.Add(McpCommand.Build());

return await root.Parse(args).InvokeAsync();
