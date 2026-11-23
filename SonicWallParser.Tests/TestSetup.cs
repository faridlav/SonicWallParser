using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace SonicWallParser.Tests;

internal static class TestSetup
{
    /// <summary>The CLI sets the QuestPDF license at startup; tests that call the exporters directly must too.</summary>
    [ModuleInitializer]
    internal static void ConfigureQuestPdf()
        => QuestPDF.Settings.License = LicenseType.Community;
}
