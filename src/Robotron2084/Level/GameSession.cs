using Robotron2084.Input;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// A game in progress: one <see cref="PlayerSlot"/> per player (1 or 2 — the ROM's
/// <c>PLRCNT</c>) plus whose turn it is (<c>CURPLR</c>). Carried across states so
/// a wave clear or a death keeps every player's score, lives and wave.
///
/// The arcade's turn rule (RRG23 <c>PLEND</c> → <c>PLEND3</c> → <c>PLE1B</c>): when
/// a life ends, the turn passes to the other player IF they still have men —
/// otherwise the same player continues, and when nobody has men the game is over.
/// </summary>
public sealed class GameSession
{
    private GameSession(PlayerSlot[] players)
    {
        Players = players;
    }

    /// <summary>True while at least one player still has men (ROM <c>ZP1LAS | ZP2LAS</c>).</summary>
    public bool AnyMenLeft() => Players.Any(p => p.HasMen);

    /// <summary>
    /// The port's control definitions, carried with the game (notes §101) so that every
    /// state holding the session — the playfield, a wave clear, the high score table —
    /// can read the PAUSE key without each of them being handed the settings separately.
    /// </summary>
    public ControlSettings Controls { get; private init; } = ControlSettings.CreateDefaults();

    /// <summary>
    /// The GAME ADJUSTMENT settings the game was started under (notes §131), carried with the game
    /// exactly as <see cref="Controls"/> is: the states that follow a game over build their
    /// <see cref="States.GameServices"/> from the session, so the settings have to ride along.
    /// </summary>
    public GameSettings Settings { get; private init; } = GameSettings.CreateFactoryDefaults();

    /// <summary>The player whose turn it is.</summary>
    public PlayerSlot Current => Players[CurrentIndex];

    /// <summary>Index into <see cref="Players"/> of the player whose turn it is (ROM CURPLR).</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>True for the arcade's two-player game (ROM PLRCNT == 2).</summary>
    public bool IsTwoPlayer => Players.Count > 1;

    /// <summary>Player 1 first; length 1 or 2 (ROM PLRCNT).</summary>
    public IReadOnlyList<PlayerSlot> Players { get; }

    /// <summary>
    /// A fresh game by player count: the arcade's START 1 / START 2 (ROM PLRCNT) — a thin
    /// wrapper over the mode-based factory, kept because the START buttons are still how
    /// the cabinet starts a game.
    /// </summary>
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

    /// <summary>
    /// A fresh game in a given mode (notes §101): the mode picks the player count, and
    /// each player gets their OWN input source — which is the point of the DEFINITIONS
    /// page, since player 2 no longer has to share player 1's controls.
    /// </summary>
    /// <param name="settings">
    /// The GAME ADJUSTMENT settings (notes §131), or null for the factory ones. TURNS PER PLAYER is
    /// the men each player starts with (ROM <c>NSHIP</c> → <c>PLAS</c>); the rest travel with the
    /// session for the states that follow.
    /// </param>
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
            Controls = controls ?? ControlSettings.CreateDefaults(),
            Settings = gameSettings,
        };
    }

    /// <summary>Every player's final score with their player number, highest first — the order the end-game ceremony offers them to the high score table (notes §98).</summary>
    public FinalScore[] GetFinalScoresHighestFirst() =>
        [.. Players.Select(player => new FinalScore(player.Number, player.Score)).OrderByDescending(score => score.Score)];

    /// <summary>Every player's score, highest first — what the game-over flow checks.</summary>
    public int[] GetScoresHighestFirst() => [.. GetFinalScoresHighestFirst().Select(score => score.Score)];

    /// <summary>
    /// The turn's advance after a death (ROM <c>PLE1B</c>): flip to the other
    /// player, and if they have no men flip back — so a 1-player game stays on
    /// player 1 and a 2-player game always alternates while both are alive.
    /// Returns true when the turn actually moved.
    /// </summary>
    public bool SwitchToPlayerWithMen()
    {
        if (Players.Count < 2)
        {
            return false;
        }

        int other = (CurrentIndex + 1) % Players.Count;
        if (!Players[other].HasMen)
        {
            return false; // ROM PLE1: EORA #3 / PLDX / LDB PLAS,X / BEQ PLE1 (toggles back)
        }

        CurrentIndex = other;
        return true;
    }
}
