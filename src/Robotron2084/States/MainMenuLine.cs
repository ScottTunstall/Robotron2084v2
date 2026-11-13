namespace Robotron2084.States;

/// <summary>A line of the menu page (<see cref="MainMenuState" />).</summary>
public enum MainMenuLine
{
    /// <summary>F1, F2 and F3: starts a game. LEFT and RIGHT pick whether it is for one player or two, and how two play.</summary>
    StartGame,

    /// <summary>F4: switches between the CLASSIC and EXTENDED games.</summary>
    GameStyle,

    /// <summary>F5: opens the GAME ADJUSTMENT page.</summary>
    Settings,

    /// <summary>F10: opens the DEVELOPER TOOLS page.</summary>
    DeveloperTools
}
