using OneWaySync.Logger;
using OneWaySync.CLIParser;
using Microsoft.Extensions.Logging;
using OneWaySync.Synchronizer;

//cli - OneWaySync.exe "C:\_test\A" "C:\_test\B" 30 "C:\_test.log.txt"


var argumentsFromCLI = UserInput.GetCLIData(args);

var loggerFactory = LoggerSetup.CreateLoggerFactory(argumentsFromCLI.LogFilePath!);
var logger = loggerFactory.CreateLogger<Program>();

var inputValidator = new InputValidator(logger, argumentsFromCLI);
inputValidator.Validate();

var synchronizer = new Synchronizer(logger, argumentsFromCLI);

synchronizer.Start();

Console.ReadLine();
synchronizer.Stop();


