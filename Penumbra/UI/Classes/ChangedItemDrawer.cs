using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImSharp;
using Lumina.Data.Files;
using Luna;
using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Services;
using MouseButton = Penumbra.Api.Enums.MouseButton;

namespace Penumbra.UI.Classes;

public class ChangedItemDrawer : IDisposable, IUiService
{
    private static readonly string[] LowerNames = ChangedItemFlagExtensions.Order.Select(f => f.ToDescription().ToString().ToLowerInvariant()).ToArray();

    public static bool TryParseIndex(ReadOnlySpan<char> input, out ChangedItemIconFlag slot)
    {
        // Handle numeric cases before TryParse because numbers
        // are not logical otherwise.
        if (int.TryParse(input, out var idx))
        {
            // We assume users will use 1-based index, but if they enter 0, just use the first.
            if (idx == 0)
            {
                slot = ChangedItemFlagExtensions.Order[0];
                return true;
            }

            // Use 1-based index.
            --idx;
            if (idx >= 0 && idx < ChangedItemFlagExtensions.Order.Count)
            {
                slot = ChangedItemFlagExtensions.Order[idx];
                return true;
            }
        }

        slot = 0;
        return false;
    }

    public static bool TryParsePartial(string lowerInput, out ChangedItemIconFlag slot)
    {
        if (TryParseIndex(lowerInput, out slot))
            return true;

        slot = 0;
        foreach (var (item, flag) in LowerNames.Zip(ChangedItemFlagExtensions.Order))
        {
            if (item.Contains(lowerInput, StringComparison.Ordinal))
                slot |= flag;
        }

        return slot != 0;
    }


    private readonly Configuration                                        _config;
    private readonly CommunicatorService                                  _communicator;
    private readonly Dictionary<ChangedItemIconFlag, IDalamudTextureWrap> _icons = new(16);
    private          float                                                _smallestIconWidth;

    public static Vector2 TypeFilterIconSize
        => new(2 * Im.Style.TextHeight);

    public ChangedItemDrawer(IUiBuilder uiBuilder, IDataManager gameData, ITextureProvider textureProvider, CommunicatorService communicator,
        Configuration config)
    {
        uiBuilder.RunWhenUiPrepared(() => CreateEquipSlotIcons(uiBuilder, gameData, textureProvider), true);
        _communicator = communicator;
        _config       = config;
    }

    public void Dispose()
    {
        foreach (var wrap in _icons.Values.Distinct())
            wrap.Dispose();
        _icons.Clear();
    }

    /// <summary> Check if a changed item should be drawn based on its category. </summary>
    public bool FilterChangedItem(string name, IIdentifiedObjectData data, string filter)
        => (_config.Ephemeral.ChangedItemFilter == ChangedItemFlagExtensions.AllFlags
             || _config.Ephemeral.ChangedItemFilter.HasFlag(data.GetIcon().ToFlag()))
         && (filter.Length is 0 || !data.IsFilteredOut(name, filter));

    /// <summary> Draw the icon corresponding to the category of a changed item. </summary>
    public void DrawCategoryIcon(IIdentifiedObjectData data, float height)
        => DrawCategoryIcon(data.GetIcon().ToFlag(), height);

    public void DrawCategoryIcon(ChangedItemIconFlag iconFlagType)
        => DrawCategoryIcon(iconFlagType, Im.Style.FrameHeight);

    public void DrawCategoryIcon(ChangedItemIconFlag iconFlagType, float height)
    {
        if (!_icons.TryGetValue(iconFlagType, out var icon))
        {
            Im.Dummy(0, height);
            return;
        }

        Im.Image.Draw(icon.Id(), new Vector2(height));
        if (Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            Im.Image.Draw(icon.Id(), new Vector2(_smallestIconWidth));
            Im.Line.Same();
            ImEx.TextFramed(iconFlagType.ToDescription(), new Vector2(0, _smallestIconWidth), 0);
        }
    }

