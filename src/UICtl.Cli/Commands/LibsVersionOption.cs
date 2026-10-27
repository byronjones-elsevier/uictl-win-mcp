using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyModel;
using UICtl.Ipc;

namespace UICtl.Cli.Commands;

/// <summary>
/// <c>--libs-version</c>: report the libraries this binary was built with,
/// read from the deps.json that DependencyContext resolves (the embedded copy
/// in a single-file publish, the sidecar file otherwise). Handled in-process;
/// no daemon round trip.
/// </summary>
internal static class LibsVersionOption
{
    public static Option<bool> Build() => new("--libs-version")
    {
        Description = "Print the libraries (name, version) this binary was built with, then exit.",
        Action = new LibsVersionAction(),
    };

    private sealed class LibsVersionAction : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            var context = DependencyContext.Default;
            if (context is null)
                return CliRunner.Print(Envelope.Failure("no dependency manifest (deps.json) available for this binary"));

            var libraries = context.RuntimeLibraries
                .Where(l => l.Type is "package" or "referenceassembly")
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .Select(l => new { name = l.Name, version = l.Version, type = l.Type })
                .ToList();

            return CliRunner.Print(Envelope.Success(new { libraries }));
        }
    }
}
