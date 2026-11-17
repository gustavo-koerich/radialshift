using Xunit;

namespace RadialShift.Ferramentas.Tests;

public class NaturalComparerTests
{
    [Fact]
    public void OrdenaNumerosPeloValor()
    {
        var nomes = new[] { "arquivo10.pdf", "arquivo2.pdf", "Arquivo1.pdf", "arquivo02b.pdf", "b.pdf", "a.pdf" };
        var ordenado = nomes.OrderBy(n => n, NaturalComparer.Instance).ToArray();
        Assert.Equal(new[] { "a.pdf", "Arquivo1.pdf", "arquivo2.pdf", "arquivo02b.pdf", "arquivo10.pdf", "b.pdf" }, ordenado);
    }

    [Theory]
    [InlineData("x", "x", 0)]
    [InlineData("x", "X", 0)]
    [InlineData("x1", "x01", 0)]
    [InlineData("abc", "abcd", -1)]
    [InlineData("parte 9", "parte 10", -1)]
    public void Compara(string a, string b, int sinal)
    {
        Assert.Equal(sinal, Math.Sign(NaturalComparer.Instance.Compare(a, b)));
        Assert.Equal(-sinal, Math.Sign(NaturalComparer.Instance.Compare(b, a)));
    }
}
