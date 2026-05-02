using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public sealed class FailedModNotification(Services.MessageService service, UiNavigator navigator)
    : AmassingNotification<(string Mod, Exception Error)>(service), IService
{
    public void AddMissingMeta(Mod mod)
        => AddObject((mod.ModPath.Name, new FileNotFoundException("No Metadata found.", Path.Combine(mod.ModPath.FullName, "meta.json"))));

    public void AddInvalidMeta(Mod mod, Exception ex)
        => AddObject((mod.ModPath.Name, ex));

    public override NotificationType NotificationType
        => NotificationType.Error;

    public override string NotificationTitle
        => $"{Count} Mod{(Count is 1 ? string.Empty : "s")} failed to load";

    // TODO: add management tab for this
    public override string NotificationMessage
        => "One or more Mods failed to load.\n\n See the Messages tab for details.\n\nA management tab for handling these cases more easily will be added later.";

    public override void NotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("Open Messages"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (ImEx.Button("Open Management"u8, width, true))
            navigator.OpenTo(ManagementTabType.ReservedFiles); // TODO
    }

    protected override StoredNotification CreateStored(in (string Mod, Exception Error) @object)
        => new Stored(this, @object.Mod, @object.Error);

    private sealed class Stored(FailedModNotification parent, string mod, Exception error) : StoredNotification(parent, (mod, error))
    {
        public override string LogMessage { get; } = $"Mod {mod} failed to load:\n{error}";

        public override StringU8 StoredMessage { get; } = new(error is FileNotFoundException
            ? $"[{mod}] failed to load: No Metadata found."
            : $"[{mod}] failed to load: Error reading Metadata.");

        public override StringU8 StoredTooltip { get; } = new($"{error}");
    }
}
