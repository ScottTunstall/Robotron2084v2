namespace Robotron2084.Entities;

/// <summary>Which way a strip explosion runs.</summary>
public enum StripEffectKind
{
    /// <summary>The spacing grows: the fan opens.</summary>
    Explode,

    /// <summary>The spacing shrinks: the fan converges.</summary>
    Appear,
}
