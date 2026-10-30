using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>A robot that needs to be told when the game goes live.</summary>
public interface IWaveStartRobot
{
    /// <summary>Tells the robot that the game has just gone live.</summary>
    /// <param name="field">The playfield the robot is on.</param>
    void BeginPlay(PlayField field);
}
