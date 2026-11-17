using System.Collections.Generic;

namespace RadialShift.App.Radial;

/// <param name="Executor">"ferramentas" (processo filho) ou "interno" (implementada no app).</param>
public sealed record RadialAction(string Id, string Label, string Executor, int TimeoutSeconds);

public sealed record RadialCategory(string Id, string Label, IReadOnlyList<RadialAction> Actions);

/// <summary>
/// Catálogo fixo (espelha config/acoes.json). Na fase 6 passa a vir do JSON,
/// com filtro por extensão e quantidade de itens.
/// </summary>
public static class FixedCatalog
{
    public static IReadOnlyList<RadialCategory> Categories { get; } = new[]
    {
        new RadialCategory("pdf", "PDF", new[]
        {
            new RadialAction("converter_pdf", "Converter", "ferramentas", 120),
            new RadialAction("juntar_pdf", "Juntar", "ferramentas", 120),
            new RadialAction("proteger_pdf", "Senha", "ferramentas", 60),
        }),
        new RadialCategory("fiscal", "Fiscal", new[]
        {
            new RadialAction("nfe_excel", "NF-e → Excel", "ferramentas", 300),
        }),
        new RadialCategory("caminho", "Caminho", new[]
        {
            new RadialAction("copiar_caminho", "Copiar", "interno", 0),
        }),
    };
}
