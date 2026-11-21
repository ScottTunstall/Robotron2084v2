# Graphics/Models Review

Scope: `src/Robotron2084/Graphics/Models/*.cs`.

## Findings

No high-confidence correctness or resource defect was found in the three model types.

- `SpriteShape` is an immutable pairing of a `SpriteMask` and a screen-space `Rectangle`. It centralizes the collision contract, but it cannot correct invalid mask or rectangle state; validation belongs in `SpriteMask.Overlaps`.
- `TunnelRing` is immutable and stores the ring in ROM-screen coordinates plus a packed two-palette-index pair. Keeping these values as a value type prevents later state mutation from changing already recorded rings.
- `LetterSpan` is immutable and only models `Start`/`Width`. It has no clipping or resource behavior of its own; coordinate validity is determined by the mask and drawing code that consumes it.

## Coordinate and lifecycle observations

- `SpriteShape.DrawnBounds` is explicitly screen-pixel state. Any position or scale bug in `BlitterDraw.GetDrawnBounds` or `SpriteMask.Overlaps` is outside this model but will be reflected directly in collision decisions.
- `TunnelRing.PackedColourPair` is packed data rather than two independently ranged values. Validity is established in `TunnelEffect.GetColourPaletteIndices`; a future constructor or caller should not assume that nibble extraction is enforced by the record.
- `LetterSpan` coordinates are texture/mask columns, not port pixels. `LargeTitleLetterAppear` is responsible for converting spans to source/destination rectangles.
- All three types are short-lived value records or structs and hold no graphics resources. They do not participate in GPU frame lifecycle and require no disposal.
