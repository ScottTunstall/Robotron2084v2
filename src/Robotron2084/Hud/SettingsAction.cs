namespace Robotron2084.Hud;

/// <summary>What Enter on an action row asked the screen to do.</summary>
public enum SettingsAction
{
    /// <summary>Nothing (no action row highlighted, or it was not set to YES).</summary>
    None,

    /// <summary>RESTORE FACTORY SETTINGS — put all three settings back to the cabinet's defaults.</summary>
    RestoreFactorySettings,

    /// <summary>HIGH SCORE TABLE RESET — throw the saved high score table away.</summary>
    ResetHighScores
}
