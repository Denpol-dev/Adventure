using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace Foxnet.Desktop.ViewModels;

public abstract partial class ScreenViewModel : ObservableObject
{
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _notice;

    protected void Run(Action action)
    {
        Error = null;
        Notice = null;
        try
        {
            action();
        }
        catch (Exception exception) when (IsDisplayable(exception))
        {
            Error = Explain(exception);
        }
    }

    protected async Task RunAsync(Func<Task> action)
    {
        Error = null;
        Notice = null;
        try
        {
            await action();
        }
        catch (Exception exception) when (IsDisplayable(exception))
        {
            Error = Explain(exception);
        }
    }

    internal static bool IsDisplayable(Exception exception) =>
        exception is IOException or InvalidDataException or SqliteException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException
            or FormatException or System.Text.Json.JsonException or System.Runtime.InteropServices.COMException;

    internal static string Explain(Exception exception) => exception switch
    {
        SqliteException { SqliteErrorCode: 11 or 26 } => "Локальная база сессии повреждена. Существующие данные оставлены на диске.",
        SqliteException => "Не удалось обратиться к локальной базе сессии. " + exception.Message,
        DecoderFallbackException => "Не удалось прочитать кодировку текстового документа.",
        FileFormatException or NotSupportedException => "Формат файла повреждён или не поддерживается.",
        _ => exception.Message
    };
}
