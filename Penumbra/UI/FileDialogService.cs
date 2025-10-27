using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Services;

namespace Penumbra.UI;

public class FileDialogService : IDisposable, IUiService
{
    private readonly CommunicatorService                  _communicator;
    private readonly FileDialogManager                    _manager;
    private readonly ConcurrentDictionary<string, string> _startPaths = new();
    private          bool                                 _isOpen;

    private readonly Func<object?, object?>? _dialogGetter;

    public FileDialogService(CommunicatorService communicator, Configuration config)
    {
        _communicator = communicator;
        _manager      = SetupFileManager(config.ModDirectory);
        _communicator.ModDirectoryChanged.Subscribe(OnModDirectoryChange, ModDirectoryChanged.Priority.FileDialogService);

        var fieldType = _manager.GetType().GetField("dialog", BindingFlags.Instance | BindingFlags.NonPublic);
        _dialogGetter = fieldType is null ? null : fieldType.GetValue;
    }

    public void OpenFilePicker(string title, string filters, Action<bool, List<string>> callback, int selectionCountMax, string? startPath,
        bool forceStartPath)
    {
        _isOpen = true;
        _manager.OpenFileDialog(title, filters, CreateCallback(title, callback), selectionCountMax,
            GetStartPath(title, startPath, forceStartPath));
    }

    public void OpenFolderPicker(string title, Action<bool, string> callback, string? startPath, bool forceStartPath)
    {
        _isOpen = true;
        _manager.OpenFolderDialog(title, CreateCallback(title, callback), GetStartPath(title, startPath, forceStartPath));
    }

    public void OpenSavePicker(string title, string filters, string defaultFileName, string defaultExtension, Action<bool, string> callback,
        string? startPath,
        bool forceStartPath)
    {
        _isOpen = true;
        _manager.SaveFileDialog(title, filters, defaultFileName, defaultExtension, CreateCallback(title, callback),
            GetStartPath(title, startPath, forceStartPath));
    }

    public void Close()
    {
        _isOpen = false;
    }

    public void Reset()
    {
        _isOpen = false;
        _manager.Reset();
    }

    public void Draw()
    {
        if (_isOpen)
            _manager.Draw();
    }

    public void Dispose()
    {
        _startPaths.Clear();
        _manager.Reset();
        _communicator.ModDirectoryChanged.Unsubscribe(OnModDirectoryChange);
    }

    private string? GetStartPath(string title, string? startPath, bool forceStartPath)
    {
        var path = !forceStartPath && _startPaths.TryGetValue(title, out var p) ? p : startPath;
        if (!path.IsNullOrEmpty() && !Directory.Exists(path))
            path = null;
        return path;
    }

    private Action<bool, List<string>> CreateCallback(string title, Action<bool, List<string>> callback)
    {
        return (valid, list) =>
        {
            _isOpen = false;
            var loc = HandleRoot(GetCurrentLocation());
            _startPaths[title] = loc;
            callback(valid, list.Select(HandleRoot).ToList());
        };
    }

    private Action<bool, string> CreateCallback(string title, Action<bool, string> callback)
    {
        return (valid, list) =>
        {
            _isOpen = false;
            var loc = HandleRoot(GetCurrentLocation());
            _startPaths[title] = loc;
            callback(valid, HandleRoot(list));
        };
    }

    private static string HandleRoot(string path)
    {
        if (path is [_, ':'])
            return path + '\\';

        return path;
    }

    private string GetCurrentLocation()
        => (_dialogGetter?.Invoke(_manager) as FileDialog)?.GetCurrentPath() ?? ".";

    /// <summary> Set up the file selector with the right flags and custom side bar items. </summary>
    private static FileDialogManager SetupFileManager(string modDirectory)
    {
        var fileManager = new FileDialogManager
        {
            AddedWindowFlags = (WindowFlags.NoCollapse | WindowFlags.NoDocking).ToDalamudWindowFlags(),
        };

        if (WindowsFunctions.GetDownloadsFolder(out var downloadsFolder))
            fileManager.CustomSideBarItems.Add(("Downloads", downloadsFolder, FontAwesomeIcon.Download, -1));

        if (WindowsFunctions.GetQuickAccessFolders(out var folders))
            foreach (var (idx, (name, path)) in folders.Index())
                fileManager.CustomSideBarItems.Add(($"{name}##{idx}", path, FontAwesomeIcon.Folder, -1));

        // Add Penumbra Root. This is not updated if the root changes right now.
        fileManager.CustomSideBarItems.Add(("Root Directory", modDirectory, FontAwesomeIcon.Gamepad, 0));

        // Remove Videos and Music.
        fileManager.CustomSideBarItems.Add(("Videos", string.Empty, 0, -1));
        fileManager.CustomSideBarItems.Add(("Music", string.Empty, 0, -1));

        return fileManager;
    }

    /// <summary> Update the Root Directory link on changes. </summary>
    private void OnModDirectoryChange(in ModDirectoryChanged.Arguments arguments)
    {
        var idx = _manager.CustomSideBarItems.IndexOf(t => t.Name == "Root Directory");
        if (idx >= 0)
            _manager.CustomSideBarItems.RemoveAt(idx);

        if (!arguments.Valid)
            return;

        if (idx >= 0)
            _manager.CustomSideBarItems.Insert(idx, ("Root Directory", arguments.Directory, FontAwesomeIcon.Gamepad, 0));
        else
            _manager.CustomSideBarItems.Add(("Root Directory", arguments.Directory, FontAwesomeIcon.Gamepad, 0));
    }
}
