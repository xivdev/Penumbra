using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Mods.Subclasses;

namespace Penumbra.Mods;

/// <summary> Groups that allow only one of their available options to be selected. </summary>
public sealed class SingleModGroup : IModGroup
{
    public GroupType Type
        => GroupType.Single;

    public string Name            { get; set; } = "Option";
    public string Description     { get; set; } = "A mutually exclusive group of settings.";
    public int    Priority        { get; set; }
    public uint   DefaultSettings { get; set; }

    public readonly List< SubMod > OptionData = new();

    public int OptionPriority( Index _ )
        => Priority;

    public ISubMod this[ Index idx ]
        => OptionData[ idx ];

    [JsonIgnore]
    public int Count
        => OptionData.Count;

    public IEnumerator< ISubMod > GetEnumerator()
        => OptionData.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public static SingleModGroup? Load( Mod mod, JObject json, int groupIdx )
    {
        var options = json[ "Options" ];
        var ret = new SingleModGroup
        {
            Name            = json[ nameof( Name ) ]?.ToObject< string >()          ?? string.Empty,
            Description     = json[ nameof( Description ) ]?.ToObject< string >()   ?? string.Empty,
            Priority        = json[ nameof( Priority ) ]?.ToObject< int >()         ?? 0,
            DefaultSettings = json[ nameof( DefaultSettings ) ]?.ToObject< uint >() ?? 0u,
        };
        if( ret.Name.Length == 0 )
        {
            return null;
        }

        if( options != null )
        {
            foreach( var child in options.Children() )
            {
                var subMod = new SubMod( mod );
                subMod.SetPosition( groupIdx, ret.OptionData.Count );
                subMod.Load( mod.ModPath, child, out _ );
                ret.OptionData.Add( subMod );
            }
        }

        if( ( int )ret.DefaultSettings >= ret.Count )
            ret.DefaultSettings = 0;

        return ret;
    }

    public IModGroup Convert( GroupType type )
    {
        switch( type )
        {
            case GroupType.Single: return this;
            case GroupType.Multi:
                var multi = new MultiModGroup()
                {
                    Name            = Name,
                    Description     = Description,
                    Priority        = Priority,
                    DefaultSettings = 1u << ( int )DefaultSettings,
                };
                multi.PrioritizedOptions.AddRange( OptionData.Select( ( o, i ) => ( o, i ) ) );
                return multi;
            default: throw new ArgumentOutOfRangeException( nameof( type ), type, null );
        }
    }

    public bool MoveOption( int optionIdxFrom, int optionIdxTo )
    {
        if( !OptionData.Move( optionIdxFrom, optionIdxTo ) )
        {
            return false;
        }

        // Update default settings with the move.
        if( DefaultSettings == optionIdxFrom )
        {
            DefaultSettings = ( uint )optionIdxTo;
        }
        else if( optionIdxFrom < optionIdxTo )
        {
            if( DefaultSettings > optionIdxFrom && DefaultSettings <= optionIdxTo )
            {
                --DefaultSettings;
            }
        }
        else if( DefaultSettings < optionIdxFrom && DefaultSettings >= optionIdxTo )
        {
            ++DefaultSettings;
        }

        UpdatePositions( Math.Min( optionIdxFrom, optionIdxTo ) );
        return true;
    }

    public void UpdatePositions( int from = 0 )
    {
        foreach( var (o, i) in OptionData.WithIndex().Skip( from ) )
        {
            o.SetPosition( o.GroupIdx, i );
        }
    }
}