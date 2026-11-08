using System.IO;

namespace Foxnet.Desktop.Services;

internal static class AppPaths
{
    public static string DataDirectory => Path.TrimEndingDirectorySeparator(Resolve("FOXNET_DATA_DIR",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FOXNET")));

    public static string ConsolePath => Resolve("FOXNET_CONSOLE_PATH",
        Path.Combine(AppContext.BaseDirectory, "Console", "Adventure.exe"));

    private static string Resolve(string variable, string fallback)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? fallback : path, AppContext.BaseDirectory);
    }
}
