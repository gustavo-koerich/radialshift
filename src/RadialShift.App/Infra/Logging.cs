using System;
using System.IO;
using Serilog;

namespace RadialShift.App.Infra;

public static class Logging
{
    /// <summary>
    /// Log rotativo diário, 14 dias. Nível padrão INFO.
    /// Nomes e caminhos de arquivos só em DEBUG; nunca teclas, senhas ou conteúdo.
    /// DEBUG liga com a variável de ambiente RADIALSHIFT_LOG_DEBUG=1 (desligado por padrão).
    /// </summary>
    public static void Configure()
    {
        Directory.CreateDirectory(Paths.LogsDir);
        var config = new LoggerConfiguration();
        config = Environment.GetEnvironmentVariable("RADIALSHIFT_LOG_DEBUG") == "1"
            ? config.MinimumLevel.Debug()
            : config.MinimumLevel.Information();
        Log.Logger = config
            .WriteTo.File(
                Path.Combine(Paths.LogsDir, "radialshift-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
