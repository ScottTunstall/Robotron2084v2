using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Graphics;

namespace Robotron2084.Level;

/// <summary>Beams the robots in at the start of a brain wave: each picture is built up out of sparkling pixels, in a buffer shared by the robots that show it.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRT2.ASM</c> <c>TRNSTV</c> (which shares an image between robots with the same picture, up to sixteen each), <c>TRNLP</c> and <c>ROBUP</c>; <c>RRG23.ASM</c> starts it with <c>MAKP TRANST</c> when <c>BRNCNT</c> is not zero</item>
/// <item>Disassembly: the transporter overlay (<c>RTORG</c>, <c>$4140</c>)</item>
/// </list>
/// Every ROM frame it takes one step of each image and draws each image where its robots stand. The sound is the transporter's own
/// (<see cref="Audio.Sound.PlayTransporter"/>). Other waves use the strip appear instead (<see cref="WaveMaterialisation"/>).
/// </remarks>
public sealed class RobotTransporter
{
    /// <summary>How many robots share one image: the first, and then fifteen more (<c>CMPY #15</c>).</summary>
    private const int RobotsPerImage = 16;

    private readonly List<(IEntity Robot, TransportImage Image)> _robots = [];
    private readonly Random _random;
    private readonly List<TransportImage> _images = [];
    private int _clockUnits;

    /// <summary>Makes a transporter.</summary>
    /// <param name="random">The field's random source, which picks each image's sparkle.</param>
    public RobotTransporter(Random random) => _random = random;

    /// <summary>Says whether every image has run out of steps.</summary>
    public bool IsFinished => _images.All(image => image.IsFinished);

    /// <summary>Works out which image each robot shares, from the picture each is showing.</summary>
    /// <param name="pictures">What each robot is showing, in order. Two robots are showing the same picture when these are the same object.</param>
    /// <returns>The number of the image each robot shares. A new image starts when the picture changes, and after sixteen robots.</returns>
    public static int[] AssignImages(IReadOnlyList<object> pictures)
    {
        int[] images = new int[pictures.Count];
        int image = -1;
        int sharing = 0;
        object? previous = null;
        for (int index = 0; index < pictures.Count; index++)
        {
            bool shares = image >= 0 && ReferenceEquals(pictures[index], previous) && sharing < RobotsPerImage;
            if (!shares)
            {
                image++;
                sharing = 0;
            }

            sharing++;
            previous = pictures[index];
            images[index] = image;
        }

        return images;
    }

    /// <summary>Starts beaming a set of robots in.</summary>
    /// <param name="robots">The robots, in the order they were made.</param>
    public void Begin(IReadOnlyList<IEntity> robots)
    {
        // A field built without sprites (a test) has robots with no picture to beam in; they are simply not shown.
        (IEntity Robot, Texture2D Frame)[] shown =
        [
            .. robots.OfType<IAnimationFrameSource>()
                .Select(source => (Robot: (IEntity)source, Frame: source.GetCurrentAnimationFrame()))
                .Where(pair => pair.Frame is not null),
        ];
        Texture2D[] frames = [.. shown.Select(pair => pair.Frame)];
        int[] numbers = AssignImages(frames);

        for (int index = 0; index < shown.Length; index++)
        {
            if (numbers[index] == _images.Count)
            {
                _images.Add(CreateImage(frames[index]));
            }

            _robots.Add((shown[index].Robot, _images[numbers[index]]));
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
                spriteBatch.Draw(sprites.WallPixel, new Rectangle(topLeft.X + (x * pixelSize), topLeft.Y + (y * pixelSize), pixelSize, pixelSize), colour);
            }
        }
    }

    private TransportImage CreateImage(Texture2D frame)
    {
        var pixels = new Color[frame.Width * frame.Height];
        frame.GetData(pixels);
        return new TransportImage(pixels, frame.Width, frame.Height, _random.Next(8));
    }
}
