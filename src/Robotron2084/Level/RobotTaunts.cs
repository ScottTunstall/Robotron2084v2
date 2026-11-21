using Robotron2084.Audio;
using Robotron2084.Audio.Models;
using Robotron2084.Core;
using Robotron2084.Level.Models;

namespace Robotron2084.Level;

/// <summary>
///     The Berzerk robot's taunts during a game: when the robots speak next, and what they say, including what they say
///     when a player is killed (<see cref="SayPlayerKilled" />). Port only. The taunts during a wave are only for a wave with
///     <see cref="WaveParams.EnableBerzerkSpeak" /> on, and what they say when a player is killed is for a
///     <see cref="GameStyle.Extended" /> game: a <see cref="GameStyle.Classic" /> game never has either.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             The robots only taunt while they may move (<see cref="PlayField.RobotsFrozen" />), once the wave-end music has
///             finished, and once nothing else is being said or waiting to be said. So a sentence never starts before the
///             last one has finished.
///         </item>
///         <item>
///             When they speak is Berzerk's talk timer: a count of seconds that starts at
///             <see cref="TalkTimerStartSeconds" /> when the game starts and goes down by one each second while the robots
///             are allowed to taunt. When it reaches 0 they say a sentence, and it starts again from 4, 8, 12 or 16 seconds
///             (<see cref="GetRandomTalkTimerSeconds" />). It carries from one wave to the next.
///         </item>
///         <item>
///             What they say depends on whether a player is a chicken (<see cref="PlayerSlot.IsChicken" />), on the
///             <see cref="Models.GameMode" />, and on whether there is a family member on the field with a hulk or a brain
///             (<see cref="RobotTauntSentences" />). A chicken is called one in about every other taunt
///             (<see cref="ChickenCallRandomRange" />).
///         </item>
///         <item>
///             Each sentence is said at a random pitch and volume, the same for all its words, as the Berzerk robots'
///             taunts are: from deep and slow to high and quick, and from a little quieter to full
///             (<see cref="GetRandomVoiceSetting" />, <see cref="BerzerkVoice" />).
///         </item>
///         <item>
///             Saying a sentence only puts its words in the speech queue, which plays them on the sound system's own
///             thread. Each tick costs a few checks, so the taunts never slow the game.
///         </item>
///     </list>
/// </remarks>
public sealed class RobotTaunts
{
    /// <summary>
    ///     How rarely a chicken is called a chicken: one taunt in this many, on average. Port only: the author asked for a
    ///     chicken to be called one regularly.
    /// </summary>
    public const int ChickenCallRandomRange = 2;

    /// <summary>The longest the talk timer starts again from after a taunt, in seconds.</summary>
    public const int LongestTalkTimerSeconds =
        ShortestTalkTimerSeconds + (TalkTimerRandomRange - 1) * TalkTimerStepSeconds;

    /// <summary>The shortest the talk timer starts again from after a taunt, in seconds.</summary>
    /// <remarks>
    ///     Original source: <c>berzerk.asm</c> <c>GENERATE_ROBOT_SPEECH</c>, <c>ADD A,$04</c> ("ensure timer is 4 minimum").
    ///     Disassembly: <c>$2BA9</c>.
    /// </remarks>
    public const int ShortestTalkTimerSeconds = 4;

    /// <summary>The talk timer when a game starts, in seconds: the robots may taunt on the game's first second.</summary>
    /// <remarks>
    ///     Original source: the author's Berzerk disassembly, <c>berzerk.asm</c>, the game start code that sets
    ///     <c>TALK_TIMER</c> to 1 (<c>INC A / LD ($089B),A</c>). Disassembly: <c>$17E6</c>.
    /// </remarks>
    public const int TalkTimerStartSeconds = 1;

    /// <summary>How many lengths of talk timer there are to pick from: the four values of two random bits.</summary>
    private const int TalkTimerRandomRange = 4;

