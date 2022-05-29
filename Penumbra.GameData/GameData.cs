using System;
using System.Collections.Generic;
using Dalamud;
using Dalamud.Data;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;

namespace Penumbra.GameData;

public static class GameData
{
    internal static          ObjectIdentification? Identification;
    internal static readonly GamePathParser        GamePathParser = new();

    public static IObjectIdentifier GetIdentifier( DataManager dataManager, ClientLanguage clientLanguage )
    {
        Identification ??= new ObjectIdentification( dataManager, clientLanguage );
        return Identification;
    }

    public static IObjectIdentifier GetIdentifier()
    {
        if( Identification == null )
        {
            throw new Exception( "Object Identification was not initialized." );
        }

        return Identification;
    }

    public static IGamePathParser GetGamePathParser()
        => GamePathParser;
}

public interface IObjectIdentifier
{
    public void                          Identify( IDictionary< string, object? > set, GamePath path );
    public Dictionary< string, object? > Identify( GamePath path );
    public Item?                         Identify( SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot );
}

public interface IGamePathParser
{
    public ObjectType     PathToObjectType( GamePath path );
    public GameObjectInfo GetFileInfo( GamePath path );
    public string         VfxToKey( GamePath path );
}