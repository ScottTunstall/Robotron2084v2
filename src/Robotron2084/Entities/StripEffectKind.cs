namespace Robotron2084.Entities;

/// <summary>Which of the two strip effects this is: strips flying apart, or strips closing up.</summary>
public enum StripEffectKind
{
    /// <summary>An explosion: the strips fly apart.</summary>
    Explode,

    /// <summary>An appear: the strips close up to make the sprite.</summary>
    Appear
}