    /// <summary>The gap between one length of talk timer and the next, in seconds: 4, 8, 12 or 16.</summary>
    /// <remarks>
    ///     Original source: <c>berzerk.asm</c> <c>GENERATE_ROBOT_SPEECH</c>, a random number turned left twice and
    ///     <c>AND $0C</c>, which leaves its lowest two bits worth 4 each. Disassembly: <c>$2BA2</c> to <c>$2BA7</c>.
    /// </remarks>
    private const int TalkTimerStepSeconds = 4;

    /// <summary>
    ///     The bits always set in a taunt's control byte: the top bit of the volume, so it is never below 4, and the bit
    ///     that marks a control byte (<c>berzerk.asm</c> <c>GENERATE_ROBOT_SPEECH</c>, <c>OR $60</c> at <c>$2BB4</c>).
    /// </summary>
    private const int TauntControlByteFixedBits = 0x60;

    /// <summary>
    ///     How many values the random part of a taunt's control byte can take: 0 to <c>$1F</c>, the pitch and the low two
    ///     bits of the volume (<c>berzerk.asm</c> <c>GENERATE_ROBOT_SPEECH</c>, <c>AND $1F</c> at <c>$2BB2</c>).
    /// </summary>
    private const int TauntControlByteRandomRange = 0x20;

    /// <summary>"GOT THE HUMANOID": what the robots say when a player is killed (one of two sentences).</summary>
    private static readonly BerzerkWord[] GotTheHumanoid = [BerzerkWord.Got, BerzerkWord.The, BerzerkWord.Humanoid];

    /// <summary>"GOT THE HUMANOIDS": said when every player is killed.</summary>
    private static readonly BerzerkWord[] GotTheHumanoids = [BerzerkWord.Got, BerzerkWord.The, BerzerkWord.Humanoids];

    /// <summary>"GOT THE INTRUDER": what the robots say when a player is killed (the other of two sentences).</summary>
    private static readonly BerzerkWord[] GotTheIntruder = [BerzerkWord.Got, BerzerkWord.The, BerzerkWord.Intruder];

    /// <summary>"GOT THE INTRUDERS": said when every player is killed.</summary>
    private static readonly BerzerkWord[] GotTheIntruders = [BerzerkWord.Got, BerzerkWord.The, BerzerkWord.Intruders];

    /// <summary>The voice of "GOT THE HUMANOID": Berzerk's own control byte for it, <c>$7B</c> (pitch 3, volume 7).</summary>
    /// <remarks>
    ///     Original source: the author's Berzerk disassembly, <c>berzerk.asm</c>
    ///     <c>GOT_THE_HUMANOID_GOT_THE_INTRUDER_SPEECH_BYTES</c>. Disassembly: <c>$2C40</c>.
    /// </remarks>
    private static readonly BerzerkVoiceSetting HumanoidVoiceSetting = BerzerkVoiceSetting.CreateFromControlByte(0x7B);

    /// <summary>The voice of "GOT THE INTRUDER": Berzerk's own control byte for it, <c>$7D</c> (pitch 5, volume 7).</summary>
    /// <remarks>The same routine as <see cref="HumanoidVoiceSetting" />, at <c>$2C45</c>.</remarks>
    private static readonly BerzerkVoiceSetting IntruderVoiceSetting = BerzerkVoiceSetting.CreateFromControlByte(0x7D);

    private readonly Func<bool> _isSpeechBusy;
    private readonly Func<bool> _isWaveEndMusicPlaying;
    private readonly Random _random;
    private readonly Action<BerzerkVoiceSetting, SpeechPriority, IReadOnlyList<BerzerkWord>> _sayBerzerkWords;

    /// <summary>
    ///     The talk timer: seconds the robots must be allowed to taunt for before they say the next sentence.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>berzerk.asm</c> <c>TALK_TIMER</c>, counted down in <c>GENERATE_ROBOT_SPEECH</c>.
    ///     Disassembly: <c>$089B</c>, counted down at <c>$2BA0</c>.
    /// </remarks>
    private int _talkTimerSeconds = TalkTimerStartSeconds;

