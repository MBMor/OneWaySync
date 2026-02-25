using Microsoft.Extensions.Logging;
using OneWaySync.CLIParser;
using OneWaySync.Logger;
using OneWaySync.Synchronizer;
using OneWaySync.Synchronizer.Helpers;

//cli - OneWaySync.exe "C:\_test\A" "C:\_test\B" 30 "C:\_test.log.txt"
try
{
    var argumentsFromCLI = InputValidator.GetCLIData(args);

    var loggerFactory = LoggerSetup.CreateLoggerFactory(argumentsFromCLI.LogFilePath!);
    var logger = loggerFactory.CreateLogger<Program>();

    var inputValidator = new InputValidator(logger);
    inputValidator.Validate(argumentsFromCLI);


    var directoryHelper = new DirectoryHelper(logger);
    var md5Helper = new Md5Helper();
    var fileOperationsHelper = new FileOperationsHelper();


    var synchronizer = new Synchronizer(
                                logger,
                                argumentsFromCLI.SourceDirectory!,
                                argumentsFromCLI.DestinationDirectory!,
                                argumentsFromCLI.SynchronizationInterval,
                                directoryHelper,
                                md5Helper,
                                fileOperationsHelper);

    synchronizer.Start();
    Console.ReadLine();
    synchronizer.Stop();
}
catch (Exception ex)

{ Console.WriteLine(ex); }

