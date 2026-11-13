namespace Robotron2084.States.SpriteTest;

/// <summary>How a sprite on the SPRITE TEST page is drawn, which is how the game draws it.</summary>
public enum SpriteTestDrawStyle
{
    /// <summary>In its own colours.</summary>
    Normal,

    /// <summary>As a shape filled with one palette colour, as an electrode is.</summary>
    Solid,

    /// <summary>As a shape filled with one palette colour on a box of another, as a prog is.</summary>
    SolidWithBackground
}
