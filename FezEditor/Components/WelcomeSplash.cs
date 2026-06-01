using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components;

public class WelcomeSplash : EditorComponent
{
    private static readonly ILogger Logger = Logging.Create<WelcomeSplash>();

    private static readonly Color BackgroundTint = Color.Black with { A = 102 };

    private static readonly Color WatermarkTint = Color.White with { A = 192 };

    private const float TextOffset = 4f;

    private const float SplashWidth = 500f;

    private const float ButtonWidth = 400f;

    private Texture2D _splashTexture = null!;

    private Texture2D _logoTexture = null!;

    private ResourceExtractor? _resourceExtractor;

    private readonly AppStorageService _appStorageService;

    private readonly EditorService _editorService;

    private readonly ResourceService _resourceService;

    private readonly ConfirmWindow _confirm;

    public WelcomeSplash(Game game) : base(game, "Welcome!")
    {
        _appStorageService = game.GetService<AppStorageService>();
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _splashTexture = ContentManager.Load<Texture2D>("Media/Splash");
        _logoTexture = ContentManager.Load<Texture2D>("Media/LogoDark");
    }

    public override void Draw()
    {
        ImGui.OpenPopup("##welcome");

        ImGuiX.SetNextWindowCentered(ImGuiCond.Always);
        ImGui.SetNextWindowSize(new NVector2(SplashWidth, 0), ImGuiCond.Always);

        ImGuiX.PushStyleColor(ImGuiCol.ModalWindowDimBg, BackgroundTint);
        if (ImGui.BeginPopupModal("##welcome", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize))
        {
            var padding = ImGui.GetStyle().WindowPadding;
            var imageWidth = SplashWidth - (padding.X * 2);
            var imageSize = new Vector2(imageWidth, imageWidth / 2f);

            ImGuiX.Image(_splashTexture, imageSize);

            var imageMin = ImGui.GetItemRectMin();
            var imageMax = ImGui.GetItemRectMax();
            var drawList = ImGui.GetWindowDrawList();

            var logoSize = new NVector2(_logoTexture.Width, _logoTexture.Height);
            var logoPos = imageMin + new NVector2(TextOffset);
            drawList.AddImage(ImGuiX.Bind(_logoTexture), logoPos, logoPos + logoSize);

            var versionSize = ImGui.CalcTextSize(FezEditor.Version);
            var versionPos = new NVector2(imageMax.X - versionSize.X - TextOffset, imageMin.Y + TextOffset);
            drawList.AddText(versionPos, Color.Black.PackedValue, FezEditor.Version);

            var splashAuthorText = $"Art by {FezEditor.SplashAuthors}";
            var splashAuthorsSize = ImGui.CalcTextSize(splashAuthorText);
            var splashAuthorsPos = new NVector2(imageMin.X + TextOffset, imageMax.Y - splashAuthorsSize.Y - TextOffset);
            drawList.AddText(splashAuthorsPos, WatermarkTint.PackedValue, splashAuthorText);

            var offsetX = (imageWidth - ButtonWidth) / 2f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

            var buttonWidth = new NVector2(ButtonWidth, 0);
            if (ImGuiX.BeginChild("##recentWrapper", new Vector2(ButtonWidth, 0), ImGuiChildFlags.AutoResizeY))
            {
                if (ImGui.CollapsingHeader("Open Recent"))
                {
                    var recentPaths = _appStorageService.RecentProviders.ToArray();
                    if (recentPaths.Length == 0)
                    {
                        ImGui.Indent();
                        ImGui.TextDisabled("No recent files.");
                        ImGui.Unindent();
                    }
                    else
                    {
                        ImGui.Indent();
                        foreach (var entry in recentPaths)
                        {
                            var name = ResourceService.GetProviderDisplayName(entry.Path);
                            if (string.IsNullOrEmpty(name))
                            {
                                name = entry.Path;
                            }

                            var icon = entry.Kind switch
                            {
                                "File" => Lucide.Package,
                                "Directory" => Lucide.Folder,
                                "Mod" => Lucide.FolderCog,
                                _ => throw new InvalidOperationException()
                            };
                            if (ImGuiX.Button($"{icon} {name}##recent_{entry.Path}", new Vector2(-1, 0)))
                            {
                                OpenRecentEntry(entry);
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(entry.Path);
                            }
                        }

                        if (ImGuiX.Button($"{Lucide.Trash2} Clear recent files", new Vector2(-1, 0)))
                        {
                            _appStorageService.ClearRecentPaths();
                        }
                    }
                }

                ImGui.EndChild();
            }

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            if (ImGui.Button($"{Lucide.Package} Open PAK file", buttonWidth))
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

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            if (ImGui.Button($"{Lucide.Folder} Open assets directory", buttonWidth))
            {
                FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
                {
                    Title = "Choose assets directory..."
                });
            }

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            if (ImGui.Button($"{Lucide.FolderCog} Open mod assets directory", buttonWidth))
            {
                FileDialog.Show(FileDialog.Type.OpenFolder, OpenMod, new FileDialog.Options
                {
                    Title = "Choose mod assets directory..."
                });
            }

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            if (ImGui.Button($"{Lucide.ArrowsUpFromLine} Extract assets and open them...", buttonWidth))
            {
                var selectOptions = new FileDialog.Options
                {
                    Title = "Select PAK files to extract",
                    AllowMultiple = true,
                    Filters = new FileDialog.Filter[]
                    {
                        new("PAK files", "pak")
                    }
                };

                FileDialog.Show(FileDialog.Type.OpenFile, source =>
                    {
                        FileDialog.Show(FileDialog.Type.OpenFolder,
                            target => ExtractPaksAndOpenDirectory(source, target), new FileDialog.Options
                            {
                                Title = "Choose a directory to save assets..."
                            });
                    },
                    selectOptions);
            }

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            if (ImGui.Button($"{Lucide.Save} Open SaveSlot file to edit", buttonWidth))
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
                {
                    Title = "Choose FEZ application directory...",
                    DefaultLocation = Path.Combine(path, "FEZ", "")
                });
            }

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
            if (ImGui.Button($"{Lucide.CopyX} Quit", buttonWidth))
            {
                Game.Exit();
            }

            ImGui.EndPopup();
        }

        ImGui.PopStyleColor();
    }

    public override void Dispose()
    {
        base.Dispose();
        Game.RemoveComponent(_confirm);
    }

    private void ExtractPaksAndOpenDirectory(string[] sources, string[] targets)
    {
        if (_resourceExtractor == null)
        {
            _resourceExtractor = new ResourceExtractor(Game, sources, targets[0]);
            _resourceExtractor.Disposed += (_, _) => _resourceExtractor = null;
            _resourceExtractor.Competed += () => OpenDirectory(targets);
            Game.AddComponent(_resourceExtractor);
        }
    }

    private void OpenPakFile(string[] files)
    {
        var pakPath = files.FirstOrDefault();
        if (!string.IsNullOrEmpty(pakPath))
        {
            _appStorageService.AddRecentProvider(pakPath, "File");
            _resourceService.OpenProvider(new PakResourceProvider(new FileInfo(pakPath)));
            _editorService.CloseEditor(this);
        }
    }

    private void OpenDirectory(string[] files)
    {
        var dirPath = files.FirstOrDefault();
        if (!string.IsNullOrEmpty(dirPath))
        {
            _appStorageService.AddRecentProvider(dirPath, "Directory");
            _resourceService.OpenProvider(new DirResourceProvider(new DirectoryInfo(dirPath)));
            _editorService.CloseEditor(this);
        }
    }

    private void OpenMod(string[] files)
    {
        var modPath = files.FirstOrDefault()!;
        var provider = new ModResourceProvider(new DirectoryInfo(modPath), _appStorageService);
        if (provider.References.Count < 1)
        {
            var options = new FileDialog.Options
            {
                Title = "Add reference PAK files...",
                AllowMultiple = true,
                Filters = new[] { new FileDialog.Filter("PAK files", "pak") }
            };

            FileDialog.Show(FileDialog.Type.OpenFile, pakFiles =>
            {
                provider.UpdateReferences(pakFiles);
                OpenModProvider();
                _resourceService.NotifyModOpenedFirstTime();
            }, options);
        }
        else
        {
            OpenModProvider();
        }

        return;

        void OpenModProvider()
        {
            _appStorageService.AddRecentProvider(modPath, "Mod");
            _resourceService.OpenProvider(provider);
            _editorService.CloseEditor(this);
        }
    }

    private void OpenRecentEntry(Settings.RecentProvider provider)
    {
        var exists = provider.Kind == "File"
            ? File.Exists(provider.Path)
            : Directory.Exists(provider.Path);

        if (!exists)
        {
            Logger.Warning("Recent path no longer exists: {Path}", provider.Path);
            return;
        }

        if (provider.Kind == "File")
        {
            OpenPakFile(new[] { provider.Path });
        }
        else if (provider.Kind == "Directory")
        {
            OpenDirectory(new[] { provider.Path });
        }
        else if (provider.Kind == "Mod")
        {
            OpenMod(new[] { provider.Path });
        }
    }
}
