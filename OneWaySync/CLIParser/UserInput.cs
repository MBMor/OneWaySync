namespace OneWaySync.CLIParser
{
    public class UserInput
    {
      public string? SourceDirectory { get; init; }
      public string? DestinationDirectory { get; init; }
      public int SynchronizationInterval { get; init; }
      public string? LogFilePath { get; init; }

    }
}
