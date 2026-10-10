using Xunit;

namespace Robotron2084.Tests;

/// <summary>A random source that hands back the numbers a test gives it, in order, so a test can choose every roll.</summary>
internal sealed class ScriptedRandom : Random
{
    private readonly Queue<int> _values;

    /// <summary>Makes a source that returns these numbers, one for each call to <see cref="Next(int)" />.</summary>
    /// <param name="values">The numbers to hand back, in order.</param>
    public ScriptedRandom(params int[] values)
    {
        _values = new Queue<int>(values);
    }

    /// <inheritdoc />
    public override int Next(int maxValue)
    {
        var value = _values.Dequeue();
        Assert.InRange(value, 0, maxValue - 1);
        return value;
    }
}
