using QuestPDF.Infrastructure;
using SonicWallParser.Cli;

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"\n✗ Fatal unhandled exception: {e.ExceptionObject}");
    Environment.Exit(ExitCodes.Fatal);
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine($"\n✗ Unobserved task exception: {e.Exception?.Message}");
    e.SetObserved();
};

QuestPDF.Settings.License = LicenseType.Community;

return CliApp.Run(args, Console.Out, Console.Error);
