namespace HsClassifier.API;

public enum UploadKind { Docx, Pdf, Xlsx, Csv }

/// <summary>
/// Checks an uploaded file before any parser touches it: extension is one of the allowed kinds and the first
/// bytes match that kind. The client-sent Content-Type is not trusted, so it is not used.
/// ponytail: this only proves the file starts like the claimed type. It is not a malware scan and does not
/// prove the ZIP is a valid Word/Excel package; the parser still has to reject a broken one.
/// </summary>
public static class UploadGuard
{
    private static readonly Dictionary<string, UploadKind> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".docx"] = UploadKind.Docx, [".pdf"] = UploadKind.Pdf, [".xlsx"] = UploadKind.Xlsx, [".csv"] = UploadKind.Csv,
    };

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    /// <summary>Error message in Indonesian, or null when the file is acceptable. Leaves the stream at position 0.</summary>
    public static string? Check(string fileName, Stream stream, params UploadKind[] allowed)
    {
        if (!Extensions.TryGetValue(Path.GetExtension(fileName), out var kind) || !allowed.Contains(kind))
            return $"Tipe file tidak diizinkan. Yang diterima: {string.Join(", ", allowed.Select(a => "." + a.ToString().ToLowerInvariant()))}.";

        var head = new byte[8192];
        stream.Position = 0;
        var read = stream.Read(head, 0, head.Length);
        stream.Position = 0;
        if (read == 0) return "File kosong.";

        var ok = kind switch
        {
            UploadKind.Docx or UploadKind.Xlsx => read >= 4 && head.AsSpan(0, 4).SequenceEqual(ZipSignature),
            // the PDF spec allows junk before the header, but only within the first 1024 bytes
            UploadKind.Pdf => head.AsSpan(0, Math.Min(read, 1024)).IndexOf(PdfSignature) >= 0,
            UploadKind.Csv => !head.AsSpan(0, read).Contains((byte)0),
            _ => false,
        };
        return ok ? null : $"Isi file tidak sesuai dengan ekstensi {Path.GetExtension(fileName).ToLowerInvariant()}.";
    }

    /// <summary>Same check for a form file.</summary>
    public static string? Check(IFormFile file, params UploadKind[] allowed)
    {
        using var s = file.OpenReadStream();
        return Check(file.FileName, s, allowed);
    }
}
