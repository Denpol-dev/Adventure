using Foxnet.Session;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Foxnet.Desktop.Services;

internal static class ExternalDocument
{
    public static bool CanOpen(DocumentInfo document)
    {
        string extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        return document.Format switch
        {
            DocumentFormat.Text => extension == ".txt",
            DocumentFormat.Markdown => extension is ".md" or ".markdown",
            DocumentFormat.Json => extension == ".json",
            DocumentFormat.Pdf => extension == ".pdf",
            DocumentFormat.Image => extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp",
            _ => false
        };
    }

    public static void Open(string dataDirectory, LibraryDocument document)
    {
        if (!CanOpen(document.Info))
            throw new InvalidOperationException("Для этого файла не настроено открытие в системе.");

        string directory = Path.Combine(dataDirectory, "Documents", Convert.ToHexString(SHA256.HashData(document.Content)));
        Directory.CreateDirectory(directory);
        string name = Path.GetFileNameWithoutExtension(document.Info.FileName);
        name = new string(name.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_').Take(80).ToArray()).Trim();
        string path = Path.Combine(directory, "FOXNET-" + (name.Length == 0 ? "document" : name) +
            Path.GetExtension(document.Info.FileName).ToLowerInvariant());
        if (!File.Exists(path))
            File.WriteAllBytes(path, document.Content);
        using var process = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
