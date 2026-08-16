using AdjustNamespace.Cli.CommandLine;
using Microsoft.Build.Locator;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Cli
{
    /// <summary>
    /// The entry point.
    ///
    /// Nothing here touches MSBuild: the assemblies of it are resolved by
    /// <see cref="MSBuildLocator"/> at the runtime, and a method which mentions an MSBuild type
    /// must not be compiled before the locator has been registered — this is why the whole work
    /// lives in <see cref="AdjustCommand"/>.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (!CliOptions.TryParse(args, out var options, out var error))
            {
                if (error is null)
                {
                    //the help was asked for
                    Console.Out.Write(CliOptions.Usage);

                    return (int)ExitCode.Success;
                }

                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
                Console.Error.Write(CliOptions.Usage);

                return (int)ExitCode.Error;
            }

            if (!TryRegisterMsBuild(out var registrationError))
            {
                Console.Error.WriteLine(registrationError);

                return (int)ExitCode.Error;
            }

            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                //the session answers a cancel between the files, so the process has to stay
                //alive until it has finished the file it is busy with
                e.Cancel = true;
                cancellation.Cancel();
            };

            try
            {
                var command = new AdjustCommand(options!, Console.Out, Console.Error);

                return (int)await command.RunAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");

                return (int)ExitCode.Error;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"error: {exception.Message}");

                return (int)ExitCode.Error;
            }
        }

        /// <summary>
        /// Point MSBuildWorkspace to the MSBuild of the installed .NET SDK: the projects are
        /// evaluated by the real MSBuild and not by a copy of it shipped with the utility.
        /// </summary>
        private static bool TryRegisterMsBuild(
            out string? error
            )
        {
            error = null;

            if (MSBuildLocator.IsRegistered)
            {
                return true;
            }

            try
            {
                MSBuildLocator.RegisterDefaults();

                return true;
            }
            catch (Exception exception)
            {
                error =
                    "error: no MSBuild has been found. Install the .NET SDK "
                    + $"(or Visual Studio) and try again. {exception.Message}";

                return false;
            }
        }
    }
}