    /// <summary>
    ///     Ticks since the last whole second, which is when the talk timer is counted down. The seconds run on whether or
    ///     not the robots may taunt, as Berzerk's clock of seconds played does.
    /// </summary>
    /// <remarks>
    ///     Original source: <c>berzerk.asm</c>, the job that adds a second to <c>CMOS_TOTAL_SECS_OF_PLAY</c> every 60 frames
    ///     and then calls <c>GENERATE_ROBOT_SPEECH</c>. Disassembly: <c>$21A3</c> to <c>$21C3</c>.
    /// </remarks>
    private int _ticksSinceSecond;

    /// <summary>Makes the taunts for a new game, with the talk timer at <see cref="TalkTimerStartSeconds" />.</summary>
    /// <param name="random">Where the talk timers, the voices and the sentences are picked from.</param>
    /// <param name="isSpeechBusy">Says whether anything is being said or waiting to be said.</param>
    /// <param name="isWaveEndMusicPlaying">Says whether the wave-end music is still playing.</param>
    /// <param name="sayBerzerkWords">
    ///     Has the Berzerk robot say a sentence's words, one after another, at a pitch and volume, and how much the sentence
    ///     matters (<see cref="SpeechPriority" />).
    /// </param>
    public RobotTaunts(Random random, Func<bool> isSpeechBusy, Func<bool> isWaveEndMusicPlaying,
        Action<BerzerkVoiceSetting, SpeechPriority, IReadOnlyList<BerzerkWord>> sayBerzerkWords)
    {
        _random = random;
        _isSpeechBusy = isSpeechBusy;
        _isWaveEndMusicPlaying = isWaveEndMusicPlaying;
        _sayBerzerkWords = sayBerzerkWords;
    }

    /// <summary>
    ///     Makes the taunts for a new game, spoken by the game's speech service (<see cref="Speech" />) and held back by
    ///     its wave-end music (<see cref="Sound.IsWaveEndMusicPlaying" />). They draw on a random source of their own, so
    ///     the robots behave the same whether or not they talk.
    /// </summary>
    public static RobotTaunts CreateWithGameSpeech()
    {
        return new RobotTaunts(new Random(), Speech.IsBusy, Sound.IsWaveEndMusicPlaying,
            (berzerkVoiceSetting, speechPriority, berzerkWords) => Speech.SayBerzerk(berzerkVoiceSetting, speechPriority, [.. berzerkWords]));
    }

    /// <summary>Says whether a sentence calls a player a chicken: it has CHICKEN or CHICKENS in it.</summary>
    /// <param name="berzerkWords">The sentence's words.</param>
    public static bool IsChickenCall(IReadOnlyList<BerzerkWord> berzerkWords)
    {
        return berzerkWords.Contains(BerzerkWord.Chicken) || berzerkWords.Contains(BerzerkWord.Chickens);
    }

    /// <summary>Gets a sentence for the robots to say, picked at random from those that fit the game just now.</summary>
    /// <param name="gameSession">The game, which says the game mode and who is a chicken.</param>
    /// <param name="playField">The field being played, which says whether the family is there with a hulk or a brain.</param>
    /// <returns>The sentence's words, in order.</returns>
    /// <remarks>
    ///     When a player is a chicken, one taunt in <see cref="ChickenCallRandomRange" /> on average calls them a chicken,
    ///     picked from the sentences that do; the rest are picked from the sentences that do not.
    /// </remarks>
    public IReadOnlyList<BerzerkWord> GetRandomSentence(GameSession gameSession, PlayField playField)
    {
        IReadOnlyList<IReadOnlyList<BerzerkWord>> familySentences =
            IsFamilyWithHulksOrBrains(playField) ? RobotTauntSentences.FamilyWithHulksOrBrains : [];
        IReadOnlyList<IReadOnlyList<BerzerkWord>> sentences = [.. GetPlayerSentences(gameSession), .. familySentences];
        if (IsChickenTaunted(gameSession))
        {
            var isChickenCallPicked = _random.Next(ChickenCallRandomRange) == 0;
            sentences = [.. sentences.Where(sentence => IsChickenCall(sentence) == isChickenCallPicked)];
        }

        return sentences[_random.Next(sentences.Count)];
    }

