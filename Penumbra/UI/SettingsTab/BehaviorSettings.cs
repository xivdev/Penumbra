using ImSharp;
using Luna;

namespace Penumbra.UI;

public sealed class BehaviorSettings(BehaviorConfig config) : IUiService
{
    public void Draw()
    {
        DrawGeneralBehavior();
        DrawCollectionAssociation();
    }

    private void DrawGeneralBehavior()
    {
        using var tree = Im.Tree.Node("General"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Automatically Select Character-Associated Collection"u8,
                "On every login, automatically select the collection associated with the current character as the current collection for editing."u8,
                config.AutoSelectCollection))
            config.AutoSelectCollection ^= true;

        if (SettingsTab.Checkbox("Use Interface Collection for other Plugin UIs"u8,
                "Use the collection assigned to your interface for other plugins requesting UI-textures and icons through Dalamud."u8,
                config.UseDalamudUiTextureRedirection))
            config.UseDalamudUiTextureRedirection ^= true;

        LunaStyle.DrawSeparator();
    }

    private void DrawCollectionAssociation()
    {
        using var tree = Im.Tree.Node("Collection Association"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Use Assigned Collections in Lobby"u8,
                "If this is disabled, no mods are applied to characters in the lobby or at the aesthetician."u8,
                config.ShowModsInLobby))
            config.ShowModsInLobby ^= true;
        if (SettingsTab.Checkbox("Use Assigned Collections in Character Window"u8,
                "Use the individual collection for your characters name or the Your Character collection in your main character window, if it is set."u8,
                config.UseCharacterCollectionInMainWindow))
            config.UseCharacterCollectionInMainWindow ^= true;
        if (SettingsTab.Checkbox("Use Assigned Collections in Adventurer Cards"u8,
                "Use the appropriate individual collection for the adventurer card you are currently looking at, based on the adventurer's name."u8,
                config.UseCharacterCollectionsInCards))
            config.UseCharacterCollectionsInCards ^= true;
        if (SettingsTab.Checkbox("Use Assigned Collections in Try-On Window"u8,
                "Use the individual collection for your character's name in your try-on, dye preview or glamour plate window, if it is set."u8,
                config.UseCharacterCollectionInTryOn))
            config.UseCharacterCollectionInTryOn ^= true;
        if (SettingsTab.Checkbox("Use No Mods in Inspect Windows"u8,
                "Use the empty collection for characters you are inspecting, regardless of the character.\n"u8
              + "Takes precedence before the next option."u8, config.UseNoModsInInspect))
            config.UseNoModsInInspect ^= true;
        if (SettingsTab.Checkbox("Use Assigned Collections in Inspect Windows"u8,
                "Use the appropriate individual collection for the character you are currently inspecting, based on their name."u8,
                config.UseCharacterCollectionInInspect))
            config.UseCharacterCollectionInInspect ^= true;
        if (SettingsTab.Checkbox("Use Assigned Collections based on Ownership"u8,
                "Use the owner's name to determine the appropriate individual collection for mounts, companions, accessories and combat pets. This includes trust or squadron companions."u8,
                config.UseOwnerNameForCharacterCollection))
            config.UseOwnerNameForCharacterCollection ^= true;
        if (config.UseOwnerNameForCharacterCollection)
            using (Im.Indent(Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X))
            {
                if (SettingsTab.Checkbox("Include Hostile Owned Actors"u8,
                        "Include any hostile actors that are owned by the character, such as enemies spawned for solo quests."u8,
                        config.UseOwnerForHostiles))
                    config.UseOwnerForHostiles ^= true;
            }
    }
}
