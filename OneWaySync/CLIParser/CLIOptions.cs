using CommandLine;

namespace OneWaySync.CLIParser
{
    public class CLIOptions
    {
        [Value(0, Required = true, MetaName = "source", HelpText = "Source directory. For example: \"c:\\test\\source\"")]
        public required string SourceDirectoryPath { get; init; }

        [Value(1, Required = true, MetaName = "destination", HelpText = "Destination directory. For example: \"c:\\test\\destination\"")]
        public required string DestinationDirectoryPath { get; init; }

        [Value(2, Required = true, MetaName = "interval", HelpText = "synchronization interval in seconds. For example: 60")]
        public required int SynchronizationInterval { get; init; }

        [Value(3, Required = true, MetaName = "log", HelpText = "Log file path. For example: \"c:\\test\\log.txt\"")]
        public required string LogFilePath { get; init; }
    }
}
