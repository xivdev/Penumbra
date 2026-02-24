using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow.Materials;

public sealed partial class MtrlTab : IWritable, IDisposable
{
    private const int ShpkPrefixLength = 16;

    private static readonly CiByteString ShpkPrefix = CiByteString.FromSpanUnsafe("shader/sm5/shpk/"u8, true, true, true);

    private readonly IDataManager            _gameData;
    private readonly IFramework              _framework;
    private readonly ObjectManager           _objects;
    private readonly CharacterBaseDestructor _characterBaseDestructor;
    private readonly StainService            _stainService;
    private readonly ResourceTreeFactory     _resourceTreeFactory;
    private readonly FileDialogService       _fileDialog;
    private readonly MaterialTemplatePickers _materialTemplatePickers;
    private readonly Configuration           _config;

    private readonly ModEditWindow _edit;
    public readonly  MtrlFile      Mtrl;
    public readonly  string        FilePath;
    public readonly  bool          Writable;

    private bool _updateOnNextFrame;

    public MtrlTab(IDataManager gameData, IFramework framework, ObjectManager objects, CharacterBaseDestructor characterBaseDestructor,
        StainService stainService, ResourceTreeFactory resourceTreeFactory, FileDialogService fileDialog,
        MaterialTemplatePickers materialTemplatePickers,
        Configuration config, ModEditWindow edit, MtrlFile file, string filePath, bool writable)
    {
        _gameData                = gameData;
        _framework               = framework;
        _objects                 = objects;
        _characterBaseDestructor = characterBaseDestructor;
        _stainService            = stainService;
        _resourceTreeFactory     = resourceTreeFactory;
        _fileDialog              = fileDialog;
        _materialTemplatePickers = materialTemplatePickers;
        _config                  = config;

        _edit                 = edit;
        Mtrl                  = file;
        FilePath              = filePath;
        Writable              = writable;
        _samplersPinned       = true;
        _associatedBaseDevkit = TryLoadShpkDevkit("_base", out _loadedBaseDevkitPathName);
        Update();
        LoadShpk(FindAssociatedShpk(out _, out _));
        if (writable)
        {
            _characterBaseDestructor.Subscribe(UnbindFromDrawObjectMaterialInstances, CharacterBaseDestructor.Priority.MtrlTab);
            BindToMaterialInstances();
        }
    }

    public bool DrawVersionUpdate(bool disabled)
    {
        if (disabled || Mtrl.IsDawntrail)
            return false;

        if (!ImEx.Button("Update MTRL Version to Dawntrail"u8, Colors.PressEnterWarningBg, default, Im.ContentRegion.Available with { Y = 0 },
                "Try using this if the material can not be loaded or should use legacy shaders.\n\nThis is not revertible."u8))
            return false;

        Mtrl.MigrateToDawntrail();
        Update();
        LoadShpk(FindAssociatedShpk(out _, out _));
        return true;
    }

    public bool DrawPanel(bool disabled)
    {
        if (_updateOnNextFrame)
        {
            _updateOnNextFrame = false;
            Update();
        }

        DrawMaterialLivePreviewRebind(disabled);
        
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        var ret = DrawBackFaceAndTransparency(disabled);
        
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        ret |= DrawShaderSection(disabled);
        
        ret |= DrawTextureSection(disabled);
        ret |= DrawColorTableSection(disabled);
        ret |= DrawConstantsSection(disabled);
        
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        DrawOtherMaterialDetails(disabled);

        return !disabled && ret;
    }

    private bool DrawBackFaceAndTransparency(bool disabled)
    {
        ref var shaderFlags = ref ShaderFlags.Wrap(ref Mtrl.ShaderPackage.Flags);

        var ret = false;

        using var dis = Im.Disabled(disabled);

        var tmp = shaderFlags.EnableTransparency;

        // guardrail: the game crashes if transparency is enabled on characterstockings.shpk
        var disallowTransparency = Mtrl.ShaderPackage.Name == "characterstockings.shpk";
        using (Im.Disabled(disallowTransparency))
        {
            if (Im.Checkbox("Enable Transparency"u8, ref tmp))
            {
                shaderFlags.EnableTransparency = tmp;
                ret                            = true;
                SetShaderPackageFlags(Mtrl.ShaderPackage.Flags);
            }
        }

        if (disallowTransparency)
            LunaStyle.DrawHelpMarker("Enabling transparency for shader package characterstockings.shpk will crash the game."u8);

        Im.Line.Same(200 * Im.Style.GlobalScale + Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X);
        tmp = shaderFlags.HideBackfaces;
        if (Im.Checkbox("Hide Backfaces"u8, ref tmp))
        {
            shaderFlags.HideBackfaces = tmp;
            ret                       = true;
            SetShaderPackageFlags(Mtrl.ShaderPackage.Flags);
        }

        if (_shpkLoading)
        {
            Im.Line.Same(400 * Im.Style.GlobalScale + 2 * Im.Style.ItemSpacing.X + Im.Style.WindowPadding.X);

            Im.Text("Loading shader (.shpk) file. Some functionality will only be available after this is done."u8,
                ImGuiColor.Text.Get().HalfBlend(0x808000));
        }

        return ret;
    }

    private void DrawOtherMaterialDetails(bool _)
    {
        if (!Im.Tree.Header("Further Content"u8))
            return;

        using (var sets = Im.Tree.Node("UV Sets"u8, TreeNodeFlags.DefaultOpen))
        {
            if (sets)
                foreach (var set in Mtrl.UvSets)
                    Im.Tree.Leaf($"#{set.Index:D2} - {set.Name}");
        }

        using (var sets = Im.Tree.Node("Color Sets"u8, TreeNodeFlags.DefaultOpen))
        {
            if (sets)
                foreach (var set in Mtrl.ColorSets)
                    Im.Tree.Leaf($"#{set.Index:D2} - {set.Name}");
        }

        if (Mtrl.AdditionalData.Length <= 0)
            return;

        using var t = Im.Tree.Node($"Additional Data (Size: {Mtrl.AdditionalData.Length})###AdditionalData");
        if (t)
            ImEx.HexViewer(Mtrl.AdditionalData);
    }

    private void UnpinResources(bool all)
    {
        _samplersPinned = false;

        if (!all)
            return;

        var keys = Mtrl.ShaderPackage.ShaderKeys;
        for (var i = 0; i < keys.Length; i++)
            keys[i].Pinned = false;

        var constants = Mtrl.ShaderPackage.Constants;
        for (var i = 0; i < constants.Length; i++)
            constants[i].Pinned = false;
    }

    private void Update()
    {
        UpdateShaders();
        UpdateTextures();
        UpdateConstants();
    }

    public void Dispose()
    {
        UnbindFromMaterialInstances();
        if (Writable)
            _characterBaseDestructor.Unsubscribe(UnbindFromDrawObjectMaterialInstances);
    }

    public bool Valid
        => Mtrl.Valid; // FIXME This should be _shadersKnown && Mtrl.Valid but the algorithm for _shadersKnown is flawed as of 7.2.

    public byte[] Write()
    {
        var output = Mtrl.Clone();
        output.GarbageCollect(_associatedShpk, TextureIds);

        return output.Write();
    }
}
