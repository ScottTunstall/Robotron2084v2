namespace Robotron2084.Hud;

/// <summary>What the initials entry is waiting for or doing at the moment.</summary>
internal enum InitialsEntryPhase
{
    /// <summary>Waiting for the fire control to be let go, so that a held press is not taken as a new one.</summary>
    AwaitingFireRelease,

    /// <summary>Looking at the controls to see what the player wants next.</summary>
    AwaitingInput,

    /// <summary>Stepping through the letters because up or down is being held.</summary>
    Cycling,

    /// <summary>Repeating a held press on the same cell, as a keyboard repeats a held key.</summary>
    Typematic
}
