using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;

namespace Robotron2084.Level;

/// <summary>Brings the robots on at the start of a wave: they appear one after another, each one forming out of strips of its own sprite. On a brain wave they are beamed in instead.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, which makes one pass
/// each ROM frame (<c>NAP 1,APL</c>); each pass starts the appear effect of the next robot on the
/// robot list, and every fourth one fans in by columns (<c>HAPST</c>)</item>
/// <item>Disassembly: <c>$28FE</c> to <c>$2962</c></item>
/// </list> The robots are switched off before the
/// loop starts (<c>ROBOFF</c>), so a robot is not on the screen until the loop draws it whole. The
/// loop does that for one more robot on each pass, starting some passes after it began (<see
/// cref="PassesBeforeRobotIsDrawnWhole"/>), by which time that robot's appear effect has finished
/// (notes §62, §143). <para> The arcade walks its robot list, and only the robots on that list appear
/// this way: the grunts, the hulks, the brains and the tanks. The spheroids and the quarks are on
/// another list and are on the screen from the start. </para> <para> "Transport" in the names here is
/// the arcade's own name for beaming the robots in at the start of a brain wave: the original source
/// calls that effect the transporter (<c>RRT2.ASM</c>, titled <c>TRANSPORTER</c>, started by
/// <c>TRNSTV</c>, "START TRANSPORTING"). Here it is done by <see cref="RobotTransporter"/>. </para></item>
/// </list>
/// </remarks>
public sealed class WaveMaterialisation
{
    /// <summary>Picks every fourth robot to fan in by columns instead of rows. It is used on the number of passes made before the robot's own: the robot fans in by columns when the last two bits of that number are both set.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, <c>ANDA #3 / CMPA #3</c>.</item>
    /// <item>Disassembly: <c>$291C</c>.</item>
    /// </list>
    /// </remarks>
    private const int ColumnFanSequenceMask = 3;

    /// <summary>How many passes after a robot's appear effect starts the loop draws the robot whole. It is added to the robot's place in the order to give the pass on which the robot stops being held back.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, <c>CMPA #32 / BLS AP4</c>, after which each
    /// pass moves the end of the list that <c>APREF</c> redraws one robot along ("EXPAND REFRESH
    /// LIST").</item>
    /// <item>Disassembly: <c>$2930</c> to <c>$293E</c>, and <c>$2965</c>.</item>
    /// </list>
    /// </remarks>
    private const int PassesBeforeRobotIsDrawnWhole = 32;

    /// <summary>The top row of the arcade's playfield on its screen. With <see cref="ArcadeBottomRow"/> it turns a robot's place on the port's playfield into the row the arcade would have it on, which sets the row its strips close in on.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>YMIN</c>.</item>
    /// <item>Disassembly: the compare at <c>$5DB6</c>.</item>
    /// </list>
    /// </remarks>
    private const int ArcadeTopRow = 24;

    /// <summary>The bottom row of the arcade's playfield on its screen.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>YMAX</c>.</item>
    /// <item>Disassembly: not separately labelled.</item>
    /// </list>
    /// </remarks>
    private const int ArcadeBottomRow = 234;

    /// <summary>The left column of the arcade's playfield on its screen. With <see cref="ArcadeRightColumn"/> it turns a robot's place on the port's playfield into the column the arcade would have it on, which sets the column its strips close in on.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>XMIN</c>.</item>
    /// <item>Disassembly: not separately labelled.</item>
    /// </list>
    /// </remarks>
    private const int ArcadeLeftColumn = 7;

    /// <summary>The right column of the arcade's playfield on its screen.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRF.ASM</c> <c>XMAX</c>.</item>
    /// <item>Disassembly: not separately labelled.</item>
    /// </list>
    /// </remarks>
    private const int ArcadeRightColumn = 0x8F;

