namespace Robotron2084.Audio;

/// <summary>A word the Berzerk robot can say, or a pause between words.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Each word's number is its word number on the Berzerk speech chip (MAME <c>berzerk.cpp</c>; the Berzerk
///             disassembly's speech table). Its sample is the file of the same name in <c>Content/Speech/Berzerk/</c>.
///         </item>
///         <item>
///             <see cref="Pause" /> is not on the chip. It is a port addition: a short silence between two words
///             (<see cref="BerzerkVoice.PauseTicks" />).
///         </item>
///         <item>"THERE" is also the robot's "THEIR": the chip has one word for both.</item>
///     </list>
/// </remarks>
public enum BerzerkWord
{
    /// <summary>"HELP". On the chip, but the game never says it.</summary>
    Help = 0x00,

    /// <summary>"KILL".</summary>
    Kill = 0x01,

    /// <summary>"ATTACK".</summary>
    Attack = 0x02,

    /// <summary>"CHARGE".</summary>
    Charge = 0x03,

    /// <summary>"GOT".</summary>
    Got = 0x04,

    /// <summary>"SHOOT".</summary>
    Shoot = 0x05,

    /// <summary>"GET".</summary>
    Get = 0x06,

    /// <summary>"IS".</summary>
    Is = 0x07,

    /// <summary>"ALERT".</summary>
    Alert = 0x08,

    /// <summary>"DETECTED".</summary>
    Detected = 0x09,

    /// <summary>"THE".</summary>
    The = 0x0A,

    /// <summary>"IN".</summary>
    In = 0x0B,

    /// <summary>"IT".</summary>
    It = 0x0C,

    /// <summary>"THERE", which is also "THEIR".</summary>
    There = 0x0D,

    /// <summary>"WHERE".</summary>
    Where = 0x0E,

    /// <summary>"HUMANOID".</summary>
    Humanoid = 0x0F,

    /// <summary>"COINS".</summary>
    Coins = 0x10,

    /// <summary>"POCKET".</summary>
    Pocket = 0x11,

    /// <summary>"INTRUDER".</summary>
    Intruder = 0x12,

    /// <summary>"NO".</summary>
    No = 0x13,

    /// <summary>"ESCAPE".</summary>
    Escape = 0x14,

    /// <summary>"DESTROY".</summary>
    Destroy = 0x15,

    /// <summary>"MUST".</summary>
    Must = 0x16,

    /// <summary>"NOT".</summary>
    Not = 0x17,

    /// <summary>"CHICKEN".</summary>
    Chicken = 0x18,

    /// <summary>"FIGHT".</summary>
    Fight = 0x19,

    /// <summary>"LIKE".</summary>
    Like = 0x1A,

    /// <summary>"A".</summary>
    A = 0x1B,

    /// <summary>"ROBOT".</summary>
    Robot = 0x1C,

    /// <summary>A short silence between two words. Not on the chip: a port addition.</summary>
    Pause = 0x40
}
