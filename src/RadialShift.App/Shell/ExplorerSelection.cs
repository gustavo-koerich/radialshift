using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using static RadialShift.App.Infra.NativeMethods;

namespace RadialShift.App.Shell;

/// <summary>
/// Lê os itens selecionados na janela do Explorer em primeiro plano (Shell.Application via COM).
/// Roda numa thread STA própria, fora do callback do hook, com timeout de 1 s.
/// </summary>
public static class ExplorerSelection
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");

    /// <param name="explorerRoot">HWND raiz (CabinetWClass) da janela em primeiro plano.</param>
    /// <returns>Caminhos dos itens do sistema de arquivos selecionados; <c>null</c> em erro ou timeout.</returns>
    public static async Task<IReadOnlyList<string>?> GetSelectedPathsAsync(IntPtr explorerRoot)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.TrySetResult(ReadSelection(explorerRoot));
            }
            catch (Exception ex)
            {
                Log.Warning("Falha ao ler a seleção do Explorer ({Tipo}: {Mensagem})", ex.GetType().Name, ex.Message);
                tcs.TrySetResult(null);
            }
        })
        {
            IsBackground = true,
            Name = "RadialShift.ExplorerSelection",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var done = await Task.WhenAny(tcs.Task, Task.Delay(Timeout)).ConfigureAwait(false);
        if (done != tcs.Task)
        {
            Log.Warning("Leitura da seleção do Explorer passou de {Ms} ms; anel não será aberto", Timeout.TotalMilliseconds);
            return null;
        }
        return await tcs.Task.ConfigureAwait(false);
    }

    private static IReadOnlyList<string>? ReadSelection(IntPtr explorerRoot)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application indisponível");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            var activeTab = FindActiveTab(explorerRoot);
            var candidates = new List<dynamic>();

            dynamic windows = shell.Windows();
            int count = windows.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic? w = windows.Item(i);
                if (w is null) continue;
                IntPtr hwnd;
                try { hwnd = new IntPtr((long)w.HWND); }
                catch (Exception) { continue; } // janelas do IE/legadas sem HWND válido
                if (hwnd == explorerRoot) candidates.Add(w);
            }

            dynamic? target = PickWindow(candidates, activeTab);
            if (target is null) return null;

            dynamic? document;
            try { document = target.Document; }
            catch (COMException) { return Array.Empty<string>(); }
            if (document is null) return Array.Empty<string>();

            dynamic items;
            try { items = document.SelectedItems(); }
            catch (Exception) { return Array.Empty<string>(); } // "Início", "Este Computador" etc. sem ShellFolderView

            var paths = new List<string>();
            int n = items.Count;
            for (var i = 0; i < n; i++)
            {
                dynamic item = items.Item(i);
                bool isFileSystem;
                string? path;
                try
                {
                    isFileSystem = item.IsFileSystem;
                    path = item.Path;
                }
                catch (COMException) { continue; }

                // Descarta Lixeira, bibliotecas virtuais, itens dentro de ZIP etc.
                if (!isFileSystem || string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path) && !Directory.Exists(path)) continue;
                paths.Add(path);
            }
            return paths;
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    /// <summary>
    /// No Windows 11 várias abas compartilham o mesmo HWND raiz. Cada aba tem uma janela
    /// ShellTabWindowClass filha; só a da aba ativa fica visível.
    /// </summary>
    private static IntPtr FindActiveTab(IntPtr explorerRoot)
    {
        var child = IntPtr.Zero;
        while ((child = FindWindowEx(explorerRoot, child, "ShellTabWindowClass", null)) != IntPtr.Zero)
        {
            if (IsWindowVisible(child)) return child;
        }
        return IntPtr.Zero;
    }

    private static dynamic? PickWindow(List<dynamic> candidates, IntPtr activeTab)
    {
        if (candidates.Count == 0)
        {
            Log.Warning("Janela do Explorer em primeiro plano não encontrada em Shell.Windows()");
            return null;
        }
        if (candidates.Count == 1) return candidates[0];

        // Várias abas: compara a janela de cada aba (via IShellBrowser) com a aba visível.
        if (activeTab != IntPtr.Zero)
        {
            foreach (var w in candidates)
            {
                if (TabWindow(w) == activeTab) return w;
            }
        }

        Log.Warning("Não foi possível identificar a aba ativa entre {Abas} abas", candidates.Count);
        return null;
    }

    private static IntPtr TabWindow(object window)
    {
        try
        {
            var sp = (IComServiceProvider)window;
            var sid = SID_STopLevelBrowser;
            var iid = IID_IShellBrowser;
            sp.QueryService(ref sid, ref iid, out var obj);
            var browser = (IShellBrowser)obj;
            browser.GetWindow(out var hwnd);
            return hwnd;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>Resumo das extensões para log INFO, sem nomes: ".pdf x2, .xml x1, (pasta) x1".</summary>
    public static string SummarizeExtensions(IEnumerable<string> paths, Func<string, bool> isDirectory)
    {
        var groups = paths
            .Select(p => isDirectory(p) ? "(pasta)" : Path.GetExtension(p).ToLowerInvariant() is { Length: > 0 } ext ? ext : "(sem extensão)")
            .GroupBy(e => e)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} x{g.Count()}");
        return string.Join(", ", groups);
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IComServiceProvider
    {
        void QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
    }

    /// <summary>Só os métodos de IOleWindow, que são os primeiros da vtable de IShellBrowser.</summary>
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow(out IntPtr phwnd);
        void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
    }
}
