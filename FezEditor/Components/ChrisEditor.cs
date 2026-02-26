using FezEditor.Actors;
using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class ChrisEditor : EditorComponent
{
    public override object Asset => _subject.GetAsset(_obj);

    private readonly ITrixelSubject _subject;
    
    private readonly ExportService _exportService;

    private readonly ConfirmWindow _confirm;
    
    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _meshActor = null!;

    private TrixelObject _obj = null!;

    private bool _showProperties = true;

    public ChrisEditor(Game game, string title, ArtObject ao) : this(game, title, new ArtObjectSubject(ao))
    {
        History.Track(ao);
    }

    public ChrisEditor(Game game, string title, TrileSet set) : this(game, title, new TrileSubject(set))
    {
        History.Track(set);
    }

    private ChrisEditor(Game game, string title, ITrixelSubject subject) : base(game, title)
    {
        _subject = subject;
        _exportService = game.GetService<ExportService>();
        _exportService.TextureReloaded += OnTextureReload;
        History.StateChanged += RevisualizeSubject;
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game);
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";
            
            var camera = _cameraActor.AddComponent<Camera>();
            var zoom = _cameraActor.AddComponent<ZoomControl>();
            _cameraActor.AddComponent<OrbitControl>();
            _cameraActor.AddComponent<OrientationGizmo>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            zoom.MinDistance = 10f / 16f;
            zoom.MaxDistance = 16f;
        }
        {
            _meshActor = _scene.CreateActor();
            _meshActor.AddComponent<TrixelsMesh>();
        }
        
        RevisualizeSubject();
        ExportTexture();
    }

    public override void Update(GameTime gameTime)
    {
        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = _scene.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _scene.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);

                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                {
                    var imageMin = ImGuiX.GetItemRectMin();
                    gizmo.UseFaceLabels = true;
                    gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                }
            }
        }

        DrawPropertiesWindow();
    }

    private void DrawPropertiesWindow()
    {
        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | 
                                           ImGuiWindowFlags.NoCollapse;
            if (ImGui.Begin($"Properties##{Title}", ref _showProperties, flags))
            {
                _subject.DrawProperties(History);
                ImGui.End();
            }
        }
    }

    public override void Dispose()
    {
        Game.RemoveComponent(_confirm);
        _exportService.TextureReloaded -= OnTextureReload;
        _exportService.UntrackTexture(Title);
        _scene.Dispose();
        base.Dispose();
    }

    private void RevisualizeSubject()
    {
        _obj = _subject.Materialize();
        
        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        var oldTexture = mesh.Texture;
        mesh.Texture = _subject.LoadTexture(Game.GraphicsDevice);
        oldTexture?.Dispose();
        mesh.Visualize(_obj);

        var zoom = _cameraActor.GetComponent<ZoomControl>();
        zoom.Distance = _obj.Size.X * 2f;
    }

    private void ExportTexture()
    {
        var texture = _meshActor.GetComponent<TrixelsMesh>().Texture;
        _exportService.ExportTexture(Title, texture!);
    }
    
    private void OnTextureReload(string path, Texture2D newTexture)
    {
        if (path != Title) return;
        
        var mesh = _meshActor.GetComponent<TrixelsMesh>();
        var oldTexture = mesh.Texture;
        mesh.Texture = newTexture;
        mesh.Visualize(_obj);
        
        _subject.UpdateTexture(newTexture);
        {
            _confirm.Title = "Confirm texture overriding";
            _confirm.Text = $"The texture has been changed externally. Save it to the bundle '{Title}'?";
            _confirm.Confirmed += () => ResourceService.Save(Title, _subject.GetAsset(_obj));
        }
        
        if (oldTexture != newTexture)
        {
            oldTexture?.Dispose();
        }
    }
}