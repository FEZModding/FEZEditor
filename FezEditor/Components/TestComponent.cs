using FezEditor.Actors;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class TestComponent : EditorComponent
{
    private readonly Scene _test;
    
    public TestComponent(Game game, string title) : base(game, title)
    {
        _test = new Scene(game);
        {
            var actor = _test.CreateActor();
            var camera = actor.AddComponent<Camera>();
            var transform = actor.GetComponent<Transform>();
            camera.Projection = Camera.ProjectionType.Orthographic;
            camera.Size = 4f;
            transform.Position = new Vector3(0f, 0f, 4f);
            transform.Rotation = Quaternion.Identity;
        }
        {
            var actor = _test.CreateActor();
            var mesh = actor.AddComponent<TestMesh>();
            mesh.Load();
        }
    }

    public override void Update(GameTime gameTime)
    {
        _test.Update(gameTime);
    }

    public override void Draw()
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;
            
        if (w > 0 && h > 0)
        {
            var texture = _test.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _test.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
            }
        }
    }

    public override void Dispose()
    {
        _test.Dispose();
        base.Dispose();
    }
}