    /// <summary>The largest number the arcade's screen coordinates and its 8-bit sums can hold. A robot's column is doubled to make it a pixel, and the doubled column is held at this when it would be more.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APCENT</c>, <c>ASLA / BCC APC1 / LDA #$FF</c>.</item>
    /// <item>Disassembly: <c>$29B5</c> onwards.</item>
    /// </list>
    /// </remarks>
    private const int LargestByte = 0xFF;

    /// <summary>The place in the order of each robot that the loop has not drawn whole yet. A robot in here does not act and is not drawn as itself.</summary>
    private readonly Dictionary<IEntity, int> _appearNumbers = [];

    /// <summary>The robot that has each place in the order, for the robots the loop has not drawn whole yet.</summary>
    private readonly Dictionary<int, IEntity> _robotsByAppearNumber = [];

    /// <summary>The robots waiting to be beamed in together, when the wave is a brain wave. "Transport" is the arcade's own word for beaming in.</summary>
    private readonly List<IEntity> _transportQueue = [];

    /// <summary>The robots being beamed in.</summary>
    private readonly HashSet<IEntity> _transported = [];

    /// <summary>The transporter that beams the robots in, or null when the wave is not a brain wave.</summary>
    private readonly RobotTransporter? _transporter;

    /// <summary>Counts up to the next ROM frame, in clock units, because the loop makes one pass on each ROM frame. It starts with a whole ROM frame in it, so that the first pass is made on the first tick, as the arcade makes its first pass at once.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, <c>NAP 1,APL</c>.</item>
    /// <item>Disassembly: <c>$2949</c>.</item>
    /// </list>
    /// </remarks>
    private int _appearClockUnits = ArcadeClock.UnitsPerRomFrame;

    /// <summary>The last place in the order that has been given out.</summary>
    private int _lastAppearNumber;

    /// <summary>How many passes the loop has made.</summary>
    private int _passes;

    /// <summary>True once the beaming in has started.</summary>
    private bool _transportBegun;

    /// <summary>Makes the sequence for one wave.</summary>
    /// <param name="random">The field's random source, which the transporter's sparkle uses.</param>
    /// <param name="beamsIn">True on a brain wave, where the robots are beamed in by the transporter instead of appearing strip by strip.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>PLS00</c> and the test of <c>BRNCNT</c> before it ("BRAIN
    /// WAVE???").</item>
    /// <item>Disassembly: <c>$2860</c> to <c>$2871</c>.</item>
    /// </list>
    /// </remarks>
    public WaveMaterialisation(Random random, bool beamsIn) => _transporter = beamsIn ? new RobotTransporter(random) : null;

    /// <summary>The number of robots that have not yet been given their turn to appear.</summary>
    public int GetPendingCount() => _appearNumbers.Values.Count(appearNumber => appearNumber > _passes) + _transportQueue.Count;

    /// <summary>Moves the sequence on by one tick. On each ROM frame the loop makes a pass: it starts the appear effect of the robot whose turn it is, and it draws whole the robot whose appear effect has had time to finish.</summary>
    /// <param name="entities">Everything on the field. The appear effects join its strip effects, and it says whether a strip routine has a record free.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>.</item>
    /// <item>Disassembly: <c>$290D</c> to <c>$294E</c>.</item>
    /// </list>
    /// </remarks>
    public void Advance(FieldEntities entities, Rectangle playfieldBounds)
    {
        if (_transporter is not null)
        {
            AdvanceTransport(_transporter);
        }

        if (_appearNumbers.Count == 0)
        {
            // There is nobody to bring on. A robot added later takes a pass on the next tick.
            _appearClockUnits = ArcadeClock.UnitsPerRomFrame;
            return;
        }

        _appearClockUnits += ArcadeClock.UnitsPerPortTick;
        if (_appearClockUnits < ArcadeClock.UnitsPerRomFrame)
        {
            return;
        }

        _appearClockUnits -= ArcadeClock.UnitsPerRomFrame;
        MakePass(entities, playfieldBounds);
    }

