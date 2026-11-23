using System.Numerics;

namespace NEREA.Core.Models;

/// <summary>Thread-safe snapshot of the live music analysis used by the ecosystem.</summary>
[Serializable]
public struct MusicEnergy
{
    public float Bass;
    public float Mid;
    public float Treble;
    public float Level;
    /// <summary>Instantaneous beat pulse (0..1, decays).</summary>
    public float Beat;
    /// <summary>Overall ecosystem activity multiplier suggestion (0.6..1.6).</summary>
    public float Activity;

    public static MusicEnergy Silence => new()
    {
        Bass = 0.04f,
        Mid = 0.03f,
        Treble = 0.02f,
        Level = 0f,
        Beat = 0f,
        Activity = 0.85f
    };

    public static MusicEnergy Lerp(in MusicEnergy a, in MusicEnergy b, float t) => new()
    {
        Bass = a.Bass + (b.Bass - a.Bass) * t,
        Mid = a.Mid + (b.Mid - a.Mid) * t,
        Treble = a.Treble + (b.Treble - a.Treble) * t,
        Level = a.Level + (b.Level - a.Level) * t,
        Beat = a.Beat + (b.Beat - a.Beat) * t,
        Activity = a.Activity + (b.Activity - a.Activity) * t
    };
}
