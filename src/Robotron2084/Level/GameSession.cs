using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>A game that is being played: each player's details, and whose turn it is. It is passed from screen to screen, so a cleared wave or a death keeps every player's score, men and wave.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>PLRCNT</c> for the number of players and <c>CURPLR</c> for whose turn it is; the turn passes in <c>PLEND</c>, <c>PLEND3</c> and <c>PLE1B</c></item>
/// <item>Disassembly: not separately labelled</item>
/// </list>
/// When a man is lost, the turn passes to the other player if they still have men. Otherwise the same player carries on, and when nobody has any men left the game is over.
/// </remarks>
public sealed class GameSession
{
    /// <summary>Makes a game with these players.</summary>
    /// <param name="players">The players, with player 1 first.</param>
    private GameSession(PlayerSlot[] players)
    {
        Players = players;
    }

    /// <summary>Says whether at least one player still has men.</summary>
    /// <remarks>Original source: <c>RRF.ASM</c> <c>ZP1LAS</c> and <c>ZP2LAS</c>. Disassembly: not separately labelled.</remarks>
    public bool AnyMenLeft() => Players.Any(p => p.HasMen);

    /// <summary>The keys and buttons the players have chosen. They go with the game so that every screen can read them, such as the pause key (notes §101).</summary>
    public ControlSettings ControlSettings { get; private init; } = ControlSettings.CreateDefaults();

    /// <summary>The game adjustment settings the game was started with. They go with the game, as <see cref="Controls"/> do, because the screens after a game over need them (notes §131).</summary>
    public GameSettings GameSettings { get; private init; } = GameSettings.CreateFactoryDefaults();

    /// <summary>The player whose turn it is.</summary>
    public PlayerSlot Current => Players[CurrentIndex];

    /// <summary>The place in <see cref="Players"/> of the player whose turn it is.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>CURPLR</c>. Disassembly: <c>current_player</c>.</remarks>
    public int CurrentIndex { get; private set; }

    /// <summary>Says whether two people are playing.</summary>
    public bool IsTwoPlayer => Players.Count > 1;

    /// <summary>The players, with player 1 first. There are one or two.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLRCNT</c>. Disassembly: <c>num_players</c>.</remarks>
    public IReadOnlyList<PlayerSlot> Players { get; }

    /// <summary>Starts a new game for one or two players, as the arcade's two start buttons do.</summary>
    /// <param name="input">The first player's controls.</param>
    /// <param name="playerCount">How many people are playing, 1 or 2.</param>
    /// <param name="secondPlayerInput">The second player's controls, or null to share the first player's.</param>
    public static GameSession CreateNewGame(IPlayerInputSource input, int playerCount, IPlayerInputSource? secondPlayerInput = null)
    {
        if (playerCount is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Robotron is a 1 or 2 player game (ROM PLRCNT).");
        }

        return CreateNewGame(
            playerCount == 2 ? GameMode.TwoPlayerAlternate : GameMode.OnePlayer,
            input,
            secondPlayerInput);
    }

    /// <summary>Starts a new game in one of the modes. The mode says how many people play, and each player has their own controls (notes §101).</summary>
    /// <param name="mode">How many people are playing, and whether two take turns.</param>
    /// <param name="playerOne">The first player's controls.</param>
    /// <param name="playerTwo">The second player's controls, or null to share the first player's.</param>
    /// <param name="controls">The keys and buttons the players have chosen, or null for the standard ones.</param>
    /// <param name="settings">The game adjustment settings, or null for the factory ones (notes §131). They say how many men each player starts with, and travel with the game for the screens that follow.</param>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>NSHIP</c>, the men each player starts with, which is loaded into <c>PLAS</c>. Disassembly: not separately labelled.</remarks>
    public static GameSession CreateNewGame(GameMode mode, IPlayerInputSource playerOne, IPlayerInputSource? playerTwo = null, ControlSettings? controls = null, GameSettings? settings = null)
    {
        GameSettings gameSettings = settings ?? GameSettings.CreateFactoryDefaults();
        int playerCount = mode == GameMode.OnePlayer ? 1 : 2;
        var players = new PlayerSlot[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            players[i] = new PlayerSlot(
                i + 1,
                i == 0 ? playerOne : playerTwo ?? playerOne,
                settings?.TurnsPerPlayer ?? PlayerTuning.StartingLives,
                PlayerTuning.StartingLevelNumber);
        }

        return new GameSession(players)
        {
            ControlSettings = controls ?? ControlSettings.CreateDefaults(),
            GameSettings = gameSettings,
        };
    }

    /// <summary>Gets every player's score with their number, highest first. It is the order they are offered to the high score table in (notes §98).</summary>
    public FinalScore[] GetFinalScoresHighestFirst() =>
        [.. Players.Select(player => new FinalScore(player.Number, player.Score)).OrderByDescending(score => score.Score)];

    /// <summary>Gets every player's score, highest first, for the game over screens to check.</summary>
    public int[] GetScoresHighestFirst() => [.. GetFinalScoresHighestFirst().Select(score => score.Score)];

    /// <summary>Passes the turn to the other player after a death, if they have men. Otherwise the turn stays where it is.</summary>
    /// <returns>True when the turn moved to the other player.</returns>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLE1B</c>. Disassembly: not separately labelled.</remarks>
    public bool SwitchToPlayerWithMen()
    {
        if (Players.Count < 2)
        {
            return false;
        }

        int otherIndex = (CurrentIndex + 1) % Players.Count;
        if (!Players[otherIndex].HasMen)
        {
            return false; // ROM: PLE1, EORA #3 / PLDX / LDB PLAS,X / BEQ PLE1 (toggles back)
        }

        CurrentIndex = otherIndex;
        return true;
    }
}
