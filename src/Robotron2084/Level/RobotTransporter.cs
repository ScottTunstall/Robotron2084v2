using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;

namespace Robotron2084.Level;

/// <summary>Beams the robots in at the start of a brain wave. Each robot's animation frame is built up out of sparkling pixels, and robots that show the same animation frame share one build-up.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRT2.ASM</c> <c>TRNSTV</c> (which shares an image between robots with the same animation frame, up to sixteen each), <c>TRNLP</c> and <c>ROBUP</c>; <c>RRG23.ASM</c> starts it with <c>MAKP TRANST</c> when <c>BRNCNT</c> is not zero</item>
/// <item>Disassembly: the transporter overlay (<c>RTORG</c>, <c>$4140</c>)</item>
/// </list>
/// Every ROM frame it takes one step of each image, and it draws each image where its robots stand. The sound is the transporter's own
/// (<see cref="Audio.Sound.PlayTransporter"/>). Other waves use the strip appear instead (<see cref="WaveMaterialisation"/>).
/// "Transporter" is the arcade's own name for beaming the robots in at the start of a brain wave (<c>RRT2.ASM</c> is titled <c>TRANSPORTER</c>).
/// </remarks>
public sealed class RobotTransporter
{
    /// <summary>The most robots that share one image: the first, and then fifteen more.</summary>
    /// <remarks>Original source: <c>RRT2.ASM</c> <c>TRNSTV</c>, <c>CMPY #15</c>. Disassembly: the transporter overlay (<c>RTORG</c>, <c>$4140</c>).</remarks>
    private const int RobotsPerImage = 16;

    /// <summary>Each robot being beamed in, with the image it shows.</summary>
    private readonly List<(IEntity Robot, TransportImage Image)> _robots = [];

    /// <summary>The field's random source, which picks each image's sparkle.</summary>
    private readonly Random _random;

    /// <summary>The images being built up. Robots with the same animation frame share one.</summary>
    private readonly List<TransportImage> _images = [];

    /// <summary>Builds up, a tick at a time, until it is time for the next ROM frame. It starts with a whole ROM frame in it, so that the first step is taken on the first tick, as the arcade takes its first step on the frame the wave is set up.</summary>
    /// <remarks>Original source: <c>RRT2.ASM</c> <c>TRNLP</c>, which <c>TRNSTV</c> runs on into before its first <c>NAP 1</c>. Disassembly: the transporter overlay (<c>RTORG</c>, <c>$4140</c>).</remarks>
    private int _clockUnits = ArcadeClock.UnitsPerRomFrame;

    /// <summary>Makes a transporter.</summary>
    /// <param name="random">The field's random source, which picks each image's sparkle.</param>
    public RobotTransporter(Random random) => _random = random;

    /// <summary>Says whether every image has run out of steps.</summary>
    public bool IsFinished => _images.All(image => image.IsFinished);

    /// <summary>Works out which image each robot shares, from the animation frame each is showing.</summary>
    /// <param name="animationFrames">The animation frame each robot is showing, in order. Two robots are showing the same one when these are the same object.</param>
    /// <returns>The number of the image each robot shares. A new image starts when the animation frame changes, and after sixteen robots.</returns>
    public static int[] AssignImages(IReadOnlyList<object> animationFrames)
    {
        int[] imageNumbers = new int[animationFrames.Count];
        int imageNumber = -1;
        int robotsSharingImage = 0;
        object? previous = null;
        for (int index = 0; index < animationFrames.Count; index++)
        {
            bool sharesImage = imageNumber >= 0 && ReferenceEquals(animationFrames[index], previous) && robotsSharingImage < RobotsPerImage;
            if (!sharesImage)
            {
                imageNumber++;
                robotsSharingImage = 0;
            }

            robotsSharingImage++;
            previous = animationFrames[index];
            imageNumbers[index] = imageNumber;
        }

        return imageNumbers;
    }

    /// <summary>Starts beaming a set of robots in.</summary>
    /// <param name="robots">The robots, in the order they were made.</param>
    public void Begin(IReadOnlyList<IEntity> robots)
    {
        // A field built without sprites (a test) has robots with no animation frame to beam in; they are simply not shown.
        (IEntity Robot, Texture2D Frame)[] shown =
        [
            .. robots.OfType<IAnimationFrameSource>()
                .Select(source => (Robot: (IEntity)source, Frame: source.GetCurrentAnimationFrame()))
                .Where(pair => pair.Frame is not null),
        ];
        Texture2D[] frames = [.. shown.Select(pair => pair.Frame)];
        int[] imageNumbers = AssignImages(frames);

        for (int index = 0; index < shown.Length; index++)
        {
            if (imageNumbers[index] == _images.Count)
            {
                _images.Add(CreateImage(frames[index]));
            }

            _robots.Add((shown[index].Robot, _images[imageNumbers[index]]));
        }
    }

    /// <summary>Moves on by one port tick, which does a step of each image on the ROM frames.</summary>
    public void Update()
    {
        _clockUnits += ArcadeClock.UnitsPerPortTick;
        while (_clockUnits >= ArcadeClock.UnitsPerRomFrame)
        {
            _clockUnits -= ArcadeClock.UnitsPerRomFrame;
            foreach (TransportImage image in _images)
            {
                image.Step(_random);
            }
        }
    }

    /// <summary>Draws each image where its robots stand.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set, for the palette and the one-pixel square the pixels are drawn with.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        sprites.Blitter.UsePassThrough();
        foreach ((IEntity robot, TransportImage image) in _robots)
        {
            Rectangle bounds = robot.Bounds;
            int left = bounds.X + ((bounds.Width - ScreenSize.ToPortPixels(image.Width)) / 2);
            int top = bounds.Y + ((bounds.Height - ScreenSize.ToPortPixels(image.Height)) / 2);
            DrawImage(spriteBatch, sprites, image, new Point(left, top));
        }
    }

    /// <summary>Draws one image, a pixel at a time.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The sprite set, for the palette and the one-pixel square.</param>
    /// <param name="image">The image to draw.</param>
    /// <param name="topLeft">Where the image's top-left corner goes, in port pixels.</param>
    private static void DrawImage(SpriteBatch spriteBatch, SpriteSet sprites, TransportImage image, Point topLeft)
    {
        int pixelSize = ScreenSize.ToPortPixels(1);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int pixel = image.GetPixel(x, y);
                if (pixel == TransportImage.PixelOff)
                {
                    continue;
                }

                Color colour = pixel == TransportImage.PixelOwnColour
                    ? image.Template[(y * image.Width) + x]
                    : sprites.Blitter.GetSlotColour(pixel);
                spriteBatch.Draw(sprites.WallPixelSprite, new Rectangle(topLeft.X + (x * pixelSize), topLeft.Y + (y * pixelSize), pixelSize, pixelSize), colour);
            }
        }
    }

    /// <summary>Makes an empty image for an animation frame, with a sparkle picked at random.</summary>
    /// <param name="frame">The animation frame the image builds up.</param>
    private TransportImage CreateImage(Texture2D frame)
    {
        var pixels = new Color[frame.Width * frame.Height];
        frame.GetData(pixels);
        return new TransportImage(pixels, frame.Width, frame.Height, _random.Next(8));
    }
}