    public void ChangedItemHandling(IIdentifiedObjectData data, bool leftClicked)
    {
        var ret = leftClicked ? MouseButton.Left : MouseButton.None;
        ret = Im.Item.RightClicked() ? MouseButton.Right : ret;
        ret = Im.Item.MiddleClicked() ? MouseButton.Middle : ret;
        if (ret is not MouseButton.None)
            _communicator.ChangedItemClick.Invoke(new ChangedItemClick.Arguments(ret, data));
        if (!Im.Item.Hovered())
            return;

        using var tt = Im.Tooltip.Begin();
        if (data.Count == 1)
            Im.Text("This item is changed through a single effective change.\n"u8);
        else
            Im.Text($"This item is changed through {data.Count} distinct effective changes.\n");
        Im.Cursor.Y += 3 * Im.Style.GlobalScale;
        Im.Separator();
        Im.Cursor.Y += 3 * Im.Style.GlobalScale;
        _communicator.ChangedItemHover.Invoke(new ChangedItemHover.Arguments(data));
    }

    /// <summary> Draw the model information, right-justified. </summary>
    public static void DrawModelData(IIdentifiedObjectData data, float height)
    {
        var additionalData = data.AdditionalData;
        if (additionalData.Length is 0)
            return;

        Im.Line.Same();
        using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
        Im.Cursor.Y += (height - Im.Style.TextHeight) / 2;
        ImEx.TextRightAligned(additionalData, Im.Style.ItemInnerSpacing.X);
    }

    /// <summary> Draw the model information, right-justified. </summary>
    public static void DrawModelData(ReadOnlySpan<byte> text, float height)
    {
        if (text.Length is 0)
            return;

        Im.Line.Same();
        using var color = ImGuiColor.Text.Push(ColorId.ItemId.Value());
        Im.Cursor.Y += (height - Im.Style.TextHeight) / 2;
        ImEx.TextRightAligned(text, Im.Style.ItemInnerSpacing.X);
    }

    /// <summary> Draw a header line with the different icon types to filter them. </summary>
    public void DrawTypeFilter()
    {
        if (_config.HideChangedItemFilters)
            return;

        var typeFilter = _config.Ephemeral.ChangedItemFilter;
        if (DrawTypeFilter(ref typeFilter))
        {
            _config.Ephemeral.ChangedItemFilter = typeFilter;
            _config.Ephemeral.Save();
        }
    }

    /// <summary> Draw a header line with the different icon types to filter them. </summary>
    public bool DrawTypeFilter(ref ChangedItemIconFlag typeFilter)
    {
        var       ret   = false;
        using var _     = Im.Id.Push("ChangedItemIconFilter"u8);
        var       size  = TypeFilterIconSize;
        using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero);


        foreach (var iconType in ChangedItemFlagExtensions.Order)
        {
            ret |= DrawIcon(iconType, ref typeFilter);
            Im.Line.Same();
        }

        Im.Cursor.X = Im.ContentRegion.Maximum.X - size.X;
        Im.Image.Draw(_icons[ChangedItemFlagExtensions.AllFlags].Id(), size, Vector2.Zero, Vector2.One,
            typeFilter switch
            {
                0                                  => new Vector4(0.6f,  0.3f,  0.3f,  1f),
                ChangedItemFlagExtensions.AllFlags => new Vector4(0.75f, 0.75f, 0.75f, 1f),
                _                                  => new Vector4(0.5f,  0.5f,  1f,    1f),
            });
        if (Im.Item.Clicked())
        {
            typeFilter = typeFilter is ChangedItemFlagExtensions.AllFlags ? 0 : ChangedItemFlagExtensions.AllFlags;
            ret        = true;
        }

        return ret;

