using RadialShift.Ferramentas.Actions;

namespace RadialShift.Ferramentas;

/// <summary>Despacha a ação pedida e traduz o resultado para o contrato (saída + código).</summary>
public static class Tool
{
    public static (ToolOutput Output, int ExitCode) Run(ToolInput input, TextReader stdin)
    {
        try
        {
            var outcome = Dispatch(input, stdin);
            var ok = outcome.Outputs.Count > 0;
            return (new ToolOutput(ok, outcome.Outputs, outcome.Message, outcome.Errors),
                ok ? ExitCodes.Success : ExitCodes.BusinessError);
        }
        catch (BusinessException ex)
        {
            return (new ToolOutput(false, Array.Empty<string>(), ex.Message, ex.Errors), ExitCodes.BusinessError);
        }
        catch (Exception ex)
        {
            // Só o tipo: a mensagem de exceções de I/O pode conter caminhos.
            return (new ToolOutput(false, Array.Empty<string>(), $"Erro inesperado ({ex.GetType().Name})",
                Array.Empty<ToolError>()), ExitCodes.Unexpected);
        }
    }

    private static ActionOutcome Dispatch(ToolInput input, TextReader stdin)
    {
        _ = stdin; // segredos (proteger_pdf, fase 5) chegam por aqui
        return input.Acao switch
        {
            "juntar_pdf" => MergePdf.Run(input.Arquivos, DateTime.Now),
#if DEBUG
            // Só em build de desenvolvimento: permite testar o timeout do ActionRunner.
            "_teste_espera" => WaitForTest(input),
#endif
            _ => throw new BusinessException($"Ação \"{input.Acao}\" ainda não está disponível"),
        };
    }

#if DEBUG
    private static ActionOutcome WaitForTest(ToolInput input)
    {
        var seconds = input.Parametros is { } p && p.TryGetValue("segundos", out var s) ? s.GetInt32() : 60;
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        return new ActionOutcome(new[] { "espera" }, "Espera concluída", Array.Empty<ToolError>());
    }
#endif
}
