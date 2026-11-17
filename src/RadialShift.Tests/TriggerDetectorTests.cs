using RadialShift.App.Input;
using Xunit;

namespace RadialShift.Tests;

public class TriggerDetectorTests
{
    private static TriggerDetector Novo() => new(tapMaxMs: 300, cooldownMs: 500);

    [Fact]
    public void ToqueRapidoIsolado_Dispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        Assert.True(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void ShiftDireito_TambemDispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Right, false, false, 1000);
        Assert.True(d.KeyUp(ShiftKey.Right, 1050));
    }

    [Fact]
    public void ExatamenteNoLimite_Dispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        Assert.True(d.KeyUp(ShiftKey.Left, 1300));
    }

    [Fact]
    public void AcimaDoLimite_NaoDispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        Assert.False(d.KeyUp(ShiftKey.Left, 1301));
    }

    [Fact]
    public void OutraTeclaDuranteShift_NaoDispara()
    {
        // Maiúscula, Shift + setas, Ctrl+Shift+Esc (Esc no meio).
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.KeyDown(ShiftKey.None, false, false, 1050);
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void CliqueDuranteShift_NaoDispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.MouseDown();
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void ModificadorJaPressionado_NaoDispara()
    {
        // Ctrl segurado + toque no Shift (atalho de troca de idioma/layout).
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, true, 1000);
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void AutoRepeat_NaoDispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.KeyDown(ShiftKey.Left, true, false, 1030);
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void OsDoisShifts_NaoDispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.KeyDown(ShiftKey.Right, false, false, 1020);
        Assert.False(d.KeyUp(ShiftKey.Right, 1050));
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void KeyUpSemKeyDown_NaoDispara()
    {
        var d = Novo();
        Assert.False(d.KeyUp(ShiftKey.Left, 1000));
    }

    [Fact]
    public void RepeatSemDownConhecido_Ignorado()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, true, false, 1000);
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void ToqueDentroDoCooldown_NaoDispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        Assert.True(d.KeyUp(ShiftKey.Left, 1100));
        d.KeyDown(ShiftKey.Left, false, false, 1200);
        Assert.False(d.KeyUp(ShiftKey.Left, 1250));
    }

    [Fact]
    public void ToqueAposCooldown_Dispara()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        Assert.True(d.KeyUp(ShiftKey.Left, 1100));
        d.KeyDown(ShiftKey.Left, false, false, 1550);
        Assert.True(d.KeyUp(ShiftKey.Left, 1600));
    }

    [Fact]
    public void ToqueInvalido_NaoIniciaCooldown()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.MouseDown();
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
        d.KeyDown(ShiftKey.Left, false, false, 1150);
        Assert.True(d.KeyUp(ShiftKey.Left, 1200));
    }

    [Fact]
    public void InvalidacaoNaoVazaParaProximoToque()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.KeyDown(ShiftKey.None, false, false, 1010);
        Assert.False(d.KeyUp(ShiftKey.Left, 1050));
        d.KeyDown(ShiftKey.Left, false, false, 2000);
        Assert.True(d.KeyUp(ShiftKey.Left, 2100));
    }

    [Fact]
    public void TeclasECliquesForaDoShift_NaoAfetam()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.None, false, false, 900);
        d.MouseDown();
        d.KeyUp(ShiftKey.None, 950);
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        Assert.True(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Fact]
    public void Reset_DescartaToqueEmAndamento()
    {
        var d = Novo();
        d.KeyDown(ShiftKey.Left, false, false, 1000);
        d.Reset();
        Assert.False(d.KeyUp(ShiftKey.Left, 1100));
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(300, -1)]
    public void ParametrosInvalidos_Lancam(int tapMax, int cooldown)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TriggerDetector(tapMax, cooldown));
    }
}
