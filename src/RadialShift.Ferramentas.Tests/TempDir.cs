using PdfSharp.Pdf;

namespace RadialShift.Ferramentas.Tests;

/// <summary>Pasta temporária com acento e espaço no nome, apagada no fim do teste.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        $"RadialShift testes ação {Guid.NewGuid():N}");

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>Cria um PDF com <paramref name="pages"/> páginas; largura da página = <paramref name="width"/> (marca a origem).</summary>
    public string Pdf(string name, int pages = 1, double width = 300, string? password = null)
    {
        var path = File(name);
        using var doc = new PdfDocument();
        for (var i = 0; i < pages; i++)
        {
            var page = doc.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromPoint(width);
        }
        if (password is not null) doc.SecuritySettings.UserPassword = password;
        doc.Save(path);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); }
        catch (IOException) { }
    }
}
