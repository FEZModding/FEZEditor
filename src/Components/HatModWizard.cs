using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Serilog;
using System.Security;
using System.Text;

namespace FezEditor.Components;

public sealed class HatModWizard : DrawableGameComponent
{
    private const string PopupTitle = "HAT 3 Mod Creation Wizard##hatModWizard";

    private const string CreatedPopupTitle = "HAT 3 Mod Created##hatModCreated";

    private const float FieldWidth = 520f;

    private static readonly ILogger Logger = Log.ForContext<HatModWizard>();

    private static readonly string[] ModTypeLabels =
    [
        "Asset Mod",
        "Code Mod",
        "Asset + Code Mod"
    ];

    private static readonly string[] RequiredGameFiles =
    [
        "HAT.dll",
        "FezEngine.dll",
        "FNA.dll"
    ];

    private readonly IContentManager _contentManager;

    private readonly AppStorageService _storage;

    private readonly ResourceService _resources;

    private string _name = "MyHatMod";

    private int _modType;

    private string _description = "";

    private string _author = "";

    private string _version = "1.0.0";

    private string _projectDirectory = "";

    private string _fezDirectory;

    private string? _error;

    private bool _pendingOpen = true;

    private bool _closeRequested;

    private bool _pendingCreatedOpen;

    private ProjectResult? _createdProject;

    public event Action? Completed;

    public HatModWizard(Game game) : base(game)
    {
        _contentManager = Game.GetService<ContentService>().Get(this);
        _storage = Game.GetService<AppStorageService>();
        _resources = Game.GetService<ResourceService>();
        _fezDirectory = string.IsNullOrWhiteSpace(_storage.HatLauncherPath)
            ? ""
            : Path.GetDirectoryName(_storage.HatLauncherPath) ?? "";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Game.GetService<ContentService>().Unload(this);
        }

