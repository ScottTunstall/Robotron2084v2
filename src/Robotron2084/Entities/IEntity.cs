using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>
/// Contract for every playfield entity (spec: "One c# class AT LEAST per
/// entity"): something that lives on the playfield, updates once per tick, can be
/// drawn, and can be taken off the field. <see cref="PlayField"/> (the central hub)
/// is passed into <see cref="Update"/> so entities can read the player, the walls and
/// the input, and spawn new entities, without any global singleton.
/// </summary>
/// <remarks>
/// <para>
/// The arcade has no such interface: it keeps its objects in linked lists of metadata
/// records and jumps straight into each object's own 6809 routine, once per frame. The
/// list the spheroid, enforcer, quark, spark and shell share is the one behind
/// <c>object_metadata_list_2_pointer</c> ($9813) and
/// <c>spheroids_enforcers_quarks_sparks_shells</c> ($9817). <see cref="Update"/> is that
/// per-object call, and <see cref="PlayField"/> stands in for the lists and the RAM they
/// point at.
/// </para>
/// <para>
/// <b>Terminology used throughout the entity classes</b> (start here if a term below
/// is unfamiliar — none of it is standard game-dev vocabulary, it's this project's
/// shorthand for ideas from the original 1982 arcade hardware and its 6809 assembly
/// source):
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>ROM frame / "vblank".</b> The original arcade board redraws the screen 50 times a
/// second, and its logic runs once per redraw, driven by the video hardware's "vertical
/// blank" interrupt. One "ROM frame" = one such redraw = 1/50 second of arcade time.
/// The disassembly and this port's comments use "ROM frame", "vblank" and "frame"
/// interchangeably for this unit.
/// </description></item>
/// <item><description>
/// <b>Port tick.</b> This C# port instead runs a fixed 60-updates-per-second loop (one
/// call to <see cref="Update"/> per tick) — a different, faster clock than the arcade's
/// 50 Hz. See <see cref="Robotron2084.Tuning.GameplayConstants.PortTicks"/> for how ROM-frame
/// delays are converted to port ticks, and for why many entities keep an integer field
/// named <c>..Fifths</c> (e.g. <c>_bodyFifths</c>, <c>_stepFifths</c>) as the fixed-point
/// clock that does that conversion exactly, tick by tick, without drift.
/// </description></item>
/// <item><description>
/// <b>"Body" / "one body".</b> This project's name for one full pass of an entity's own
/// update routine, as opposed to something that happens every single port tick. It
/// comes from the arcade's own scheduler: the ROM's cooperative multitasking primitive
/// <c>NAP n</c> tells an object's routine to go back to sleep and not run again for
/// <c>n</c> ROM frames, so most enemies only act — move, re-aim, advance an animation
/// frame — once every few ROM frames rather than every frame. "A grunt's body runs every
/// 4 ROM frames" means its <c>NAP</c> period is 4, i.e. it only decides anything on
/// 1-in-4 frames; port ticks between bodies just let a paused-looking entity's timers
/// advance.
/// </description></item>
/// <item><description>
/// <b>ROM source files (e.g. <c>RRP8.ASM</c>, <c>RRTK4.ASM</c>, <c>RRG23.ASM</c>) and
/// labels (e.g. <c>ROBSPD</c>, <c>HLKSPD</c>, <c>DRAW_GRUNT</c>).</b> Filenames and
/// symbol names from the arcade's own 1982 Williams assembly source listing (kept under
/// <c>ref/original-source/</c>), which is this port's authority for what an entity is
/// meant to do. A remark like "RRP8.ASM <c>ROBKIL</c>" means "the routine named
/// <c>ROBKIL</c>, in the source file documenting grunts and electrodes ('ROBOTS AND
/// POSTS')". "R5" refers to that ROM build/revision specifically, as opposed to the
/// disassembly (a reconstruction of the compiled ROM, used only to find code — the
/// original source listing is the authority on what it means).
/// </description></item>
/// <item><description>
/// <b>"notes §NN".</b> A cross-reference to a numbered section of
/// <c>docs/arcade-fidelity-notes.md</c>, this repository's running log of everything
/// learned from reading the ROM/disassembly/source, including the reasoning behind
/// values that look arbitrary and the bugs that earlier attempts produced.
/// </description></item>
/// </list>
public interface IEntity
{
    /// <summary>Top-left corner of the entity on screen.</summary>
    IntVector2 Position { get; }

    /// <summary>
    /// Collision and draw rectangle. For most entities this is simply the art's own
    /// size at <see cref="Position"/>; the player's hit box is the exception (see
    /// <see cref="Player"/>).
    /// </summary>
    Rectangle Bounds { get; }

    /// <summary>Where the entity is in its life cycle — see <see cref="EntityLifeState"/>.</summary>
    EntityLifeState LifeState { get; }

    /// <summary>
    /// Advances the entity by one game tick: movement, animation, firing and collision.
    /// </summary>
    /// <param name="gameTime">Elapsed time for this tick.</param>
    /// <param name="field">
    /// The playfield the entity is on — the player, the walls, the input, and the helpers
    /// that spawn new entities.
    /// </param>
    /// <remarks>
    /// The arcade runs each object once per ROM FRAME (50 Hz) while the port ticks at 60 Hz;
    /// the sixths accumulator described in notes §52/§93 is what keeps an entity's speed on
    /// the arcade's own clock.
    /// </remarks>
    void Update(GameTime gameTime, PlayField field);

    /// <summary>Draws the entity at its current position.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, holding this entity's art and the palette.</param>
    /// <remarks>
    /// Takes the shared <see cref="SpriteSet"/> so entities can stay free of statics and
    /// unit-testable (Texture2D requires a GraphicsDevice, so textures can't be baked into
    /// entity constructors in tests — plan 9.4 threads the SpriteSet into
    /// <c>PlayField.Draw</c>, which forwards it here).
    /// </remarks>
    void Draw(SpriteBatch spriteBatch, SpriteSet sprites);
}
