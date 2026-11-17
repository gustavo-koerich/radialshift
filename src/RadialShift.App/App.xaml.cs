using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RadialShift.App.Actions;
using RadialShift.App.Actions.Internal;
using System.Windows.Threading;
using RadialShift.App.Infra;
using RadialShift.App.Input;
using RadialShift.App.Radial;
using RadialShift.App.Shell;
using Serilog;
using Forms = System.Windows.Forms;

namespace RadialShift.App;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(60);

    private Mutex? _singleInstance;
    private TriggerDetector? _detector;
    private KeyboardHook? _keyboardHook;
    private MouseHook? _mouseHook;
    private DispatcherTimer? _watchdog;
    private long _lastEventCount;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _pauseItem;
    private bool _paused;
    private RadialWindow? _radial;
    private ActionRunner? _runner;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\RadialShift.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            System.Windows.MessageBox.Show("O RadialShift já está em execução (veja o ícone na bandeja).",
                "RadialShift", MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        Logging.Configure();
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Erro não tratado na UI");
            args.Handled = true;
        };

        var settings = TriggerSettings.Load(Paths.ConfigFile);
        Log.Information("RadialShift iniciado. Gatilho: {Modo}, toque até {TapMax} ms, cooldown {Cooldown} ms",
            settings.Mode, settings.TapMaxMs, settings.CooldownMs);

        _detector = new TriggerDetector(settings.TapMaxMs, settings.CooldownMs);
        // Dentro do callback: só despacha para a UI, nada mais.
        _keyboardHook = new KeyboardHook(_detector, () => Dispatcher.BeginInvoke(OnTap));
        _mouseHook = new MouseHook(_detector);
        InstallHooks();

        _watchdog = new DispatcherTimer { Interval = WatchdogInterval };
        _watchdog.Tick += (_, _) => CheckHooks();
        _watchdog.Start();

        _runner = new ActionRunner(Paths.ToolExe, Paths.TmpDir, new IInternalAction[] { new CopyPathAction(Dispatcher) });

        CreateTray();
    }

    private void InstallHooks()
    {
        try
        {
            _keyboardHook!.Install();
            _mouseHook!.Install();
            _lastEventCount = _keyboardHook.EventCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao instalar hooks de teclado/mouse");
        }
    }

    private void UninstallHooks()
    {
        _keyboardHook?.Uninstall();
        _mouseHook?.Uninstall();
        _detector?.Reset();
    }

    /// <summary>
    /// O Windows remove hooks lentos sem avisar e não há API para saber. Se não chegou nenhum evento
    /// de teclado no intervalo (ocioso ou hook removido), reinstala preventivamente.
    /// </summary>
    private void CheckHooks()
    {
        if (_paused || _keyboardHook is null) return;
        if (_keyboardHook.EventCount != _lastEventCount)
        {
            Log.Debug("Watchdog: hook ativo ({Eventos} eventos de teclado no intervalo)",
                _keyboardHook.EventCount - _lastEventCount);
            _lastEventCount = _keyboardHook.EventCount;
            return;
        }

        UninstallHooks();
        InstallHooks();
        Log.Debug("Watchdog: hooks reinstalados preventivamente (sem eventos em {Segundos} s)",
            WatchdogInterval.TotalSeconds);
    }

    private async void OnTap()
    {
        if (_paused) return;
        Log.Debug("Toque no Shift detectado pelo hook");

        // Novo toque com o anel aberto: fecha sem executar.
        if (_radial is not null)
        {
            _radial.CloseRing();
            return;
        }

        var root = FocusInspector.ForegroundRoot();
        var foreground = FocusInspector.ForegroundClassName();
        if (foreground != "CabinetWClass")
        {
            Log.Debug("Toque ignorado: Explorer não está em primeiro plano (janela {Classe})", foreground);
            return;
        }

        var textFocused = await FocusInspector.IsTextInputFocusedAsync();
        if (textFocused is null)
        {
            Log.Warning("Toque ignorado: não foi possível verificar o foco (UI Automation falhou ou passou de 1 s)");
            return;
        }
        if (textFocused.Value)
        {
            Log.Information("Toque ignorado: foco em campo de texto");
            return;
        }

        var paths = await ExplorerSelection.GetSelectedPathsAsync(root);
        if (paths is null) return; // erro ou timeout, já registrado
        if (paths.Count == 0)
        {
            Log.Information("Toque ignorado: nenhum arquivo selecionado");
            return;
        }

        Log.Information("Disparo: {Quantidade} item(ns) selecionado(s); extensões: {Extensoes}",
            paths.Count, ExplorerSelection.SummarizeExtensions(paths, System.IO.Directory.Exists));
        foreach (var p in paths) Log.Debug("Selecionado: {Caminho}", p);

        // O usuário pode ter trocado de janela durante as leituras assíncronas.
        if (_radial is not null || FocusInspector.ForegroundRoot() != root) return;
        ShowRadial(paths);
    }

    private void ShowRadial(IReadOnlyList<string> paths)
    {
        var ring = new RadialWindow(FixedCatalog.Categories, paths.Count);
        ring.ActionChosen += (category, action) => { _ = RunActionAsync(action, paths); };
        ring.Closed += (_, _) => _radial = null;
        _radial = ring;
        try
        {
            ring.ShowAtCursor();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao abrir o anel");
            ring.CloseRing();
        }
    }

    private async Task RunActionAsync(RadialAction action, IReadOnlyList<string> paths)
    {
        Log.Information("Ação {Acao} iniciada: {Quantidade} item(ns); extensões: {Extensoes}",
            action.Id, paths.Count, ExplorerSelection.SummarizeExtensions(paths, System.IO.Directory.Exists));

        var result = await _runner!.RunAsync(action, paths);

        Log.Information("Ação {Acao} terminou: {Resultado} em {Duracao} ms; {Saidas} saída(s), {Erros} erro(s); tipo: {Tipo}; {Mensagem}",
            action.Id, result.Status, (long)result.Duration.TotalMilliseconds, result.Outputs.Count,
            result.Errors.Count, result.ErrorType ?? "-", result.Message);
        foreach (var e in result.Errors)
        {
            Log.Information("Ação {Acao}: erro em item: {Motivo}", action.Id, e.Motivo);
            if (e.Arquivo is not null) Log.Debug("Item com erro: {Caminho}", e.Arquivo);
        }
        foreach (var o in result.Outputs) Log.Debug("Saída gerada: {Caminho}", o);

        Notify(result);
    }

    /// <summary>Aviso provisório pelo balão da bandeja; os toasts da seção 8 entram na fase 5.</summary>
    private void Notify(ActionResult result)
    {
        if (_tray is null) return;
        var ok = result.Status == ActionStatus.Success;
        var text = ok && result.Outputs.Count == 1
            ? $"Pronto: {System.IO.Path.GetFileName(result.Outputs[0])}"
            : result.Message;
        if (result.Errors.Count > 0)
            text += $"\n{result.Errors.Count} item(ns) com problema: {result.Errors[0].Motivo}";
        _tray.ShowBalloonTip(5000, "RadialShift", text, ok ? Forms.ToolTipIcon.Info : Forms.ToolTipIcon.Error);
    }

    private void CreateTray()
    {
        _pauseItem = new Forms.ToolStripMenuItem("Pausar gatilho") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => SetPaused(_pauseItem.Checked);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Abrir log", null, (_, _) => OpenLogFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Shutdown());

        _tray = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "RadialShift",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused) UninstallHooks();
        else InstallHooks();
        _tray!.Text = paused ? "RadialShift (pausado)" : "RadialShift";
        Log.Information(paused ? "Gatilho pausado" : "Gatilho retomado");
    }

    private static void OpenLogFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Paths.LogsDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Paths.LogsDir}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao abrir a pasta de logs");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watchdog?.Stop();
        UninstallHooks();
        _keyboardHook?.Dispose();
        _mouseHook?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        if (_singleInstance is not null)
        {
            Log.Information("RadialShift encerrado");
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
