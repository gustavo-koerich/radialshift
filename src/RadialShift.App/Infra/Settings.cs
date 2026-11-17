using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace RadialShift.App.Infra;

/// <summary>Bloco "trigger" do acoes.json. Valores inválidos caem nos padrões da spec.</summary>
public sealed record TriggerSettings(string Mode, int TapMaxMs, int CooldownMs)
{
    public static TriggerSettings Default { get; } = new("tap", 300, 500);

    public static TriggerSettings Load(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return Parse(doc.RootElement);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warning("Configuração não pôde ser lida ({Tipo}); usando padrões do gatilho", ex.GetType().Name);
            return Default;
        }
    }

    internal static TriggerSettings Parse(JsonElement root)
    {
        if (!root.TryGetProperty("trigger", out var t) || t.ValueKind != JsonValueKind.Object) return Default;

        var mode = t.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()! : Default.Mode;
        if (mode != "tap")
        {
            // doubletap e hold entram na fase 7.
            Log.Warning("Modo de gatilho {Modo} ainda não suportado; usando tap", mode);
            mode = "tap";
        }

        var tapMax = ReadInt(t, "tap_max_ms", Default.TapMaxMs, 50, 2000);
        var cooldown = ReadInt(t, "cooldown_ms", Default.CooldownMs, 0, 10000);
        return new TriggerSettings(mode, tapMax, cooldown);
    }

    private static int ReadInt(JsonElement obj, string name, int fallback, int min, int max)
    {
        if (obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) && n >= min && n <= max) return n;
        if (obj.TryGetProperty(name, out _))
            Log.Warning("Valor inválido em trigger.{Campo}; usando {Padrao}", name, fallback);
        return fallback;
    }
}
