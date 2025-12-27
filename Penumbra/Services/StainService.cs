using Dalamud.Plugin.Services;
using ImSharp;
using OtterGui.Widgets;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.StainMapStructs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.UI.AdvancedWindow.Materials;

namespace Penumbra.Services;

public sealed record StainTemplate(StringPair Id, Vector4 Diffuse, Vector4 Specular, Vector4 Emissive, int Key, bool Found)
{
    public Vector4 Diffuse  { get; set; } = Diffuse;
    public Vector4 Specular { get; set; } = Specular;
    public Vector4 Emissive { get; set; } = Emissive;
    public bool    Found    { get; set; } = Found;
}

public sealed class TemplateFilter : TextFilterBase<StainTemplate>
{
    protected override string ToFilterString(in StainTemplate item, int globalIndex)
        => item.Id;
}

public sealed class StainTemplateCombo<TDyePack> : ImSharp.FilterComboBase<StainTemplate>
    where TDyePack : unmanaged, IDyePack
{
    private readonly StainService.StainCombo[] _stainCombos;
    private readonly StmFile<TDyePack>         _stmFile;

    private int    _currentDyeChannel;
    private ushort _currentSelection;

    public StainTemplateCombo(StainService.StainCombo[] stainCombos, StmFile<TDyePack> stmFile)
        : base(new TemplateFilter())
    {
        PreviewAlignment = new Vector2(0.90f, 0.5f);
        _stainCombos     = stainCombos;
        _stmFile         = stmFile;
        ComputeWidth     = true;
    }

    protected override FilterComboBaseCache<StainTemplate> CreateCache()
        => new Cache(this);

    private sealed class Cache : FilterComboBaseCache<StainTemplate>
    {
        private readonly StainTemplateCombo<TDyePack> _parent;
        private          int                          _dyeChannel;

        public Cache(StainTemplateCombo<TDyePack> parent)
            : base(parent)
        {
            _parent = parent;
            foreach (var combo in _parent._stainCombos)
                combo.SelectionChanged += OnSelectionChanged;
            _dyeChannel = _parent._currentDyeChannel;
        }

        public override void Update()
        {
            base.Update();
            if (_dyeChannel != _parent._currentDyeChannel)
            {
                UpdateItems();
                ComputeWidth();
                _dyeChannel = _parent._currentDyeChannel;
            }
        }

        private void OnSelectionChanged(Luna.FilterComboColors.Item obj)
        {
            UpdateItems();
            ComputeWidth();
        }

        private void UpdateItems()
        {
            foreach (var item in UnfilteredItems)
            {
                var dye = _parent._stainCombos[_parent._currentDyeChannel].CurrentSelection.Id;
                if (dye > 0 && _parent._stmFile.TryGetValue(item.Key, dye, out var dyes))
                {
                    item.Found    = true;
                    item.Diffuse  = new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)dyes.DiffuseColor),  1);
                    item.Specular = new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)dyes.SpecularColor), 1);
                    item.Emissive = new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)dyes.EmissiveColor), 1);
                }
                else
                {
                    item.Found = false;
                }
            }
        }


        protected override void ComputeWidth()
        {
            ComboWidth = Im.Font.Mono.CalculateTextSize("0000"u8).X + Im.Style.ScrollbarSize + Im.Style.ItemInnerSpacing.X;
            if (_parent._stainCombos[_parent._currentDyeChannel].CurrentSelection.Id is 0)
                return;

            ComboWidth += Im.Style.TextHeight * 3 + Im.Style.ItemInnerSpacing.X * 3;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var combo in _parent._stainCombos)
                combo.SelectionChanged -= OnSelectionChanged;
        }
    }

    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, ushort currentSelection, int currentDyeChannel,
        Utf8StringHandler<TextStringHandlerBuffer> tooltip, out int newSelection, float previewWidth, float itemHeight,
        ComboFlags flags = ComboFlags.None)
    {
        Flags              = flags;
        _currentDyeChannel = currentDyeChannel;
        _currentSelection  = currentSelection;
        using var font = Im.Font.PushMono();
        if (!base.Draw(label, $"{_currentSelection,4}", tooltip, previewWidth, out var selection))
        {
            newSelection = 0;
            return false;
        }

        newSelection = selection.Key;
        return true;
    }

    protected override IEnumerable<StainTemplate> GetItems()
    {
        var dye = _stainCombos[_currentDyeChannel].CurrentSelection.Id;
        foreach (var key in _stmFile.Entries.Keys.Prepend(0))
        {
            if (dye > 0 && _stmFile.TryGetValue(key, dye, out var dyes))
                yield return new StainTemplate(new StringPair($"{key,4}"),
                    new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)dyes.DiffuseColor),  1),
                    new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)dyes.SpecularColor), 1),
                    new Vector4(MtrlTab.PseudoSqrtRgb((Vector3)dyes.EmissiveColor), 1), key.Int, true);
            else
                yield return new StainTemplate(new StringPair($"{key,4}"), Vector4.Zero, Vector4.Zero, Vector4.Zero, key.Int, false);
        }
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in StainTemplate item, int globalIndex, bool selected)
    {
        var ret = Im.Selectable(item.Id.Utf8, selected);
        if (item.Found)
        {
            Im.Line.SameInner();
            var frame = new Vector2(Im.Style.TextHeight);
            Im.Color.Button("D"u8, item.Diffuse, 0, frame);
            Im.Line.SameInner();
            Im.Color.Button("S"u8, item.Specular, 0, frame);
            Im.Line.SameInner();
            Im.Color.Button("E"u8, item.Emissive, 0, frame);
        }

        return ret;
    }

    protected override bool IsSelected(StainTemplate item, int globalIndex)
        => item.Key == _currentSelection;

    protected override bool DrawFilter(float width, FilterComboBaseCache<StainTemplate> cache)
    {
        using var font = Im.Font.PushDefault();
        return base.DrawFilter(width, cache);
    }
}

