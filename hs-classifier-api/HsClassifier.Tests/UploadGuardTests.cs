using System.Text;
using HsClassifier.API;

namespace HsClassifier.Tests;

public class UploadGuardTests
{
    private static MemoryStream Bytes(params byte[] b) => new(b);
    private static MemoryStream Text(string s) => new(Encoding.UTF8.GetBytes(s));
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];

    [Fact]
    public void Real_zip_with_docx_extension_is_accepted()
        => Assert.Null(UploadGuard.Check("btki.docx", Bytes(Zip), UploadKind.Docx));

    [Fact]
    public void Pdf_renamed_to_docx_is_refused()
    {
        var error = UploadGuard.Check("btki.docx", Text("%PDF-1.7\n..."), UploadKind.Docx);
        Assert.NotNull(error);
    }

    [Fact]
    public void Executable_renamed_to_pdf_is_refused()
        => Assert.NotNull(UploadGuard.Check("bab84.pdf", Bytes(0x4D, 0x5A, 0x90, 0x00), UploadKind.Pdf));

    [Fact]
    public void Pdf_header_is_accepted()
        => Assert.Null(UploadGuard.Check("bab84.pdf", Text("%PDF-1.4\n%âãÏÓ"), UploadKind.Pdf));

    [Fact]
    public void Extension_outside_the_allowed_kinds_is_refused()
        => Assert.NotNull(UploadGuard.Check("data.csv", Text("a,b\n1,2"), UploadKind.Docx, UploadKind.Pdf));

    [Fact]
    public void Empty_file_is_refused()
        => Assert.NotNull(UploadGuard.Check("a.docx", Bytes(), UploadKind.Docx));

    [Fact]
    public void Csv_with_a_null_byte_is_refused_and_plain_csv_is_accepted()
    {
        Assert.NotNull(UploadGuard.Check("t.csv", Bytes(0x61, 0x2C, 0x00, 0x62), UploadKind.Csv));
        Assert.Null(UploadGuard.Check("t.csv", Text("kode,uraian\n8471,Mesin"), UploadKind.Csv));
    }

    [Fact]
    public void Xlsx_needs_zip_signature()
    {
        Assert.Null(UploadGuard.Check("t.xlsx", Bytes(Zip), UploadKind.Xlsx));
        Assert.NotNull(UploadGuard.Check("t.xlsx", Text("not a zip"), UploadKind.Xlsx));
    }

    [Fact]
    public void Stream_is_left_at_the_start_for_the_parser()
    {
        var s = Bytes(Zip);
        UploadGuard.Check("a.docx", s, UploadKind.Docx);
        Assert.Equal(0, s.Position);
    }
}
