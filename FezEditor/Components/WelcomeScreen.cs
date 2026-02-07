using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

[UsedImplicitly]
public class WelcomeScreen : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private ResourceExtractor? _resourceExtractor;

    private readonly IStateService _stateService;

    public WelcomeScreen(Game game, IStateService stateService) : base(game)
    {
        _stateService = stateService;
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Content/Icon");
    }

    public void Draw()
    {
        // Estimate content size for centering
        const float contentWidth = 250f;
        const float contentHeight = 230f;

        var regionSize = ImGui.GetContentRegionAvail();
        var offsetX = Math.Max(0, (regionSize.X - contentWidth) / 2);
        var offsetY = Math.Max(0, (regionSize.Y - contentHeight) / 2);

        ImGui.SetCursorPos(ImGui.GetCursorPos() + new System.Numerics.Vector2(offsetX, offsetY));
        ImGui.BeginGroup();

        ImGuiX.Image(_logoTexture);
        ImGui.NewLine();
        ImGui.Text("Welcome to FEZEDITOR!");
        ImGui.NewLine();

        if (ImGui.Button("Open PAK..."))
        {
            FileDialog.Show(FileDialog.Type.OpenFile, OpenPakFile, new FileDialog.Options
            {
                Title = "Choose PAK file...",
                Filters = new FileDialog.Filter[]
                {
                    new("PAK files", "pak")
                }
            });
        }

        if (ImGui.Button("Open assets directory..."))
        {
            FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
            {
                Title = "Choose assets directory..."
            });
        }

        if (ImGui.Button("Extract assets and open them..."))
        {
            var selectOptions = new FileDialog.Options
            {
                Title = "Select PAK files to extract...",
                AllowMultiple = true,
                Filters = new FileDialog.Filter[]
                {
                    new("PAK files", "pak")
                }
            };

            FileDialog.Show(FileDialog.Type.OpenFile, source =>
                {
                    if (source.Files.Length > 0)
                    {
                        FileDialog.Show(FileDialog.Type.OpenFolder,
                            target => ExtractPaksAndOpenDirectory(source, target), new FileDialog.Options
                            {
                                Title = "Choose a directory to save assets..."
                            });
                    }
                },
                selectOptions);
        }

        if (ImGui.Button("Quit"))
        {
            _stateService.Quit();
        }

        ImGui.EndGroup();
    }

    private void ExtractPaksAndOpenDirectory(FileDialog.Result source, FileDialog.Result target)
    {
        if (source.Files.Length > 0 && target.Files.Length > 0 && _resourceExtractor == null)
        {
            try
            {
                _resourceExtractor = Game.CreateComponent<ResourceExtractor>();
                _resourceExtractor.Disposed += (_, _) => { _resourceExtractor = null; };
                _resourceExtractor.Initialize(source.Files, target.Files[0]);
                _resourceExtractor.Competed += () => OpenDirectory(target);
            }
            catch (Exception ex)
            {
                Game.CreateComponent<ModalWindow>().Show(ex.Message, "Error occured!");
            }
        }
    }

    private void OpenPakFile(FileDialog.Result result)
    {
        if (result.Files.Length > 0)
        {
            try
            {
                var pakService = Game.CreateService<IResourceService, PakResourceService>();
                pakService.Initialize(new FileInfo(result.Files[0]));
                Game.RemoveComponent(this);
            }
            catch (Exception ex)
            {
                Game.CreateComponent<ModalWindow>().Show(ex.Message, "Error occured!");
            }
        }
    }

    private void OpenDirectory(FileDialog.Result result)
    {
        if (result.Files.Length > 0)
        {
            try
            {
                var dirService = Game.CreateService<IResourceService, DirResourceService>();
                dirService.Initialize(new DirectoryInfo(result.Files[0]));
                Game.RemoveComponent(this);
            }
            catch (Exception ex)
            {
                Game.CreateComponent<ModalWindow>().Show(ex.Message, "Error occured!");
            }
        }
    }
}
