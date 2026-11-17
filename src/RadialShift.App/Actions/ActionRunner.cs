using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RadialShift.App.Radial;
using RadialShift.Ferramentas;

namespace RadialShift.App.Actions;

/// <summary>Ação implementada dentro do app (executor "interno"), sem processo filho.</summary>
public interface IInternalAction
{
    string Id { get; }
    Task<ActionResult> RunAsync(IReadOnlyList<string> files);
}

/// <summary>
/// Executa ações (seção 8 da spec): no máximo 2 em paralelo, o resto em fila.
/// Executor "ferramentas" roda RadialShift.Ferramentas.exe com o contrato JSON da seção 7;
/// ao estourar o timeout, mata a árvore de processos.
/// </summary>
public sealed class ActionRunner
{
    public const int MaxParallel = 2;

    private readonly SemaphoreSlim _slots = new(MaxParallel, MaxParallel);
    private readonly string _toolExe;
    private readonly string _tmpDir;
    private readonly Dictionary<string, IInternalAction> _internal;

    public ActionRunner(string toolExe, string tmpDir, IEnumerable<IInternalAction> internalActions)
    {
        _toolExe = toolExe;
        _tmpDir = tmpDir;
        _internal = internalActions.ToDictionary(a => a.Id);
    }

    /// <param name="parameters">Parâmetros não sensíveis (vão no JSON temporário).</param>
    /// <param name="secret">Segredo (ex.: senha do PDF): só via stdin, nunca em arquivo ou argumento.</param>
    public async Task<ActionResult> RunAsync(RadialAction action, IReadOnlyList<string> files,
        Dictionary<string, JsonElement>? parameters = null, string? secret = null)
    {
        await _slots.WaitAsync().ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        try
        {
            if (action.Executor == "interno")
            {
                return _internal.TryGetValue(action.Id, out var handler)
                    ? await handler.RunAsync(files).ConfigureAwait(false) with { Duration = sw.Elapsed }
                    : ActionResult.Failure(ActionStatus.BusinessError, "Ação interna desconhecida", sw.Elapsed, "AcaoDesconhecida");
            }

            return await RunToolAsync(action, files, parameters, secret, sw).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ActionResult.Failure(ActionStatus.UnexpectedError, "Erro inesperado ao executar a ação",
                sw.Elapsed, ex.GetType().Name);
        }
        finally
        {
            _slots.Release();
        }
    }

    private async Task<ActionResult> RunToolAsync(RadialAction action, IReadOnlyList<string> files,
        Dictionary<string, JsonElement>? parameters, string? secret, Stopwatch sw)
    {
        Directory.CreateDirectory(_tmpDir);
        var inputFile = Path.Combine(_tmpDir, $"{Guid.NewGuid():N}.json");
        try
        {
            var input = new ToolInput(action.Id, files, parameters ?? new Dictionary<string, JsonElement>());
            await File.WriteAllTextAsync(inputFile, JsonSerializer.Serialize(input), new UTF8Encoding(false))
                .ConfigureAwait(false);

            var psi = new ProcessStartInfo(_toolExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("--entrada");
            psi.ArgumentList.Add(inputFile);

            Process process;
            try
            {
                process = Process.Start(psi) ?? throw new InvalidOperationException();
            }
            catch (Win32Exception)
            {
                return ActionResult.Failure(ActionStatus.UnexpectedError,
                    "Executor de ações não encontrado (RadialShift.Ferramentas.exe)", sw.Elapsed, "ExecutorAusente");
            }

            using (process)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync(); // drenado para não travar; não é registrado

                if (secret is not null) await process.StandardInput.WriteAsync(secret).ConfigureAwait(false);
                process.StandardInput.Close();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, action.TimeoutSeconds)));
                try
                {
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { } // já terminou
                    await process.WaitForExitAsync().ConfigureAwait(false);
                    return ActionResult.Failure(ActionStatus.Timeout,
                        $"A ação passou do limite de {action.TimeoutSeconds} s e foi cancelada", sw.Elapsed, "Timeout");
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                await stderrTask.ConfigureAwait(false);
                return Interpret(stdout, process.ExitCode, sw.Elapsed);
            }
        }
        finally
        {
            try { File.Delete(inputFile); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static ActionResult Interpret(string stdout, int exitCode, TimeSpan duration)
    {
        var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        ToolOutput? output = null;
        if (line is not null)
        {
            try { output = JsonSerializer.Deserialize<ToolOutput>(line); }
            catch (JsonException) { }
        }
        if (output is null)
        {
            return ActionResult.Failure(ActionStatus.UnexpectedError, "O executor não devolveu uma resposta válida",
                duration, $"SaidaInvalida(codigo {exitCode})");
        }

        var status = exitCode switch
        {
            ExitCodes.Success => ActionStatus.Success,
            ExitCodes.BusinessError => ActionStatus.BusinessError,
            ExitCodes.TimeoutOrCancelled => ActionStatus.Timeout,
            _ => ActionStatus.UnexpectedError,
        };
        return new ActionResult(status, output.Saidas ?? Array.Empty<string>(), output.Mensagem ?? "",
            output.Erros ?? Array.Empty<ToolError>(), duration,
            status == ActionStatus.Success ? null : $"Codigo{exitCode}");
    }
}
