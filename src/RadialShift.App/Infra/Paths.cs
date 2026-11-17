using System;
using System.IO;

namespace RadialShift.App.Infra;

/// <summary>Pastas do app. Tudo em %LOCALAPPDATA%\RadialShift (sem admin).</summary>
public static class Paths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadialShift");

    public static string LogsDir { get; } = Path.Combine(DataDir, "logs");

    /// <summary>JSONs de entrada do executor; apagados ao fim de cada execução.</summary>
    public static string TmpDir { get; } = Path.Combine(DataDir, "tmp");

    public static string ToolExe { get; } = Path.Combine(AppContext.BaseDirectory, "RadialShift.Ferramentas.exe");

    public static string ConfigFile { get; } = Path.Combine(AppContext.BaseDirectory, "config", "acoes.json");
}
