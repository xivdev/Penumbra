using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using Penumbra.GameData.Files;
using Penumbra.Import.Textures;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Textures;

public sealed partial class CombiningTextureEditor : IFileEditor
{
    private readonly TextureManager      _textures;
    private readonly IDragDropManager    _dragDropManager;
    private readonly FileDialogService   _fileDialog;
    private readonly Configuration       _config;
    private readonly IFramework          _framework;
    private readonly ModManager          _modManager;
    private readonly CommunicatorService _communicator;
    private readonly TextureSelectCombo? _textureSelectCombo;

    private readonly FileEditingContext? _context;
    private readonly bool                _inModEditWindow;
    private readonly bool                _writable;

    private readonly Texture         _left  = new();
    private readonly Texture         _right = new();
    private readonly CombinedTexture _center;

    private int  _currentSaveAs = (int)CombinedTexture.TextureSaveType.AsIs;
    private bool _addMipMaps    = true;

    private CombinedTexture.TextureSaveType? _nextSaveAs;
    private bool?                            _nextAddMipMaps;

    public event Action? SaveRequested;

    public CombiningTextureEditor(TextureManager textures, IDragDropManager dragDropManager, FileDialogService fileDialog, Configuration config,
        IFramework framework, ModManager modManager, CommunicatorService communicator, TextureSelectCombo? textureSelectCombo,
        FileEditingContext? context, bool inModEditWindow, bool writable)
    {
        _textures           = textures;
        _dragDropManager    = dragDropManager;
        _fileDialog         = fileDialog;
        _config             = config;
        _framework          = framework;
        _modManager         = modManager;
        _communicator       = communicator;
        _textureSelectCombo = textureSelectCombo;

        _context         = context;
        _inModEditWindow = inModEditWindow;
        _writable        = writable;

        _center = new CombinedTexture(_left, _right);

        if (inModEditWindow)
            SaveRequested += SaveRequestedInModEditWindow;
    }

    public void Dispose()
    {
        _left.Dispose();
        _right.Dispose();
        _center.Dispose();
    }

    public void LoadLeft(string path)
        => _left.Load(_textures, path);

    public async Task<byte[]> WriteAsync()
    {
        var saveAs     = _nextSaveAs ?? (CombinedTexture.TextureSaveType)_currentSaveAs;
        var addMipMaps = _nextAddMipMaps ?? _addMipMaps;
        _nextSaveAs     = null;
        _nextAddMipMaps = null;

        using var stream = new MemoryStream();
        await _center.SaveAs(TextureType.Tex, _textures, stream, saveAs, addMipMaps);
        return stream.ToArray();
    }

    bool IWritable.Valid
        => true;

    byte[] IWritable.Write()
        => WriteAsync().Result;

    private void SaveRequestedInModEditWindow()
    {
        if (!_writable)
        {
            _nextSaveAs     = null;
            _nextAddMipMaps = null;
            return;
        }

        var saveAs     = _nextSaveAs ?? (CombinedTexture.TextureSaveType)_currentSaveAs;
        var addMipMaps = _nextAddMipMaps ?? _addMipMaps;
        _nextSaveAs     = null;
        _nextAddMipMaps = null;

        var path = _left.Path;
        _center.SaveAs(null, _textures, path, saveAs, addMipMaps);
        AddPostSaveTask(path);
    }
}
