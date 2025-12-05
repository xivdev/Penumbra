using ImSharp;

namespace Penumbra.UI.Classes;

/// <summary> List of currently available tutorials. </summary>
public enum BasicTutorialSteps
{
    GeneralTooltips,
    ModDirectory,
    EnableMods,
    Deprecated1,
    GeneralSettings,
    Collections,
    EditingCollections,
    CurrentCollection,
    SimpleAssignments,
    IndividualAssignments,
    GroupAssignments,
    CollectionDetails,
    Incognito,
    Deprecated2,
    Mods,
    ModImport,
    AdvancedHelp,
    ModFilters,
    CollectionSelectors,
    Redrawing,
    EnablingMods,
    Priority,
    ModOptions,
    Fin,
    Deprecated3,
    Faq1,
    Faq2,
    Favorites,
    Tags,
}

/// <summary> Service for the in-game tutorial. </summary>
public class TutorialService(EphemeralConfig config) : Luna.IUiService
{
    private readonly Luna.Tutorial _tutorial = new Luna.Tutorial
        {
            BorderColor    = new Rgba32(Colors.TutorialBorder).ToVector(),
            HighlightColor = new Rgba32(Colors.TutorialMarker).ToVector(),
            PopupLabel     = new StringU8("Settings Tutorial"u8),
        }
        .Register("General Tooltips"u8, "This symbol gives you further information about whatever setting it appears next to.\n\n"u8
          + "Hover over them when you are unsure what something does or how to do something."u8)
        .Register("Initial Setup, Step 1: Mod Directory"u8,
            "The first step is to set up your mod directory, which is where your mods are extracted to.\n\n"u8
          + "The mod directory should be a short path - like 'C:\\FFXIVMods' - on your fastest available drive. Faster drives improve performance.\n\n"u8
          + "The folder should be an empty folder no other applications write to."u8)
        .Register("Initial Setup, Step 2: Enable Mods"u8, "Do not forget to enable your mods in case they are not."u8)
        .Deprecated()
        .Register("General Settings"u8, "Look through all of these settings before starting, they might help you a lot!\n\n"u8
          + "If you do not know what some of these do yet, return to this later!"u8)
        .Register("Initial Setup, Step 3: Collections"u8, "Collections are lists of settings for your installed mods.\n\n"u8
          + "This is our next stop!\n\n"u8
          + "Go here after setting up your root folder to continue the tutorial!"u8)
        .Register("Initial Setup, Step 4: Managing Collections"u8,
            "On the left, we have the collection selector. Here, we can create new collections - either empty ones or by duplicating existing ones - and delete any collections not needed anymore.\n"u8
          + "There will always be one collection called \"Default\" that can not be deleted."u8)
        .Register("Initial Setup, Step 5: Selected Collection"u8,
            "The Selected Collection is the one we highlighted in the selector. It is the collection we are currently looking at and editing.\nAny changes we make in our mod settings later in the next tab will edit this collection.\n"u8
          + "We should already have the collection named \"Default\" selected, and for our simple setup, we do not need to do anything here.\n\n"u8)
        .Register("Initial Setup, Step 6: Simple Assignments"u8,
            "Aside from being a collection of settings, we can also assign collections to different functions. This is used to make different mods apply to different characters.\n"u8
          + "The Simple Assignments panel shows you the possible assignments that are enough for most people along with descriptions.\n"u8
          + "If you are just starting, you can see that the \"Default\" collection is currently assigned to Default and Interface.\n"u8
          + "You can also assign 'Use No Mods' instead of a collection by clicking on the function buttons."u8)
        .Register("Individual Assignments"u8,
            "In the Individual Assignments panel, you can manually create assignments for very specific characters or monsters, not just yourself or ones you can currently target."u8)
        .Register("Group Assignments"u8,
            "In the Group Assignments panel, you can create Assignments for more specific groups of characters based on race or age."u8)
        .Register("Collection Details"u8,
            "In the Collection Details panel, you can see a detailed overview over the usage of the currently selected collection, as well as remove outdated mod settings and setup inheritance.\n"u8
          + "Inheritance can be used to make one collection take the settings of another as long as it does not setup the mod in question itself."u8)
        .Register("Incognito Mode"u8,
            "This button can toggle Incognito Mode, which shortens all collection names to two letters and a number,\n"u8
          + "and all displayed individual character names to their initials and world, in case you want to share screenshots.\n"u8
          + "It is strongly recommended to not show your characters name in public screenshots when using Penumbra."u8)
        .Deprecated()
        .Register("Initial Setup, Step 7: Mods"u8, "Our last stop is the Mods tab, where you can import and setup your mods.\n\n"u8
          + "Please go there after verifying that your Selected Collection and Default Collection are setup to your liking."u8)
        .Register("Initial Setup, Step 8: Mod Import"u8,
            "Click this button to open a file selector with which to select TTMP mod files. You can select multiple at once.\n\n"u8
          + "It is not recommended to import huge mod packs of all your TexTools mods, but rather import the mods themselves, otherwise you lose out on a lot of Penumbra features!\n\n"u8
          + "A feature to import raw texture mods for Tattoos etc. is available under Advanced Editing, but is currently a work in progress."u8)
        .Register("Advanced Help"u8, "Click this button to get detailed information on what you can do in the mod selector.\n\n"u8
          + "Import and select a mod now to continue."u8)
        .Register("Mod Filters"u8, "You can filter the available mods by name, author, changed items or various attributes here."u8)
        .Register("Collection Selectors"u8, "This row provides shortcuts to set your Selected Collection.\n\n"u8
          + "The first button sets it to your Base Collection (if any).\n\n"u8
          + "The second button sets it to the collection the settings of the currently selected mod are inherited from (if any).\n\n"u8
          + "The third is a regular collection selector to let you choose among all your collections."u8)
        .Register("Redrawing"u8,
            "Whenever you change your mod configuration, changes do not immediately take effect. You will need to force the game to reload the relevant files (or if this is not possible, restart the game).\n\n"u8
          + "For this, Penumbra has these buttons as well as the '/penumbra redraw' command, which redraws all actors at once. You can also use several modifiers described in the help marker instead.\n\n"u8
          + "Feel free to use these slash commands (e.g. '/penumbra redraw self') as a macro, too."u8)
        .Register("Initial Setup, Step 9: Enabling Mods"u8,
            "Enable a mod here. Disabled mods will not apply to anything in the current collection.\n\n"u8
          + "Mods can be enabled or disabled in a collection, or they can be unconfigured, in which case they will use Inheritance."u8)
        .Register("Initial Setup, Step 10: Priority"u8,
            "If two enabled mods in one collection change the same files, there is a conflict.\n\n"u8
          + "Conflicts can be solved by setting a priority. The mod with the higher number will be used for all the conflicting files.\n\n"u8
          + "Conflicts are not a problem, as long as they are correctly resolved with priorities. Negative priorities are possible."u8)
        .Register("Mod Options"u8, "Many mods have options themselves. You can also choose those here.\n\n"u8
          + "Pulldown-options are mutually exclusive, whereas checkmark options can all be enabled separately."u8)
        .Register("Initial Setup - Fin"u8, "Now you should have all information to get Penumbra running and working!\n\n"u8
          + "If there are further questions or you need more help for the advanced features, take a look at the guide linked in the settings page."u8)
        .Deprecated()
        .Register("FAQ 1"u8,
            "It is advised to not use TexTools and Penumbra at the same time. Penumbra may refuse to work if TexTools broke your game indices."u8)
        .Register("FAQ 2"u8, "Penumbra can change the skin material a mod uses. This is under advanced editing."u8)
        .Register("Favorites"u8,
            "You can now toggle mods as favorites using this button. You can filter for favorited mods in the mod selector. Favorites are stored locally, not within the mod, but independently of collections."u8)
        .Register("Tags"u8,
            "Mods can now have two types of tags:\n\n- Local Tags are those that you can set for yourself. They are stored locally and are not saved in any way in the mod directory itself.\n- Mod Tags are stored in the mod metadata, are set by the mod creator and are exported together with the mod, they can only be edited in the Edit Mod tab.\n\nIf a mod has a tag in its Mod Tags, this overwrites any identical Local Tags.\n\nYou can filter for tags in the mod selector via 't:text'."u8)
        .EnsureSize(Enum.GetValues<BasicTutorialSteps>().Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OpenTutorial(BasicTutorialSteps step)
        => _tutorial.Open((int)step, config.TutorialStep, v =>
        {
            config.TutorialStep = v;
            config.Save();
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipTutorial(BasicTutorialSteps step)
        => _tutorial.Skip((int)step, config.TutorialStep, v =>
        {
            config.TutorialStep = v;
            config.Save();
        });

    /// <summary> Update the current tutorial step if tutorials have changed since last update. </summary>
    public void UpdateTutorialStep()
    {
        var tutorial = _tutorial.CurrentEnabledId(config.TutorialStep);
        if (tutorial != config.TutorialStep)
        {
            config.TutorialStep = tutorial;
            config.Save();
        }
    }
}
