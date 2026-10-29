namespace HsClassifier.Application.Interfaces;

/// <summary>Reads the official BTKI PDF tables (one chapter or the whole book) into tariff nodes.</summary>
public interface IBtkiPdfParser
{
    Task<BtkiParseResult> ParseAsync(Stream pdf, string fileName);

    /// <summary>
    /// Several chapter PDFs as one import. Files are read in name order and the checksum does not depend on
    /// upload order. The same code in two files is reported by the structure check (DUPLICATE_CODE, an error).
    /// </summary>
    Task<BtkiParseResult> ParseManyAsync(IReadOnlyList<(Stream Stream, string FileName)> files);
}
