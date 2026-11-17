namespace RadialShift.Ferramentas;

/// <summary>Regras de saída (seção 7.3): nunca sobrescrever e escrever de forma atômica.</summary>
public static class OutputNames
{
    private const int MaxAttempts = 1000;

    /// <summary>Devolve <paramref name="desired"/> se estiver livre; senão nome_1, nome_2…</summary>
    public static string Available(string desired)
    {
        if (!Exists(desired)) return desired;
        var dir = Path.GetDirectoryName(desired) ?? "";
        var name = Path.GetFileNameWithoutExtension(desired);
        var ext = Path.GetExtension(desired);
        for (var i = 1; i < MaxAttempts; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!Exists(candidate)) return candidate;
        }
        throw new IOException("Não há nome livre para o arquivo de saída");
    }

    /// <summary>
    /// Grava num temporário na mesma pasta e renomeia para um nome livre no fim,
    /// para nunca deixar arquivo pela metade nem sobrescrever. Devolve o caminho final.
    /// </summary>
    public static string WriteNew(string desired, Action<Stream> write)
    {
        var dir = Path.GetDirectoryName(desired) ?? throw new ArgumentException("Caminho sem pasta", nameof(desired));
        var temp = Path.Combine(dir, $".{Path.GetFileName(desired)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(fs);
                fs.Flush(true);
            }

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var final = Available(desired);
                try
                {
                    File.Move(temp, final, overwrite: false);
                    return final;
                }
                catch (IOException) when (Exists(final))
                {
                    // Outro processo criou o mesmo nome entre a verificação e o Move: tenta o próximo.
                }
            }
            throw new IOException("Não há nome livre para o arquivo de saída");
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
