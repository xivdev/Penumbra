using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

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
public class TutorialService
{
    public const string SelectedCollection    = "Selected Collection";
    public const string DefaultCollection     = "Base Collection";
    public const string InterfaceCollection   = "Interface Collection";
    public const string AssignedCollections   = "Assigned Collections";

    public const string SupportedRedrawModifiers = "    - nothing, to redraw all characters\n"
      + "    - 'self' or '<me>': your own character\n"
      + "    - 'target' or '<t>': your target\n"
      + "    - 'focus' or '<f>: your focus target\n"
      + "    - 'mouseover' or '<mo>': the actor you are currently hovering over\n"
      + "    - any specific actor name to redraw all actors of that exactly matching name.";

    private readonly Configuration _config;
    private readonly Tutorial      _tutorial;

    public TutorialService(Configuration config)
    {
        _config = config;
        _tutorial = new Tutorial()
            {
                BorderColor    = Colors.TutorialBorder,
                HighlightColor = Colors.TutorialMarker,
                PopupLabel     = "Settings Tutorial",
            }
            .Register("General Tooltips", "This symbol gives you further information about whatever setting it appears next to.\n\n"
              + "Hover over them when you are unsure what something does or how to do something.")
            .Register("Initial Setup, Step 1: Mod Directory",
                "The first step is to set up your mod directory, which is where your mods are extracted to.\n\n"
              + "The mod directory should be a short path - like 'C:\\FFXIVMods' - on your fastest available drive. Faster drives improve performance.\n\n"
              + "The folder should be an empty folder no other applications write to.")
            .Register("Initial Setup, Step 2: Enable Mods", "Do not forget to enable your mods in case they are not.")
            .Deprecated()
            .Register("General Settings", "Look through all of these settings before starting, they might help you a lot!\n\n"
              + "If you do not know what some of these do yet, return to this later!")
            .Register("Initial Setup, Step 3: Collections", "Collections are lists of settings for your installed mods.\n\n"
              + "This is our next stop!\n\n"
              + "Go here after setting up your root folder to continue the tutorial!")
            .Register("Initial Setup, Step 4: Managing Collections", "On the left, we have the collection selector. Here, we can create new collections - either empty ones or by duplicating existing ones - and delete any collections not needed anymore.\n"
              + $"There will always be one collection called {ModCollection.DefaultCollectionName} that can not be deleted.")
            .Register($"Initial Setup, Step 5: {SelectedCollection}",
                $"The {SelectedCollection} is the one we highlighted in the selector. It is the collection we are currently looking at and editing.\nAny changes we make in our mod settings later in the next tab will edit this collection.\n"
              + $"We should already have the collection named {ModCollection.DefaultCollectionName} selected, and for our simple setup, we do not need to do anything here.\n\n")
            .Register("Initial Setup, Step 6: Simple Assignments", "Aside from being a collection of settings, we can also assign collections to different functions. This is used to make different mods apply to different characters.\n"
              + "The Simple Assignments panel shows you the possible assignments that are enough for most people along with descriptions.\n"
              + $"If you are just starting, you can see that the {ModCollection.DefaultCollectionName} is currently assigned to {CollectionType.Default.ToName()} and {CollectionType.Interface.ToName()}.\n"
              + "You can also assign 'Use No Mods' instead of a collection by clicking on the function buttons.")
            .Register("Individual Assignments", "In the Individual Assignments panel, you can manually create assignments for very specific characters or monsters, not just yourself or ones you can currently target.")
            .Register("Group Assignments", "In the Group Assignments panel, you can create Assignments for more specific groups of characters based on race or age.")
            .Register("Collection Details", "In the Collection Details panel, you can see a detailed overview over the usage of the currently selected collection, as well as remove outdated mod settings and setup inheritance.\n"
              + "Inheritance can be used to make one collection take the settings of another as long as it does not setup the mod in question itself.")
            .Register("Incognito Mode", "This button can toggle Incognito Mode, which shortens all collection names to two letters and a number,\n"
              + "and all displayed individual character names to their initials and world, in case you want to share screenshots.\n"
              + "It is strongly recommended to not show your characters name in public screenshots when using Penumbra.")
            .Deprecated()
            .Register("Initial Setup, Step 7: Mods", "Our last stop is the Mods tab, where you can import and setup your mods.\n\n"
              + $"Please go there after verifying that your {SelectedCollection} and {DefaultCollection} are setup to your liking.")
            .Register("Initial Setup, Step 8: Mod Import",
                "Click this button to open a file selector with which to select TTMP mod files. You can select multiple at once.\n\n"
              + "It is not recommended to import huge mod packs of all your TexTools mods, but rather import the mods themselves, otherwise you lose out on a lot of Penumbra features!\n\n"
              + "A feature to import raw texture mods for Tattoos etc. is available under Advanced Editing, but is currently a work in progress.") // TODO
            .Register("Advanced Help", "Click this button to get detailed information on what you can do in the mod selector.\n\n"
              + "Import and select a mod now to continue.")
            .Register("Mod Filters", "You can filter the available mods by name, author, changed items or various attributes here.")
            .Register("Collection Selectors", $"This row provides shortcuts to set your {SelectedCollection}.\n\n"
              + $"The first button sets it to your {DefaultCollection} (if any).\n\n"
              + "The second button sets it to the collection the settings of the currently selected mod are inherited from (if any).\n\n"
              + "The third is a regular collection selector to let you choose among all your collections.")
            .Register("Redrawing",
                "Whenever you change your mod configuration, changes do not immediately take effect. You will need to force the game to reload the relevant files (or if this is not possible, restart the game).\n\n"
              + "For this, Penumbra has these buttons as well as the '/penumbra redraw' command, which redraws all actors at once. You can also use several modifiers described in the help marker instead.\n\n"
              + "Feel free to use these slash commands (e.g. '/penumbra redraw self') as a macro, too.")
            .Register("Initial Setup, Step 9: Enabling Mods",
                "Enable a mod here. Disabled mods will not apply to anything in the current collection.\n\n"
              + "Mods can be enabled or disabled in a collection, or they can be unconfigured, in which case they will use Inheritance.")
            .Register("Initial Setup, Step 10: Priority",
                "If two enabled mods in one collection change the same files, there is a conflict.\n\n"
              + "Conflicts can be solved by setting a priority. The mod with the higher number will be used for all the conflicting files.\n\n"
              + "Conflicts are not a problem, as long as they are correctly resolved with priorities. Negative priorities are possible.")
            .Register("Mod Options", "Many mods have options themselves. You can also choose those here.\n\n"
              + "Pulldown-options are mutually exclusive, whereas checkmark options can all be enabled separately.")
            .Register("Initial Setup - Fin", "Now you should have all information to get Penumbra running and working!\n\n"
              + "If there are further questions or you need more help for the advanced features, take a look at the guide linked in the settings page.")
            .Deprecated()
            .Register("FAQ 1",
                "It is advised to not use TexTools and Penumbra at the same time. Penumbra may refuse to work if TexTools broke your game indices.")
            .Register("FAQ 2", "Penumbra can change the skin material a mod uses. This is under advanced editing.")
            .Register("Favorites",
                "You can now toggle mods as favorites using this button. You can filter for favorited mods in the mod selector. Favorites are stored locally, not within the mod, but independently of collections.")
            .Register("Tags",
                "Mods can now have two types of tags:\n\n- Local Tags are those that you can set for yourself. They are stored locally and are not saved in any way in the mod directory itself.\n- Mod Tags are stored in the mod metadata, are set by the mod creator and are exported together with the mod, they can only be edited in the Edit Mod tab.\n\nIf a mod has a tag in its Mod Tags, this overwrites any identical Local Tags.\n\nYou can filter for tags in the mod selector via 't:text'.")
            .EnsureSize(Enum.GetValues<BasicTutorialSteps>().Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OpenTutorial(BasicTutorialSteps step)
        => _tutorial.Open((int)step, _config.TutorialStep, v =>
        {
            _config.TutorialStep = v;
            _config.Save();
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipTutorial(BasicTutorialSteps step)
        => _tutorial.Skip((int)step, _config.TutorialStep, v =>
        {
            _config.TutorialStep = v;
            _config.Save();
        });

    /// <summary> Update the current tutorial step if tutorials have changed since last update. </summary>
    public void UpdateTutorialStep()
    {
        var tutorial = _tutorial.CurrentEnabledId(_config.TutorialStep);
        if (tutorial != _config.TutorialStep)
        {
            _config.TutorialStep = tutorial;
            _config.Save();
        }
    }
}
