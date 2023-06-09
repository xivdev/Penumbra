using System;
using System.Collections.Generic;
using Dalamud;
using Dalamud.Data;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData;

public static class GameData
{
    /// <summary>
    /// Obtain an object identifier that can link a game path to game objects that use it, using your client language.
    /// </summary>
    public static IObjectIdentifier GetIdentifier(DalamudPluginInterface pluginInterface, DataManager dataManager)
        => new ObjectIdentification(pluginInterface, dataManager, dataManager.Language);

    /// <summary>
    /// Obtain an object identifier that can link a game path to game objects that use it using the given language.
    /// </summary>
    public static IObjectIdentifier GetIdentifier(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
        => new ObjectIdentification(pluginInterface, dataManager, language);

    /// <summary>
    /// Obtain a parser for game paths.
    /// </summary>
    public static IGamePathParser GetGamePathParser()
        => new GamePathParser();
}

public interface IObjectIdentifier : IDisposable
{
    /// <summary>
    /// An accessible parser for game paths.
    /// </summary>
    public IGamePathParser GamePathParser { get; }

    /// <summary>
    /// Add all known game objects using the given game path to the dictionary.
    /// </summary>
    /// <param name="set">A pre-existing dictionary to which names (and optional linked objects) can be added.</param>
    /// <param name="path">The game path to identify.</param>
    public void Identify(IDictionary<string, object?> set, string path);

    /// <summary>
    /// Return named information and possibly linked objects for all known game objects using the given path.
    /// </summary>
    /// <param name="path">The game path to identify.</param>
    public Dictionary<string, object?> Identify(string path);

    /// <summary>
    /// Identify an equippable item by its model values.
    /// </summary>
    /// <param name="setId">The primary model ID for the piece of equipment.</param>
    /// <param name="weaponType">The secondary model ID for weapons, WeaponType.Zero for equipment and accessories.</param>
    /// <param name="variant">The variant ID of the model.</param>
    /// <param name="slot">The equipment slot the piece of equipment uses.</param>
    public IEnumerable<EquipItem> Identify(SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot);

    /// <inheritdoc cref="Identify(SetId, WeaponType, ushort, EquipSlot)"/>
    public IEnumerable<EquipItem> Identify(SetId setId, ushort variant, EquipSlot slot)
        => Identify(setId, 0, variant, slot);
}

public interface IGamePathParser
{
    public ObjectType     PathToObjectType(string path);
    public GameObjectInfo GetFileInfo(string path);
    public string         VfxToKey(string path);
}