    /// <summary>Draws the robots being beamed in, when this is a brain wave and the beaming is under way.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRT2.ASM</c>, the transporter, which draws each
    /// image where its robots stand (<c>TRNLP</c>)</item>
    /// <item>Disassembly: the transporter overlay (<c>RTORG</c>, <c>$4140</c>)</item>
    /// </list> "Transport"
    /// is the arcade's own word for beaming the robots in.</item>
    /// </list>
    /// </remarks>
    public void DrawTransport(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (_transportBegun && _transporter is not null && !_transporter.IsFinished())
        {
            _transporter.Draw(spriteBatch, sprites);
        }
    }

    /// <summary>Says whether a robot is still being brought on. It does not act and is not drawn as itself: its appear effect, or the transporter, draws it.</summary>
    /// <param name="entity">The entity to test.</param>
    public bool IsAssembling(IEntity entity) => _appearNumbers.ContainsKey(entity) || _transported.Contains(entity) || _transportQueue.Contains(entity);

    /// <summary>Adds a robot to the end of the sequence. It counts as appearing from now, because the arcade keeps every robot off until the loop draws it whole.</summary>
    /// <param name="robot">The robot to bring in.</param>
    public void Queue(IEntity robot)
    {
        if (_transporter is not null && !_transportBegun)
        {
            _transportQueue.Add(robot);
            return;
        }

        // A robot added after the loop has passed its place takes the next pass.
        _lastAppearNumber = Math.Max(_lastAppearNumber, _passes) + 1;
        _appearNumbers[robot] = _lastAppearNumber;
        _robotsByAppearNumber[_lastAppearNumber] = robot;
    }

    /// <summary>Works out which row of a robot's sprite its strips close in on. It is the same share of the way down the sprite as the robot is of the way down the arcade's screen, so a robot near the top closes in on a row near its top and its strips stay on the screen.</summary>
    /// <param name="robotBounds">The robot's box, in port pixels.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels.</param>
    /// <returns>A function that takes the number of rows in the sprite and gives the row, counted from the top.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APCENT</c>, <c>LDB OBJH,U / LDA OBJY,X / MUL / ADDA
    /// OBJY,X</c>.</item>
    /// <item>Disassembly: <c>$29B5</c> onwards.</item>
    /// </list>
    /// </remarks>
    internal static Func<int, int> GetCentreRowFinder(Rectangle robotBounds, Rectangle playfieldBounds)
    {
        int arcadeRow = ArcadeTopRow + ((robotBounds.Y - playfieldBounds.Y) * (ArcadeBottomRow - ArcadeTopRow) / playfieldBounds.Height);
        return spriteRows => (spriteRows * arcadeRow) >> ScreenSize.SubpixelBits;
    }

    /// <summary>Works out which column of a robot's sprite its strips close in on, in the same way as <see cref="GetCentreRowFinder"/> does for a row. The arcade works it out in its own columns, which are two pixels wide, so the answer is always an even number of pixels.</summary>
    /// <param name="robotBounds">The robot's box, in port pixels.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels.</param>
    /// <returns>A function that takes the number of pixel columns in the sprite and gives the column, counted from the left.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APCENT</c>, <c>LDB OBJW,U / LDA OBJX,X / ASLA / MUL /
    /// ADDA OBJX,X</c>, and <c>RRHX4.ASM</c> <c>APNTOK</c>, <c>ASLB</c> ("DOUBLE CENTER FOR BYTE
    /// ADJUSTMENT").</item>
    /// <item>Disassembly: <c>$29B5</c> onwards.</item>
    /// </list>
    /// </remarks>
    internal static Func<int, int> GetCentreColumnFinder(Rectangle robotBounds, Rectangle playfieldBounds)
    {
        int arcadeColumn = ArcadeLeftColumn + ((robotBounds.X - playfieldBounds.X) * (ArcadeRightColumn - ArcadeLeftColumn) / playfieldBounds.Width);
        int arcadePixel = Math.Min(arcadeColumn * ScreenSize.ArcadePixelsPerColumn, LargestByte);
        return spritePixelColumns =>
            ((spritePixelColumns / ScreenSize.ArcadePixelsPerColumn * arcadePixel) >> ScreenSize.SubpixelBits) * ScreenSize.ArcadePixelsPerColumn;
    }

