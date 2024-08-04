using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Text.Widget.Editors;
using Penumbra.GameData.Files.ShaderStructs;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.AdvancedWindow.Materials;

public partial class MtrlTab
{
    private const float MaterialConstantSize = 250.0f;

    public readonly
        List<(string Header, List<(string Label, int ConstantIndex, Range Slice, string Description, bool MonoFont, IEditor<byte> Editor)>
            Constants)> Constants = new(16);

    private void UpdateConstants()
    {
        static List<T> FindOrAddGroup<T>(List<(string, List<T>)> groups, string name)
        {
            foreach (var (groupName, group) in groups)
            {
                if (string.Equals(name, groupName, StringComparison.Ordinal))
                    return group;
            }

            var newGroup = new List<T>(16);
            groups.Add((name, newGroup));
            return newGroup;
        }

        Constants.Clear();
        string mpPrefix;
        if (_associatedShpk == null)
        {
            mpPrefix = MaterialParamsConstantName.Value!;
            var fcGroup = FindOrAddGroup(Constants, "Further Constants");
            foreach (var (constant, index) in Mtrl.ShaderPackage.Constants.WithIndex())
            {
                var values = Mtrl.GetConstantValue<float>(constant);
                for (var i = 0; i < values.Length; i += 4)
                {
                    fcGroup.Add(($"0x{constant.Id:X8}", index, i..Math.Min(i + 4, values.Length), string.Empty, true,
                        ConstantEditors.DefaultFloat));
                }
            }
        }
        else
        {
            mpPrefix = _associatedShpk.GetConstantById(MaterialParamsConstantId)?.Name ?? MaterialParamsConstantName.Value!;
            var autoNameMaxLength = Math.Max(Names.LongestKnownNameLength, mpPrefix.Length + 8);
            foreach (var shpkConstant in _associatedShpk.MaterialParams)
            {
                var name            = Names.KnownNames.TryResolve(shpkConstant.Id);
                var constant        = Mtrl.GetOrAddConstant(shpkConstant.Id, _associatedShpk, out var constantIndex);
                var values          = Mtrl.GetConstantValue<byte>(constant);
                var handledElements = new IndexSet(values.Length, false);

                var dkData = TryGetShpkDevkitData<DevkitConstant[]>("Constants", shpkConstant.Id, true);
                if (dkData != null)
                    foreach (var dkConstant in dkData)
                    {
                        var offset       = (int)dkConstant.EffectiveByteOffset;
                        var length       = values.Length - offset;
                        var constantSize = dkConstant.EffectiveByteSize;
                        if (constantSize.HasValue)
                            length = Math.Min(length, (int)constantSize.Value);
                        if (length <= 0)
                            continue;

                        var editor = dkConstant.CreateEditor(_materialTemplatePickers);
                        if (editor != null)
                            FindOrAddGroup(Constants, dkConstant.Group.Length > 0 ? dkConstant.Group : "Further Constants")
                                .Add((dkConstant.Label, constantIndex, offset..(offset + length), dkConstant.Description, false, editor));
                        handledElements.AddRange(offset, length);
                    }

                if (handledElements.IsFull)
                    continue;

                var fcGroup = FindOrAddGroup(Constants, "Further Constants");
                foreach (var (start, end) in handledElements.Ranges(complement: true))
                {
                    if (start == 0 && end == values.Length && end - start <= 16)
                        if (name.Value != null)
                        {
                            fcGroup.Add((
                                $"{name.Value.PadRight(autoNameMaxLength)} (0x{shpkConstant.Id:X8})",
                                constantIndex, 0..values.Length, string.Empty, true, DefaultConstantEditorFor(name)));
                            continue;
                        }

                    if ((shpkConstant.ByteOffset & 0x3) == 0 && (shpkConstant.ByteSize & 0x3) == 0)
                    {
                        var offset = shpkConstant.ByteOffset;
                        for (int i = (start & ~0xF) - (offset & 0xF), j = offset >> 4; i < end; i += 16, ++j)
                        {
                            var rangeStart = Math.Max(i, start);
                            var rangeEnd   = Math.Min(i + 16, end);
                            if (rangeEnd > rangeStart)
                            {
                                var autoName =
                                    $"{mpPrefix}[{j,2:D}]{VectorSwizzle(((offset + rangeStart) & 0xF) >> 2, ((offset + rangeEnd - 1) & 0xF) >> 2)}";
                                fcGroup.Add((
                                    $"{autoName.PadRight(autoNameMaxLength)} (0x{shpkConstant.Id:X8})",
                                    constantIndex, rangeStart..rangeEnd, string.Empty, true, DefaultConstantEditorFor(name)));
                            }
                        }
                    }
                    else
                    {
                        for (var i = start; i < end; i += 16)
                        {
                            fcGroup.Add(($"{"???".PadRight(autoNameMaxLength)} (0x{shpkConstant.Id:X8})", constantIndex,
                                i..Math.Min(i + 16, end), string.Empty, true,
                                DefaultConstantEditorFor(name)));
                        }
                    }
                }
            }
        }

        Constants.RemoveAll(group => group.Constants.Count == 0);
        Constants.Sort((x, y) =>
        {
            if (string.Equals(x.Header, "Further Constants", StringComparison.Ordinal))
                return 1;
            if (string.Equals(y.Header, "Further Constants", StringComparison.Ordinal))
                return -1;

            return string.Compare(x.Header, y.Header, StringComparison.Ordinal);
        });
        // HACK the Replace makes w appear after xyz, for the cbuffer-location-based naming scheme, and cbuffer-location names appear after known variable names
        foreach (var (_, group) in Constants)
        {
            group.Sort((x, y) => string.CompareOrdinal(
                x.MonoFont ? x.Label.Replace("].w", "].{").Replace(mpPrefix, "}_MaterialParameter") : x.Label,
                y.MonoFont ? y.Label.Replace("].w", "].{").Replace(mpPrefix, "}_MaterialParameter") : y.Label));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IEditor<byte> DefaultConstantEditorFor(Name name)
        => ConstantEditors.DefaultFor(name, _materialTemplatePickers);

    private bool DrawConstantsSection(bool disabled)
    {
        if (Constants.Count == 0)
            return false;

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        if (!ImGui.CollapsingHeader("Material Constants"))
            return false;

        using var _ = ImRaii.PushId("MaterialConstants");

        var ret = false;
        foreach (var (header, group) in Constants)
        {
            using var t = ImRaii.TreeNode(header, ImGuiTreeNodeFlags.DefaultOpen);
            if (!t)
                continue;

            foreach (var (label, constantIndex, slice, description, monoFont, editor) in group)
            {
                var constant = Mtrl.ShaderPackage.Constants[constantIndex];
                var buffer   = Mtrl.GetConstantValue<byte>(constant);
                if (buffer.Length > 0)
                {
                    using var id = ImRaii.PushId($"##{constant.Id:X8}:{slice.Start}");
                    ImGui.SetNextItemWidth(MaterialConstantSize * UiHelpers.Scale);
                    if (editor.Draw(buffer[slice], disabled))
                    {
                        ret = true;
                        SetMaterialParameter(constant.Id, slice.Start, buffer[slice]);
                    }

                    var shpkConstant         = _associatedShpk?.GetMaterialParamById(constant.Id);
                    var defaultConstantValue = shpkConstant.HasValue ? _associatedShpk!.GetMaterialParamDefault<byte>(shpkConstant.Value) : [];
                    var defaultValue         = IsValid(slice, defaultConstantValue.Length) ? defaultConstantValue[slice] : [];
                    var canReset = _associatedShpk?.MaterialParamsDefaults != null
                        ? defaultValue.Length > 0 && !defaultValue.SequenceEqual(buffer[slice])
                        : buffer[slice].ContainsAnyExcept((byte)0);
                    ImUtf8.SameLineInner();
                    if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Backspace.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
                            "Reset this constant to its default value.\n\nHold Ctrl to unlock.", !ImGui.GetIO().KeyCtrl || !canReset, true))
                    {
                        ret = true;
                        if (defaultValue.Length > 0)
                            defaultValue.CopyTo(buffer[slice]);
                        else
                            buffer[slice].Clear();

                        SetMaterialParameter(constant.Id, slice.Start, buffer[slice]);
                    }

                    ImGui.SameLine();
                    using var font = ImRaii.PushFont(UiBuilder.MonoFont, monoFont);
                    if (description.Length > 0)
                        ImGuiUtil.LabeledHelpMarker(label, description);
                    else
                        ImGui.TextUnformatted(label);
                }
            }
        }

        return ret;
    }

