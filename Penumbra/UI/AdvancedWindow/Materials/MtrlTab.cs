using Dalamud.Plugin.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData;
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

    public unsafe MtrlTab(IDataManager gameData, IFramework framework, ObjectManager objects, CharacterBaseDestructor characterBaseDestructor,
        StainService stainService, ResourceTreeFactory resourceTreeFactory, FileDialogService fileDialog, MaterialTemplatePickers materialTemplatePickers,
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

        _edit                = edit;
        Mtrl                 = file;
        FilePath             = filePath;
        Writable             = writable;
        AssociatedBaseDevkit = TryLoadShpkDevkit("_base", out LoadedBaseDevkitPathName);
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

        if (!ImUtf8.ButtonEx("Update MTRL Version to Dawntrail"u8,
                "Try using this if the material can not be loaded or should use legacy shaders.\n\nThis is not revertible."u8,
                new Vector2(-0.1f, 0), false, 0, Colors.PressEnterWarningBg))
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

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        var ret = DrawBackFaceAndTransparency(disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        ret |= DrawShaderSection(disabled);

        ret |= DrawTextureSection(disabled);
        ret |= DrawColorTableSection(disabled);
        ret |= DrawConstantsSection(disabled);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawOtherMaterialDetails(disabled);

        return !disabled && ret;
    }

    private bool DrawBackFaceAndTransparency(bool disabled)
    {
        ref var shaderFlags = ref ShaderFlags.Wrap(ref Mtrl.ShaderPackage.Flags);

        var ret = false;

        using var dis = ImRaii.Disabled(disabled);

        var tmp = shaderFlags.EnableTransparency;
        if (ImGui.Checkbox("Enable Transparency", ref tmp))
        {
            shaderFlags.EnableTransparency = tmp;
            ret                            = true;
            SetShaderPackageFlags(Mtrl.ShaderPackage.Flags);
        }

        ImGui.SameLine(200 * UiHelpers.Scale + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X);
        tmp = shaderFlags.HideBackfaces;
        if (ImGui.Checkbox("Hide Backfaces", ref tmp))
        {
            shaderFlags.HideBackfaces = tmp;
            ret                       = true;
            SetShaderPackageFlags(Mtrl.ShaderPackage.Flags);
        }

        if (ShpkLoading)
        {
            ImGui.SameLine(400 * UiHelpers.Scale + 2 * ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X);

            ImUtf8.Text("Loading shader (.shpk) file. Some functionality will only be available after this is done."u8,
                ImGuiUtil.HalfBlendText(0x808000u)); // Half cyan
        }

        return ret;
    }

    private void DrawOtherMaterialDetails(bool _)
    {
        if (!ImGui.CollapsingHeader("Further Content"))
            return;

        using (var sets = ImRaii.TreeNode("UV Sets", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (sets)
                foreach (var set in Mtrl.UvSets)
                    ImRaii.TreeNode($"#{set.Index:D2} - {set.Name}", ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var sets = ImRaii.TreeNode("Color Sets", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (sets)
                foreach (var set in Mtrl.ColorSets)
                    ImRaii.TreeNode($"#{set.Index:D2} - {set.Name}", ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        if (Mtrl.AdditionalData.Length <= 0)
            return;

        using var t = ImRaii.TreeNode($"Additional Data (Size: {Mtrl.AdditionalData.Length})###AdditionalData");
        if (t)
            Widget.DrawHexViewer(Mtrl.AdditionalData);
    }

    public void Update()
    {
        UpdateShaders();
        UpdateTextures();
        UpdateConstants();
    }

    public unsafe void Dispose()
    {
        UnbindFromMaterialInstances();
        if (Writable)
            _characterBaseDestructor.Unsubscribe(UnbindFromDrawObjectMaterialInstances);
    }

    public bool Valid
        => ShadersKnown && Mtrl.Valid;

    public byte[] Write()
    {
        var output = Mtrl.Clone();
        output.GarbageCollect(AssociatedShpk, SamplerIds);

        return output.Write();
    }
}
