using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdjustNamespace.Cli.CommandLine
{
    /// <summary>
    /// What the user has asked for on the command line.
    /// </summary>
    public sealed class CliOptions
    {
        /// <summary>
        /// Full path to the solution (<c>.sln</c>, <c>.slnx</c>) or to the project
        /// (<c>.csproj</c>) to work with.
        /// </summary>
        public string SolutionPath
        {
            get;
        }

        /// <summary>
        /// Files and folders the adjusting is limited to. An empty list means the whole solution.
        /// </summary>
        public IReadOnlyList<string> Paths
        {
            get;
        }

        /// <summary>
        /// User defined regex which additionally modifies every target namespace.
        /// </summary>
        public NamespaceReplaceRegex ReplaceRegex
        {
            get;
        }

        /// <summary>
        /// Report the files which have to be adjusted and change nothing.
        /// </summary>
        public bool DryRun
        {
            get;
        }

        /// <summary>
        /// Same as <see cref="DryRun"/>, but the exit code says whether there is something
        /// to adjust. This is the mode a CI job runs the utility in.
        /// </summary>
        public bool Check
        {
            get;
        }

        /// <summary>
        /// Adjust even if the solution does not compile. The adjusting is based on the semantic
        /// model, so the result over a broken solution may be incorrect.
        /// </summary>
        public bool Force
        {
            get;
        }

        /// <summary>
        /// Report every processed file and not the changed ones only.
        /// </summary>
        public bool Verbose
        {
            get;
        }

        /// <summary>
        /// Write the detailed <c>[Adjust]</c> diagnostics into
        /// <c>%TEMP%\AdjustNamespace.cli.log</c> (and mention the path on the console).
        /// </summary>
        public bool Debug
        {
            get;
        }

        private CliOptions(
            string solutionPath,
            IReadOnlyList<string> paths,
            NamespaceReplaceRegex replaceRegex,
            bool dryRun,
            bool check,
            bool force,
            bool verbose,
            bool debug
            )
        {
            SolutionPath = solutionPath;
            Paths = paths;
            ReplaceRegex = replaceRegex;
            DryRun = dryRun;
            Check = check;
            Force = force;
            Verbose = verbose;
            Debug = debug;
        }

        /// <summary>
        /// The help text, which is also the description of everything this class understands.
        /// </summary>
        public const string Usage = @"adjustns - bring the C# namespaces in accordance with the location of the files.

Usage:
  adjustns <solution> [options]

  <solution>                 Path to a .sln, .slnx or .csproj file. A folder is accepted too
                             if it contains exactly one of them.

Options:
  -p, --path <path>          Adjust this file, or the files under this folder, only.
                             May be repeated; the whole solution is taken if it is omitted.
      --regex <regex>        Regex applied to every target namespace (^[^.]+, for example).
      --replacement <text>   What the match of the regex is replaced with. The regex is
                             applied only if both of them are given.
  -n, --dry-run              Report what would be changed and change nothing.
      --check                Change nothing and exit with the code 2 if there is something
                             to adjust (for a CI job).
      --force                Adjust even if the solution does not compile.
  -v, --verbose              Report every processed file.
      --debug                Write the detailed [Adjust] diagnostics into
                             %TEMP%\AdjustNamespace.cli.log.
  -h, --help                 Show this text.

The folders which must not take a part in the namespaces are read from
adjust_namespaces_settings.xml of the solution folder, exactly as the Visual Studio
extension reads them.

Exit codes:
  0   done (or nothing to do)
  1   the utility has failed, or at least one file cannot be adjusted
  2   --check has found the files to adjust (and none is blocked)
";

        /// <summary>
        /// Parse the command line.
        /// </summary>
        /// <param name="args">The arguments as they came to <c>Main</c>.</param>
        /// <param name="options">(out) The parsed options, or <c>null</c> if the parsing failed
        /// or the help was asked for.</param>
        /// <param name="error">(out) The reason the parsing failed, or <c>null</c> if the user
        /// has asked for the help.</param>
        /// <returns><c>false</c> if there is nothing to run.</returns>
        public static bool TryParse(
            string[] args,
            out CliOptions? options,
            out string? error
            )
        {
            options = null;
            error = null;

            string? solutionArgument = null;
            var paths = new List<string>();
            var regex = string.Empty;
            var replacement = string.Empty;
            var dryRun = false;
            var check = false;
            var force = false;
            var verbose = false;
            var debug = false;

            for (var i = 0; i < args.Length; i++)
            {
                var argument = args[i];

                switch (argument)
                {
                    case "-h":
                    case "--help":
                        return false;

                    case "-n":
                    case "--dry-run":
                        dryRun = true;
                        break;

                    case "--check":
                        check = true;
                        break;

                    case "--force":
                        force = true;
                        break;

                    case "-v":
                    case "--verbose":
                        verbose = true;
                        break;

                    case "--debug":
                        debug = true;
                        break;

                    case "-p":
                    case "--path":
                        if (!TryTakeValue(args, ref i, out var path))
                        {
                            error = $"The option {argument} requires a value.";
                            return false;
                        }

                        paths.Add(Path.GetFullPath(path!));
                        break;

                    case "--regex":
                        if (!TryTakeValue(args, ref i, out var regexValue))
                        {
                            error = $"The option {argument} requires a value.";
                            return false;
                        }

                        regex = regexValue!;
                        break;

                    case "--replacement":
                        if (!TryTakeValue(args, ref i, out var replacementValue))
                        {
                            error = $"The option {argument} requires a value.";
                            return false;
                        }

                        replacement = replacementValue!;
                        break;

                    default:
                        if (argument.StartsWith("-"))
                        {
                            error = $"Unknown option {argument}.";
                            return false;
                        }

                        if (solutionArgument != null)
                        {
                            error = "More than one solution is given.";
                            return false;
                        }

                        solutionArgument = argument;
                        break;
                }
            }

            if (solutionArgument is null)
            {
                error = "A solution (.sln, .slnx) or a project (.csproj) is required.";
                return false;
            }

            if (!TryResolveSolutionPath(solutionArgument, out var solutionPath, out error))
            {
                return false;
            }

            options = new CliOptions(
                solutionPath!,
                paths,
                new NamespaceReplaceRegex(regex, replacement),
                dryRun,
                check,
                force,
                verbose,
                debug
                );

            return true;
        }

        private static bool TryTakeValue(
            string[] args,
            ref int i,
            out string? value
            )
        {
            if (i + 1 >= args.Length)
            {
                value = null;
                return false;
            }

            value = args[++i];
            return true;
        }

        /// <summary>
        /// The solution to work with: the given file, or the single solution/project of the
        /// given folder.
        /// </summary>
        private static bool TryResolveSolutionPath(
            string argument,
            out string? solutionPath,
            out string? error
            )
        {
            solutionPath = null;
            error = null;

            var fullPath = Path.GetFullPath(argument);

            if (File.Exists(fullPath))
            {
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (extension != ".sln" && extension != ".slnx" && extension != ".csproj")
                {
                    error = $"{fullPath} is neither a .sln, nor a .slnx, nor a .csproj file.";
                    return false;
                }

                solutionPath = fullPath;
                return true;
            }

            if (!Directory.Exists(fullPath))
            {
                error = $"{fullPath} does not exist.";
                return false;
            }

            //a folder is accepted as soon as it is unambiguous: the solution first,
            //the project only if there is no solution at all
            var found = Directory.GetFiles(fullPath, "*.slnx")
                .Concat(Directory.GetFiles(fullPath, "*.sln"))
                .ToList();
            if (found.Count == 0)
            {
                found = Directory.GetFiles(fullPath, "*.csproj").ToList();
            }

            if (found.Count == 0)
            {
                error = $"There is no .sln, .slnx or .csproj file in {fullPath}.";
                return false;
            }

            if (found.Count > 1)
            {
                error = $"There is more than one solution in {fullPath}, please name the one to adjust.";
                return false;
            }

            solutionPath = found[0];
            return true;
        }
    }
}