    private static bool IsValid(Range range, int length)
    {
        var start = range.Start.GetOffset(length);
        var end   = range.End.GetOffset(length);
        return start >= 0 && start <= length && end >= start && end <= length;
    }

    internal static string? MaterialParamName(bool componentOnly, int offset)
    {
        if (offset < 0)
            return null;

        return (componentOnly, offset & 0x3) switch
        {
            (true, 0)  => "x",
            (true, 1)  => "y",
            (true, 2)  => "z",
            (true, 3)  => "w",
            (false, 0) => $"[{offset >> 2:D2}].x",
            (false, 1) => $"[{offset >> 2:D2}].y",
            (false, 2) => $"[{offset >> 2:D2}].z",
            (false, 3) => $"[{offset >> 2:D2}].w",
            _          => null,
        };
    }

    /// <remarks> Returned string is 4 chars long. </remarks>
    private static string VectorSwizzle(int firstComponent, int lastComponent)
        => (firstComponent, lastComponent) switch
        {
            (0, 4) => "    ",
            (0, 0) => ".x  ",
            (0, 1) => ".xy ",
            (0, 2) => ".xyz",
            (0, 3) => "    ",
            (1, 1) => ".y  ",
            (1, 2) => ".yz ",
            (1, 3) => ".yzw",
            (2, 2) => ".z  ",
            (2, 3) => ".zw ",
            (3, 3) => ".w  ",
            _      => string.Empty,
        };

    internal static (string? Name, bool ComponentOnly) MaterialParamRangeName(string prefix, int valueOffset, int valueLength)
    {
        if (valueLength == 0 || valueOffset < 0)
            return (null, false);

        var firstVector    = valueOffset >> 2;
        var lastVector     = (valueOffset + valueLength - 1) >> 2;
        var firstComponent = valueOffset & 0x3;
        var lastComponent  = (valueOffset + valueLength - 1) & 0x3;
        if (firstVector == lastVector)
            return ($"{prefix}[{firstVector}]{VectorSwizzle(firstComponent, lastComponent)}", true);

        var sb = new StringBuilder(128);
        sb.Append($"{prefix}[{firstVector}]{VectorSwizzle(firstComponent, 3).TrimEnd()}");
        for (var i = firstVector + 1; i < lastVector; ++i)
            sb.Append($", [{i}]");

        sb.Append($", [{lastVector}]{VectorSwizzle(0, lastComponent)}");
        return (sb.ToString(), false);
    }
}
