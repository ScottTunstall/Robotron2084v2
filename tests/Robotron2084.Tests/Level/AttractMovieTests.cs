using Microsoft.Xna.Framework;
using Robotron2084.Level.Attract;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
/// The attract movie (notes §95/§96): the ROM's HISTO page script and its object
/// scripts, replayed by the port's two interpreters. These tests run the ROM's own
/// bytes, so they are the check that the decode of the opcode tables, the walk
/// tables and the font's pen advance all agree with the arcade's data.
/// </summary>
public sealed class AttractMovieTests
{
    private static GameTime Tick() => new(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));

    /// <summary>The story text as lines: cells grouped by row, in pen order.</summary>
    private static List<string> Lines(AttractPageMachine page)
    {
        var lines = new List<string>();
        foreach (IGrouping<int, MovieTextCell> group in page.Text.GroupBy(c => c.Y).OrderBy(g => g.Key))
        {
            lines.Add(new string(group.OrderBy(c => c.X).Select(c => c.Character).ToArray()));
        }

        return lines;
    }

    [Fact]
    public void IntroScreen_PrintsTheRomsPrologueText()
    {
        var movie = new AttractMovie(AttractMovieData.Histo, new Random(7));

        List<string>? prologue = null;
        for (int tick = 0; tick < 8000 && prologue is null; tick++)
        {
            movie.Update(Tick());
            List<string> lines = Lines(movie.Page);
            if (lines.Contains("AND THEREFORE MUST BE DESTROYED."))
            {
                prologue = lines;
            }
        }

        Assert.NotNull(prologue);
        Assert.Equal(
            [
                "INSPIRED BY HIS NEVER ENDING",
                "QUEST FOR PROGRESS,",
                "IN 2084 MAN PERFECTS THE ROBOTRONS:",
                "A ROBOT SPECIES SO ADVANCED THAT",
                "MAN IS INFERIOR TO HIS OWN CREATION.",
                "GUIDED BY THEIR INFALLIBLE LOGIC,",
                "THE ROBOTRONS CONCLUDE:",
                "THE HUMAN RACE IS INEFFICIENT,",
                "AND THEREFORE MUST BE DESTROYED.",
            ],
            prologue);
    }

    [Fact]
    public void IntroScreen_KeepsTheTitleBandClearOfTheStoryText()
    {
        var movie = new AttractMovie(AttractMovieData.Histo, new Random(7));

        for (int tick = 0; tick < 1500; tick++)
        {
            movie.Update(Tick());

            // The ROM's CLEARM starts at row 48 (and the title is printed at row
            // 36), so no story character may land above it.
            Assert.All(movie.Page.Text, cell => Assert.True(cell.Y >= 48, $"printed at row {cell.Y}"));
        }
    }

    [Fact]
    public void Storyline_ReachesDoneAndPlaysEveryScene()
    {
        var movie = new AttractMovie(AttractMovieData.Histo, new Random(3));
        var seen = new HashSet<MovieArt>();
        var messages = new HashSet<string>();
        bool exploded = false;

        for (int tick = 0; tick < 60_000 && !movie.Finished; tick++)
        {
            movie.Update(Tick());
            foreach (MovieObject item in movie.Objects.Objects)
            {
                if (item.Descriptor is { } descriptor)
                {
                    seen.Add(descriptor.Art);
                }
            }

            if (movie.Page.Message is { } message)
            {
                messages.Add(message.Text);
            }

            exploded |= movie.Objects.DrainExplosions().Count > 0;
        }

        Assert.True(movie.Finished, "HISTO never reached DONE2");
        Assert.Contains(MovieArt.Player, seen);     // the hero walks on and shoots
        Assert.Contains(MovieArt.Grunt, seen);      // the 14 grunts
        Assert.Contains(MovieArt.Hulk, seen);       // the hulk bounces in
        Assert.Contains(MovieArt.Spheroid, seen);   // the spheroid scene
        Assert.Contains(MovieArt.Enforcer, seen);
        Assert.Contains(MovieArt.Tank, seen);
        Assert.Contains(MovieArt.Brain, seen);      // the reprogramming
        Assert.Contains(MovieArt.Posts, seen);      // the score posts
        Assert.Contains(MovieArt.Mummy, seen);
        Assert.Contains(MovieArt.Daddy, seen);
        Assert.Contains(MovieArt.Mikey, seen);
        Assert.True(exploded, "no EXP ever fired");
        Assert.Contains("MOMMY", messages);
        Assert.Contains("DADDY", messages);
        Assert.Contains("MIKEY", messages);
    }

    [Fact]
    public void ObjectMachine_WalksTheHeroLeftWithTheRomsOwnStepSize()
    {
        var machine = new AttractObjectMachine(new Random(11));

        // DUMYOU ($8739) — the title's "dumb player": YOU at (104,106), MLEFT $80,
        // DIE. YOU's descriptor walks the BR* way: step 1 (half a column) every 2
        // frames, so 128 steps cover 128 pixels.
        machine.StartScript(0x8739);
        machine.StepFrame();

        MovieObject hero = machine.Objects[0];
        Assert.Equal(104, hero.Column);

        // The BR* walker cycles ANATAB (0,1,0,2) on top of the direction's base
        // image, so a LEFT walk shows images 0,1,0,2 — every one of them inside
        // the descriptor's 12 pictures (the source of the "player animations are
        // not quite right" report: a 12-byte ANATAB made it cycle garbage).
        var images = new List<int>();
        for (int frame = 0; frame < 300; frame++)
        {
            machine.StepFrame();
            images.Add(hero.ImageIndex);
        }

        Assert.Equal(104 - 64, hero.Column);
        Assert.All(images, index => Assert.InRange(index, 0, 11));

        // The ROM's BANA1 sleeps the descriptor's nap BEFORE the first step and
        // sets the picture on the step, so the cycle is one entry every 2 frames
        // starting at frame 3.
        Assert.Equal([0, 1, 0, 2], new[] { images[2], images[4], images[6], images[8] });
    }

    [Fact]
    public void ObjectMachine_WalkCostsExactlyOneStepPeriodPerStep()
    {
        var machine = new AttractObjectMachine(new Random(4));

        // DUMYOU again: 128 left steps at YOU's 2-frame nap, then DIE — so the
        // object is gone on frame 256 exactly. ANA2/BANA2 only SLEEP again while
        // steps remain (`DEC ... / BNE` then `JMP [LEV2,U]`), so waiting once more
        // after the LAST step would push every following opcode a period late and
        // drag the script's phase with it (notes §96.10: the hulk reached a human
        // half a second before her scripted death).
        machine.StartScript(0x8739);
        machine.StepFrame();
        Assert.NotEmpty(machine.Objects);

        for (int frame = 0; frame < 255; frame++)
        {
            machine.StepFrame();
        }

        Assert.NotEmpty(machine.Objects);
        machine.StepFrame();
        Assert.Empty(machine.Objects);
    }

    [Fact]
    public void ObjectMachine_RprogIsAWholeScript_AndDoesNotRunOnIntoTheNext()
    {
        var machine = new AttractObjectMachine(new Random(3));

        // PSHAKE ($868C) is a ONE-BYTE script — `FCB RPROG` — GHOSTed onto the human
        // the brain is reprogramming. RPROG OWNS the process until the shake ends:
        // R5 $7CD9 stores the base row and a 64 countdown, allocates its own
        // two-frame tasks (+RND(0..7) rows, then −RND(0..7)) and frees the process at
        // $7D16 — it never returns to the script reader. Returning "keep reading"
        // made the process run on into the bytes that FOLLOW PSHAKE, which are
        // BRAING ($868D): the shaking human's object executed `SETOB BRAIN /
        // SETPOS (10,160)` on itself, and the movie showed a SECOND brain streaking
        // off across the screen instead of a reprogrammed mummy (notes §97.4).
        machine.StartScript(0x868C);

        MovieObject shaking = Assert.Single(machine.Objects);
        Assert.Null(shaking.Descriptor); // nothing has run on into BRAING's SETOB

        bool shook = false;
        for (int frame = 0; frame < 300; frame++)
        {
            machine.StepFrame();

            // The property: whatever else happens, the one-byte script never runs
            // on into the bytes that follow it (which are BRAING's).
            Assert.Null(shaking.Descriptor);
            shook |= shaking.ShakeRowOffset != 0;
        }

        Assert.True(shook, "PSHAKE never moved the object's row");
        Assert.Equal(0, shaking.ShakeRowOffset); // the shake ends by restoring the base row
        Assert.False(shaking.Dead); // the ROM frees the ghost's metadata entry, not the object
    }

    [Fact]
    public void ObjectMachine_WalksAGruntAndExplodesIt()
    {
        var machine = new AttractObjectMachine(new Random(11));

        // GS1 ($84F5): the grunt walks left down its HUMANA steps, then GSSS1 ends
        // in EXP.
        machine.StartScript(0x84F5);

        MovieObject? grunt = null;
        for (int i = 0; i < 4; i++)
        {
            machine.StepFrame();
            grunt ??= machine.Objects.FirstOrDefault(o => o.Descriptor?.Art == MovieArt.Grunt);
        }

        Assert.NotNull(grunt);
        int startX = grunt.X;

        List<MovieExplosion> explosions = [];
        for (int frame = 0; frame < 3000 && explosions.Count == 0; frame++)
        {
            machine.StepFrame();
            explosions.AddRange(machine.DrainExplosions());
        }

        Assert.True(grunt.X < startX, "the grunt did not walk left");
        MovieExplosion explosion = Assert.Single(explosions);
        Assert.Equal(MovieArt.Grunt, explosion.Art);
        Assert.Equal(0xA6, explosion.Row); // EXPP's ACTHIT+6
    }

    [Fact]
    public void PageMachine_PrintsACharacterEveryThreeFramesAndSleepsForTheRest()
    {
        // "A", then a $5F sleep (95 ROM frames), then "B" — the ROM's SPWAKE.
        byte[] script = [0x41, 0x5F, 0x42];
        var page = new AttractPageMachine(script, new AttractObjectMachine(new Random(1)), new Random(1));

        for (int frame = 0; frame < 3; frame++)
        {
            page.StepFrame();
        }

        Assert.Equal(['A'], page.Text.Select(c => c.Character).ToArray());

        for (int frame = 0; frame < 200; frame++)
        {
            page.StepFrame();
        }

        Assert.Equal(['A', 'B'], page.Text.Select(c => c.Character).ToArray());
    }
}
