using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using RadialShift.Ferramentas;

namespace RadialShift.App.Actions.Internal;

/// <summary>copiar_caminho: caminhos completos, um por linha, na área de transferência.</summary>
public sealed class CopyPathAction : IInternalAction
{
    private readonly Dispatcher _ui;

    public CopyPathAction(Dispatcher ui) => _ui = ui;

    public string Id => "copiar_caminho";

    public async Task<ActionResult> RunAsync(IReadOnlyList<string> files)
    {
        var text = string.Join(Environment.NewLine, files);

        // A área de transferência exige thread STA e pode estar ocupada por outro programa.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _ui.InvokeAsync(() => System.Windows.Clipboard.SetText(text));
                break;
            }
            catch (COMException) when (attempt < 5)
            {
                await Task.Delay(100);
            }
            catch (COMException)
            {
                return ActionResult.Failure(ActionStatus.BusinessError,
                    "Área de transferência ocupada por outro programa", TimeSpan.Zero, "ClipboardOcupado");
            }
        }

        var message = files.Count == 1 ? "Caminho copiado" : $"{files.Count} caminhos copiados";
        return new ActionResult(ActionStatus.Success, Array.Empty<string>(), message, Array.Empty<ToolError>(), TimeSpan.Zero);
    }
}
