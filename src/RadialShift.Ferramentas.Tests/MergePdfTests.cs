using PdfSharp.Pdf.IO;
using RadialShift.Ferramentas.Actions;
using Xunit;

namespace RadialShift.Ferramentas.Tests;

public class MergePdfTests
{
    private static readonly DateTime Agora = new(2026, 10, 24, 9, 5, 0);

    [Fact]
    public void JuntaEmOrdemNatural()
    {
        using var dir = new TempDir();
        var p10 = dir.Pdf("parte10.pdf", pages: 1, width: 310);
        var p2 = dir.Pdf("parte2.pdf", pages: 2, width: 302);
        var p1 = dir.Pdf("parte1.pdf", pages: 1, width: 301);

        var r = MergePdf.Run(new[] { p10, p2, p1 }, Agora);

        var saida = Assert.Single(r.Outputs);
        Assert.Equal(dir.File("juntado_2026-10-24_0905.pdf"), saida);
        Assert.Empty(r.Errors);
        using var doc = PdfReader.Open(saida, PdfDocumentOpenMode.Import);
        var larguras = doc.Pages.Cast<PdfSharp.Pdf.PdfPage>().Select(p => (int)p.Width.Point).ToArray();
        Assert.Equal(new[] { 301, 302, 302, 310 }, larguras);
    }

    [Fact]
    public void NaoSobrescreveJuntadoExistente()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("juntado_2026-10-24_0905.pdf"), "existente");
        var r = MergePdf.Run(new[] { dir.Pdf("a.pdf"), dir.Pdf("b.pdf") }, Agora);
        Assert.Equal(dir.File("juntado_2026-10-24_0905_1.pdf"), Assert.Single(r.Outputs));
        Assert.Equal("existente", File.ReadAllText(dir.File("juntado_2026-10-24_0905.pdf")));
    }

    [Fact]
    public void ProtegidoPorSenha_ViraErroDoItemESegue()
    {
        using var dir = new TempDir();
        var a = dir.Pdf("a.pdf");
        var b = dir.Pdf("b.pdf");
        var c = dir.Pdf("c.pdf", password: "segredo123");

        var r = MergePdf.Run(new[] { a, b, c }, Agora);

        Assert.Single(r.Outputs);
        var erro = Assert.Single(r.Errors);
        Assert.Equal(c, erro.Arquivo);
        Assert.Equal("PDF protegido por senha", erro.Motivo);
    }

    [Fact]
    public void ArquivoCorrompido_ViraErroDoItem()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("ruim.pdf"), "não é pdf");
        var r = MergePdf.Run(new[] { dir.Pdf("a.pdf"), dir.Pdf("b.pdf"), dir.File("ruim.pdf") }, Agora);
        Assert.Equal("PDF inválido ou corrompido", Assert.Single(r.Errors).Motivo);
    }

    [Fact]
    public void MenosDeDoisValidos_ErroDeNegocio()
    {
        using var dir = new TempDir();
        var ex = Assert.Throws<BusinessException>(() =>
            MergePdf.Run(new[] { dir.Pdf("a.pdf"), dir.Pdf("b.pdf", password: "segredo123") }, Agora));
        Assert.Single(ex.Errors);
        Assert.Empty(Directory.GetFiles(dir.Path, "juntado_*"));
    }

    [Fact]
    public void UmSoArquivo_ErroDeNegocio()
    {
        using var dir = new TempDir();
        Assert.Throws<BusinessException>(() => MergePdf.Run(new[] { dir.Pdf("a.pdf") }, Agora));
    }
}
