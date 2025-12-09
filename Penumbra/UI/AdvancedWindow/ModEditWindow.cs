using System.Collections.Frozen;
using Dalamud.Interface.Components;
using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using OtterGui;
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
using Penumbra.UI.Combos;
using MdlMaterialEditor = Penumbra.Mods.Editor.MdlMaterialEditor;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow : IndexedWindow, IDisposable
{
    private const string WindowBaseLabel = "###SubModEdit";

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
    private readonly OptionSelectCombo   _optionSelect;

    private Vector2 _iconSize = Vector2.Zero;
    private bool    _allowReduplicate;

    public Mod? Mod { get; private set; }


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

        WindowName = $"{mod.Name} (LOADING){WindowBaseLabel}{Index}";
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
            _itemSwapTab.UpdateMod(mod, _activeCollections.Current.GetInheritedSettings(mod.Index).Settings);
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
            sb.Append($"   |   {_editor.Files.Available.Count} Files ({FormattingFunctions.HumanReadableSize(size)})");

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
        sb.Append(Index);
        WindowName = sb.ToString();
    }

    public override void OnClose()
    {
        base.OnClose();
        if (Mod is not null && _config.Ephemeral.AdvancedEditingOpenForModPaths.Remove(Mod.Identifier))
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
        if (Mod is not null && _config.Ephemeral.AdvancedEditingOpenForModPaths.Add(Mod.Identifier))
            _config.Ephemeral.Save();

        if (IsLoading)
        {
            var radius    = 100 * Im.Style.GlobalScale;
            var thickness = (int)(20 * Im.Style.GlobalScale);
            var offsetX   = Im.ContentRegion.Available.X / 2 - radius;
            var offsetY   = Im.ContentRegion.Available.Y / 2 - radius;
            Im.Cursor.Position += new Vector2(offsetX, offsetY);
            ImEx.Spinner("##spinner"u8, radius, thickness, ImGuiColor.Text.Get());
            return;
        }

        using var tabBar = Im.TabBar.Begin("##tabs"u8);
        if (!tabBar)
            return;

        _iconSize = new Vector2(Im.Style.FrameHeight);
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
        using (var tab = tabBar.Item("Item Swap"u8))
        {
            if (tab)
                _itemSwapTab.DrawContent();
        }

        _pbdTab.Draw();

        DrawMissingFilesTab();
        DrawMaterialReassignmentTab();
    }

    private static readonly FrozenDictionary<GenderRace, StringU8> RaceCodeNames = Enum.GetValues<GenderRace>().ToFrozenDictionary(v => v, v =>
    {
        if (v is GenderRace.Unknown)
            return new StringU8("All Races and Genders");

        var (gender, race) = v.Split();
        return new StringU8($"({v.ToRaceCode()}) {race.ToNameU8()} {gender.ToNameU8()} ");
    });

    /// <summary> A row of three buttonSizes and a help marker that can be used for material suffix changing. </summary>
    private static class MaterialSuffix
    {
        private static string     _materialSuffixFrom = string.Empty;
        private static string     _materialSuffixTo   = string.Empty;
        private static GenderRace _raceCode           = GenderRace.Unknown;

        private static void DrawRaceCodeCombo(Vector2 buttonSize)
        {
            Im.Item.SetNextWidth(buttonSize.X);
            using var combo = Im.Combo.Begin("##RaceCode"u8, RaceCodeNames[_raceCode]);
            if (!combo)
                return;

            foreach (var (raceCode, name) in RaceCodeNames)
            {
                if (Im.Selectable(name, _raceCode == raceCode))
                    _raceCode = raceCode;
            }
        }

        public static void Draw(ModEditor editor, Vector2 buttonSize)
        {
            DrawRaceCodeCombo(buttonSize);
            Im.Line.Same();
            Im.Item.SetNextWidth(buttonSize.X);
            Im.Input.Text("##suffixFrom"u8, ref _materialSuffixFrom, "From..."u8);
            Im.Line.Same();
            Im.Item.SetNextWidth(buttonSize.X);
            Im.Input.Text("##suffixTo"u8, ref _materialSuffixTo, "To..."u8);
            Im.Line.Same();
            var disabled = !MdlMaterialEditor.ValidString(_materialSuffixTo);
            Utf8StringHandler<TextStringHandlerBuffer> tt = _materialSuffixTo.Length is 0
                ? "Please enter a target suffix."
                : _materialSuffixFrom == _materialSuffixTo
                    ? "The source and target are identical."
                    : disabled
                        ? "The suffix is invalid."
                        : _materialSuffixFrom.Length is 0
                            ? _raceCode is GenderRace.Unknown
                                ? "Convert all skin material suffices to the target."
                                : "Convert all skin material suffices for the given race code to the target."
                            : _raceCode is GenderRace.Unknown
                                ? $"Convert all skin material suffices that are currently '{_materialSuffixFrom}' to '{_materialSuffixTo}'."
                                : $"Convert all skin material suffices for the given race code that are currently '{_materialSuffixFrom}' to '{_materialSuffixTo}'.";
            if (ImEx.Button("Change Material Suffix"u8, buttonSize, tt, disabled))
                editor.MdlMaterialEditor.ReplaceAllMaterials(_materialSuffixTo, _materialSuffixFrom, _raceCode);

            var anyChanges = editor.MdlMaterialEditor.ModelFiles.Any(m => m.Changed);
            if (ImEx.Button("Save All Changes"u8, buttonSize,
                    anyChanges ? "Irreversibly rewrites all currently applied changes to model files."u8 : "No changes made yet."u8,
                    !anyChanges))
                editor.MdlMaterialEditor.SaveAllModels(editor.Compactor);

            Im.Line.Same();
            if (ImEx.Button("Revert All Changes"u8, buttonSize,
                    anyChanges ? "Revert all currently made and unsaved changes."u8 : "No changes made yet."u8, !anyChanges))
                editor.MdlMaterialEditor.RestoreAllModels();

            Im.Line.SameInner();
            LunaStyle.DrawAlignedHelpMarker(
                "Model files refer to the skin material they should use. This skin material is always the same, but modders have started using different suffices to differentiate between body types.\n"u8
              + "This option allows you to switch the suffix of all model files to another. This changes the files, so you do this on your own risk.\n"u8
              + "If you do not know what the currently used suffix of this mod is, you can leave 'From' blank and it will replace all suffices with 'To', instead of only the matching ones."u8);
        }
    }

    private void DrawMissingFilesTab()
    {
        if (_editor.Files.Missing.Count is 0)
            return;

        using var tab = Im.TabBar.BeginItem("Missing Files"u8);
        if (!tab)
            return;

        Im.Line.New();
        if (Im.Button("Remove Missing Files from Mod"u8))
            _editor.FileEditor.RemoveMissingPaths(Mod!, _editor.Option!);

        using var child = Im.Child.Begin("##unusedFiles"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##missingFiles"u8, 1, TableFlags.RowBackground, Im.ContentRegion.Available);
        if (!table)
            return;

        foreach (var path in _editor.Files.Missing)
            table.DrawColumn(path.FullName);
    }

    private void DrawDuplicatesTab()
    {
        using var tab = Im.TabBar.BeginItem("Duplicates"u8);
        if (!tab)
            return;

        if (_editor.Duplicates.Worker.IsCompleted)
        {
            if (ImEx.Button("Scan for Duplicates"u8, Vector2.Zero,
                    "Search for identical files in this mod. This may take a while."u8))
                _editor.Duplicates.StartDuplicateCheck(_editor.Files.Available);
        }
        else
        {
            if (ImEx.Button("Cancel Scanning for Duplicates"u8, Vector2.Zero, "Cancel the current scanning operation..."u8))
                _editor.Duplicates.Clear();
        }

        var modifier = _config.DeleteModModifier.IsActive();

        if (_editor.ModNormalizer.Running)
        {
            Im.ProgressBar((float)_editor.ModNormalizer.Step / _editor.ModNormalizer.TotalSteps,
                new Vector2(300 * Im.Style.GlobalScale, Im.Style.FrameHeight),
                $"{_editor.ModNormalizer.Step} / {_editor.ModNormalizer.TotalSteps}");
        }
        else if (ImEx.Button("Re-Duplicate and Normalize Mod"u8, Vector2.Zero,
                     "Tries to create a unique copy of a file for every game path manipulated and put them in [Groupname]/[Optionname]/[GamePath] order.\n"u8
                   + "This will also delete all unused files and directories if it succeeds.\n"u8
                   + "Care was taken that a failure should not destroy the mod but revert to its original state, but you use this at your own risk anyway."u8,
                     !_allowReduplicate && !modifier))
        {
            _editor.ModNormalizer.Normalize(Mod!);
            _editor.ModNormalizer.Worker.ContinueWith(_ => _editor.LoadMod(Mod!, _editor.GroupIdx, _editor.DataIdx), TaskScheduler.Default);
        }

        if (_allowReduplicate && !modifier)
            Im.Tooltip.OnHover($"\n\nNo duplicates detected! Hold {_config.DeleteModModifier} to force normalization anyway.");

        if (!_editor.Duplicates.Worker.IsCompleted)
            return;

        if (_editor.Duplicates.Duplicates.Count is 0)
        {
            Im.Line.New();
            Im.Text("No duplicates found."u8);
            return;
        }

        if (Im.Button("Delete and Redirect Duplicates"u8))
            _editor.Duplicates.DeleteDuplicates(_editor.Files, _editor.Mod!, _editor.Option!, true);

        if (_editor.Duplicates.SavedSpace > 0)
        {
            Im.Line.Same();
            Im.Text($"Frees up {FormattingFunctions.HumanReadableSize(_editor.Duplicates.SavedSpace)} from your hard drive.");
        }

        using var child = Im.Child.Begin("##duptable"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##duplicates"u8, 3, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
        if (!table)
            return;

        var width = Im.Font.CalculateSize("NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN "u8).X;
        table.SetupColumn("file"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("size"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("NNN.NNN  "u8).X);
        table.SetupColumn("hash"u8, TableColumnFlags.WidthFixed,
            Im.Window.Width > 2 * width ? width : Im.Font.CalculateSize("NNNNNNNN... "u8).X);
        foreach (var (set, size, hash) in _editor.Duplicates.Duplicates.Where(s => s.Paths.Length > 1))
        {
            table.NextColumn();
            using var tree = Im.Tree.Node(set[0].FullName[(Mod!.ModPath.FullName.Length + 1)..],
                TreeNodeFlags.NoTreePushOnOpen);
            table.NextColumn();
            ImEx.TextRightAligned(FormattingFunctions.HumanReadableSize(size));
            table.NextColumn();
            using (var _ = Im.Font.PushMono())
            {
                if (Im.Window.Width > 2 * width)
                    ImEx.TextRightAligned(FormattingFunctions.BytewiseHex(hash));
                else
                    ImEx.TextRightAligned($"{FormattingFunctions.BytewiseHex(hash.AsSpan(4))}...");
            }

            if (!tree)
                continue;

            using var indent = Im.Indent();
            foreach (var duplicate in set.Skip(1))
            {
                table.NextColumn();
                table.SetBackgroundColor(TableBackgroundTarget.Cell, Colors.RedTableBgTint);
                Im.Tree.Leaf(duplicate.FullName.AsSpan(Mod!.ModPath.FullName.Length + 1), TreeNodeFlags.Leaf);
                table.NextColumn();
                table.SetBackgroundColor(TableBackgroundTarget.Cell, Colors.RedTableBgTint);
                table.NextColumn();
                table.SetBackgroundColor(TableBackgroundTarget.Cell, Colors.RedTableBgTint);
            }
        }
    }

    private bool DrawOptionSelectHeader()
    {
        using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero).Push(ImStyleSingle.FrameRounding, 0);
        var       width = new Vector2(Im.ContentRegion.Available.X / 3, 0);
        var       ret   = false;
        if (ImEx.Button("Default Option"u8, width, "Switch to the default option for the mod.\nThis resets unsaved changes."u8,
                _editor.Option is DefaultSubMod))
        {
            _editor.LoadOption(-1, 0).Wait();
            ret = true;
        }

        Im.Line.Same();
        if (ImEx.Button("Refresh Data"u8, width, "Refresh data for the current option.\nThis resets unsaved changes."u8))
        {
            _editor.LoadMod(_editor.Mod!, _editor.GroupIdx, _editor.DataIdx).Wait();
            ret = true;
        }

        Im.Line.Same();
        if (_optionSelect.Draw("##option"u8, _editor.Option?.GetFullName() ?? string.Empty, default, width.X, out var option))
        {
            _editor.LoadOption(option.GroupIndex, option.DataIndex).Wait();
            ret = true;
        }

        return ret;
    }

    private string _newSwapKey   = string.Empty;
    private string _newSwapValue = string.Empty;

    private void DrawSwapTab()
    {
        using var tab = Im.TabBar.BeginItem("File Swaps"u8);
        if (!tab)
            return;

        DrawOptionSelectHeader();

        var setsEqual = !_editor.SwapEditor.Changes;
        var tt        = setsEqual ? "No changes staged."u8 : "Apply the currently staged changes to the option."u8;
        Im.Line.New();
        if (ImEx.Button("Apply Changes"u8, Vector2.Zero, tt, setsEqual))
            _editor.SwapEditor.Apply(_editor.Option!);

        Im.Line.Same();
        tt = setsEqual ? "No changes staged."u8 : "Revert all currently staged changes."u8;
        if (ImEx.Button("Revert Changes"u8, Vector2.Zero, tt, setsEqual))
            _editor.SwapEditor.Revert(_editor.Option!);

        var otherSwaps = _editor.Mod!.TotalSwapCount - _editor.Option!.FileSwaps.Count;
        if (otherSwaps > 0)
        {
            Im.Line.Same();
            ImEx.TextFramed($"There are {otherSwaps} file swaps configured in other options.", Vector2.Zero,
                ColorId.RedundantAssignment.Value().Color);
        }

        using var child = Im.Child.Begin("##swaps"u8, Im.ContentRegion.Available, true);
        if (!child)
            return;

        using var table = Im.Table.Begin("##table"u8, 3, TableFlags.RowBackground, Im.ContentRegion.Available);
        if (!table)
            return;

        var idx      = 0;
        var iconSize = Im.Style.FrameHeight * Vector2.One;
        var pathSize = Im.ContentRegion.Available.X / 2 - iconSize.X;
        table.SetupColumn("button"u8, TableColumnFlags.WidthFixed, iconSize.X);
        table.SetupColumn("source"u8, TableColumnFlags.WidthFixed, pathSize);
        table.SetupColumn("value"u8,  TableColumnFlags.WidthFixed, pathSize);

        foreach (var (gamePath, file) in _editor.SwapEditor.Swaps.ToList())
        {
            using var id = Im.Id.Push(idx++);
            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "Delete this swap."u8))
                _editor.SwapEditor.Remove(gamePath);

            table.NextColumn();
            var tmp = file.FullName;
            Im.Item.SetNextWidth(-1);
            if (Im.Input.Text("##value"u8, ref tmp, maxLength: Utf8GamePath.MaxGamePathLength) && tmp.Length > 0)
                _editor.SwapEditor.Change(gamePath, new FullPath(tmp));

            table.NextColumn();
            tmp = gamePath.Path.ToString();
            Im.Item.SetNextWidth(-1);
            if (Im.Input.Text("##key"u8, ref tmp, maxLength: Utf8GamePath.MaxGamePathLength)
             && Utf8GamePath.FromString(tmp, out var path)
             && !_editor.SwapEditor.Swaps.ContainsKey(path))
                _editor.SwapEditor.Change(gamePath, path);
        }

        table.NextColumn();
        var addable = Utf8GamePath.FromString(_newSwapKey, out var newPath)
         && newPath.Length > 0
         && _newSwapValue.Length > 0
         && _newSwapValue != _newSwapKey
         && !_editor.SwapEditor.Swaps.ContainsKey(newPath);
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, "Add a new file swap to this option."u8, !addable))
        {
            _editor.SwapEditor.Add(newPath, new FullPath(_newSwapValue));
            _newSwapKey   = string.Empty;
            _newSwapValue = string.Empty;
        }

        table.NextColumn();
        Im.Item.SetNextWidth(-1);
        Im.Input.Text("##swapKey"u8, ref _newSwapValue, "Load this file..."u8, maxLength: Utf8GamePath.MaxGamePathLength);
        table.NextColumn();
        Im.Item.SetNextWidth(-1);
        Im.Input.Text("##swapValue"u8, ref _newSwapKey, "... instead of this file."u8, maxLength: Utf8GamePath.MaxGamePathLength);
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
        if (currentFile is not null)
            return currentFile.Value;

        if (Mod is not null)
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

        if (Mod is not null)
            foreach (var option in Mod.AllDataContainers)
                foreach (var path in option.Files.Keys.Where(path => path.Path.StartsWith(prefix)))
                    ret.Add(path);

        return ret;
    }

    public ModEditWindow(FileDialogService fileDialog, ItemSwapTab itemSwapTab, IDataManager gameData,
        Configuration config, ModEditor editor, ResourceTreeFactory resourceTreeFactory, MetaFileManager metaFileManager,
        ActiveCollections activeCollections, ModMergeTab modMergeTab,
        CommunicatorService communicator, TextureManager textures, ModelManager models, IDragDropManager dragDropManager,
        ResourceTreeViewerFactory resourceTreeViewerFactory, IFramework framework,
        MetaDrawers metaDrawers,
        MtrlTabFactory mtrlTabFactory, int index)
        : base(WindowBaseLabel, index)
    {
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
        _optionSelect      = new OptionSelectCombo(editor, this);
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
        _pbdTab = new FileEditor<PbdTab>(this, _communicator, gameData, config, _editor.Compactor, _fileDialog, "Deformers", ".pbd",
            () => _editor.Files.Pbd, DrawDeformerPanel,
            () => Mod?.ModPath.FullName ?? string.Empty,
            (bytes, path, _) => new PbdTab(bytes, path));
        _center              = new CombinedTexture(_left, _right);
        _textureSelectCombo  = new TextureSelectCombo(resourceTreeFactory, editor, gameData);
        _resourceTreeFactory = resourceTreeFactory;
        _quickImportViewer   = resourceTreeViewerFactory.Create(1, OnQuickImportRefresh, DrawQuickImportActions);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModEditWindow);
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

    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Mod != Mod)
            return;

        switch (arguments.Type)
        {
            case ModPathChangeType.Reloaded or ModPathChangeType.Moved:
                Mod = null;
                ChangeMod(arguments.Mod);
                break;
            case ModPathChangeType.Deleted:
                IsOpen = false;
                Dispose();
                break;
        }
    }
}
