using CommandLine;

namespace OneWaySync.CLIParser
{
    public class UserInput
    {
      public string? SourceDirectory { get; init; }
      public string? DestinationDirectory { get; init; }
      public int SynchronizationInterval { get; init; }
      public string? LogFilePath { get; init; }

        public UserInput(ParserResult<Options> cliArguments)
        {
            SourceDirectory = NormalizePath(cliArguments.Value.SourceDirectoryPath);
            DestinationDirectory = NormalizePath(cliArguments.Value.DestinationDirectoryPath);
            SynchronizationInterval = 
                cliArguments.Value.SynchronizationInterval == 0 ? 
                1 : Math.Abs(cliArguments.Value.SynchronizationInterval); //min value 1
            LogFilePath = NormalizePath(cliArguments.Value.LogFilePath);
        }

        public static UserInput GetCLIData(string[] args)
        {
            return new UserInput(Parser.Default.ParseArguments<Options>(args)); 
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

    }
}