    /// <summary>
    ///     Gets how long the talk timer starts again from after a taunt, picked at random as Berzerk picks it: 4, 8, 12 or 16
    ///     seconds, each as likely as the others.
    /// </summary>
    /// <returns>The talk timer, in seconds.</returns>
    public int GetRandomTalkTimerSeconds()
    {
        return ShortestTalkTimerSeconds + _random.Next(TalkTimerRandomRange) * TalkTimerStepSeconds;
    }

    /// <summary>
    ///     Gets the voice for a sentence, picked at random as Berzerk picks it: any pitch from
    ///     <see cref="BerzerkVoice.MinimumPitch" /> (deepest and slowest) to <see cref="BerzerkVoice.MaximumPitch" /> (highest
    ///     and quickest), and any volume from 4 to <see cref="BerzerkVoice.MaximumVolume" />, each as likely as the others.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: the author's Berzerk disassembly, <c>berzerk.asm</c> <c>GENERATE_ROBOT_SPEECH</c>: the
    ///             sentence's control byte is a random number <c>AND $1F / OR $60</c> (<see cref="BerzerkVoiceSetting" />).
    ///         </item>
    ///         <item>Disassembly: <c>$2BAF</c> to <c>$2BB6</c>.</item>
    ///     </list>
    /// </remarks>
    public BerzerkVoiceSetting GetRandomVoiceSetting()
    {
        var controlByte = _random.Next(TauntControlByteRandomRange) | TauntControlByteFixedBits;
        return BerzerkVoiceSetting.CreateFromControlByte(controlByte);
    }

    /// <summary>
    ///     Says whether the robots may taunt just now: the wave has Berzerk speech on, the robots may move, the wave-end
    ///     music has finished, and nothing is being said or waiting to be said.
    /// </summary>
    /// <param name="playField">The field being played.</param>
    public bool IsAllowedToTaunt(PlayField playField)
    {
        return playField.WaveParams.EnableBerzerkSpeak && !playField.RobotsFrozen() && !_isWaveEndMusicPlaying() &&
               !_isSpeechBusy();
    }

    /// <summary>
    ///     In EXTENDED, has the robots say they have got the player they have just killed: "GOT THE HUMANOID" or "GOT THE
    ///     INTRUDER", picked at random. When every player of a two player game has been killed, they say both, in the plural:
    ///     "GOT THE HUMANOIDS" and "GOT THE INTRUDERS".
    /// </summary>
    /// <param name="gameSession">The game, which says which game style it is and whether any player has lives left.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: the author's Berzerk disassembly, <c>berzerk.asm</c> <c>SAY_GOT_THE_HUMANOID_GOT_THE_INTRUDER</c>
    ///             (<c>$2C1F</c>), which the game calls when the player is killed by a robot (<c>$1FB3</c>).
    ///         </item>
    ///         <item>
    ///             They are <see cref="SpeechPriority.Important" />: they stop a taunt being said, and anything waiting to be
    ///             said, so they are heard at once. A CLASSIC game never says them.
    ///         </item>
    ///     </list>
    /// </remarks>
    public void SayPlayerKilled(GameSession gameSession)
    {
        ArgumentNullException.ThrowIfNull(gameSession);
        if (gameSession.GameSettings.GameStyle != GameStyle.Extended) return;

        if (gameSession.IsTwoPlayer() && !gameSession.AnyPlayerSlotHasLives())
        {
            _sayBerzerkWords(HumanoidVoiceSetting, SpeechPriority.Important, GotTheHumanoids);
            _sayBerzerkWords(IntruderVoiceSetting, SpeechPriority.Important, GotTheIntruders);
            return;
        }

        if (_random.Next(2) == 0) _sayBerzerkWords(HumanoidVoiceSetting, SpeechPriority.Important, GotTheHumanoid);
        else _sayBerzerkWords(IntruderVoiceSetting, SpeechPriority.Important, GotTheIntruder);
    }

