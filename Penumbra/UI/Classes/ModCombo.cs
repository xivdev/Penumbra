using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

public class ModCombo(ModStorage modStorage) : SimpleFilterCombo<Mod>(SimpleFilterType.Regex), IUiService
{
    protected readonly ModStorage ModStorage = modStorage;

    public override StringU8 DisplayString(in Mod value)
        => new(value.Name);

    public override string FilterString(in Mod value)
        => value.Name;

    public override IEnumerable<Mod> GetBaseItems()
        => ModStorage;
}

public sealed class ModComboWithoutCurrent(ModStorage modStorage, ModMerger modMerger) : ModCombo(modStorage), IConstructedService
{
    public override IEnumerable<Mod> GetBaseItems()
        => ModStorage.Where(m => m != modMerger.MergeFromMod);
}

public sealed class ModComboWithAll : ModCombo, IDisposable
{
    private readonly        CommunicatorService _communicator;
    private static readonly Mod                 AllMods = new(new DirectoryInfo(".")) { Name = "All Mods" };

    public ModComboWithAll(ModStorage modStorage, CommunicatorService communicator)
        : base(modStorage)
    {
        _communicator = communicator;
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModComboWithAll);
    }

    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Type is ModPathChangeType.Deleted && ReferenceEquals(arguments.Mod, Selection))
            Selection = null;
    }

    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, float previewWidth)
    {
        if (!base.Draw(label, Selection?.Name ?? AllMods.Name, StringU8.Empty, previewWidth, out var newSelection))
            return false;

        Selection = newSelection.Item == AllMods ? null : newSelection.Item;
        return true;
    }

    public Mod? Selection { get; private set; }

    public void Dispose()
        => _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
}
