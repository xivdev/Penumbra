using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.ShaderStructs;
using Penumbra.Interop.Processing;
using Penumbra.String.Classes;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    // strings path/to/the.exe | grep --fixed-strings '.shpk' | sort -u | sed -e 's#^shader/sm5/shpk/##'
    // Apricot shader packages are unlisted because
    // 1. they cause severe performance/memory issues when calculating the effective shader set
    // 2. they probably aren't intended for use with materials anyway
    private static readonly IReadOnlyList<string> StandardShaderPackages =
    [
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
    ];

    private static readonly byte[] UnknownShadersString = "Vertex Shaders: ???\nPixel Shaders: ???"u8.ToArray();

    private string[]? _shpkNames;

    private string    _shaderHeader             = "Shader###Shader";
    private FullPath  _loadedShpkPath           = FullPath.Empty;
    private string    _loadedShpkPathName       = string.Empty;
    private string    _loadedShpkDevkitPathName = string.Empty;
    private string    _shaderComment            = string.Empty;
    private ShpkFile? _associatedShpk;
    private bool      _shpkLoading;
    private JObject?  _associatedShpkDevkit;

    private readonly string   _loadedBaseDevkitPathName;
    private readonly JObject? _associatedBaseDevkit;

    // Shader Key State
    private readonly
        List<(string Label, int Index, string Description, bool MonoFont, IReadOnlyList<(string Label, uint Value, string Description)>
            Values)> _shaderKeys = new(16);

    private readonly HashSet<int>         _vertexShaders = new(16);
    private readonly HashSet<int>         _pixelShaders  = new(16);
    private          bool                 _shadersKnown;
    private          ReadOnlyMemory<byte> _shadersString = UnknownShadersString;

    private string[] GetShpkNames()
    {
        if (null != _shpkNames)
            return _shpkNames;

        var names = new HashSet<string>(StandardShaderPackages);
        names.UnionWith(_edit.FindPathsStartingWith(ShpkPrefix).Select(path => path.ToString()[ShpkPrefixLength..]));

        _shpkNames = names.ToArray();
        Array.Sort(_shpkNames);

        return _shpkNames;
    }

    private FullPath FindAssociatedShpk(out string defaultPath, out Utf8GamePath defaultGamePath)
    {
        defaultPath = GamePaths.Shader(Mtrl.ShaderPackage.Name);
        if (!Utf8GamePath.FromString(defaultPath, out defaultGamePath))
            return FullPath.Empty;

        var path = _edit.FindBestMatch(defaultGamePath);
        if (!path.IsRooted || ShpkPathPreProcessor.SanityCheck(path.FullName) == ShpkPathPreProcessor.SanityCheckResult.Success)
            return path;

        return new FullPath(defaultPath);
    }

    private void LoadShpk(FullPath path)
        => Task.Run(() => DoLoadShpk(path));

    private async Task DoLoadShpk(FullPath path)
    {
        _shadersKnown = false;
        _shaderHeader = $"Shader ({Mtrl.ShaderPackage.Name})###Shader";
        _shpkLoading  = true;

        try
        {
            var data = path.IsRooted
                ? await File.ReadAllBytesAsync(path.FullName)
                : _gameData.GetFile(path.InternalName.ToString())?.Data;
            _loadedShpkPath     = path;
            _associatedShpk     = data?.Length > 0 ? new ShpkFile(data) : throw new Exception("Failure to load file data.");
            _loadedShpkPathName = path.ToPath();
        }
        catch (Exception e)
        {
            _loadedShpkPath     = FullPath.Empty;
            _loadedShpkPathName = string.Empty;
            _associatedShpk     = null;
            Penumbra.Messager.NotificationMessage(e, $"Could not load {_loadedShpkPath.ToPath()}.", NotificationType.Error, false);
        }
        finally
        {
            _shpkLoading = false;
        }

        if (_loadedShpkPath.InternalName.IsEmpty)
        {
            _associatedShpkDevkit     = null;
            _loadedShpkDevkitPathName = string.Empty;
        }
        else
        {
            _associatedShpkDevkit =
                TryLoadShpkDevkit(Path.GetFileNameWithoutExtension(Mtrl.ShaderPackage.Name), out _loadedShpkDevkitPathName);
        }

        UpdateShaderKeys();
        _updateOnNextFrame = true;
    }

    private void UpdateShaderKeys()
    {
        _shaderKeys.Clear();
        if (_associatedShpk != null)
            foreach (var key in _associatedShpk.MaterialKeys)
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
                _shaderKeys.Add((hasDkLabel ? dkData!.Label : keyName.ToString(), mtrlKeyIndex, dkData?.Description ?? string.Empty,
                    !hasDkLabel, values));
            }
        else
            foreach (var (index, key) in Mtrl.ShaderPackage.ShaderKeys.Index())
            {
                var keyName   = Names.KnownNames.TryResolve(key.Key);
                var valueName = keyName.WithKnownSuffixes().TryResolve(Names.KnownNames, key.Value);
                _shaderKeys.Add((keyName.ToString(), index, string.Empty, true, [(valueName.ToString(), key.Value, string.Empty)]));
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

        _vertexShaders.Clear();
        _pixelShaders.Clear();

        var vertexShadersByPass = new Dictionary<uint, HashSet<int>>();
        var pixelShadersByPass  = new Dictionary<uint, HashSet<int>>();

        if (_associatedShpk == null || !_associatedShpk.IsExhaustiveNodeAnalysisFeasible())
        {
            _shadersKnown = false;
        }
        else
        {
            _shadersKnown = true;
            var systemKeySelectors  = AllSelectors(_associatedShpk.SystemKeys).ToArray();
            var sceneKeySelectors   = AllSelectors(_associatedShpk.SceneKeys).ToArray();
            var subViewKeySelectors = AllSelectors(_associatedShpk.SubViewKeys).ToArray();
            var materialKeySelector =
                BuildSelector(_associatedShpk.MaterialKeys.Select(key => Mtrl.GetOrAddShaderKey(key.Id, key.DefaultValue).Value));

            foreach (var systemKeySelector in systemKeySelectors)
            {
                foreach (var sceneKeySelector in sceneKeySelectors)
                {
                    foreach (var subViewKeySelector in subViewKeySelectors)
                    {
                        var selector = BuildSelector(systemKeySelector, sceneKeySelector, materialKeySelector, subViewKeySelector);
                        var node     = _associatedShpk.GetNodeBySelector(selector);
                        if (node.HasValue)
                            foreach (var pass in node.Value.Passes)
                            {
                                AddShader(_vertexShaders, vertexShadersByPass, pass.Id, (int)pass.VertexShader);
                                AddShader(_pixelShaders,  pixelShadersByPass,  pass.Id, (int)pass.PixelShader);
                            }
                        else
                            _shadersKnown = false;
                    }
                }
            }
        }

        if (_shadersKnown)
        {
            var builder = new StringBuilder();
            foreach (var (passId, passVertexShader) in vertexShadersByPass)
            {
                if (builder.Length > 0)
                    builder.Append("\n\n");

                var passName = Names.KnownNames.TryResolve(passId);
                var shaders  = passVertexShader.OrderBy(i => i).Select(i => $"#{i}");
                builder.Append($"Vertex Shaders ({passName}): {string.Join(", ", shaders)}");
                if (pixelShadersByPass.TryGetValue(passId, out var passPixelShader))
                {
                    shaders = passPixelShader.OrderBy(i => i).Select(i => $"#{i}");
                    builder.Append($"\nPixel Shaders ({passName}): {string.Join(", ", shaders)}");
                }
            }

            foreach (var (passId, passPixelShader) in pixelShadersByPass)
            {
                if (vertexShadersByPass.ContainsKey(passId))
                    continue;

                if (builder.Length > 0)
                    builder.Append("\n\n");

                var passName = Names.KnownNames.TryResolve(passId);
                var shaders  = passPixelShader.OrderBy(i => i).Select(i => $"#{i}");
                builder.Append($"Pixel Shaders ({passName}): {string.Join(", ", shaders)}");
            }

            _shadersString = Encoding.UTF8.GetBytes(builder.ToString());
        }
        else
        {
            _shadersString = UnknownShadersString;
        }

        _shaderComment = TryGetShpkDevkitData<string>("Comment", null, true) ?? string.Empty;
    }

    private bool DrawShaderSection(bool disabled)
    {
        var ret = false;
        if (Im.Tree.Header(_shaderHeader))
        {
            ret |= DrawPackageNameInput(disabled);
            ret |= DrawShaderFlagsInput(disabled);
            ret |= DrawAdditionalFlagsInput(disabled);
            DrawCustomAssociations();
            ret |= DrawMaterialShaderKeys(disabled);
            DrawMaterialShaders();
        }

        if (!_shpkLoading && (_associatedShpk is null || _associatedShpkDevkit is null))
        {
            Im.Dummy(new Vector2(Im.Style.TextHeight / 2));

            if (_associatedShpk is null)
                Im.Text("Unable to find a suitable shader (.shpk) file for cross-references. Some functionality will be missing."u8,
                    ImGuiColor.Text.Get().HalfBlend(0x80)); // Half red
            else
                Im.Text(
                    "No dev-kit file found for this material's shaders. Please install one for optimal editing experience, such as actual constant names instead of hexadecimal identifiers."u8,
                    ImGuiColor.Text.Get().HalfBlend(0x8080u)); // Half yellow
        }

        return ret;
    }

    private bool DrawPackageNameInput(bool disabled)
    {
        if (disabled)
        {
            Im.Text($"Shader Package: {Mtrl.ShaderPackage.Name}");
            return false;
        }

        var ret = false;
        Im.Item.SetNextWidthScaled(250.0f);
        using var c = Im.Combo.Begin("Shader Package"u8, Mtrl.ShaderPackage.Name);
        if (c)
            foreach (var value in GetShpkNames())
            {
                if (!Im.Selectable(value, value == Mtrl.ShaderPackage.Name))
                    continue;

                Mtrl.ShaderPackage.Name = value;
                ret                     = true;
                _associatedShpk         = null;
                _loadedShpkPath         = FullPath.Empty;
                UnpinResources(true);
                LoadShpk(FindAssociatedShpk(out _, out _));
            }

        return ret;
    }

    private bool DrawShaderFlagsInput(bool disabled)
    {
        var shpkFlags = (int)Mtrl.ShaderPackage.Flags;
        Im.Item.SetNextWidthScaled(250.0f);
        if (!Im.Input.Scalar("Shader Flags"u8, ref shpkFlags, "%08X"u8,
                flags: InputTextFlags.CharsHexadecimal | (disabled ? InputTextFlags.ReadOnly : InputTextFlags.None)))
            return false;

        Mtrl.ShaderPackage.Flags = (uint)shpkFlags;
        SetShaderPackageFlags((uint)shpkFlags);
        return true;
    }

    private bool DrawAdditionalFlagsInput(bool disabled)
    {
        const uint lockedFlags = 0x00000FFC;

        if (Mtrl.AdditionalData.Length < 4)
            return false;

        var originalFlags   = Mtrl.TableFlags.Flags;
        var additionalFlags = originalFlags;
        Im.Item.SetNextWidthScaled(250.0f);
        if (!Im.Input.Scalar("Additional Flags"u8, ref additionalFlags, "%08X"u8,
                flags: InputTextFlags.CharsHexadecimal | (disabled ? InputTextFlags.ReadOnly : InputTextFlags.None)))
            return false;

        Mtrl.TableFlags = new((additionalFlags & ~lockedFlags) | (originalFlags & lockedFlags));
        return true;
    }

    /// <summary>
    /// Show the currently associated shpk file, if any, and the buttons to associate
    /// a specific shpk from your drive, the modded shpk by path or the default shpk.
    /// </summary>
    private void DrawCustomAssociations()
    {
        var tooltip = "Click to copy file path to clipboard."u8;

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        ImEx.CopyOnClickSelectable(_associatedShpk is null ? "Associated .shpk file: None" : $"Associated .shpk file: {_loadedShpkPathName}",
            _loadedShpkPathName, tooltip);
        ImEx.CopyOnClickSelectable(
            _associatedShpkDevkit is null ? "Associated dev-kit file: None" : $"Associated dev-kit file: {_loadedShpkDevkitPathName}",
            _loadedShpkDevkitPathName, tooltip);
        ImEx.CopyOnClickSelectable(
            _associatedBaseDevkit is null ? "Base dev-kit file: None" : $"Base dev-kit file: {_loadedBaseDevkitPathName}",
            _loadedBaseDevkitPathName, tooltip);

        if (Im.Button("Associate Custom .shpk File"u8))
            _fileDialog.OpenFilePicker("Associate Custom .shpk File...", ".shpk", (success, name) =>
            {
                if (success)
                    LoadShpk(new FullPath(name[0]));
            }, 1, _edit.Mod!.ModPath.FullName, false);

        var moddedPath = FindAssociatedShpk(out var defaultPath, out var gamePath);
        Im.Line.Same();
        if (ImEx.Button("Associate Default .shpk File"u8, Vector2.Zero, moddedPath.ToPath(), moddedPath.Equals(_loadedShpkPath)))
            LoadShpk(moddedPath);

        if (!gamePath.Path.Equals(moddedPath.InternalName))
        {
            Im.Line.Same();
            if (ImEx.Button("Associate Unmodded .shpk File"u8, Vector2.Zero, defaultPath, gamePath.Path.Equals(_loadedShpkPath.InternalName)))
                LoadShpk(new FullPath(gamePath));
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
    }

    private bool DrawMaterialShaderKeys(bool disabled)
    {
        if (_shaderKeys.Count == 0)
            return false;

        var ret = false;
        foreach (var (label, index, description, monoFont, values) in _shaderKeys)
        {
            using var font         = Im.Font.Mono.Push(monoFont);
            ref var   key          = ref Mtrl.ShaderPackage.ShaderKeys[index];
            using var id           = Im.Id.Push((int)key.Key);
            var       shpkKey      = _associatedShpk?.GetMaterialKeyById(key.Key);
            var       currentValue = key.Value;
            var (currentLabel, _, currentDescription) =
                values.FirstOrNull(v => v.Value == currentValue) ?? ($"0x{currentValue:X8}", currentValue, string.Empty);
            if (!disabled && shpkKey.HasValue)
            {
                Im.Item.SetNextWidthScaled(250.0f);
                using (var c = Im.Combo.Begin(StringU8.Empty, currentLabel))
                {
                    if (c)
                        foreach (var (valueLabel, value, valueDescription) in values)
                        {
                            if (Im.Selectable(valueLabel, value == currentValue))
                            {
                                key.Value = value;
                                ret       = true;
                                UnpinResources(false);
                                Update();
                            }

                            if (valueDescription.Length > 0)
                                LunaStyle.DrawRightAlignedHelpMarker(valueDescription);
                        }
                }

                Im.Line.SameInner();
                if (description.Length > 0)
                    LunaStyle.DrawHelpMarkerLabel(label, description);
                else
                    Im.Text(label);
            }
            else if (description.Length > 0 || currentDescription.Length > 0)
            {
                LunaStyle.DrawHelpMarkerLabel($"{label}: {currentLabel}",
                    description + (description.Length > 0 && currentDescription.Length > 0 ? "\n\n" : string.Empty) + currentDescription);
            }
            else
            {
                Im.Text($"{label}: {currentLabel}");
            }
        }

        return ret;
    }

    private void DrawMaterialShaders()
    {
        if (_associatedShpk is null)
            return;

        using (var node = Im.Tree.Node("Candidate Shaders"u8))
        {
            if (node)
                Im.Text(_shadersString.Span);
        }

        if (_shaderComment.Length > 0)
        {
            Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
            Im.Text(_shaderComment);
        }
    }
}