    /// <summary>
    ///     Moves the taunts on by one tick. On each whole second, while the robots are allowed to taunt, counts the talk
    ///     timer down; when it reaches 0, starts it again and says a sentence at a random pitch and volume.
    /// </summary>
    /// <param name="gameSession">The game, which says the game mode and who is a chicken.</param>
    /// <param name="playField">The field being played.</param>
    /// <remarks>
    ///     Original source: <c>berzerk.asm</c> <c>GENERATE_ROBOT_SPEECH</c>, which leaves at once while the voice is talking
    ///     (<c>VOICE_PC</c> is set), so the timer only counts the seconds the voice is quiet. Disassembly: <c>$2B97</c> to
    ///     <c>$2BAB</c>.
    /// </remarks>
    public void Tick(GameSession gameSession, PlayField playField)
    {
        if (++_ticksSinceSecond < ArcadeClock.PortTicksPerSecond) return;

        _ticksSinceSecond = 0;
        if (!IsAllowedToTaunt(playField)) return;

        if (--_talkTimerSeconds > 0) return;

        _talkTimerSeconds = GetRandomTalkTimerSeconds();
        _sayBerzerkWords(GetRandomVoiceSetting(), SpeechPriority.Normal, GetRandomSentence(gameSession, playField));
    }

    /// <summary>Gets the sentences about the players: for one player or two on the field, and for a chicken or not.</summary>
    /// <param name="gameSession">The game.</param>
    private static IReadOnlyList<IReadOnlyList<BerzerkWord>> GetPlayerSentences(GameSession gameSession)
    {
        var isChickenTaunted = IsChickenTaunted(gameSession);
        if (IsTwoPlayersOnField(gameSession))
            return isChickenTaunted ? RobotTauntSentences.TwoPlayersChicken : RobotTauntSentences.TwoPlayers;

        return isChickenTaunted ? RobotTauntSentences.OnePlayerChicken : RobotTauntSentences.OnePlayer;
    }

    /// <summary>
    ///     Says whether the robots taunt a chicken: with two players on the field at once, either of them is a chicken;
    ///     otherwise the current player is.
    /// </summary>
    /// <param name="gameSession">The game.</param>
    private static bool IsChickenTaunted(GameSession gameSession)
    {
        if (IsTwoPlayersOnField(gameSession)) return gameSession.PlayerSlots.Any(playerSlot => playerSlot.IsChicken);

        return gameSession.GetCurrent().IsChicken;
    }

    /// <summary>Says whether a family member is on the field with at least one hulk or brain.</summary>
    /// <param name="playField">The field being played.</param>
    private static bool IsFamilyWithHulksOrBrains(PlayField playField)
    {
        var fieldEntities = playField.FieldEntities;
        return fieldEntities.FamilyList.AnyAlive() &&
               (fieldEntities.Hulks.AnyAlive() || fieldEntities.Brains.AnyAlive());
    }

    /// <summary>
    ///     Says whether two players are on the field at once: a two-player simultaneous game in which both players still
    ///     have lives. When only one is left, the robots taunt that one player alone.
    /// </summary>
    /// <param name="gameSession">The game.</param>
    private static bool IsTwoPlayersOnField(GameSession gameSession)
    {
        return gameSession.GameMode == GameMode.TwoPlayerSimultaneous &&
               gameSession.PlayerSlots.All(playerSlot => playerSlot.HasLives());
    }
}