public class StainService : Luna.IService
{
    public const int ChannelCount = 2;

    public readonly StainCombo                        StainCombo1;
    public readonly StainCombo                        StainCombo2; // FIXME is there a better way to handle this?
    public readonly StmFile<LegacyDyePack>            LegacyStmFile;
    public readonly StmFile<DyePack>                  GudStmFile;
    public readonly StainTemplateCombo<LegacyDyePack> LegacyTemplateCombo;
    public readonly StainTemplateCombo<DyePack>       GudTemplateCombo;

    public unsafe StainService(IDataManager dataManager, CharacterUtility characterUtility, DictStain stainData)
    {
        StainCombo1 = new StainCombo(stainData);
        StainCombo2 = new StainCombo(stainData);

        if (characterUtility.Address == null)
        {
            LegacyStmFile = LoadStmFile<LegacyDyePack>(null, dataManager);
            GudStmFile    = LoadStmFile<DyePack>(null, dataManager);
        }
        else
        {
            LegacyStmFile = LoadStmFile<LegacyDyePack>(characterUtility.Address->LegacyStmResource, dataManager);
            GudStmFile    = LoadStmFile<DyePack>(characterUtility.Address->GudStmResource, dataManager);
        }


        StainCombo[] stainCombos = [StainCombo1, StainCombo2];

        LegacyTemplateCombo = new StainTemplateCombo<LegacyDyePack>(stainCombos, LegacyStmFile);
        GudTemplateCombo    = new StainTemplateCombo<DyePack>(stainCombos, GudStmFile);
    }

    /// <summary> Retrieves the <see cref="FilterComboColors"/> instance for the given channel. Indexing is zero-based. </summary>
    public StainCombo GetStainCombo(int channel)
        => channel switch
        {
            0 => StainCombo1,
            1 => StainCombo2,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel,
                $"Unsupported dye channel {channel} (supported values are 0 and 1)"),
        };

    /// <summary> Loads a STM file. Opportunistically attempts to re-use the file already read by the game, with Lumina fallback. </summary>
    private static unsafe StmFile<TDyePack> LoadStmFile<TDyePack>(ResourceHandle* stmResourceHandle, IDataManager dataManager)
        where TDyePack : unmanaged, IDyePack
    {
        if (stmResourceHandle != null)
        {
            var stmData = stmResourceHandle->CsHandle.GetDataSpan();
            if (stmData.Length > 0)
            {
                Penumbra.Log.Debug($"[StainService] Loading StmFile<{typeof(TDyePack)}> from ResourceHandle 0x{(nint)stmResourceHandle:X}");
                return new StmFile<TDyePack>(stmData);
            }
        }

        Penumbra.Log.Debug($"[StainService] Loading StmFile<{typeof(TDyePack)}> from Lumina");
        return new StmFile<TDyePack>(dataManager);
    }

    public sealed class StainCombo(DictStain stainData) : Luna.FilterComboColors
    {
        protected override IEnumerable<Item> GetItems()
            => stainData.Value.Select(t => new Item(new StringPair(t.Value.Name), t.Value.Dye, t.Key, t.Value.Gloss)).Prepend(None);
    }
}
