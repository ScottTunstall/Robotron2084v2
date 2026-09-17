using System;
using System.Collections.Generic;
using System.Linq;
using Robotron2084.Input;
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

    /// <summary>Player 1 first; length 1 or 2 (ROM PLRCNT).</summary>
    public IReadOnlyList<PlayerSlot> Players { get; }

    /// <summary>Index into <see cref="Players"/> of the player whose turn it is (ROM CURPLR).</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>The player whose turn it is.</summary>
    public PlayerSlot Current => Players[CurrentIndex];

    /// <summary>True for the arcade's two-player game (ROM PLRCNT == 2).</summary>
    public bool IsTwoPlayer => Players.Count > 1;

    /// <summary>True while at least one player still has men (ROM <c>ZP1LAS | ZP2LAS</c>).</summary>
    public bool AnyMenLeft => Players.Any(p => p.HasMen);

    /// <summary>
    /// A fresh game: <paramref name="playerCount"/> players (1 or 2) at wave 1 with
    /// the CMOS "ships per credit" lives (ROM START1/START2 → <c>ZP1LAS = NSHIP</c>).
    /// </summary>
    public static GameSession NewGame(IPlayerInputSource input, int playerCount, IPlayerInputSource? secondPlayerInput = null)
    {
        if (playerCount is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Robotron is a 1 or 2 player game (ROM PLRCNT).");
        }

        var players = new PlayerSlot[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            players[i] = new PlayerSlot(
                i + 1,
                i == 0 ? input : secondPlayerInput ?? input,
                GameplayConstants.StartingLives,
                GameplayConstants.StartingLevelNumber);
        }

        return new GameSession(players);
    }

    /// <summary>Rebuilds a session from carried-over player state (tests / save-style flows).</summary>
    public static GameSession FromSlots(IEnumerable<PlayerSlot> players, int currentIndex)
    {
        PlayerSlot[] array = players.ToArray();
        if (array.Length is < 1 or > 2)
        {
            throw new ArgumentException("a session has 1 or 2 players", nameof(players));
        }

        return new GameSession(array) { CurrentIndex = currentIndex };
    }

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

    /// <summary>Every player's score, highest first — what the game-over flow checks.</summary>
    public int[] ScoresHighestFirst() => Players.Select(p => p.Score).OrderByDescending(s => s).ToArray();
}
