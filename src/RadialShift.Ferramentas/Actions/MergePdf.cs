using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace RadialShift.Ferramentas.Actions;

/// <summary>juntar_pdf (seção 7.4): ordem alfabética natural, saída na pasta do primeiro arquivo.</summary>
public static class MergePdf
{
    public static ActionOutcome Run(IReadOnlyList<string> files, DateTime now)
    {
        var ordered = files
            .OrderBy(f => Path.GetFileName(f), NaturalComparer.Instance)
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ordered.Count < 2) throw new BusinessException("Selecione pelo menos 2 PDFs para juntar");

        var errors = new List<ToolError>();
        var sources = new List<PdfDocument>();
        try
        {
            using var output = new PdfDocument();
            var merged = 0;
            foreach (var file in ordered)
            {
                if (!string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ToolError(file, "Não é um arquivo PDF"));
                    continue;
                }

                var source = TryOpen(file, errors);
                if (source is null) continue;
                sources.Add(source);
                foreach (var page in source.Pages) output.AddPage(page);
                merged++;
            }

            if (merged < 2)
                throw new BusinessException("É preciso pelo menos 2 PDFs válidos para juntar", errors);

            var dir = Path.GetDirectoryName(ordered[0])!;
            var desired = Path.Combine(dir, $"juntado_{now:yyyy-MM-dd_HHmm}.pdf");
            var final = OutputNames.WriteNew(desired, s => output.Save(s, false));
            return new ActionOutcome(new[] { final }, $"{merged} PDFs juntados", errors);
        }
        finally
        {
            foreach (var s in sources) s.Dispose();
        }
    }

    private static PdfDocument? TryOpen(string file, List<ToolError> errors)
    {
        var needsPassword = false;
        try
        {
            // No MVP não pedimos senha: o provedor só registra que ela seria necessária e aborta.
            // Ao abortar, o PDFsharp devolve null em vez de lançar exceção.
            var doc = PdfReader.Open(file, PdfDocumentOpenMode.Import, args =>
            {
                needsPassword = true;
                args.Abort = true;
            });
            if (doc is null) errors.Add(new ToolError(file, "PDF protegido por senha"));
            return doc;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(new ToolError(file, needsPassword
                ? "PDF protegido por senha"
                : ex is FileNotFoundException or DirectoryNotFoundException
                    ? "Arquivo não encontrado"
                    : ex is IOException or UnauthorizedAccessException
                        ? "Não foi possível ler o arquivo (em uso, sem permissão ou falha ao baixar do OneDrive)"
                        : "PDF inválido ou corrompido"));
            return null;
        }
    }
}
