using RadialShift.App.Radial;
using Xunit;

namespace RadialShift.Tests;

public class RadialGeometryTests
{
    private const double Tol = 1e-9;

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 120)]
    [InlineData(2, 3, 240)]
    [InlineData(0, 8, 0)]
    [InlineData(7, 8, 315)]
    public void CategoriaCentro_PrimeiraNoTopo(int i, int n, double esperado)
    {
        Assert.Equal(esperado, RadialGeometry.CategoryCenter(i, n), Tol);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, 3, 324)]
    [InlineData(1, 3, 0)]
    [InlineData(2, 3, 36)]
    [InlineData(0, 2, 342)]
    [InlineData(1, 2, 18)]
    [InlineData(4, 5, 72)]
    public void AcoesCentradasNaCategoria(int j, int k, double esperado)
    {
        Assert.Equal(esperado, RadialGeometry.ActionCenter(0, j, k), Tol);
    }

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, 0, 90)]
    [InlineData(0, 1, 180)]
    [InlineData(-1, 0, 270)]
    public void AnguloHorarioAPartirDoTopo(double dx, double dy, double esperado)
    {
        Assert.Equal(esperado, RadialGeometry.AngleOf(dx, dy), Tol);
    }

    [Fact]
    public void PointAt_InversoDeAngleOf()
    {
        var (x, y) = RadialGeometry.PointAt(100, 135);
        Assert.Equal(135, RadialGeometry.AngleOf(x, y), 1e-6);
        Assert.Equal(100, Math.Sqrt(x * x + y * y), 1e-6);
    }

    [Fact]
    public void HitTest_Furo()
    {
        Assert.Equal(RadialZone.Hole, RadialGeometry.HitTest(10, 10, 3, -1, 0).Zone);
    }

    [Theory]
    [InlineData(0, -80, 0)]     // topo
    [InlineData(80, 0, 1)]      // 90° → setor 1 de 3 (60°..180°)
    [InlineData(-80, 0, 2)]     // 270° → setor 2 (180°..300°)
    [InlineData(-30, -80, 0)]   // ~339° → volta para o setor 0
    public void HitTest_Categoria(double dx, double dy, int esperado)
    {
        var hit = RadialGeometry.HitTest(dx, dy, 3, -1, 0);
        Assert.Equal(RadialZone.Category, hit.Zone);
        Assert.Equal(esperado, hit.Index);
    }

    [Fact]
    public void HitTest_AcaoDaCategoriaAberta()
    {
        // Categoria 0 (topo) com 3 ações: 324°, 0°, 36°.
        var (x, y) = RadialGeometry.PointAt(140, 36);
        var hit = RadialGeometry.HitTest(x, y, 3, 0, 3);
        Assert.Equal(new RadialHit(RadialZone.Action, 2), hit);

        (x, y) = RadialGeometry.PointAt(140, 324);
        Assert.Equal(new RadialHit(RadialZone.Action, 0), RadialGeometry.HitTest(x, y, 3, 0, 3));
    }

    [Fact]
    public void HitTest_ArcoForaDasAcoes_Nada()
    {
        var (x, y) = RadialGeometry.PointAt(140, 180);
        Assert.Equal(RadialZone.None, RadialGeometry.HitTest(x, y, 3, 0, 3).Zone);
    }

    [Fact]
    public void HitTest_SemCategoriaAberta_ArcoNaoResponde()
    {
        var (x, y) = RadialGeometry.PointAt(140, 0);
        Assert.Equal(RadialZone.None, RadialGeometry.HitTest(x, y, 3, -1, 0).Zone);
    }

    [Fact]
    public void HitTest_VaoEntreAneisEForaDoAnel_Nada()
    {
        Assert.Equal(RadialZone.None, RadialGeometry.HitTest(0, -112, 3, 0, 3).Zone);
        Assert.Equal(RadialZone.None, RadialGeometry.HitTest(0, -200, 3, 0, 3).Zone);
    }

    [Fact]
    public void ClampCenter_DentroDaArea_NaoMexe()
    {
        Assert.Equal((500.0, 400.0), RadialGeometry.ClampCenter(500, 400, 174, 0, 0, 1920, 1040));
    }

    [Fact]
    public void ClampCenter_NoCanto_Desloca()
    {
        Assert.Equal((174.0, 174.0), RadialGeometry.ClampCenter(5, 5, 174, 0, 0, 1920, 1040));
        Assert.Equal((1746.0, 866.0), RadialGeometry.ClampCenter(1919, 1039, 174, 0, 0, 1920, 1040));
    }

    [Fact]
    public void ClampCenter_MonitorComOrigemNegativa()
    {
        // Monitor à esquerda do principal.
        Assert.Equal((-1746.0, 300.0), RadialGeometry.ClampCenter(-1900, 300, 174, -1920, 0, 0, 1080));
        Assert.Equal((-174.0, 300.0), RadialGeometry.ClampCenter(-10, 300, 174, -1920, 0, 0, 1080));
    }

    [Fact]
    public void ClampCenter_AreaMenorQueAnel_Centraliza()
    {
        Assert.Equal((100.0, 100.0), RadialGeometry.ClampCenter(10, 190, 174, 0, 0, 200, 200));
    }
}
