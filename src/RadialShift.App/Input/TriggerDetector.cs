using System;

namespace RadialShift.App.Input;

/// <summary>Qual Shift gerou o evento. <see cref="None"/> = qualquer outra tecla.</summary>
public enum ShiftKey
{
    None,
    Left,
    Right,
}

/// <summary>
/// Detecta o "toque no Shift" (modo tap) a partir de eventos abstratos, sem depender do hook real.
/// Por regra de segurança, guarda apenas: qual Shift está pressionado e desde quando,
/// e se houve outra tecla/clique (sim/não). Nunca guarda qual outra tecla foi pressionada.
/// Não é thread-safe: deve ser chamado sempre da mesma thread (a do hook, que é a de UI).
/// </summary>
public sealed class TriggerDetector
{
    private readonly long _tapMaxMs;
    private readonly long _cooldownMs;

    private ShiftKey _down = ShiftKey.None;
    private long _downAt;
    private bool _invalidated;
    private long? _lastTriggerAt;

    public TriggerDetector(int tapMaxMs, int cooldownMs)
    {
        if (tapMaxMs <= 0) throw new ArgumentOutOfRangeException(nameof(tapMaxMs));
        if (cooldownMs < 0) throw new ArgumentOutOfRangeException(nameof(cooldownMs));
        _tapMaxMs = tapMaxMs;
        _cooldownMs = cooldownMs;
    }

    /// <param name="key">Shift esquerdo/direito, ou <see cref="ShiftKey.None"/> para outra tecla.</param>
    /// <param name="isRepeat">KeyDown gerado por auto-repeat (tecla segurada).</param>
    /// <param name="otherModifiersHeld">Ctrl, Alt ou Win já estavam pressionados.</param>
    /// <param name="timestampMs">Relógio monotônico em milissegundos.</param>
    public void KeyDown(ShiftKey key, bool isRepeat, bool otherModifiersHeld, long timestampMs)
    {
        if (key == ShiftKey.None)
        {
            // Outra tecla durante o Shift (maiúscula, setas, Esc...): invalida.
            if (_down != ShiftKey.None) _invalidated = true;
            return;
        }

        if (_down == ShiftKey.None)
        {
            if (isRepeat)
            {
                // Repeat sem down conhecido (ex.: hook reinstalado com Shift segurado): ignora.
                return;
            }

            _down = key;
            _downAt = timestampMs;
            _invalidated = otherModifiersHeld;
            return;
        }

        // Já há um Shift pressionado: auto-repeat do mesmo ou o outro Shift. Ambos invalidam.
        _invalidated = true;
    }

    public void MouseDown()
    {
        if (_down != ShiftKey.None) _invalidated = true;
    }

    /// <returns>true se este KeyUp completa um toque válido (e fora do cooldown).</returns>
    public bool KeyUp(ShiftKey key, long timestampMs)
    {
        if (key == ShiftKey.None || key != _down) return false;

        var elapsed = timestampMs - _downAt;
        var valid = !_invalidated && elapsed >= 0 && elapsed <= _tapMaxMs;
        _down = ShiftKey.None;
        _invalidated = false;

        if (!valid) return false;
        if (_lastTriggerAt is long last && timestampMs - last < _cooldownMs) return false;

        _lastTriggerAt = timestampMs;
        return true;
    }

    /// <summary>Esquece o estado do toque em andamento (ex.: após reinstalar o hook).</summary>
    public void Reset()
    {
        _down = ShiftKey.None;
        _invalidated = false;
    }
}
