using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.ShaderStructs;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    // strings path/to/the.exe | grep --fixed-strings '.shpk' | sort -u | sed -e 's#^shader/sm5/shpk/##'
    // Apricot shader packages are unlisted because
    // 1. they cause severe performance/memory issues when calculating the effective shader set
    // 2. they probably aren't intended for use with materials anyway
    internal static readonly IReadOnlyList<string> StandardShaderPackages = new[]
    {
        "3dui.shpk",
        // "apricot_decal_dummy.shpk",
        // "apricot_decal_ring.shpk",
        // "apricot_decal.shpk",
        // "apricot_fogModel.shpk",
        // "apricot_gbuffer_decal_dummy.shpk",
        // "apricot_gbuffer_decal_ring.shpk",
        // "apricot_gbuffer_decal.shpk",
        // "apricot_lightmodel.shpk",
        // "apricot_model_dummy.shpk",
        // "apricot_model_morph.shpk",
        // "apricot_model.shpk",
        // "apricot_powder_dummy.shpk",
        // "apricot_powder.shpk",
        // "apricot_shape_dummy.shpk",
        // "apricot_shape.shpk",
        "bgcolorchange.shpk",
        "bg_composite.shpk",
        "bgcrestchange.shpk",
        "bgdecal.shpk",
        "bgprop.shpk",
        "bg.shpk",
        "bguvscroll.shpk",
        "characterglass.shpk",
        "characterinc.shpk",
        "characterlegacy.shpk",
        "characterocclusion.shpk",
        "characterreflection.shpk",
        "characterscroll.shpk",
        "charactershadowoffset.shpk",
        "character.shpk",
        "characterstockings.shpk",
        "charactertattoo.shpk",
        "charactertransparency.shpk",
        "cloud.shpk",
        "createviewposition.shpk",
        "crystal.shpk",
        "directionallighting.shpk",
        "directionalshadow.shpk",
        "furblur.shpk",
        "grassdynamicwave.shpk",
        "grass.shpk",
        "hairmask.shpk",
        "hair.shpk",
        "iris.shpk",
        "lightshaft.shpk",
        "linelighting.shpk",
        "planelighting.shpk",
        "pointlighting.shpk",
        "river.shpk",
        "shadowmask.shpk",
        "skin.shpk",
        "spotlighting.shpk",
        "subsurfaceblur.shpk",
        "verticalfog.shpk",
        "water.shpk",
        "weather.shpk",
    };

    private static readonly byte[] UnknownShadersString = Encoding.UTF8.GetBytes("Vertex Shaders: ???\nPixel Shaders: ???");

    private string[]? _shpkNames;

    public string          ShaderHeader             = "Shader###Shader";
    public FullPath        LoadedShpkPath           = FullPath.Empty;
    public string          LoadedShpkPathName       = string.Empty;
    public string          LoadedShpkDevkitPathName = string.Empty;
    public string          ShaderComment            = string.Empty;
    public ShpkFile?       AssociatedShpk;
    public bool            ShpkLoading;
    public JObject?        AssociatedShpkDevkit;

    public readonly string   LoadedBaseDevkitPathName;
    public readonly JObject? AssociatedBaseDevkit;

    // Shader Key State
    public readonly
        List<(string Label, int Index, string Description, bool MonoFont, IReadOnlyList<(string Label, uint Value, string Description)>
            Values)> ShaderKeys = new(16);

    public readonly HashSet<int>         VertexShaders = new(16);
    public readonly HashSet<int>         PixelShaders  = new(16);
    public          bool                 ShadersKnown;
    public          ReadOnlyMemory<byte> ShadersString = UnknownShadersString;

    public string[] GetShpkNames()
    {
        if (null != _shpkNames)
            return _shpkNames;

        var names = new HashSet<string>(StandardShaderPackages);
        names.UnionWith(_edit.FindPathsStartingWith(ShpkPrefix).Select(path => path.ToString()[ShpkPrefixLength..]));

        _shpkNames = names.ToArray();
        Array.Sort(_shpkNames);

        return _shpkNames;
    }

    public FullPath FindAssociatedShpk(out string defaultPath, out Utf8GamePath defaultGamePath)
    {
        defaultPath = GamePaths.Shader.ShpkPath(Mtrl.ShaderPackage.Name);
        if (!Utf8GamePath.FromString(defaultPath, out defaultGamePath))
            return FullPath.Empty;

        return _edit.FindBestMatch(defaultGamePath);
    }

    public void LoadShpk(FullPath path)
        => Task.Run(() => DoLoadShpk(path));

    private async Task DoLoadShpk(FullPath path)
    {
        ShadersKnown = false;
        ShaderHeader = $"Shader ({Mtrl.ShaderPackage.Name})###Shader";
        ShpkLoading  = true;

        try
        {
            var data = path.IsRooted
                ? await File.ReadAllBytesAsync(path.FullName)
                : _gameData.GetFile(path.InternalName.ToString())?.Data;
            LoadedShpkPath     = path;
            AssociatedShpk     = data?.Length > 0 ? new ShpkFile(data) : throw new Exception("Failure to load file data.");
            LoadedShpkPathName = path.ToPath();
        }
        catch (Exception e)
        {
            LoadedShpkPath     = FullPath.Empty;
            LoadedShpkPathName = string.Empty;
            AssociatedShpk     = null;
            Penumbra.Messager.NotificationMessage(e, $"Could not load {LoadedShpkPath.ToPath()}.", NotificationType.Error, false);
        }
        finally
        {
            ShpkLoading  = false;
        }

        if (LoadedShpkPath.InternalName.IsEmpty)
        {
            AssociatedShpkDevkit     = null;
            LoadedShpkDevkitPathName = string.Empty;
        }
        else
        {
            AssociatedShpkDevkit =
                TryLoadShpkDevkit(Path.GetFileNameWithoutExtension(Mtrl.ShaderPackage.Name), out LoadedShpkDevkitPathName);
        }

        UpdateShaderKeys();
        _updateOnNextFrame = true;
    }

    private void UpdateShaderKeys()
    {
        ShaderKeys.Clear();
        if (AssociatedShpk != null)
            foreach (var key in AssociatedShpk.MaterialKeys)
            {
                var keyName    = Names.KnownNames.TryResolve(key.Id);
                var dkData     = TryGetShpkDevkitData<DevkitShaderKey>("ShaderKeys", key.Id, false);
                var hasDkLabel = !string.IsNullOrEmpty(dkData?.Label);

                var valueSet = new HashSet<uint>(key.Values);
                if (dkData != null)
                    valueSet.UnionWith(dkData.Values.Keys);

                var valueKnownNames = keyName.WithKnownSuffixes();

                var mtrlKeyIndex = Mtrl.FindOrAddShaderKey(key.Id, key.DefaultValue);
                var values = valueSet.Select<uint, (string Label, uint Value, string Description)>(value =>
                {
                    var valueName = valueKnownNames.TryResolve(Names.KnownNames, value);
                    if (dkData != null && dkData.Values.TryGetValue(value, out var dkValue))
                        return (dkValue.Label.Length > 0 ? dkValue.Label : valueName.ToString(), value, dkValue.Description);

                    return (valueName.ToString(), value, string.Empty);
                }).ToArray();
                Array.Sort(values, (x, y) =>
                {
                    if (x.Value == key.DefaultValue)
                        return -1;
                    if (y.Value == key.DefaultValue)
                        return 1;

                    return string.Compare(x.Label, y.Label, StringComparison.Ordinal);
                });
                ShaderKeys.Add((hasDkLabel ? dkData!.Label : keyName.ToString(), mtrlKeyIndex, dkData?.Description ?? string.Empty,
                    !hasDkLabel, values));
            }
        else
            foreach (var (key, index) in Mtrl.ShaderPackage.ShaderKeys.WithIndex())
            {
                var keyName   = Names.KnownNames.TryResolve(key.Category);
                var valueName = keyName.WithKnownSuffixes().TryResolve(Names.KnownNames, key.Value);
                ShaderKeys.Add((keyName.ToString(), index, string.Empty, true, [(valueName.ToString(), key.Value, string.Empty)]));
            }
    }

    private void UpdateShaders()
    {
        static void AddShader(HashSet<int> globalSet, Dictionary<uint, HashSet<int>> byPassSets, uint passId, int shaderIndex)
        {
            globalSet.Add(shaderIndex);
            if (!byPassSets.TryGetValue(passId, out var passSet))
            {
                passSet = [];
                byPassSets.Add(passId, passSet);
            }
            passSet.Add(shaderIndex);
        }

        VertexShaders.Clear();
        PixelShaders.Clear();

        var vertexShadersByPass = new Dictionary<uint, HashSet<int>>();
        var pixelShadersByPass  = new Dictionary<uint, HashSet<int>>();

        if (AssociatedShpk == null || !AssociatedShpk.IsExhaustiveNodeAnalysisFeasible())
        {
            ShadersKnown = false;
        }
        else
        {
            ShadersKnown = true;
            var systemKeySelectors  = AllSelectors(AssociatedShpk.SystemKeys).ToArray();
            var sceneKeySelectors   = AllSelectors(AssociatedShpk.SceneKeys).ToArray();
            var subViewKeySelectors = AllSelectors(AssociatedShpk.SubViewKeys).ToArray();
            var materialKeySelector =
                BuildSelector(AssociatedShpk.MaterialKeys.Select(key => Mtrl.GetOrAddShaderKey(key.Id, key.DefaultValue).Value));

            foreach (var systemKeySelector in systemKeySelectors)
            {
                foreach (var sceneKeySelector in sceneKeySelectors)
                {
                    foreach (var subViewKeySelector in subViewKeySelectors)
                    {
                        var selector = BuildSelector(systemKeySelector, sceneKeySelector, materialKeySelector, subViewKeySelector);
                        var node     = AssociatedShpk.GetNodeBySelector(selector);
                        if (node.HasValue)
                            foreach (var pass in node.Value.Passes)
                            {
                                AddShader(VertexShaders, vertexShadersByPass, pass.Id, (int)pass.VertexShader);
                                AddShader(PixelShaders,  pixelShadersByPass,  pass.Id, (int)pass.PixelShader);
                            }
                        else
                            ShadersKnown = false;
                    }
                }
            }
        }

        if (ShadersKnown)
        {
            var builder = new StringBuilder();
            foreach (var (passId, passVS) in vertexShadersByPass)
            {
                if (builder.Length > 0)
                    builder.Append("\n\n");

                var passName = Names.KnownNames.TryResolve(passId);
                var shaders  = passVS.OrderBy(i => i).Select(i => $"#{i}");
                builder.Append($"Vertex Shaders ({passName}): {string.Join(", ", shaders)}");
                if (pixelShadersByPass.TryGetValue(passId, out var passPS))
                {
                    shaders = passPS.OrderBy(i => i).Select(i => $"#{i}");
                    builder.Append($"\nPixel Shaders ({passName}): {string.Join(", ", shaders)}");
                }
            }
            foreach (var (passId, passPS) in pixelShadersByPass)
            {
                if (vertexShadersByPass.ContainsKey(passId))
                    continue;

                if (builder.Length > 0)
                    builder.Append("\n\n");

                var passName = Names.KnownNames.TryResolve(passId);
                var shaders  = passPS.OrderBy(i => i).Select(i => $"#{i}");
                builder.Append($"Pixel Shaders ({passName}): {string.Join(", ", shaders)}");
            }

            ShadersString = Encoding.UTF8.GetBytes(builder.ToString());
        }
        else
            ShadersString = UnknownShadersString;

        ShaderComment = TryGetShpkDevkitData<string>("Comment", null, true) ?? string.Empty;
    }

    private bool DrawShaderSection(bool disabled)
    {
        var ret = false;
        if (ImGui.CollapsingHeader(ShaderHeader))
        {
            ret |= DrawPackageNameInput(disabled);
            ret |= DrawShaderFlagsInput(disabled);
            DrawCustomAssociations();
            ret |= DrawMaterialShaderKeys(disabled);
            DrawMaterialShaders();
        }

        if (!ShpkLoading && (AssociatedShpk == null || AssociatedShpkDevkit == null))
        {
            ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

            if (AssociatedShpk == null)
            {
                ImUtf8.Text("Unable to find a suitable shader (.shpk) file for cross-references. Some functionality will be missing."u8,
                    ImGuiUtil.HalfBlendText(0x80u)); // Half red
            }
            else
            {
                ImUtf8.Text("No dev-kit file found for this material's shaders. Please install one for optimal editing experience, such as actual constant names instead of hexadecimal identifiers."u8,
                    ImGuiUtil.HalfBlendText(0x8080u)); // Half yellow
            }
        }

        return ret;
    }

    private bool DrawPackageNameInput(bool disabled)
    {
        if (disabled)
        {
            ImGui.TextUnformatted("Shader Package: " + Mtrl.ShaderPackage.Name);
            return false;
        }

        var ret = false;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
        using var c = ImRaii.Combo("Shader Package", Mtrl.ShaderPackage.Name);
        if (c)
            foreach (var value in GetShpkNames())
            {
                if (ImGui.Selectable(value, value == Mtrl.ShaderPackage.Name))
                {
                    Mtrl.ShaderPackage.Name = value;
                    ret                     = true;
                    AssociatedShpk          = null;
                    LoadedShpkPath          = FullPath.Empty;
                    LoadShpk(FindAssociatedShpk(out _, out _));
                }
            }

        return ret;
    }

    private bool DrawShaderFlagsInput(bool disabled)
    {
        var shpkFlags = (int)Mtrl.ShaderPackage.Flags;
        ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
        if (!ImGui.InputInt("Shader Flags", ref shpkFlags, 0, 0,
                ImGuiInputTextFlags.CharsHexadecimal | (disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None)))
            return false;

        Mtrl.ShaderPackage.Flags = (uint)shpkFlags;
        SetShaderPackageFlags((uint)shpkFlags);
        return true;
    }

    /// <summary>
    /// Show the currently associated shpk file, if any, and the buttons to associate
    /// a specific shpk from your drive, the modded shpk by path or the default shpk.
    /// </summary>
    private void DrawCustomAssociations()
    {
        const string tooltip = "Click to copy file path to clipboard.";
        var text = AssociatedShpk == null
            ? "Associated .shpk file: None"
            : $"Associated .shpk file: {LoadedShpkPathName}";
        var devkitText = AssociatedShpkDevkit == null
            ? "Associated dev-kit file: None"
            : $"Associated dev-kit file: {LoadedShpkDevkitPathName}";
        var baseDevkitText = AssociatedBaseDevkit == null
            ? "Base dev-kit file: None"
            : $"Base dev-kit file: {LoadedBaseDevkitPathName}";

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        ImGuiUtil.CopyOnClickSelectable(text,           LoadedShpkPathName,       tooltip);
        ImGuiUtil.CopyOnClickSelectable(devkitText,     LoadedShpkDevkitPathName, tooltip);
        ImGuiUtil.CopyOnClickSelectable(baseDevkitText, LoadedBaseDevkitPathName, tooltip);

        if (ImGui.Button("Associate Custom .shpk File"))
            _fileDialog.OpenFilePicker("Associate Custom .shpk File...", ".shpk", (success, name) =>
            {
                if (success)
                    LoadShpk(new FullPath(name[0]));
            }, 1, _edit.Mod!.ModPath.FullName, false);

        var moddedPath = FindAssociatedShpk(out var defaultPath, out var gamePath);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("Associate Default .shpk File", Vector2.Zero, moddedPath.ToPath(),
                moddedPath.Equals(LoadedShpkPath)))
            LoadShpk(moddedPath);

        if (!gamePath.Path.Equals(moddedPath.InternalName))
        {
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("Associate Unmodded .shpk File", Vector2.Zero, defaultPath,
                    gamePath.Path.Equals(LoadedShpkPath.InternalName)))
                LoadShpk(new FullPath(gamePath));
        }

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private bool DrawMaterialShaderKeys(bool disabled)
    {
        if (ShaderKeys.Count == 0)
            return false;

        var ret = false;
        foreach (var (label, index, description, monoFont, values) in ShaderKeys)
        {
            using var font         = ImRaii.PushFont(UiBuilder.MonoFont, monoFont);
            ref var   key          = ref Mtrl.ShaderPackage.ShaderKeys[index];
            var       shpkKey      = AssociatedShpk?.GetMaterialKeyById(key.Category);
            var       currentValue = key.Value;
            var (currentLabel, _, currentDescription) =
                values.FirstOrNull(v => v.Value == currentValue) ?? ($"0x{currentValue:X8}", currentValue, string.Empty);
            if (!disabled && shpkKey.HasValue)
            {
                ImGui.SetNextItemWidth(UiHelpers.Scale * 250.0f);
                using (var c = ImRaii.Combo($"##{key.Category:X8}", currentLabel))
                {
                    if (c)
                        foreach (var (valueLabel, value, valueDescription) in values)
                        {
                            if (ImGui.Selectable(valueLabel, value == currentValue))
                            {
                                key.Value = value;
                                ret       = true;
                                Update();
                            }

                            if (valueDescription.Length > 0)
                                ImGuiUtil.SelectableHelpMarker(valueDescription);
                        }
                }

                ImGui.SameLine();
                if (description.Length > 0)
                    ImGuiUtil.LabeledHelpMarker(label, description);
                else
                    ImGui.TextUnformatted(label);
            }
            else if (description.Length > 0 || currentDescription.Length > 0)
            {
                ImGuiUtil.LabeledHelpMarker($"{label}: {currentLabel}",
                    description + (description.Length > 0 && currentDescription.Length > 0 ? "\n\n" : string.Empty) + currentDescription);
            }
            else
            {
                ImGui.TextUnformatted($"{label}: {currentLabel}");
            }
        }

        return ret;
    }

    private void DrawMaterialShaders()
    {
        if (AssociatedShpk == null)
            return;

        using (var node = ImUtf8.TreeNode("Candidate Shaders"u8))
        {
            if (node)
                ImUtf8.Text(ShadersString.Span);
        }

        if (ShaderComment.Length > 0)
        {
            ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
            ImGui.TextUnformatted(ShaderComment);
        }
    }
}
