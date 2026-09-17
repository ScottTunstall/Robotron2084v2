namespace Robotron2084.Entities;

/// <summary>
/// An entity's life cycle. <see cref="Dying"/> plays the entity's own death
/// animation (the ROM's kill processes — a strip explosion, a shrivel, or the
/// player's PDTHV flash-and-fade); <see cref="Dead"/> entities are pruned from
/// the playfield.
/// </summary>
public enum EntityLifeState
{
    Alive,
    Dying,
    Dead,
}
