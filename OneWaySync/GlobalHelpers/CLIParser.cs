using CommandLine;
using OneWaySync.CLIParser;

namespace OneWaySync.GlobalHelpers
{
    public interface ICLIParser
    {
        UserInput Parse(string[] args);
    }

    public class CLIParser : ICLIParser
    {
        private const int MinimalSyncInterval = 1;
        public UserInput Parse(string[] args)
        {
            var result = Parser.Default.ParseArguments<CLIOptions>(args);

            return result.MapResult(
                cliOptions => new UserInput
                {
                    SourceDirectory = cliOptions.SourceDirectoryPath,
                    DestinationDirectory = cliOptions.DestinationDirectoryPath,
                    SynchronizationInterval = GuardSyncInterval(cliOptions.SynchronizationInterval),
                    LogFilePath = cliOptions.LogFilePath
                },
                erros => throw new ArgumentException("Invalid CLI arguments")
            );
        }

        private static int GuardSyncInterval(int interval)
        {
            if (interval == 0)
                return MinimalSyncInterval; 

            if (interval == int.MinValue) //Int overflow guard
                return int.MaxValue;

            return Math.Abs(interval);
        }
    }
}
