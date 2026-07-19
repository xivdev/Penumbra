using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Files;
using Penumbra.Mods;

namespace Penumbra.UI.ManagementTab;

public sealed class FailedModNotification(Services.MessageService service, UiNavigator navigator)
    : AmassingNotification<(string Mod, Exception Error)>(service), IService
{
    public void Add(Mod mod, Exception ex)
        => AddObject((mod.ModPath.Name, ex));

    public override NotificationType NotificationType
        => NotificationType.Error;

    public override string NotificationTitle
        => $"{Count} Mod{(Count is 1 ? string.Empty : "s")} failed to load";

    public override string NotificationMessage
        => "One or more Mods failed to load.\n\n See the Messages tab for details. See Management -> Broken Mods to fix the issues.";

    public override void NotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("Open Messages"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (ImEx.Button("Open Management"u8, width))
            navigator.OpenTo(ManagementTabType.BrokenMods);
    }

    protected override StoredNotification CreateStored(in (string Mod, Exception Error) @object)
        => new Stored(this, @object.Mod, @object.Error);

    private sealed class Stored(FailedModNotification parent, string mod, Exception error) : StoredNotification(parent, (mod, error))
    {
        public override string LogMessage { get; } = $"Mod {mod} failed to load:\n{error}";

        public override StringU8 StoredMessage { get; } = FromException(mod, error);

        private static StringU8 FromException(string mod, Exception error)
            => error switch
            {
                MetaMissingException => new StringU8($"[{mod}] failed to load: No Metadata found."),
                InvalidMetaException => new StringU8($"[{mod}] failed to load: Error reading Metadata."),
                MissingFeatureException => new StringU8($"[{mod}] failed to load: Unsupported features required."),
                AggregateException { InnerExceptions.Count: 1 } aggregate => FromException(mod, aggregate.InnerExceptions.First()),
                AggregateException { InnerExceptions.Count: > 1 } aggregate when aggregate.InnerExceptions.First() is InvalidMetaException
                    or MissingFeatureException => FromException(mod, aggregate.InnerExceptions.First()),
                _ => new StringU8($"[{mod}] failed to load: {error.Message}"),
            };

        public override StringU8 StoredTooltip { get; } = new(
            error is MetaMissingException
                ? "The folder mentioned is not an installed mod, please reserve the Penumbra root folder for Penumbra and do not place your own folders in there.\n\n"
              + "Delete this folder or move it out of the root folder to get rid of the warning."
                : $"{error}");
    }
}
