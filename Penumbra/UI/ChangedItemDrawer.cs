using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Data;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Parsing;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public class ChangedItemDrawer : IDisposable
{
    private const EquipSlot MonsterSlot       = (EquipSlot)100;
    private const EquipSlot DemihumanSlot     = (EquipSlot)101;
    private const EquipSlot CustomizationSlot = (EquipSlot)102;
    private const EquipSlot ActionSlot        = (EquipSlot)103;

    private readonly CommunicatorService                _communicator;
    private readonly Dictionary<EquipSlot, TextureWrap> _icons = new(16);

    public ChangedItemDrawer(UiBuilder uiBuilder, DataManager gameData, CommunicatorService communicator)
    {
        uiBuilder.RunWhenUiPrepared(() => CreateEquipSlotIcons(uiBuilder, gameData), true);
        _communicator = communicator;
    }

    public void Dispose()
    {
        foreach (var wrap in _icons.Values.Distinct())
            wrap.Dispose();
        _icons.Clear();
    }

    /// <summary> Apply Changed Item Counters to the Name if necessary. </summary>
    public static string ChangedItemName(string name, object? data)
        => data is int counter ? $"{counter} Files Manipulating {name}s" : name;

    /// <summary> Add filterable information to the string. </summary>
    public static string ChangedItemFilterName(string name, object? data)
        => data switch
        {
            int counter => $"{counter} Files Manipulating {name}s",
            Item it => $"{name}\0{((EquipSlot)it.EquipSlotCategory.Row).ToName()}\0{(GetChangedItemObject(it, out var t) ? t : string.Empty)}",
            ModelChara m => $"{name}\0{(GetChangedItemObject(m, out var t) ? t : string.Empty)}",
            _ => name,
        };

    /// <summary>
    /// Draw a changed item, invoking the Api-Events for clicks and tooltips.
    /// Also draw the item Id in grey if requested.
    /// </summary>
    public void DrawChangedItem(string name, object? data, bool drawId)
    {
        name = ChangedItemName(name, data);
        DrawCategoryIcon(name, data);
        ImGui.SameLine();
        using (var style = ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0,   0.5f))
                   .Push(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, ImGui.GetStyle().CellPadding.Y * 2)))
        {
            var ret = ImGui.Selectable(name, false, ImGuiSelectableFlags.None, new Vector2(0, ImGui.GetFrameHeight())) ? MouseButton.Left : MouseButton.None;
            ret = ImGui.IsItemClicked(ImGuiMouseButton.Right) ? MouseButton.Right : ret;
            ret = ImGui.IsItemClicked(ImGuiMouseButton.Middle) ? MouseButton.Middle : ret;
            if (ret != MouseButton.None)
                _communicator.ChangedItemClick.Invoke(ret, data);
        }

        if (_communicator.ChangedItemHover.HasTooltip && ImGui.IsItemHovered())
        {
            // We can not be sure that any subscriber actually prints something in any case.
            // Circumvent ugly blank tooltip with less-ugly useless tooltip.
            using var tt    = ImRaii.Tooltip();
            using var group = ImRaii.Group();
            _communicator.ChangedItemHover.Invoke(data);
            group.Dispose();
            if (ImGui.GetItemRectSize() == Vector2.Zero)
                ImGui.TextUnformatted("No actions available.");
        }

        if (!drawId || !GetChangedItemObject(data, out var text))
            return;

        ImGui.SameLine(ImGui.GetContentRegionAvail().X);
        ImGui.AlignTextToFramePadding();
        ImGuiUtil.RightJustify(text, ColorId.ItemId.Value());
    }

    private void DrawCategoryIcon(string name, object? obj)
    {
        var height = ImGui.GetFrameHeight();
        var slot   = EquipSlot.Unknown;
        var desc   = string.Empty;
        if (obj is Item it)
        {
            slot = (EquipSlot)it.EquipSlotCategory.Row;
            desc = slot.ToName();
        }
        else if (obj is ModelChara m)
        {
            (slot, desc) = (CharacterBase.ModelType)m.Type switch
            {
                CharacterBase.ModelType.DemiHuman => (DemihumanSlot, "Demi-Human"),
                CharacterBase.ModelType.Monster   => (MonsterSlot, "Monster"),
                _                                 => (EquipSlot.Unknown, string.Empty),
            };
        }
        else if (name.StartsWith("Action: "))
        {
            (slot, desc) = (ActionSlot, "Action");
        }
        else if (name.StartsWith("Customization: "))
        {
            (slot, desc) = (CustomizationSlot, "Customization");
        }

        if (!_icons.TryGetValue(slot, out var icon))
        {
            ImGui.Dummy(new Vector2(height));
            return;
        }

        ImGui.Image(icon.ImGuiHandle, new Vector2(height));
        if (ImGui.IsItemHovered() && icon.Height > height)
        {
            using var tt = ImRaii.Tooltip();
            ImGui.Image(icon.ImGuiHandle, new Vector2(icon.Width, icon.Height));
            ImGui.SameLine();
            ImGuiUtil.DrawTextButton(desc, new Vector2(0, icon.Height), 0);
        }
    }

    /// <summary> Return more detailed object information in text, if it exists. </summary>
    public static bool GetChangedItemObject(object? obj, out string text)
    {
        switch (obj)
        {
            case Item it:
                var quad = (Quad)it.ModelMain;
                text = quad.C == 0 ? $"({quad.A}-{quad.B})" : $"({quad.A}-{quad.B}-{quad.C})";
                return true;
            case ModelChara m:
                text = $"({((CharacterBase.ModelType)m.Type).ToName()} {m.Model}-{m.Base}-{m.Variant})";
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    private bool CreateEquipSlotIcons(UiBuilder uiBuilder, DataManager gameData)
    {
        using var equipTypeIcons = uiBuilder.LoadUld("ui/uld/ArmouryBoard.uld");

        if (!equipTypeIcons.Valid)
            return false;

        // Weapon
        var tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 0);
        if (tex != null)
        {
            _icons.Add(EquipSlot.MainHand, tex);
            _icons.Add(EquipSlot.BothHand, tex);
        }

        // Hat
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 1);
        if (tex != null)
            _icons.Add(EquipSlot.Head, tex);

        // Body
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 2);
        if (tex != null)
        {
            _icons.Add(EquipSlot.Body,              tex);
            _icons.Add(EquipSlot.BodyHands,         tex);
            _icons.Add(EquipSlot.BodyHandsLegsFeet, tex);
            _icons.Add(EquipSlot.BodyLegsFeet,      tex);
            _icons.Add(EquipSlot.ChestHands,        tex);
            _icons.Add(EquipSlot.FullBody,          tex);
            _icons.Add(EquipSlot.HeadBody,          tex);
        }

        // Hands
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 3);
        if (tex != null)
            _icons.Add(EquipSlot.Hands, tex);

        // Pants
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 5);
        if (tex != null)
        {
            _icons.Add(EquipSlot.Legs,     tex);
            _icons.Add(EquipSlot.LegsFeet, tex);
        }

        // Shoes
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 6);
        if (tex != null)
            _icons.Add(EquipSlot.Feet, tex);

        // Offhand
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 7);
        if (tex != null)
            _icons.Add(EquipSlot.OffHand, tex);

        // Earrings
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 8);
        if (tex != null)
            _icons.Add(EquipSlot.Ears, tex);

        // Necklace
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 9);
        if (tex != null)
            _icons.Add(EquipSlot.Neck, tex);

        // Bracelet
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 10);
        if (tex != null)
            _icons.Add(EquipSlot.Wrists, tex);

        // Ring
        tex = equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 11);
        if (tex != null)
            _icons.Add(EquipSlot.RFinger, tex);

        // Monster
        tex = gameData.GetImGuiTexture("ui/icon/062000/062042_hr1.tex");
        if (tex != null)
            _icons.Add(MonsterSlot, tex);

        // Demihuman
        tex = gameData.GetImGuiTexture("ui/icon/062000/062041_hr1.tex");
        if (tex != null)
            _icons.Add(DemihumanSlot, tex);

        // Customization
        tex = gameData.GetImGuiTexture("ui/icon/062000/062043_hr1.tex");
        if (tex != null)
            _icons.Add(CustomizationSlot, tex);

        // Action
        tex = gameData.GetImGuiTexture("ui/icon/062000/062001_hr1.tex");
        if (tex != null)
            _icons.Add(ActionSlot, tex);

        return true;
    }
}
