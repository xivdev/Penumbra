using Dalamud.Interface;
using ImGuiNET;
using Lumina.Data;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.Interop.ResourceTree;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileDialogService                                         _fileDialog;
    private readonly ResourceTreeFactory                                       _resourceTreeFactory;
    private readonly ResourceTreeViewer                                        _quickImportViewer;
    private readonly Dictionary<FullPath, IWritable?>                          _quickImportWritables = new();
    private readonly Dictionary<(Utf8GamePath, IWritable?), QuickImportAction> _quickImportActions   = new();

    private HashSet<string> GetPlayerResourcesOfType(ResourceType type)
    {
        var resources = ResourceTreeApiHelper
            .GetResourcesOfType(_resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly), type)
            .Values
            .SelectMany(r => r.Values)
            .Select(r => r.Item1);

        return new HashSet<string>(resources, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<FileRegistry> PopulateIsOnPlayer(IReadOnlyList<FileRegistry> files, ResourceType type)
    {
        var playerResources = GetPlayerResourcesOfType(type);
        foreach (var file in files)
            file.IsOnPlayer = playerResources.Contains(file.File.ToPath());

        return files;
    }

    private void DrawQuickImportTab()
    {
        using var tab = ImUtf8.TabItem("Import from Screen"u8);
        if (!tab)
        {
            _quickImportActions.Clear();
            return;
        }

        if (DrawOptionSelectHeader())
            _quickImportActions.Clear();
        _quickImportViewer.Draw();
    }

    private void OnQuickImportRefresh()
    {
        _quickImportWritables.Clear();
        _quickImportActions.Clear();
    }

    private void DrawQuickImportActions(ResourceNode resourceNode, Vector2 buttonSize)
    {
        if (!_quickImportWritables!.TryGetValue(resourceNode.FullPath, out var writable))
        {
            var path = resourceNode.FullPath.ToPath();
            if (resourceNode.FullPath.IsRooted)
            {
                writable = new RawFileWritable(path);
            }
            else
            {
                var file = _gameData.GetFile(path);
                writable = file is null ? null : new RawGameFileWritable(file);
            }

            _quickImportWritables.Add(resourceNode.FullPath, writable);
        }

        if (ImUtf8.IconButton(FontAwesomeIcon.Save, "Export this file."u8, buttonSize,
                resourceNode.FullPath.FullName.Length is 0 || writable is null))
        {
            var fullPathStr = resourceNode.FullPath.FullName;
            var ext = resourceNode.PossibleGamePaths.Length == 1
                ? Path.GetExtension(resourceNode.GamePath.ToString())
                : Path.GetExtension(fullPathStr);
            _fileDialog.OpenSavePicker($"Export {Path.GetFileName(fullPathStr)} to...", ext, Path.GetFileNameWithoutExtension(fullPathStr), ext,
                (success, name) =>
                {
                    if (!success)
                        return;

                    try
                    {
                        _editor.Compactor.WriteAllBytes(name, writable!.Write());
                    }
                    catch (Exception e)
                    {
                        Penumbra.Log.Error($"Could not export {fullPathStr}:\n{e}");
                    }
                }, null, false);
        }

        ImGui.SameLine();
        if (!_quickImportActions!.TryGetValue((resourceNode.GamePath, writable), out var quickImport))
        {
            quickImport = QuickImportAction.Prepare(this, resourceNode.GamePath, writable);
            _quickImportActions.Add((resourceNode.GamePath, writable), quickImport);
        }

        var canQuickImport     = quickImport.CanExecute;
        var quickImportEnabled = canQuickImport && (!resourceNode.Protected || _config.DeleteModModifier.IsActive());
        if (ImUtf8.IconButton(FontAwesomeIcon.FileImport,
                $"Add a copy of this file to {quickImport.OptionName}.{(canQuickImport && !quickImportEnabled ? $"\nHold {_config.DeleteModModifier} while clicking to add." : string.Empty)}",
                buttonSize,
                !quickImportEnabled))
        {
            quickImport.Execute();
            _quickImportActions.Remove((resourceNode.GamePath, writable));
        }
    }

    private record RawFileWritable(string Path) : IWritable
    {
        public bool Valid
            => true;

        public byte[] Write()
            => File.ReadAllBytes(Path);
    }

    private record RawGameFileWritable(FileResource FileResource) : IWritable
    {
        public bool Valid
            => true;

        public byte[] Write()
            => FileResource.Data;
    }

    public class QuickImportAction
    {
        public const string FallbackOptionName = "the current option";

        private readonly string       _optionName;
        private readonly Utf8GamePath _gamePath;
        private readonly ModEditor    _editor;
        private readonly IWritable?   _file;
        private readonly string?      _targetPath;
        private readonly int          _subDirs;

        public string OptionName
            => _optionName;

        public Utf8GamePath GamePath
            => _gamePath;

        public bool CanExecute
            => !_gamePath.IsEmpty && _editor.Mod != null && _file != null && _targetPath != null;

        /// <summary>
        /// Creates a non-executable QuickImportAction.
        /// </summary>
        private QuickImportAction(ModEditor editor, string optionName, Utf8GamePath gamePath)
        {
            _optionName = optionName;
            _gamePath   = gamePath;
            _editor     = editor;
            _file       = null;
            _targetPath = null;
            _subDirs    = 0;
        }

        /// <summary>
        /// Creates an executable QuickImportAction.
        /// </summary>
        private QuickImportAction(string optionName, Utf8GamePath gamePath, ModEditor editor, IWritable file, string targetPath, int subDirs)
        {
            _optionName = optionName;
            _gamePath   = gamePath;
            _editor     = editor;
            _file       = file;
            _targetPath = targetPath;
            _subDirs    = subDirs;
        }

        public static QuickImportAction Prepare(ModEditWindow owner, Utf8GamePath gamePath, IWritable? file)
        {
            var editor = owner._editor;
            if (editor is null)
                return new QuickImportAction(owner._editor, FallbackOptionName, gamePath);

            var subMod     = editor.Option!;
            var optionName = subMod is IModOption o ? o.FullName : FallbackOptionName;
            if (gamePath.IsEmpty || file is null || editor.FileEditor.Changes)
                return new QuickImportAction(editor, optionName, gamePath);

            if (subMod.Files.ContainsKey(gamePath) || subMod.FileSwaps.ContainsKey(gamePath))
                return new QuickImportAction(editor, optionName, gamePath);

            var mod = owner.Mod;
            if (mod is null)
                return new QuickImportAction(editor, optionName, gamePath);

            var (preferredPath, subDirs) = GetPreferredPath(mod, subMod as IModOption, owner._config.ReplaceNonAsciiOnImport);
            var targetPath = new FullPath(Path.Combine(preferredPath.FullName, gamePath.ToString())).FullName;
            if (File.Exists(targetPath))
                return new QuickImportAction(editor, optionName, gamePath);

            return new QuickImportAction(optionName, gamePath, editor, file, targetPath, subDirs);
        }

        public FileRegistry Execute()
        {
            if (!CanExecute)
                throw new InvalidOperationException();

            var directory = Path.GetDirectoryName(_targetPath);
            if (directory != null)
                Directory.CreateDirectory(directory);
            _editor.Compactor.WriteAllBytes(_targetPath!, _file!.Write());
            _editor.FileEditor.Revert(_editor.Mod!, _editor.Option!);
            var fileRegistry = _editor.Files.Available.First(file => file.File.FullName == _targetPath);
            _editor.FileEditor.AddPathsToSelected(_editor.Option!, new[]
            {
                fileRegistry,
            }, _subDirs);
            _editor.FileEditor.Apply(_editor.Mod!, _editor.Option!);

            return fileRegistry;
        }

        private static (DirectoryInfo, int) GetPreferredPath(Mod mod, IModOption? subMod, bool replaceNonAscii)
        {
            var path    = mod.ModPath;
            var subDirs = 0;
            if (subMod is null)
                return (path, subDirs);

            var name     = subMod.Name;
            var fullName = subMod.FullName;
            if (fullName.EndsWith(": " + name))
            {
                path    = ModCreator.NewOptionDirectory(path, fullName[..^(name.Length + 2)], replaceNonAscii);
                path    = ModCreator.NewOptionDirectory(path, name,                           replaceNonAscii);
                subDirs = 2;
            }
            else
            {
                path    = ModCreator.NewOptionDirectory(path, fullName, replaceNonAscii);
                subDirs = 1;
            }

            return (path, subDirs);
        }
    }
}
