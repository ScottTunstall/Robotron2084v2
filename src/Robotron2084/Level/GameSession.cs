using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
///     A game that is being played: each player's details, and whose turn it is. It is passed from screen to screen,
///     so a cleared wave or a death keeps every player's score, lives and wave.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>PLRCNT</c> for the number of
///             players and <c>CURPLR</c> for whose turn it is; the turn passes in <c>PLEND</c>, <c>PLEND3</c> and
///             <c>PLE1B</c>
///         </item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
///     When a life is lost, the turn passes to the
///     other player if they still have lives. Otherwise the same player carries on, and when nobody has any
///     lives left the game is over.
/// </remarks>
public sealed class GameSession
{
    /// <summary>Makes a game with these players.</summary>
    /// <param name="players">The players, with player 1 first.</param>
    private GameSession(PlayerSlot[] players)
    {
        Players = players;
    }

    /// <summary>
    ///     The keys and buttons the players have chosen. They go with the game so that every screen can read them, such
    ///     as the pause key (notes §101).
    /// </summary>
    public ControlSettings ControlSettings { get; private init; } = ControlSettings.CreateDefaults();

    /// <summary>The place in <see cref="Players" /> of the player whose turn it is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>CURPLR</c>.</item>
    ///         <item>Disassembly: <c>current_player</c>.</item>
    ///     </list>
    /// </remarks>
    public int CurrentIndex { get; private set; }

    /// <summary>
    ///     The places in the family list that still held a family member when the last wave or life ended, lowest place
    ///     first. The next field's hulks pick what to stalk from these. A new game starts with none.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HTAB</c>. There is one list for the whole machine, so in a two-player
    ///             game
    ///             one player's hulks search what the other player left.
    ///         </item>
    ///         <item>Disassembly: the list at <c>$B354</c>, which is only cleared when a family is put on (<c>$0200</c>).</item>
    ///     </list>
    /// </remarks>
    public IReadOnlyList<int> FamilySlotsLeftOver { get; set; } = [];

    /// <summary>
    ///     The game adjustment settings the game was started with. They go with the game, as <see cref="ControlSettings" /> do,
    ///     because the screens after a game over need them (notes §131).
    /// </summary>
    public GameSettings GameSettings { get; private init; } = GameSettings.CreateFactoryDefaults();

    /// <summary>The players, with player 1 first. There are one or two.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLRCNT</c>.</item>
    ///         <item>Disassembly: <c>num_players</c>.</item>
    ///     </list>
    /// </remarks>
    public IReadOnlyList<PlayerSlot> Players { get; }

    /// <summary>Starts a new game for one or two players, as the arcade's two start buttons do.</summary>
    /// <param name="playerInputSource">The first player's controls.</param>
    /// <param name="playerCount">How many people are playing, 1 or 2.</param>
    /// <param name="secondPlayerInputSource">The second player's controls, or null to share the first player's.</param>
    public static GameSession CreateNewGame(IPlayerInputSource playerInputSource, int playerCount,
        IPlayerInputSource? secondPlayerInputSource = null)
    {
        if (playerCount is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount,
                "Robotron is a 1 or 2 player game (ROM PLRCNT).");

        var mode = GameMode.OnePlayer;
        if (playerCount == 2) mode = GameMode.TwoPlayerAlternate;

        return CreateNewGame(
            mode,
            playerInputSource,
            secondPlayerInputSource);
    }

    /// <summary>
    ///     Starts a new game in one of the modes. The mode says how many people play, and each player has their own
    ///     controls (notes §101).
    /// </summary>
    /// <param name="mode">How many people are playing, and whether two take turns.</param>
    /// <param name="firstPlayerInputSource">The first player's controls.</param>
    /// <param name="secondPlayerInputSource">The second player's controls, or null to share the first player's.</param>
    /// <param name="controlSettings">The keys and buttons the players have chosen, or null for the standard ones.</param>
    /// <param name="gameSettings">
    ///     The game adjustment settings, or null for the factory ones (notes §131). They say how many lives
    ///     each player starts with, and travel with the game for the screens that follow.
    /// </param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>NSHIP</c>, the lives each player starts with, which is
    ///             loaded into <c>PLAS</c>.
    ///         </item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public static GameSession CreateNewGame(GameMode mode, IPlayerInputSource firstPlayerInputSource,
        IPlayerInputSource? secondPlayerInputSource = null, ControlSettings? controlSettings = null, GameSettings? gameSettings = null)
    {
        var chosenGameSettings = gameSettings ?? GameSettings.CreateFactoryDefaults();
        var playerCount = 2;
        if (mode == GameMode.OnePlayer) playerCount = 1;

        var players = new PlayerSlot[playerCount];
        for (var i = 0; i < playerCount; i++)
            players[i] = new PlayerSlot(
                i + 1,
                i == 0 ? firstPlayerInputSource : secondPlayerInputSource ?? firstPlayerInputSource,
                gameSettings?.TurnsPerPlayer ?? PlayerTuning.StartingLives,
                PlayerTuning.StartingWaveNumber);

        return new GameSession(players)
        {
            ControlSettings = controlSettings ?? ControlSettings.CreateDefaults(),
            GameSettings = chosenGameSettings
        };
    }

    /// <summary>Says whether at least one player still has lives.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>ZP1LAS</c> and <c>ZP2LAS</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public bool AnyPlayerSlotHasLives()
    {
        return Players.Any(p => p.HasLives());
    }

    /// <summary>The player whose turn it is.</summary>
    public PlayerSlot GetCurrent()
    {
        return Players[CurrentIndex];
    }

    /// <summary>
    ///     Gets every player's score with their number, highest first. It is the order they are offered to the high score
    ///     table in (notes §98).
    /// </summary>
    public FinalScore[] GetFinalScoresHighestFirst()
    {
        return
        [
            .. Players.Select(playerSlot => new FinalScore(playerSlot.Number, playerSlot.Score))
                .OrderByDescending(score => score.Score)
        ];
    }

    /// <summary>Gets every player's score, highest first, for the game over screens to check.</summary>
    public int[] GetScoresHighestFirst()
    {
        return [.. GetFinalScoresHighestFirst().Select(score => score.Score)];
    }

    /// <summary>Says whether two people are playing.</summary>
    public bool IsTwoPlayer()
    {
        return Players.Count > 1;
    }

    /// <summary>Passes the turn to the other player after a death, if they have lives. Otherwise the turn stays where it is.</summary>
    /// <returns>True when the turn moved to the other player.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLE1B</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public bool SwitchToPlayerWithLives()
    {
        if (Players.Count < 2) return false;

        var otherIndex = (CurrentIndex + 1) % Players.Count;
        if (!Players[otherIndex]
                .HasLives()) return false; // ROM: PLE1, EORA #3 / PLDX / LDB PLAS,X / BEQ PLE1 (toggles back)

        CurrentIndex = otherIndex;
        return true;
    }
}
