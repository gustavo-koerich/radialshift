using System.Text.Json;
using System.Text.Json.Serialization;

namespace RadialShift.Ferramentas;

/// <summary>Entrada lida do JSON temporário (seção 7.1 da spec). Segredos nunca vêm aqui: só por stdin.</summary>
public sealed record ToolInput(
    [property: JsonPropertyName("acao")] string Acao,
    [property: JsonPropertyName("arquivos")] IReadOnlyList<string> Arquivos,
    [property: JsonPropertyName("parametros")] Dictionary<string, JsonElement>? Parametros);

/// <summary>Problema com um arquivo específico (ou geral, se <see cref="Arquivo"/> for nulo).</summary>
public sealed record ToolError(
    [property: JsonPropertyName("arquivo")] string? Arquivo,
    [property: JsonPropertyName("motivo")] string Motivo);

/// <summary>Única linha JSON escrita no stdout (seção 7.2). <see cref="Mensagem"/> nunca contém caminhos.</summary>
public sealed record ToolOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("saidas")] IReadOnlyList<string> Saidas,
    [property: JsonPropertyName("mensagem")] string Mensagem,
    [property: JsonPropertyName("erros")] IReadOnlyList<ToolError> Erros);

public static class ExitCodes
{
    public const int Success = 0;
    public const int BusinessError = 1;
    public const int Unexpected = 2;
    public const int TimeoutOrCancelled = 3;
}

/// <summary>Erro de negócio tratado (arquivo inválido, PDF protegido etc.): código de saída 1.</summary>
public sealed class BusinessException : Exception
{
    public IReadOnlyList<ToolError> Errors { get; }

    public BusinessException(string message, IReadOnlyList<ToolError>? errors = null) : base(message)
    {
        Errors = errors ?? Array.Empty<ToolError>();
    }
}

/// <summary>Resultado de uma ação bem-sucedida (total ou parcial).</summary>
public sealed record ActionOutcome(IReadOnlyList<string> Outputs, string Message, IReadOnlyList<ToolError> Errors);
