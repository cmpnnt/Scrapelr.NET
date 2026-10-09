using System.Diagnostics;

namespace Scrapelr.NET.Tests.Helpers;

/// <summary>
/// Runs the built Scrapelr.NET executable, which the project reference copies next to the tests.
/// </summary>
internal static class AppRunner
{
    public static async Task<(int ExitCode, string Output)> Run(string workingDirectory, params string[] args)
    {
        string app = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Scrapelr.NET.exe" : "Scrapelr.NET");
        var startInfo = new ProcessStartInfo(app, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(startInfo)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdout + await stderr);
    }
}
