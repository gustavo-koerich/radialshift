using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using static RadialShift.App.Infra.NativeMethods;

namespace RadialShift.App.Shell;

/// <summary>Verificações de contexto feitas fora do callback do hook.</summary>
public static class FocusInspector
{
    private static readonly TimeSpan UiaTimeout = TimeSpan.FromSeconds(1);

    /// <summary>A janela em primeiro plano é o Explorer (CabinetWClass)?</summary>
    public static bool IsExplorerForeground() => ForegroundClassName() == "CabinetWClass";

    /// <summary>Classe da janela raiz em primeiro plano (ex.: CabinetWClass, Progman). Vazio se nenhuma.</summary>
    public static string ForegroundClassName() => ClassName(ForegroundRoot());

    /// <summary>HWND raiz da janela em primeiro plano.</summary>
    public static IntPtr ForegroundRoot() => GetAncestor(GetForegroundWindow(), GA_ROOT);

    private static string ClassName(IntPtr root)
    {
        if (root == IntPtr.Zero) return "";
        var sb = new StringBuilder(64);
        GetClassName(root, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// O elemento com foco é campo de texto (renomear com F2, barra de endereço, busca)?
    /// Em caso de erro ou timeout, retorna <c>null</c> (quem chama deve tratar como "não disparar").
    /// </summary>
    public static async Task<bool?> IsTextInputFocusedAsync()
    {
        // UI Automation fora da thread de UI para não travar a janela do app.
        var task = Task.Run(() =>
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null) return false;
            return focused.Current.ControlType == ControlType.Edit;
        });

        var done = await Task.WhenAny(task, Task.Delay(UiaTimeout)).ConfigureAwait(false);
        if (done != task) return null;
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
