using RadialShift.App.Shell;
using Xunit;

namespace RadialShift.Tests;

public class ExplorerSelectionTests
{
    [Fact]
    public void ResumoAgrupaExtensoesSemNomes()
    {
        var paths = new[] { @"C:\a\Relatório.PDF", @"C:\a\b.pdf", @"C:\a\nota.xml", @"C:\a\Pasta", @"C:\a\LEIAME" };
        var resumo = ExplorerSelection.SummarizeExtensions(paths, p => p.EndsWith("Pasta"));

        Assert.Equal(".pdf x2, (pasta) x1, (sem extensão) x1, .xml x1", resumo);
        Assert.DoesNotContain("Relatório", resumo);
    }

    [Fact]
    public void ResumoVazio()
    {
        Assert.Equal("", ExplorerSelection.SummarizeExtensions(Array.Empty<string>(), _ => false));
    }
}