    /// <summary>Starts the beaming in once the robots are all queued, moves it on, and lets the robots go when it has finished.</summary>
    /// <param name="transporter">The transporter that does the beaming in.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRT2.ASM</c> <c>TRNSTV</c> ("START
    /// TRANSPORTING"), which <c>RRG23.ASM</c> starts with <c>MAKP TRANST</c> on a brain wave</item>
    /// <item>Disassembly: the transporter overlay (<c>RTORG</c>, <c>$4140</c>)</item>
    /// </list>
    /// </remarks>
    private void AdvanceTransport(RobotTransporter transporter)
    {
        if (!_transportBegun && _transportQueue.Count > 0)
        {
            transporter.Begin(_transportQueue);
            _transported.UnionWith(_transportQueue);
            _transportQueue.Clear();
            _transportBegun = true;
        }

        if (!_transportBegun)
        {
            return;
        }

        transporter.Update();
        if (transporter.IsFinished())
        {
            _transported.Clear();
        }
    }

    /// <summary>Makes one pass of the loop.</summary>
    /// <param name="entities">Everything on the field.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels.</param>
    private void MakePass(FieldEntities entities, Rectangle playfieldBounds)
    {
        _passes++;
        if (_robotsByAppearNumber.TryGetValue(_passes, out IEntity? appearingRobot))
        {
            StartAppear(appearingRobot, entities, playfieldBounds);
        }

        // The arcade's loop now draws this robot whole on every pass (ROM: APREF), so it stops being held back.
        if (_robotsByAppearNumber.Remove(_passes - PassesBeforeRobotIsDrawnWhole, out IEntity? wholeRobot))
        {
            _appearNumbers.Remove(wholeRobot);
        }
    }

    /// <summary>Gives a robot its appear effect, if the strip routine that would run it has a record free. When it has none the robot gets no effect, and the loop moves on to the next robot all the same.</summary>
    /// <param name="robot">The robot whose turn it is.</param>
    /// <param name="entities">Everything on the field.</param>
    /// <param name="playfieldBounds">The inside of the wall, in port pixels.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APL1</c> to <c>AP2</c>: <c>STX
    /// PD2,U</c> moves the loop on whether or not <c>APST</c> or <c>HAPST</c> found a record
    /// (<c>RRX7.ASM</c> <c>APBY</c> gives the robot back in <c>X</c> either way)</item>
    /// <item>Disassembly: <c>$2911</c> to <c>$292A</c>, and <c>$5BCA</c> for the return when there is no
    /// record</item>
    /// </list> The arcade does not set a lean before it asks for a row fan here, and
    /// its vertical routine has none, so the lean is nothing.</item>
    /// </list>
    /// </remarks>
    private void StartAppear(IEntity robot, FieldEntities entities, Rectangle playfieldBounds)
    {
        if (robot is not IAnimationFrameSource frameSource)
        {
            return;
        }

        bool fansInByColumns = ((_passes - 1) & ColumnFanSequenceMask) == ColumnFanSequenceMask;
        if (!entities.HasRoomForStripEffect(fansInByColumns ? StripEngine.Horizontal : StripEngine.Vertical))
        {
            return;
        }

        Rectangle robotBounds = robot.GetBounds();

        // The arcade's strip routines take their first step on the ROM frame after this pass, so the effect starts with what is left of this one.
        int startClockUnits = _appearClockUnits - ArcadeClock.UnitsPerPortTick;
        entities.Add(StripEffect.CreateAppear(
            frameSource,
            robotBounds,
            fansInByColumns ? StripFanAxis.Columns : StripFanAxis.Rows,
            slope: 0,
            StripClip.CreateFromPortPixels(playfieldBounds),
            fansInByColumns ? GetCentreColumnFinder(robotBounds, playfieldBounds) : GetCentreRowFinder(robotBounds, playfieldBounds),
            startClockUnits));
    }
}