        base.Dispose(disposing);
    }

    public override void Update(GameTime gameTime)
    {
        if (_closeRequested)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_createdProject != null)
        {
            DrawCreatedPopup();
            return;
        }

        if (_pendingOpen)
        {
            ImGui.OpenPopup(PopupTitle);
            _pendingOpen = false;
        }

        var isOpen = true;
        ImGuiX.SetNextWindowCentered(ImGuiCond.Always);
        if (ImGui.BeginPopupModal(PopupTitle, ref isOpen,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            DrawForm();
            ImGui.EndPopup();
        }

        if (!isOpen)
        {
            _closeRequested = true;
        }
    }

    private void DrawForm()
    {
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + FieldWidth);
        ImGui.TextWrapped("This wizard will create a HAT 3 mod project for the selected FEZ installation.");
        ImGui.PopTextWrapPos();
        ImGui.Separator();

        ImGui.Text("Mod type");
        ImGui.SetNextItemWidth(FieldWidth);
        if (ImGui.Combo("##modType", ref _modType, ModTypeLabels, ModTypeLabels.Length))
        {
            _error = null;
        }

        DrawTextField("Mod name", "##modName", ref _name, 128);
        DrawTextField("Author", "##author", ref _author, 128);
        DrawTextField("Version", "##version", ref _version, 32);
        ImGui.Text("Description");
        ImGui.InputTextMultiline("##description", ref _description, 1024, new NVector2(FieldWidth, 80));

        ImGui.Spacing();
        DrawDirectoryPicker(
            "Project folder",
            _projectDirectory,
            path => _projectDirectory = path,
            "Choose an empty folder for the new mod project...");
        DrawDirectoryPicker(
            "FEZ game folder",
            _fezDirectory,
            path =>
            {
                _fezDirectory = path;
                _storage.HatLauncherPath = Path.Combine(path, OperatingSystem.IsWindows() ? "FEZ.exe" : "FEZ");
            },
            "Choose FEZ folder with HAT installed...");

        var requestType = _modType switch
        {
            0 => ModType.Asset,
            1 => ModType.Code,
            _ => ModType.Asset | ModType.Code
        };

        var request = new ProjectRequest(
            _name,
            _description,
            _author,
            _version,
            _projectDirectory,
            _fezDirectory,
            requestType
        );

        var validationError = Validate(request);

        if (!string.IsNullOrWhiteSpace(_projectDirectory))
        {
            ImGui.TextDisabled("The project files will be created directly in this folder.");
        }

        var modName = ToIdentifier(_name);
        if (requestType.HasFlag(ModType.Code) && !string.IsNullOrWhiteSpace(_fezDirectory) && !string.IsNullOrEmpty(modName))
        {
            ImGui.TextDisabled($"Mod output: {Path.Combine(_fezDirectory, "Mods", modName)}");
        }

        if (!string.IsNullOrWhiteSpace(_error))
        {
            ImGui.Spacing();
            ImGui.TextColored(new NVector4(1f, 0.35f, 0.35f, 1f), _error);
        }
        else if (validationError != null)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(validationError);
        }

        ImGui.Spacing();
        ImGui.BeginDisabled(validationError != null);
        if (ImGui.Button($"{Lucide.FilePlusCorner} Create mod"))
        {
            CreateProject(request);
        }

        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            ImGui.CloseCurrentPopup();
            _closeRequested = true;
        }
    }

    private void DrawDirectoryPicker(string label, string value, Action<string> setValue, string title)
    {
        ImGui.Text(label);
        ImGui.SetNextItemWidth(FieldWidth);
        ImGui.InputText($"##{label}", ref value, 1024);
        ImGui.SameLine();
        if (ImGui.Button($"{Lucide.FolderOpen}##{label}Browse"))
        {
            FileDialog.Show(FileDialog.Type.OpenFolder, paths =>
            {
                var path = paths.FirstOrDefault();
                if (!string.IsNullOrEmpty(path))
                {
                    setValue(path);
                    _error = null;
                }
            }, new FileDialog.Options { Title = title, DefaultLocation = value });
        }
    }

    private void CreateProject(ProjectRequest request)
    {
        try
        {
            _createdProject = Create(request);
            _pendingCreatedOpen = true;
            ImGui.CloseCurrentPopup();
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Unable to create HAT mod project");
            _error = exception.Message;
        }
    }

    private ProjectResult Create(ProjectRequest request)
    {
        var validationError = Validate(request);
        if (validationError != null)
        {
            throw new ArgumentException(validationError, nameof(request));
        }

        var modName = ToIdentifier(request.Name);
        var projectDirectory = request.ProjectDirectory;
        var temporaryDirectory = Path.Combine(projectDirectory, ".fez-editor-staging-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            if (request.Type.HasFlag(ModType.Asset))
            {
                Directory.CreateDirectory(Path.Combine(temporaryDirectory, "Assets"));
            }

            if (request.Type.HasFlag(ModType.Code))
            {
                Directory.CreateDirectory(Path.Combine(temporaryDirectory, "Source"));
            }

            var values = new Dictionary<string, string>
            {
                ["MOD_NAME"] = SecurityElement.Escape(request.Name.Trim()),
                ["ASSEMBLY_NAME"] = modName,
                ["NAMESPACE"] = modName,
                ["LIBRARY_NAME"] = request.Type.HasFlag(ModType.Code) ? modName + ".dll" : string.Empty,
                ["ENTRYPOINT"] = request.Type.HasFlag(ModType.Code) ? modName + ".ModComponent" : string.Empty,
                ["ASSETS_IMPORT"] = request.Type.HasFlag(ModType.Asset)
                    ? "<Import Project=\"Assets.targets\" />"
                    : string.Empty,
                ["AUTHOR"] = SecurityElement.Escape(request.Author.Trim()),
                ["DESCRIPTION"] = SecurityElement.Escape(request.Description.Trim()),
                ["VERSION"] = SecurityElement.Escape(request.Version.Trim()),
                ["FEZ_DIRECTORY"] = SecurityElement.Escape(Path.GetFullPath(request.FezDirectory))
            };

            if (request.Type.HasFlag(ModType.Code))
            {
                WriteTemplate("ModTemplate/Project.csproj", Path.Combine(temporaryDirectory, modName + ".csproj"), values);
                WriteTemplate("ModTemplate/ModComponent.cs", Path.Combine(temporaryDirectory, "Source", "ModComponent.cs"), values);

                if (request.Type.HasFlag(ModType.Asset))
                {
                    WriteTemplate("ModTemplate/Assets.targets", Path.Combine(temporaryDirectory, "Assets.targets"), values);
                }

                var readmeTemplate = request.Type.HasFlag(ModType.Asset)
                    ? "ModTemplate/README.md"
                    : "ModTemplate/README.Code.md";

                WriteTemplate(readmeTemplate, Path.Combine(temporaryDirectory, "README.md"), values);
                WriteTemplate("ModTemplate/UserPropertiesTemplate", Path.Combine(temporaryDirectory, "UserProperties.xml"), values);
                var templateValues = new Dictionary<string, string>(values)
                {
                    ["FEZ_DIRECTORY"] = string.Empty
                };

                WriteTemplate("ModTemplate/UserPropertiesTemplate", Path.Combine(temporaryDirectory, "UserProperties.xml.template"), templateValues);
                WriteTemplate("ModTemplate/.gitignore", Path.Combine(temporaryDirectory, ".gitignore"), values);
            }

            WriteTemplate("ModTemplate/Metadata.xml", Path.Combine(temporaryDirectory, "Metadata.xml"), values);

            foreach (var path in Directory.EnumerateFileSystemEntries(temporaryDirectory).ToArray())
            {
                var destination = Path.Combine(projectDirectory, Path.GetFileName(path));
                if (Directory.Exists(path))
                {
                    Directory.Move(path, destination);
                }
                else
                {
                    File.Move(path, destination);
                }
            }

            Directory.Delete(temporaryDirectory);
            return new ProjectResult(projectDirectory, request.Type);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }

            throw;
        }
    }

    private void WriteTemplate(string templatePath, string outputPath, IReadOnlyDictionary<string, string> values)
    {
        using var stream = _contentManager.LoadStream(templatePath);
        using var reader = new StreamReader(stream);
        var output = reader.ReadToEnd();
        foreach (var (name, value) in values)
        {
            output = output.Replace("{{" + name + "}}", value, StringComparison.Ordinal);
        }

        File.WriteAllText(outputPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void DrawCreatedPopup()
    {
        if (_pendingCreatedOpen)
        {
            ImGui.OpenPopup(CreatedPopupTitle);
            _pendingCreatedOpen = false;
        }

        var isOpen = true;
        ImGuiX.SetNextWindowCentered(ImGuiCond.Always);
        if (ImGui.BeginPopupModal(CreatedPopupTitle, ref isOpen,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("HAT mod created successfully.");
            ImGui.TextWrapped(_createdProject!.Type.HasFlag(ModType.Asset)
                ? "Open the new mod project in the editor now?"
                : "Open the C# project in your code editor to start editing the mod.");
            ImGui.TextWrapped(_createdProject.ProjectDirectory);
            ImGui.Spacing();

            if (_createdProject.Type.HasFlag(ModType.Asset))
            {
                if (ImGui.Button($"{Lucide.FolderCog} Open in editor"))
                {
                    OpenCreatedProject();
                }

                ImGui.SameLine();
            }

            if (ImGui.Button("Close"))
            {
                ImGui.CloseCurrentPopup();
                _closeRequested = true;
            }

            ImGui.EndPopup();
        }

        if (!isOpen)
        {
            _closeRequested = true;
        }
    }

    private void OpenCreatedProject()
    {
        try
        {
            var result = _createdProject ?? throw new InvalidOperationException("No created project is available.");
            var provider = new ModResourceProvider(new DirectoryInfo(result.ProjectDirectory), _storage);
            ImGui.CloseCurrentPopup();

            if (provider.References.Count < 1)
            {
                FileDialog.Show(FileDialog.Type.OpenFile, pakFiles =>
                {
                    provider.UpdateReferences(pakFiles);
                    OpenCreatedProjectProvider(provider, result.ProjectDirectory);
                    _resources.NotifyModOpenedFirstTime();
                }, new FileDialog.Options
                {
                    Title = "Add reference PAK files...",
                    AllowMultiple = true,
                    Filters = [new FileDialog.Filter("PAK files", "pak")]
                });
            }
            else
            {
                OpenCreatedProjectProvider(provider, result.ProjectDirectory);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Unable to open the created HAT mod project");
            _error = exception.Message;
            _createdProject = null;
            _pendingOpen = true;
        }
    }

    private void OpenCreatedProjectProvider(ModResourceProvider provider, string projectDirectory)
    {
        _storage.AddRecentProvider(projectDirectory, "Mod");
        _resources.OpenProvider(provider);
        Completed?.Invoke();
        _closeRequested = true;
    }

    private static string? Validate(ProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Enter a mod name.";
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return "Enter a version.";
        }

        var modName = ToIdentifier(request.Name);
        if (string.IsNullOrEmpty(modName))
        {
            return "The mod name must contain at least one letter or number.";
        }

        if (!Directory.Exists(request.ProjectDirectory))
        {
            return "Choose an existing empty folder for the project.";
        }

        if (Directory.EnumerateFileSystemEntries(request.ProjectDirectory).Any())
        {
            return "The project folder must be empty.";
        }

        if (!Directory.Exists(request.FezDirectory))
        {
            return "Choose the FEZ game folder with HAT installed.";
        }

        var requiredFiles = request.Type.HasFlag(ModType.Code) ? RequiredGameFiles : ["HAT.dll"];
        var missing = requiredFiles.FirstOrDefault(file => !File.Exists(Path.Combine(request.FezDirectory, file)));
        if (missing != null)
        {
            return $"The selected FEZ folder is missing {missing}.";
        }

        if (request.Type.HasFlag(ModType.Code) &&
            !Directory.Exists(Path.Combine(request.FezDirectory, "HATDependencies", "MonoMod")))
        {
            return "The selected FEZ folder is missing HATDependencies/MonoMod.";
        }

        var deploymentDirectory = Path.Combine(request.FezDirectory, "Mods", modName);
        if (Directory.Exists(deploymentDirectory) || File.Exists(deploymentDirectory))
        {
            return $"The HAT mod directory already exists: {deploymentDirectory}";
        }

        return null;
    }

    private static string ToIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0)
        {
            return string.Empty;
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static void DrawTextField(string label, string id, ref string value, uint maxLength)
    {
        ImGui.Text(label);
        ImGui.SetNextItemWidth(FieldWidth);
        ImGui.InputText(id, ref value, maxLength);
    }

    [Flags]
    private enum ModType
    {
        Asset = 0b01,
        Code = 0b10
    }

    private sealed record ProjectRequest(
        string Name,
        string Description,
        string Author,
        string Version,
        string ProjectDirectory,
        string FezDirectory,
        ModType Type
    );

    private sealed record ProjectResult(string ProjectDirectory, ModType Type);
}
