using Xunit;

namespace RadialShift.Ferramentas.Tests;

public class OutputNamesTests
{
    [Fact]
    public void NomeLivre_UsaODesejado()
    {
        using var dir = new TempDir();
        Assert.Equal(dir.File("a.pdf"), OutputNames.Available(dir.File("a.pdf")));
    }

    [Fact]
    public void NomeOcupado_UsaSufixo()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("a.pdf"), "x");
        File.WriteAllText(dir.File("a_1.pdf"), "x");
        Assert.Equal(dir.File("a_2.pdf"), OutputNames.Available(dir.File("a.pdf")));
    }

    [Fact]
    public void WriteNew_NaoSobrescreveENaoDeixaTemporario()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("saída.txt"), "original");

        var final = OutputNames.WriteNew(dir.File("saída.txt"), s => s.Write("novo"u8));

        Assert.Equal(dir.File("saída_1.txt"), final);
        Assert.Equal("original", File.ReadAllText(dir.File("saída.txt")));
        Assert.Equal("novo", File.ReadAllText(final));
        Assert.Equal(2, Directory.GetFiles(dir.Path).Length);
    }

    [Fact]
    public void WriteNew_FalhaNaEscrita_NaoDeixaNada()
    {
        using var dir = new TempDir();
        Assert.Throws<InvalidOperationException>(() =>
            OutputNames.WriteNew(dir.File("x.pdf"), _ => throw new InvalidOperationException()));
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void CaminhoLongo_Funciona()
    {
        using var dir = new TempDir();
        var deep = Path.Combine(dir.Path, new string('p', 120), new string('q', 120));
        Directory.CreateDirectory(deep);
        var final = OutputNames.WriteNew(Path.Combine(deep, "arquivo com espaço.txt"), s => s.Write("ok"u8));
        Assert.True(final.Length > 260);
        Assert.True(File.Exists(final));
    }
}
