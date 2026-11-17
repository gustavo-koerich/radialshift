using System;
using System.Collections.Generic;
using RadialShift.Ferramentas;

namespace RadialShift.App.Actions;

public enum ActionStatus
{
    Success,
    BusinessError,
    UnexpectedError,
    Timeout,
}

/// <param name="Message">Texto curto para o usuário; nunca contém caminhos.</param>
/// <param name="ErrorType">Tipo do erro para log INFO (sem dados do arquivo).</param>
public sealed record ActionResult(
    ActionStatus Status,
    IReadOnlyList<string> Outputs,
    string Message,
    IReadOnlyList<ToolError> Errors,
    TimeSpan Duration,
    string? ErrorType = null)
{
    public static ActionResult Failure(ActionStatus status, string message, TimeSpan duration, string errorType) =>
        new(status, Array.Empty<string>(), message, Array.Empty<ToolError>(), duration, errorType);
}
