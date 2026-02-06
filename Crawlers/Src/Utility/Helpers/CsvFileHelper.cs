using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Crawlers.Src.Utility.Helpers;

public static class CsvFileHelper
{
    /// <summary>
    /// Create a FileStreamResult for the given CSV lines and filename.
    /// </summary>
    public static async Task<FileStreamResult> CreateFileStreamResultAsync(IEnumerable<string> lines, string fileName, string contentType = "text/csv")
    {
        var memory = await BuildCsvMemoryStreamAsync(lines);
        return new FileStreamResult(memory, contentType) { FileDownloadName = fileName };
    }

    /// <summary>
    /// Build a CSV file from provided lines, write to a temporary file, load into a MemoryStream and delete the temp file.
    /// Returns a MemoryStream positioned at 0.
    /// </summary>
    public static async Task<MemoryStream> BuildCsvMemoryStreamAsync(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }

        var tempPath = Path.GetTempPath();
        var csvFilePath = Path.Combine(tempPath, $"csv_{Guid.NewGuid()}.csv");

        await File.WriteAllTextAsync(csvFilePath, sb.ToString(), Encoding.UTF8);

        var memory = new MemoryStream();
        using (var stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await stream.CopyToAsync(memory);
        }

        memory.Position = 0;

        try
        {
            File.Delete(csvFilePath); //// ßR∞£º»¶s¿…
        }
        catch
        {
            // best-effort cleanup
        }

        return memory;
    }
}
