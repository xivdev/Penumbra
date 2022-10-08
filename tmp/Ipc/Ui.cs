using System;
using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class PreSettingsDraw
    {
        public const string Label = $"Penumbra.{nameof( PreSettingsDraw )}";

        public static EventProvider< string > Provider( DalamudPluginInterface pi, Action< Action< string > > sub,
            Action< Action< string > > unsub )
            => new(pi, Label, ( sub, unsub ));

        public static EventSubscriber< string > Subscriber( DalamudPluginInterface pi, params Action< string >[] actions )
            => new(pi, Label, actions);
    }

    public static class PostSettingsDraw
    {
        public const string Label = $"Penumbra.{nameof( PostSettingsDraw )}";

        public static EventProvider< string > Provider( DalamudPluginInterface pi, Action< Action< string > > sub,
            Action< Action< string > > unsub )
            => new(pi, Label, ( sub, unsub ));

        public static EventSubscriber< string > Subscriber( DalamudPluginInterface pi, params Action< string >[] actions )
            => new(pi, Label, actions);
    }

    public static class ChangedItemTooltip
    {
        public const string Label = $"Penumbra.{nameof( ChangedItemTooltip )}";

        public static EventProvider< ChangedItemType, uint > Provider( DalamudPluginInterface pi, Action add, Action del )
            => new(pi, Label, add, del);

        public static EventSubscriber< ChangedItemType, uint > Subscriber( DalamudPluginInterface pi, params Action< ChangedItemType, uint >[] actions )
            => new(pi, Label, actions);
    }

    public static class ChangedItemClick
    {
        public const string Label = $"Penumbra.{nameof( ChangedItemClick )}";

        public static EventProvider< MouseButton, ChangedItemType, uint > Provider( DalamudPluginInterface pi, Action add, Action del )
            => new(pi, Label, add, del);

        public static EventSubscriber< MouseButton, ChangedItemType, uint > Subscriber( DalamudPluginInterface pi, params Action< MouseButton, ChangedItemType, uint >[] actions )
            => new(pi, Label, actions);
    }
}