        bool DrawIcon(ChangedItemIconFlag type, ref ChangedItemIconFlag typeFilter)
        {
            var localRet = false;
            var icon     = _icons[type];
            var flag     = typeFilter.HasFlag(type);
            Im.Image.Draw(icon.Id(), size, Vector2.Zero, Vector2.One, flag ? Vector4.One : new Vector4(0.6f, 0.3f, 0.3f, 1f));
            if (Im.Item.Clicked())
            {
                typeFilter = flag ? typeFilter & ~type : typeFilter | type;
                localRet   = true;
            }

            using var popup = Im.Popup.BeginContextItem($"{type}");
            if (popup)
                if (Im.Menu.Item("Enable Only This"u8))
                {
                    typeFilter = type;
                    localRet   = true;
                    Im.Popup.CloseCurrent();
                }

            if (Im.Item.Hovered())
            {
                using var tt = Im.Tooltip.Begin();
                Im.Image.Draw(icon.Id(), new Vector2(_smallestIconWidth));
                Im.Line.Same();
                ImEx.TextFramed(type.ToDescription(), new Vector2(0, _smallestIconWidth), 0);
            }

            return localRet;
        }
    }

    /// <summary> Initialize the icons. </summary>
    private bool CreateEquipSlotIcons(IUiBuilder uiBuilder, IDataManager gameData, ITextureProvider textureProvider)
    {
        using var equipTypeIcons = uiBuilder.LoadUld("ui/uld/ArmouryBoard.uld");

        if (!equipTypeIcons.Valid)
            return false;

        void Add(ChangedItemIconFlag icon, IDalamudTextureWrap? tex)
        {
            if (tex != null)
                _icons.Add(icon, tex);
        }

        Add(ChangedItemIconFlag.Mainhand,       equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 0));
        Add(ChangedItemIconFlag.Head,           equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 1));
        Add(ChangedItemIconFlag.Body,           equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 2));
        Add(ChangedItemIconFlag.Hands,          equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 3));
        Add(ChangedItemIconFlag.Legs,           equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 5));
        Add(ChangedItemIconFlag.Feet,           equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 6));
        Add(ChangedItemIconFlag.Offhand,        equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 7));
        Add(ChangedItemIconFlag.Ears,           equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 8));
        Add(ChangedItemIconFlag.Neck,           equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 9));
        Add(ChangedItemIconFlag.Wrists,         equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 10));
        Add(ChangedItemIconFlag.Finger,         equipTypeIcons.LoadTexturePart("ui/uld/ArmouryBoard_hr1.tex", 11));
        Add(ChangedItemIconFlag.Monster,        textureProvider.CreateFromTexFile(gameData.GetFile<TexFile>("ui/icon/062000/062044_hr1.tex")!));
        Add(ChangedItemIconFlag.Demihuman,      textureProvider.CreateFromTexFile(gameData.GetFile<TexFile>("ui/icon/062000/062043_hr1.tex")!));
        Add(ChangedItemIconFlag.Customization,  textureProvider.CreateFromTexFile(gameData.GetFile<TexFile>("ui/icon/062000/062045_hr1.tex")!));
        Add(ChangedItemIconFlag.Action,         textureProvider.CreateFromTexFile(gameData.GetFile<TexFile>("ui/icon/062000/062001_hr1.tex")!));
        Add(ChangedItemIconFlag.Emote,          LoadEmoteTexture(gameData, textureProvider));
        Add(ChangedItemIconFlag.Unknown,        LoadUnknownTexture(gameData, textureProvider));
        Add(ChangedItemFlagExtensions.AllFlags, textureProvider.CreateFromTexFile(gameData.GetFile<TexFile>("ui/icon/114000/114052_hr1.tex")!));

        _smallestIconWidth = _icons.Values.Min(i => i.Width);

        return true;
    }

    private static IDalamudTextureWrap? LoadUnknownTexture(IDataManager gameData, ITextureProvider textureProvider)
    {
        var unk = gameData.GetFile<TexFile>("ui/uld/levelup2_hr1.tex");
        if (unk == null)
            return null;

        var image = unk.GetRgbaImageData();
        var bytes = new byte[unk.Header.Height * unk.Header.Height * 4];
        var diff  = 2 * (unk.Header.Height - unk.Header.Width);
        for (var y = 0; y < unk.Header.Height; ++y)
            image.AsSpan(4 * y * unk.Header.Width, 4 * unk.Header.Width).CopyTo(bytes.AsSpan(4 * y * unk.Header.Height + diff));

        return textureProvider.CreateFromRaw(RawImageSpecification.Rgba32(unk.Header.Height, unk.Header.Height), bytes, "Penumbra.UnkItemIcon");
    }

    private static unsafe IDalamudTextureWrap? LoadEmoteTexture(IDataManager gameData, ITextureProvider textureProvider)
    {
        var emote = gameData.GetFile<TexFile>("ui/icon/000000/000019_hr1.tex");
        if (emote == null)
            return null;

        var image2 = emote.GetRgbaImageData();
        fixed (byte* ptr = image2)
        {
            var color = (uint*)ptr;
            for (var i = 0; i < image2.Length / 4; ++i)
            {
                if (color[i] == 0xFF000000)
                    image2[i * 4 + 3] = 0;
            }
        }

        return textureProvider.CreateFromRaw(RawImageSpecification.Rgba32(emote.Header.Width, emote.Header.Height), image2,
            "Penumbra.EmoteItemIcon");
    }
}
