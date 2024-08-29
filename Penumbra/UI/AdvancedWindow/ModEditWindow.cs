using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.Import.Textures;
using Penumbra.Interop.ResourceTree;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.AdvancedWindow.Materials;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;
using Penumbra.Util;
using MdlMaterialEditor = Penumbra.Mods.Editor.MdlMaterialEditor;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow : Window, IDisposable, IUiService
{
    private const string WindowBaseLabel = "###SubModEdit";

    private readonly PerformanceTracker  _performance;
    private readonly ModEditor           _editor;
    private readonly Configuration       _config;
    private readonly ItemSwapTab         _itemSwapTab;
    private readonly MetaFileManager     _metaFileManager;
    private readonly ActiveCollections   _activeCollections;
    private readonly ModMergeTab         _modMergeTab;
    private readonly CommunicatorService _communicator;
    private readonly IDragDropManager    _dragDropManager;
    private readonly IDataManager        _gameData;
    private readonly IFramework          _framework;

    private Vector2 _iconSize = Vector2.Zero;
    private bool    _allowReduplicate;

    public Mod?   Mod { get; private set; }


    public bool IsLoading
    {
        get
        {
            lock (_lock)
            {
                return _editor.IsLoading || _loadingMod is { IsCompleted: false };
            }
        }
    }

    private readonly object _lock = new();
    private          Task?  _loadingMod;


    private void AppendTask(Action run)
    {
        lock (_lock)
        {
            if (_loadingMod == null || _loadingMod.IsCompleted)
                _loadingMod = Task.Run(run);
            else
                _loadingMod = _loadingMod.ContinueWith(_ => run());
        }
    }

    public void ChangeMod(Mod mod)
    {
        if (mod == Mod)
            return;

        WindowName = $"{mod.Name} (LOADING){WindowBaseLabel}";
        AppendTask(() =>
        {
            _editor.LoadMod(mod, -1, 0).Wait();
            Mod = mod;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(1240, 600),
                MaximumSize = 4000 * Vector2.One,
            };
            _selectedFiles.Clear();
            _modelTab.Reset();
            _materialTab.Reset();
            _shaderPackageTab.Reset();
            _itemSwapTab.UpdateMod(mod, _activeCollections.Current[mod.Index].Settings);
            UpdateModels();
            _forceTextureStartPath = true;
        });
    }

    public void ChangeOption(IModDataContainer? subMod)
    {
        AppendTask(() =>
        {
            var (groupIdx, dataIdx) = subMod?.GetDataIndices() ?? (-1, 0);
            _editor.LoadOption(groupIdx, dataIdx).Wait();
        });
    }

    public void UpdateModels()
    {
        if (Mod != null)
            _editor.MdlMaterialEditor.ScanModels(Mod);
    }

    public override bool DrawConditions()
        => Mod != null;

    public override void PreDraw()
    {
        if (IsLoading)
            return;

        using var performance = _performance.Measure(PerformanceType.UiAdvancedWindow);

        var sb = new StringBuilder(256);

        var redirections = 0;
        var unused       = 0;
        var size = _editor.Files.Available.Sum(f =>
        {
            if (f.SubModUsage.Count > 0)
                redirections += f.SubModUsage.Count;
            else
                ++unused;

            return f.FileSize;
        });
        var manipulations = 0;
        var subMods       = 0;
        var swaps = Mod!.AllDataContainers.Sum(m =>
        {
            ++subMods;
            manipulations += m.Manipulations.Count;
            return m.FileSwaps.Count;
        });
        sb.Append(Mod!.Name);
        if (subMods > 1)
            sb.Append($"   |   {subMods} Options");

        if (size > 0)
            sb.Append($"   |   {_editor.Files.Available.Count} Files ({Functions.HumanReadableSize(size)})");

        if (unused > 0)
            sb.Append($"   |   {unused} Unused Files");

        if (_editor.Files.Missing.Count > 0)
            sb.Append($"   |   {_editor.Files.Missing.Count} Missing Files");

        if (redirections > 0)
            sb.Append($"   |   {redirections} Redirections");

        if (manipulations > 0)
            sb.Append($"   |   {manipulations} Manipulations");

        if (swaps > 0)
            sb.Append($"   |   {swaps} Swaps");

        _allowReduplicate = redirections != _editor.Files.Available.Count || _editor.Files.Missing.Count > 0 || unused > 0;
        sb.Append(WindowBaseLabel);
        WindowName = sb.ToString();
    }

    public override void OnClose()
    {
        _config.Ephemeral.AdvancedEditingOpen = false;
        _config.Ephemeral.Save();
        AppendTask(() =>
        {
            _left.Dispose();
            _right.Dispose();
            _materialTab.Reset();
            _modelTab.Reset();
            _shaderPackageTab.Reset();
        });
    }

    public override void Draw()
    {
        using var performance = _performance.Measure(PerformanceType.UiAdvancedWindow);

        if (!_config.Ephemeral.AdvancedEditingOpen)
        {
            _config.Ephemeral.AdvancedEditingOpen = true;
            _config.Ephemeral.Save();
        }

        if (IsLoading)
        {
            var radius    = 100 * ImUtf8.GlobalScale;
            var thickness = (int) (20 * ImUtf8.GlobalScale);
            var offsetX   = ImGui.GetContentRegionAvail().X / 2 - radius;
            var offsetY   = ImGui.GetContentRegionAvail().Y / 2 - radius;
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(offsetX, offsetY));
            ImUtf8.Spinner("##spinner"u8, radius, thickness, ImGui.GetColorU32(ImGuiCol.Text));
            return;
        }

        using var tabBar = ImRaii.TabBar("##tabs");
        if (!tabBar)
            return;

        _iconSize = new Vector2(ImGui.GetFrameHeight());
        DrawFileTab();
        DrawMetaTab();
        DrawSwapTab();
        _modMergeTab.Draw();
        DrawDuplicatesTab();
        DrawQuickImportTab();
        _modelTab.Draw();
        _materialTab.Draw();
        DrawTextureTab();
        _shaderPackageTab.Draw();
        using (var tab = ImRaii.TabItem("Item Swap"))
        {
            if (tab)
                _itemSwapTab.DrawContent();
        }

        DrawMissingFilesTab();
        DrawMaterialReassignmentTab();
    }

    /// <summary> A row of three buttonSizes and a help marker that can be used for material suffix changing. </summary>
    private static class MaterialSuffix
    {
        private static string     _materialSuffixFrom = string.Empty;
        private static string     _materialSuffixTo   = string.Empty;
        private static GenderRace _raceCode           = GenderRace.Unknown;

        private static string RaceCodeName(GenderRace raceCode)
        {
            if (raceCode == GenderRace.Unknown)
                return "All Races and Genders";

            var (gender, race) = raceCode.Split();
            return $"({raceCode.ToRaceCode()}) {race.ToName()} {gender.ToName()} ";
        }

        private static void DrawRaceCodeCombo(Vector2 buttonSize)
        {
            ImGui.SetNextItemWidth(buttonSize.X);
            using var combo = ImRaii.Combo("##RaceCode", RaceCodeName(_raceCode));
            if (!combo)
                return;

            foreach (var raceCode in Enum.GetValues<GenderRace>())
            {
                if (ImGui.Selectable(RaceCodeName(raceCode), _raceCode == raceCode))
                    _raceCode = raceCode;
            }
        }

        public static void Draw(ModEditor editor, Vector2 buttonSize)
        {
            DrawRaceCodeCombo(buttonSize);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(buttonSize.X);
            ImGui.InputTextWithHint("##suffixFrom", "From...", ref _materialSuffixFrom, 32);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(buttonSize.X);
            ImGui.InputTextWithHint("##suffixTo", "To...", ref _materialSuffixTo, 32);
            ImGui.SameLine();
            var disabled = !MdlMaterialEditor.ValidString(_materialSuffixTo);
            var tt = _materialSuffixTo.Length == 0
                ? "Please enter a target suffix."
                : _materialSuffixFrom == _materialSuffixTo
                    ? "The source and target are identical."
                    : disabled
                        ? "The suffix is invalid."
                        : _materialSuffixFrom.Length == 0
                            ? _raceCode == GenderRace.Unknown
                                ? "Convert all skin material suffices to the target."
                                : "Convert all skin material suffices for the given race code to the target."
                            : _raceCode == GenderRace.Unknown
                                ? $"Convert all skin material suffices that are currently '{_materialSuffixFrom}' to '{_materialSuffixTo}'."
                                : $"Convert all skin material suffices for the given race code that are currently '{_materialSuffixFrom}' to '{_materialSuffixTo}'.";
            if (ImGuiUtil.DrawDisabledButton("Change Material Suffix", buttonSize, tt, disabled))
                editor.MdlMaterialEditor.ReplaceAllMaterials(_materialSuffixTo, _materialSuffixFrom, _raceCode);

            var anyChanges = editor.MdlMaterialEditor.ModelFiles.Any(m => m.Changed);
            if (ImGuiUtil.DrawDisabledButton("Save All Changes", buttonSize,
                    anyChanges ? "Irreversibly rewrites all currently applied changes to model files." : "No changes made yet.", !anyChanges))
                editor.MdlMaterialEditor.SaveAllModels(editor.Compactor);

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("Revert All Changes", buttonSize,
                    anyChanges ? "Revert all currently made and unsaved changes." : "No changes made yet.", !anyChanges))
                editor.MdlMaterialEditor.RestoreAllModels();

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Model files refer to the skin material they should use. This skin material is always the same, but modders have started using different suffices to differentiate between body types.\n"
              + "This option allows you to switch the suffix of all model files to another. This changes the files, so you do this on your own risk.\n"
              + "If you do not know what the currently used suffix of this mod is, you can leave 'From' blank and it will replace all suffices with 'To', instead of only the matching ones.");
        }
    }

    private void DrawMissingFilesTab()
    {
        if (_editor.Files.Missing.Count == 0)
            return;

        using var tab = ImRaii.TabItem("Missing Files");
        if (!tab)
            return;

        ImGui.NewLine();
        if (ImGui.Button("Remove Missing Files from Mod"))
            _editor.FileEditor.RemoveMissingPaths(Mod!, _editor.Option!);

        using var child = ImRaii.Child("##unusedFiles", -Vector2.One, true);
        if (!child)
            return;

        using var table = ImRaii.Table("##missingFiles", 1, ImGuiTableFlags.RowBg, -Vector2.One);
        if (!table)
            return;

        foreach (var path in _editor.Files.Missing)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(path.FullName);
        }
    }

    private void DrawDuplicatesTab()
    {
        using var tab = ImRaii.TabItem("Duplicates");
        if (!tab)
            return;

        if (_editor.Duplicates.Worker.IsCompleted)
        {
            if (ImGuiUtil.DrawDisabledButton("Scan for Duplicates", Vector2.Zero,
                    "Search for identical files in this mod. This may take a while.", false))
                _editor.Duplicates.StartDuplicateCheck(_editor.Files.Available);
        }
        else
        {
            if (ImGuiUtil.DrawDisabledButton("Cancel Scanning for Duplicates", Vector2.Zero, "Cancel the current scanning operation...", false))
                _editor.Duplicates.Clear();
        }

        const string desc =
            "Tries to create a unique copy of a file for every game path manipulated and put them in [Groupname]/[Optionname]/[GamePath] order.\n"
          + "This will also delete all unused files and directories if it succeeds.\n"
          + "Care was taken that a failure should not destroy the mod but revert to its original state, but you use this at your own risk anyway.";

        var modifier = _config.DeleteModModifier.IsActive();

        var tt = _allowReduplicate ? desc :
            modifier ? desc : desc + $"\n\nNo duplicates detected! Hold {_config.DeleteModModifier} to force normalization anyway.";

        if (_editor.ModNormalizer.Running)
        {
            ImGui.ProgressBar((float)_editor.ModNormalizer.Step / _editor.ModNormalizer.TotalSteps,
                new Vector2(300 * UiHelpers.Scale, ImGui.GetFrameHeight()),
                $"{_editor.ModNormalizer.Step} / {_editor.ModNormalizer.TotalSteps}");
        }
        else if (ImGuiUtil.DrawDisabledButton("Re-Duplicate and Normalize Mod", Vector2.Zero, tt, !_allowReduplicate && !modifier))
        {
            _editor.ModNormalizer.Normalize(Mod!);
            _editor.ModNormalizer.Worker.ContinueWith(_ => _editor.LoadMod(Mod!, _editor.GroupIdx, _editor.DataIdx), TaskScheduler.Default);
        }

        if (!_editor.Duplicates.Worker.IsCompleted)
            return;

        if (_editor.Duplicates.Duplicates.Count == 0)
        {
            ImGui.NewLine();
            ImGui.TextUnformatted("No duplicates found.");
            return;
        }

        if (ImGui.Button("Delete and Redirect Duplicates"))
            _editor.Duplicates.DeleteDuplicates(_editor.Files, _editor.Mod!, _editor.Option!, true);

        if (_editor.Duplicates.SavedSpace > 0)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"Frees up {Functions.HumanReadableSize(_editor.Duplicates.SavedSpace)} from your hard drive.");
        }

        using var child = ImRaii.Child("##duptable", -Vector2.One, true);
        if (!child)
            return;

        using var table = ImRaii.Table("##duplicates", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One);
        if (!table)
            return;

        var width = ImGui.CalcTextSize("NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN ").X;
        ImGui.TableSetupColumn("file", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("size", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("NNN.NNN  ").X);
        ImGui.TableSetupColumn("hash", ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetWindowWidth() > 2 * width ? width : ImGui.CalcTextSize("NNNNNNNN... ").X);
        foreach (var (set, size, hash) in _editor.Duplicates.Duplicates.Where(s => s.Paths.Length > 1))
        {
            ImGui.TableNextColumn();
            using var tree = ImRaii.TreeNode(set[0].FullName[(Mod!.ModPath.FullName.Length + 1)..],
                ImGuiTreeNodeFlags.NoTreePushOnOpen);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(Functions.HumanReadableSize(size));
            ImGui.TableNextColumn();
            using (var _ = ImRaii.PushFont(UiBuilder.MonoFont))
            {
                if (ImGui.GetWindowWidth() > 2 * width)
                    ImGuiUtil.RightAlign(string.Concat(hash.Select(b => b.ToString("X2"))));
                else
                    ImGuiUtil.RightAlign(string.Concat(hash.Take(4).Select(b => b.ToString("X2"))) + "...");
            }

            if (!tree)
                continue;

            using var indent = ImRaii.PushIndent();
            foreach (var duplicate in set.Skip(1))
            {
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint);
                using var node = ImRaii.TreeNode(duplicate.FullName[(Mod!.ModPath.FullName.Length + 1)..], ImGuiTreeNodeFlags.Leaf);
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint);
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Colors.RedTableBgTint);
            }
        }
    }

    private bool DrawOptionSelectHeader()
    {
        const string defaultOption = "Default Option";
        using var    style         = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero).Push(ImGuiStyleVar.FrameRounding, 0);
        var          width         = new Vector2(ImGui.GetContentRegionAvail().X / 3, 0);
        var          ret           = false;
        if (ImGuiUtil.DrawDisabledButton(defaultOption, width, "Switch to the default option for the mod.\nThis resets unsaved changes.",
                _editor.Option is DefaultSubMod))
        {
            _editor.LoadOption(-1, 0).Wait();
            ret = true;
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Refresh Data", width, "Refresh data for the current option.\nThis resets unsaved changes.", false))
        {
            _editor.LoadMod(_editor.Mod!, _editor.GroupIdx, _editor.DataIdx).Wait();
            ret = true;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(width.X);
        style.Push(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);
        using var color = ImRaii.PushColor(ImGuiCol.Border, ColorId.FolderLine.Value());
        using var combo = ImRaii.Combo("##optionSelector", _editor.Option!.GetFullName());
        if (!combo)
            return ret;

        foreach (var (option, idx) in Mod!.AllDataContainers.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            if (ImGui.Selectable(option.GetFullName(), option == _editor.Option))
            {
                var (groupIdx, dataIdx) = option.GetDataIndices();
                _editor.LoadOption(groupIdx, dataIdx).Wait();
                ret = true;
            }
        }

        return ret;
    }

    private string _newSwapKey   = string.Empty;
    private string _newSwapValue = string.Empty;

    private void DrawSwapTab()
    {
        using var tab = ImRaii.TabItem("File Swaps");
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.SwapEditor.Changes;
        var tt        = setsEqual ? "No changes staged." : "Apply the currently staged changes to the option.";
        ImGui.NewLine();
        if (ImGuiUtil.DrawDisabledButton("Apply Changes", Vector2.Zero, tt, setsEqual))
            _editor.SwapEditor.Apply(_editor.Option!);

        ImGui.SameLine();
        tt = setsEqual ? "No changes staged." : "Revert all currently staged changes.";
        if (ImGuiUtil.DrawDisabledButton("Revert Changes", Vector2.Zero, tt, setsEqual))
            _editor.SwapEditor.Revert(_editor.Option!);

        var otherSwaps = _editor.Mod!.TotalSwapCount - _editor.Option!.FileSwaps.Count;
        if (otherSwaps > 0)
        {
            ImGui.SameLine();
            ImGuiUtil.DrawTextButton($"There are {otherSwaps} file swaps configured in other options.", Vector2.Zero,
                ColorId.RedundantAssignment.Value());
        }

        using var child = ImRaii.Child("##swaps", -Vector2.One, true);
        if (!child)
            return;

        using var list = ImRaii.Table("##table", 3, ImGuiTableFlags.RowBg, -Vector2.One);
        if (!list)
            return;

        var idx      = 0;
        var iconSize = ImGui.GetFrameHeight() * Vector2.One;
        var pathSize = ImGui.GetContentRegionAvail().X / 2 - iconSize.X;
        ImGui.TableSetupColumn("button", ImGuiTableColumnFlags.WidthFixed, iconSize.X);
        ImGui.TableSetupColumn("source", ImGuiTableColumnFlags.WidthFixed, pathSize);
        ImGui.TableSetupColumn("value",  ImGuiTableColumnFlags.WidthFixed, pathSize);

        foreach (var (gamePath, file) in _editor.SwapEditor.Swaps.ToList())
        {
            using var id = ImRaii.PushId(idx++);
            ImGui.TableNextColumn();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this swap.", false, true))
                _editor.SwapEditor.Remove(gamePath);

            ImGui.TableNextColumn();
            var tmp = file.FullName;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##value", ref tmp, Utf8GamePath.MaxGamePathLength) && tmp.Length > 0)
                _editor.SwapEditor.Change(gamePath, new FullPath(tmp));

            ImGui.TableNextColumn();
            tmp = gamePath.Path.ToString();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##key", ref tmp, Utf8GamePath.MaxGamePathLength)
             && Utf8GamePath.FromString(tmp, out var path)
             && !_editor.SwapEditor.Swaps.ContainsKey(path))
                _editor.SwapEditor.Change(gamePath, path);
        }

        ImGui.TableNextColumn();
        var addable = Utf8GamePath.FromString(_newSwapKey, out var newPath)
         && newPath.Length > 0
         && _newSwapValue.Length > 0
         && _newSwapValue != _newSwapKey
         && !_editor.SwapEditor.Swaps.ContainsKey(newPath);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), iconSize, "Add a new file swap to this option.", !addable,
                true))
        {
            _editor.SwapEditor.Add(newPath, new FullPath(_newSwapValue));
            _newSwapKey   = string.Empty;
            _newSwapValue = string.Empty;
        }

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##swapKey", "Load this file...", ref _newSwapValue, Utf8GamePath.MaxGamePathLength);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##swapValue", "... instead of this file.", ref _newSwapKey, Utf8GamePath.MaxGamePathLength);
    }

    /// <summary>
    /// Find the best matching associated file for a given path.
    /// </summary>
    /// <remarks>
    /// Tries to resolve from the current collection first and chooses the currently resolved file if any exists.
    /// If none exists, goes through all options in the currently selected mod (if any) in order of priority and resolves in them. 
    /// If no redirection is found in either of those options, returns the original path.
    /// </remarks>
    internal FullPath FindBestMatch(Utf8GamePath path)
    {
        var currentFile = _activeCollections.Current.ResolvePath(path);
        if (currentFile != null)
            return currentFile.Value;

        if (Mod != null)
        {
            foreach (var option in Mod.Groups.OrderByDescending(g => g.Priority))
            {
                if (option.FindBestMatch(path) is { } fullPath)
                    return fullPath;
            }

            if (Mod.Default.Files.TryGetValue(path, out var value) || Mod.Default.FileSwaps.TryGetValue(path, out value))
                return value;
        }

        return new FullPath(path);
    }

    internal HashSet<Utf8GamePath> FindPathsStartingWith(CiByteString prefix)
    {
        var ret = new HashSet<Utf8GamePath>();

        foreach (var path in _activeCollections.Current.ResolvedFiles.Keys)
        {
            if (path.Path.StartsWith(prefix))
                ret.Add(path);
        }

        if (Mod != null)
            foreach (var option in Mod.AllDataContainers)
            {
                foreach (var path in option.Files.Keys)
                {
                    if (path.Path.StartsWith(prefix))
                        ret.Add(path);
                }
            }

        return ret;
    }

    public ModEditWindow(PerformanceTracker performance, FileDialogService fileDialog, ItemSwapTab itemSwapTab, IDataManager gameData,
        Configuration config, ModEditor editor, ResourceTreeFactory resourceTreeFactory, MetaFileManager metaFileManager,
        ActiveCollections activeCollections, ModMergeTab modMergeTab,
        CommunicatorService communicator, TextureManager textures, ModelManager models, IDragDropManager dragDropManager,
        ResourceTreeViewerFactory resourceTreeViewerFactory, IFramework framework,
        MetaDrawers metaDrawers, MigrationManager migrationManager,
        MtrlTabFactory mtrlTabFactory, ModSelection selection)
        : base(WindowBaseLabel)
    {
        _performance       = performance;
        _itemSwapTab       = itemSwapTab;
        _gameData          = gameData;
        _config            = config;
        _editor            = editor;
        _metaFileManager   = metaFileManager;
        _activeCollections = activeCollections;
        _modMergeTab       = modMergeTab;
        _communicator      = communicator;
        _dragDropManager   = dragDropManager;
        _textures          = textures;
        _models            = models;
        _fileDialog        = fileDialog;
        _framework         = framework;
        _metaDrawers       = metaDrawers;
        _materialTab = new FileEditor<MtrlTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "Materials", ".mtrl",
            () => PopulateIsOnPlayer(_editor.Files.Mtrl, ResourceType.Mtrl), DrawMaterialPanel, () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, path, writable) => mtrlTabFactory.Create(this, new MtrlFile(bytes), path, writable));
        _modelTab = new FileEditor<MdlTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "Models", ".mdl",
            () => PopulateIsOnPlayer(_editor.Files.Mdl, ResourceType.Mdl), DrawModelPanel, () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, path, _) => new MdlTab(this, bytes, path));
        _shaderPackageTab = new FileEditor<ShpkTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "Shaders", ".shpk",
            () => PopulateIsOnPlayer(_editor.Files.Shpk, ResourceType.Shpk), DrawShaderPackagePanel,
            () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, path, _) => new ShpkTab(_fileDialog, bytes, path));
        _center              = new CombinedTexture(_left, _right);
        _textureSelectCombo  = new TextureDrawer.PathSelectCombo(textures, editor, () => GetPlayerResourcesOfType(ResourceType.Tex));
        _resourceTreeFactory = resourceTreeFactory;
        _quickImportViewer   = resourceTreeViewerFactory.Create(2, OnQuickImportRefresh, DrawQuickImportActions);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModEditWindow);
        IsOpen = _config is { OpenWindowAtStart: true, Ephemeral.AdvancedEditingOpen: true };
        if (IsOpen && selection.Mod != null)
            ChangeMod(selection.Mod);
    }

    public void Dispose()
    {
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _editor.Dispose();
        _materialTab.Dispose();
        _modelTab.Dispose();
        _shaderPackageTab.Dispose();
        _left.Dispose();
        _right.Dispose();
        _center.Dispose();
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? _1, DirectoryInfo? _2)
    {
        if (type is not (ModPathChangeType.Reloaded or ModPathChangeType.Moved) || mod != Mod)
            return;

        Mod = null;
        ChangeMod(mod);
    }
}
