using Microsoft.Extensions.Logging;
using OneWaySync.CLIParser;
using OneWaySync.GlobalHelpers;
using OneWaySync.Logger;
using OneWaySync.Synchronizer;
using OneWaySync.Synchronizer.Helpers;

//cli - OneWaySync.exe "C:\_test\A" "C:\_test\B" 30 "C:\_test.log.txt"
try
{
    var fileSystemHelper = new FileSystem();
    var pathService = new PathService();

    var logPath = args[3];

    fileSystemHelper.FilePathAllowsCreateOrUseFile(logPath);
    var logger = LoggerSetup.CreateLoggerFactory(logPath).CreateLogger<Program>();

    var md5Helper = new Md5Helper();
    var directoryHelper = new DirectoryScaner(logger, fileSystemHelper, pathService);

    var inputValidator = new InputValidator(logger, fileSystemHelper, pathService);
    var argumentsFromCLI = inputValidator.GetCLIData(args);
    inputValidator.Validate(argumentsFromCLI);


    var synchronizationProcessor = new SynchronizationProcessor(
                                logger,
                                argumentsFromCLI.SourceDirectory!,
                                argumentsFromCLI.DestinationDirectory!,
                                directoryHelper,
                                md5Helper,
                                fileSystemHelper,
                                pathService);

    var synchronizer = new Synchronizer(logger, synchronizationProcessor, argumentsFromCLI.SynchronizationInterval);

    synchronizer.Start();
    Console.ReadLine();
    synchronizer.Stop();
}
catch (Exception ex)

{ Console.WriteLine(ex); }

