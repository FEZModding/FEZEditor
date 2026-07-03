using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Components.Eddy;

public abstract record ToolState
{
    public sealed record Select : ToolState
    {
        public InstanceId.Trile? DragOrigin { get; set; }
        public bool IsRectSelecting { get; set; }
    }

    public sealed record Translate : ToolState
    {
        public IDisposable? HistoryScope { get; set; }

        public override void Clear()
        {
            HistoryScope?.Dispose();
            HistoryScope = null;
        }
    }

    public sealed record Rotate : ToolState;

    public sealed record Scale : ToolState
    {
        public IDisposable? HistoryScope { get; set; }
        public int PreviousSteps { get; set; }
        public Vector3I Direction { get; set; }
        public List<(TrileEmplacement Emplacement, int TrileId, byte PhiLight)> Snapshot { get; } = new();

        public override void Clear()
        {
            HistoryScope?.Dispose();
            HistoryScope = null;
            PreviousSteps = 0;
            Direction = Vector3I.Zero;
            Snapshot.Clear();
        }
    }

    public abstract record Paint : ToolState
    {
        public sealed record Trile(string AssetName, int Id) : Paint
        {
            public PaintRotationMode RotationMode { get; set; } = new PaintRotationMode.Fixed(0);
            public HashSet<InstanceId> Stroke { get; } = new();
            public IDisposable? HistoryScope { get; set; }
            public (TrileEmplacement Anchor, TrileEmplacement Position, FaceOrientation Face)? ParkingSpot { get; set; }

            public override void Clear()
            {
                HistoryScope?.Dispose();
                HistoryScope = null;
                ParkingSpot = null;
                Stroke.Clear();
            }
        }

        public sealed record ArtObject(string AssetName) : Paint;

        public sealed record BackgroundPlane(string AssetName) : Paint;

        public sealed record NonPlayableCharacter(string AssetName) : Paint;

        public sealed record Volume : Paint;

        public sealed record Path : Paint;

        public sealed record None : Paint;
    }

    public sealed record Pick : ToolState;

    public virtual void Clear()
    {
    }
}