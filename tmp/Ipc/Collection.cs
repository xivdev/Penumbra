using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Penumbra.Api.Helpers;

namespace Penumbra.Api;

public static partial class Ipc
{
    public static class GetCollections
    {
        public const string Label = $"Penumbra.{nameof( GetCollections )}";

        public static FuncProvider< IList< string > > Provider( DalamudPluginInterface pi, Func< IList< string > > func )
            => new(pi, Label, func);

        public static FuncSubscriber< IList< string > > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetCurrentCollectionName
    {
        public const string Label = $"Penumbra.{nameof( GetCurrentCollectionName )}";

        public static FuncProvider< string > Provider( DalamudPluginInterface pi, Func< string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetDefaultCollectionName
    {
        public const string Label = $"Penumbra.{nameof( GetDefaultCollectionName )}";

        public static FuncProvider< string > Provider( DalamudPluginInterface pi, Func< string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetInterfaceCollectionName
    {
        public const string Label = $"Penumbra.{nameof( GetInterfaceCollectionName )}";

        public static FuncProvider< string > Provider( DalamudPluginInterface pi, Func< string > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetCharacterCollectionName
    {
        public const string Label = $"Penumbra.{nameof( GetCharacterCollectionName )}";

        public static FuncProvider< string, (string, bool) > Provider( DalamudPluginInterface pi, Func< string, (string, bool) > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, (string, bool) > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }

    public static class GetChangedItems
    {
        public const string Label = $"Penumbra.{nameof( GetChangedItems )}";

        public static FuncProvider< string, IReadOnlyDictionary< string, object? > > Provider( DalamudPluginInterface pi,
            Func< string, IReadOnlyDictionary< string, object? > > func )
            => new(pi, Label, func);

        public static FuncSubscriber< string, IReadOnlyDictionary< string, object? > > Subscriber( DalamudPluginInterface pi )
            => new(pi, Label);
    }
}