using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ReservedFileNotification(
    Services.MessageService service,
    UiNavigator navigator)
    : AmassingNotification<(string Path, string Mod)>(service), IService
{
    public bool IsRedirectionSupported(Utf8GamePath path, IMod mod, bool temporaryCollection)
    {
        if (ReservedFiles.Files.ContainsKey((uint)path.Path.Crc32))
        {
            if (!temporaryCollection)
                AddObject((path.ToString(), mod.Name));
            return false;
        }

        var ext = path.Extension().AsciiToLower().ToString();
        switch (ext)
        {
            case ".atch" or ".eqp" or ".eqdp" or ".est" or ".gmp" or ".cmp" or ".imc":
                if (!temporaryCollection)
                    Penumbra.Messager.NotificationMessage(
                        $"Redirection of {ext} files for {mod.Name} is unsupported. This probably means that the mod is outdated and may not work correctly.\n\nPlease tell the mod creator to use the corresponding meta manipulations instead.",
                        NotificationType.Warning);
                return false;
            case ".lvb" or ".lgb" or ".sgb":
                if (!temporaryCollection)
                    Penumbra.Messager.NotificationMessage(
                        $"Redirection of {ext} files for {mod.Name} is unsupported as this breaks the game.\n\nThis mod will probably not work correctly.",
                        NotificationType.Warning);
                return false;
            default: return true;
        }
    }

    public override NotificationType NotificationType
        => NotificationType.Warning;

    public override string NotificationMessage
        => "Redirection of these files is disabled because unexpected replacements will cause crashes.\n\n"
          + "See Management -> Reserved Files for more details.";

    public override string NotificationTitle
        => $"{Count} Reserved File{(Count is 1 ? string.Empty : "s")} Encountered";

    protected override StoredNotification CreateStored(in (string Path, string Mod) @object)
        => new Stored(this, @object.Path, @object.Mod);

    public override void NotificationActions(INotificationDrawArgs args)
    {
        var width = Im.ContentRegion.Available with { Y = 0 };
        width.X = (width.X - Im.Style.ItemInnerSpacing.X) / 2;
        if (Im.Button("Open Messages"u8, width))
            navigator.OpenTo(TabType.Messages);
        Im.Line.SameInner();
        if (Im.Button("Open Management"u8, width))
            navigator.OpenTo(ManagementTabType.ReservedFiles);
    }

    private sealed class Stored(ReservedFileNotification parent, string file, string mod) : StoredNotification(parent, (file, mod))
    {
        public override string   LogMessage    { get; } = $"Redirection of {file} in mod {mod} stopped.";
        public override StringU8 StoredMessage { get; } = new($"{file} in {mod}: Reserved File Redirection.");
        public override StringU8 StoredTooltip { get; } = new($"File: {file}\nMod: {mod}");
    }
}
