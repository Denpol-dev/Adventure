using Foxnet.Session;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Foxnet.Desktop.Services;

internal sealed class WindowsConsoleHost : IConsoleHost
{
    public ConsoleIdentity Launch()
    {
        string path = AppPaths.ConsolePath;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Консоль Adventure не найдена по пути «{path}».", path);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
            ArgumentList = { "--session", ExpeditionAddress.DefaultDatabasePath },
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("Windows не вернула процесс консоли.");

        if (process.WaitForExit(150))
            throw new InvalidOperationException($"Консоль закрылась сразу после запуска (код {process.ExitCode}).");

        return new ConsoleIdentity(process.Id, process.StartTime.ToUniversalTime().Ticks);
    }

    public bool IsRunning(ConsoleIdentity identity)
    {
        using var process = Find(identity);
        return process != null;
    }

    public void Stop(ConsoleIdentity identity)
    {
        using var process = Find(identity);
        if (process == null)
            return;
        try
        {
            process.Kill();
            if (!process.WaitForExit(2000))
                throw new InvalidOperationException("Консоль еще завершает работу.");
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }
    }

    private static Process? Find(ConsoleIdentity identity)
    {
        Process? process = null;
        bool match = false;
        try
        {
            process = Process.GetProcessById(identity.ProcessId);
            _ = process.SafeHandle;
            match = !process.HasExited && process.StartTime.ToUniversalTime().Ticks == identity.StartedAtUtcTicks;
            return match ? process : null;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException ||
                                           exception is Win32Exception && process?.HasExited == true)
        {
            return null;
        }
        finally
        {
            if (!match)
                process?.Dispose();
        }
    }
}
