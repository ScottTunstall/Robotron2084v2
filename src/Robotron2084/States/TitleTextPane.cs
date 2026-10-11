namespace Robotron2084.States;

/// <summary>
///     Which of the title page's two TEXT PANES (notes §107) a <see cref="TitleScreenState" /> opens on.
///     The attract cycle leaves this unset, so the two panes alternate as the arcade's page and the
///     port's own lines take turns in the page's text slot. Set, the page opens on that pane and holds
///     it — which is what the DEVELOPER TOOLS page's TITLE PAGE 1 / TITLE PAGE 2 options want: one
///     screen each, so a pane can be looked at without the other taking it away.
/// </summary>
public enum TitleTextPane
{
    /// <summary>The arcade's own pane: "PRESENTED BY / WILLIAMS ELECTRONICS INC." and the credit strings.</summary>
    Arcade,

    /// <summary>The port's pane: its LARGE-font credit, "REMAKE DEVELOPED BY / SCOTT TUNSTALL".</summary>
    Port
}
