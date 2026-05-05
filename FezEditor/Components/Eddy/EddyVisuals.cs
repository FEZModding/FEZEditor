namespace FezEditor.Components.Eddy;

[Flags]
public enum EddyVisuals
{
    #region Instances

    Triles = 1 << 0,
    EmptyTriles = 1 << 1,
    DisplacedTriles = 1 << 2,
    OverlappedTriles = 1 << 3,
    ArtObjects = 1 << 4,
    BackgroundPlanes = 1 << 5,
    NonPlayableCharacters = 1 << 6,
    Gomez = 1 << 7,
    Liquid = 1 << 8,
    Sky = 1 << 9,
    Rain = 1 << 10,

    #endregion

    #region Overlays

    Volumes = 1 << 11,
    Paths = 1 << 12,
    LevelBounds = 1 << 13,
    CollisionMap = 1 << 14,
    PickableBounds = 1 << 15,

    #endregion

    #region Presets

    Default = Triles | EmptyTriles | DisplacedTriles | OverlappedTriles | ArtObjects |
              BackgroundPlanes | NonPlayableCharacters | Gomez | Liquid | Sky | Rain |
              Volumes | Paths | LevelBounds,
    Preview = Triles | ArtObjects | BackgroundPlanes | Liquid | Sky

    #endregion
}