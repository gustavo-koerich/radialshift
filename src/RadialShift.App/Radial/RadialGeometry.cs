using System;

namespace RadialShift.App.Radial;

public enum RadialZone
{
    None,
    Hole,
    Category,
    Action,
}

public readonly record struct RadialHit(RadialZone Zone, int Index)
{
    public static RadialHit Nothing { get; } = new(RadialZone.None, -1);
}

/// <summary>
/// Cálculos do anel (seção 5.2 da spec), em DIP, sem dependência de UI.
/// Ângulos em graus, sentido horário, 0 = topo. Coordenadas relativas ao centro, com Y para baixo.
/// </summary>
public static class RadialGeometry
{
    public const double HoleRadius = 56;
    public const double CategoryInner = 56;
    public const double CategoryOuter = 110;
    public const double ActionInner = 114;
    public const double ActionOuter = 166;
    public const double ActionSweep = 36;
    public const int MaxCategories = 8;
    public const int MaxActions = 5;

    /// <summary>Folga além do arco de ações (borda/sombra).</summary>
    public const double Margin = 8;

    public static double OuterRadius => ActionOuter + Margin;

    public static double CategorySweep(int count) => count <= 0 ? 0 : 360.0 / count;

    /// <summary>Ângulo central da categoria <paramref name="index"/>; a primeira fica no topo.</summary>
    public static double CategoryCenter(int index, int count) => Normalize(index * CategorySweep(count));

    /// <summary>Ângulo central da ação <paramref name="index"/> de <paramref name="count"/>, centradas na categoria.</summary>
    public static double ActionCenter(double categoryCenter, int index, int count) =>
        Normalize(categoryCenter + (index - (count - 1) / 2.0) * ActionSweep);

    public static double AngleOf(double dx, double dy) => Normalize(Math.Atan2(dx, -dy) * 180 / Math.PI);

    public static (double X, double Y) PointAt(double radius, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180;
        return (radius * Math.Sin(rad), -radius * Math.Cos(rad));
    }

    /// <param name="openCategory">Categoria com arco de ações aberto, ou -1.</param>
    /// <param name="openActionCount">Quantidade de ações da categoria aberta.</param>
    public static RadialHit HitTest(double dx, double dy, int categoryCount, int openCategory, int openActionCount)
    {
        var r = Math.Sqrt(dx * dx + dy * dy);
        if (r < HoleRadius) return new RadialHit(RadialZone.Hole, -1);

        var angle = AngleOf(dx, dy);
        if (r <= CategoryOuter)
        {
            if (categoryCount <= 0) return RadialHit.Nothing;
            var sweep = CategorySweep(categoryCount);
            var index = (int)(Normalize(angle + sweep / 2) / sweep) % categoryCount;
            return new RadialHit(RadialZone.Category, index);
        }

        if (r >= ActionInner && r <= ActionOuter && openCategory >= 0 && openCategory < categoryCount)
        {
            var center = CategoryCenter(openCategory, categoryCount);
            for (var j = 0; j < openActionCount; j++)
            {
                if (AngularDistance(angle, ActionCenter(center, j, openActionCount)) <= ActionSweep / 2)
                    return new RadialHit(RadialZone.Action, j);
            }
        }

        return RadialHit.Nothing;
    }

    /// <summary>
    /// Desloca o centro para o anel de raio <paramref name="radius"/> caber na área de trabalho.
    /// Se a área for menor que o anel, centraliza nela.
    /// </summary>
    public static (double X, double Y) ClampCenter(double x, double y, double radius,
        double left, double top, double right, double bottom)
    {
        return (Clamp(x, left + radius, right - radius), Clamp(y, top + radius, bottom - radius));

        static double Clamp(double v, double min, double max) =>
            min > max ? (min + max) / 2 : Math.Min(Math.Max(v, min), max);
    }

    public static double Normalize(double angle)
    {
        var a = angle % 360;
        return a < 0 ? a + 360 : a;
    }

    public static double AngularDistance(double a, double b)
    {
        var d = Math.Abs(Normalize(a) - Normalize(b));
        return d > 180 ? 360 - d : d;
    }
}
