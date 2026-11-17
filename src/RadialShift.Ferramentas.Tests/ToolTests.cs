using Xunit;

namespace RadialShift.Ferramentas.Tests;

public class ToolTests
{
    [Fact]
    public void AcaoDesconhecida_CodigoDeNegocio()
    {
        var (output, code) = Tool.Run(new ToolInput("nao_existe", Array.Empty<string>(), null), TextReader.Null);
        Assert.Equal(ExitCodes.BusinessError, code);
        Assert.False(output.Ok);
    }

    [Fact]
    public void Juntar_Sucesso()
    {
        using var dir = new TempDir();
        var (output, code) = Tool.Run(
            new ToolInput("juntar_pdf", new[] { dir.Pdf("a.pdf"), dir.Pdf("b.pdf") }, null), TextReader.Null);
        Assert.Equal(ExitCodes.Success, code);
        Assert.True(output.Ok);
        Assert.Single(output.Saidas);
        Assert.Equal("2 PDFs juntados", output.Mensagem);
    }

    [Fact]
    public void MensagemDeErroInesperadoNaoTemCaminho()
    {
        // Arquivo inexistente com extensão .pdf cai como erro do item, não exceção; força erro geral com pasta inexistente.
        var (output, code) = Tool.Run(
            new ToolInput("juntar_pdf", new[] { @"Z:\nao\existe\a.pdf", @"Z:\nao\existe\b.pdf" }, null), TextReader.Null);
        Assert.Equal(ExitCodes.BusinessError, code);
        Assert.DoesNotContain(@"Z:\", output.Mensagem);
        Assert.All(output.Erros, e => Assert.Equal("Arquivo não encontrado", e.Motivo));
    }
}
