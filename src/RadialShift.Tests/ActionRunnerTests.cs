using System.Diagnostics;
using System.Text.Json;
using PdfSharp.Pdf;
using RadialShift.App.Actions;
using RadialShift.App.Radial;
using Xunit;

namespace RadialShift.Tests;

/// <summary>Ponta a ponta: ActionRunner → RadialShift.Ferramentas.exe real → JSON de volta.</summary>
public sealed class ActionRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"RadialShift runner {Guid.NewGuid():N}");
    private readonly string _tmp;
    private readonly ActionRunner _runner;

    public ActionRunnerTests()
    {
        Directory.CreateDirectory(_dir);
        _tmp = Path.Combine(_dir, "tmp");
        _runner = new ActionRunner(Path.Combine(AppContext.BaseDirectory, "RadialShift.Ferramentas.exe"), _tmp,
            Array.Empty<IInternalAction>());
    }

    private string Pdf(string name)
    {
        var path = Path.Combine(_dir, name);
        using var doc = new PdfDocument();
        doc.AddPage();
        doc.Save(path);
        return path;
    }

    [Fact]
    public async Task Juntar_PontaAPonta()
    {
        var action = new RadialAction("juntar_pdf", "Juntar", "ferramentas", 60);
        var result = await _runner.RunAsync(action, new[] { Pdf("ação 1.pdf"), Pdf("ação 2.pdf") });

        Assert.Equal(ActionStatus.Success, result.Status);
        var saida = Assert.Single(result.Outputs);
        Assert.True(File.Exists(saida));
        Assert.Empty(Directory.GetFiles(_tmp)); // JSON temporário apagado
    }

    [Fact]
    public async Task ErroDeNegocio_Propagado()
    {
        var action = new RadialAction("juntar_pdf", "Juntar", "ferramentas", 60);
        var result = await _runner.RunAsync(action, new[] { Pdf("só um.pdf") });
        Assert.Equal(ActionStatus.BusinessError, result.Status);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public async Task Timeout_MataOProcessoFilho()
    {
        var antes = Process.GetProcessesByName("RadialShift.Ferramentas").Select(p => p.Id).ToHashSet();
        var action = new RadialAction("_teste_espera", "Espera", "ferramentas", 1);
        var parametros = new Dictionary<string, JsonElement> { ["segundos"] = JsonSerializer.SerializeToElement(30) };

        var sw = Stopwatch.StartNew();
        var result = await _runner.RunAsync(action, Array.Empty<string>(), parametros);

        Assert.Equal(ActionStatus.Timeout, result.Status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10));
        var sobrando = Process.GetProcessesByName("RadialShift.Ferramentas").Where(p => !antes.Contains(p.Id));
        Assert.Empty(sobrando);
        Assert.Empty(Directory.GetFiles(_tmp));
    }

    [Fact]
    public async Task ExecutorAusente_ErroClaro()
    {
        var runner = new ActionRunner(Path.Combine(_dir, "nao-existe.exe"), _tmp, Array.Empty<IInternalAction>());
        var result = await runner.RunAsync(new RadialAction("juntar_pdf", "Juntar", "ferramentas", 5), new[] { "x" });
        Assert.Equal(ActionStatus.UnexpectedError, result.Status);
        Assert.Equal("ExecutorAusente", result.ErrorType);
    }

    [Fact]
    public async Task NoMaximoDuasEmParalelo()
    {
        var gate = new TaskCompletionSource();
        var ativos = 0;
        var pico = 0;
        var interna = new FakeInternal(async () =>
        {
            var n = Interlocked.Increment(ref ativos);
            InterlockedMax(ref pico, n);
            await gate.Task;
            Interlocked.Decrement(ref ativos);
        });
        var runner = new ActionRunner("x", _tmp, new IInternalAction[] { interna });
        var action = new RadialAction("fake", "Fake", "interno", 0);

        var tarefas = Enumerable.Range(0, 5).Select(_ => runner.RunAsync(action, new[] { "a" })).ToArray();
        await Task.Delay(200);
        Assert.Equal(2, Volatile.Read(ref ativos));
        gate.SetResult();
        await Task.WhenAll(tarefas);
        Assert.Equal(ActionRunner.MaxParallel, pico);
    }

    [Fact]
    public void Interpret_SaidaInvalida()
    {
        var r = ActionRunner.Interpret("lixo", 0, TimeSpan.Zero);
        Assert.Equal(ActionStatus.UnexpectedError, r.Status);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    private sealed class FakeInternal(Func<Task> body) : IInternalAction
    {
        public string Id => "fake";

        public async Task<ActionResult> RunAsync(IReadOnlyList<string> files)
        {
            await body();
            return new ActionResult(ActionStatus.Success, Array.Empty<string>(), "ok",
                Array.Empty<RadialShift.Ferramentas.ToolError>(), TimeSpan.Zero);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }
}
