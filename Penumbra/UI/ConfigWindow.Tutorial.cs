using System;
using System.Runtime.CompilerServices;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    public const string SelectedCollection    = "Selected Collection";
    public const string DefaultCollection     = "Base Collection";
    public const string ActiveCollections     = "Active Collections";
    public const string GroupAssignment       = "Group Assignment";
    public const string CharacterGroups       = "Character Groups";
    public const string ConditionalGroup      = "Group";
    public const string ConditionalIndividual = "Character";
    public const string IndividualAssignments = "Individual Assignments";

    private static void UpdateTutorialStep()
    {
        var tutorial = Tutorial.CurrentEnabledId( Penumbra.Config.TutorialStep );
        if( tutorial != Penumbra.Config.TutorialStep )
        {
            Penumbra.Config.TutorialStep = tutorial;
            Penumbra.Config.Save();
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static void OpenTutorial( BasicTutorialSteps step )
        => Tutorial.Open( ( int )step, Penumbra.Config.TutorialStep, v =>
        {
            Penumbra.Config.TutorialStep = v;
            Penumbra.Config.Save();
        } );

    public enum BasicTutorialSteps
    {
        GeneralTooltips,
        ModDirectory,
        EnableMods,
        AdvancedSettings,
        GeneralSettings,
        Collections,
        EditingCollections,
        CurrentCollection,
        Inheritance,
        ActiveCollections,
        DefaultCollection,
        SpecialCollections1,
        SpecialCollections2,
        Mods,
        ModImport,
        AdvancedHelp,
        ModFilters,
        CollectionSelectors,
        EnablingMods,
        Priority,
        ModOptions,
        Fin,
        Faq1,
        Faq2,
        Faq3,
    }

    public static readonly Tutorial Tutorial = new Tutorial()
        {
            BorderColor    = Colors.TutorialBorder,
            HighlightColor = Colors.TutorialMarker,
            PopupLabel     = "Settings Tutorial",
        }
       .Register( "General Tooltips", "This symbol gives you further information about whatever setting it appears next to.\n\n"
          + "Hover over them when you are unsure what something does or how to do something." )
       .Register( "Initial Setup, Step 1: Mod Directory",
            "The first step is to set up your mod directory, which is where your mods are extracted to.\n\n"
          + "The mod directory should be a short path - like 'C:\\FFXIVMods' - on your fastest available drive. Faster drives improve performance.\n\n"
          + "The folder should be an empty folder no other applications write to." )
       .Register( "Initial Setup, Step 2: Enable Mods", "Do not forget to enable your mods in case they are not." )
       .Register( "Advanced Settings", "When you are just starting, you should leave this off.\n\n"
          + "If you need to do any editing of your mods, you will have to turn it on later." )
       .Register( "General Settings", "Look through all of these settings before starting, they might help you a lot!\n\n"
          + "If you do not know what some of these do yet, return to this later!" )
       .Register( "Initial Setup, Step 3: Collections", "Collections are lists of settings for your installed mods.\n\n"
          + "This is our next stop!\n\n"
          + "Go here after setting up your root folder to continue the tutorial!" )
       .Register( "Initial Setup, Step 4: Editing Collections", "First, we need to open the Collection Settings.\n\n"
          + "In here, we can create new collections, delete collections, or make them inherit from each other." )
       .Register( $"Initial Setup, Step 5: {SelectedCollection}",
            $"The {SelectedCollection} is the one we are currently editing. Any changes we make in our mod settings later in the next tab will edit this collection."
          + $"We should already have a collection named {ModCollection.DefaultCollection} selected, and for our simple setup, we do not need to do anything here.\n\n" )
       .Register( "Inheritance",
            "This is a more advanced feature. Click the help button for more information, but we will ignore this for now." )
       .Register( $"Initial Setup, Step 6: {ActiveCollections}",
            $"{ActiveCollections} are those that are actually assigned to conditions at the moment.\n\n"
          + "Any collection assigned here will apply to the game under certain conditions.\n\n"
          + $"The {SelectedCollection} is also active for technical reasons, while not necessarily being assigned to anything.\n\n"
          + "Open this now to continue." )
       .Register( $"Initial Setup, Step 7: {DefaultCollection}",
            $"The {DefaultCollection} - which should currently be set to a collection named {ModCollection.DefaultCollection} - is the main one.\n\n"
          + $"As long as no more specific conditions apply to an object in the game, the mods from the {DefaultCollection} will be used.\n\n"
          + "This is also the collection you need to use for all UI mods, music mods or any mods not associated with a character in the game at all." )
       .Register( GroupAssignment + 's',
            "Collections assigned here are used for groups of characters for which specific conditions are met.\n\n"
          + "The more specific the condition, the higher its priority (i.e. Your Character > Player Characters > Race).\n\n"
          + $"{IndividualAssignments} always take precedence before groups.")
       .Register( IndividualAssignments,
            "Collections assigned here are used only for individual characters or NPCs that have the specified name.\n\n"
          + "They may also apply to objects 'owned' by those characters, e.g. minions or mounts - see the general settings for options on this.\n\n" )
       .Register( "Initial Setup, Step 8: Mods", "Our last stop is the Mods tab, where you can import and setup your mods.\n\n"
          + $"Please go there after verifying that your {SelectedCollection} and {DefaultCollection} are setup to your liking." )
       .Register( "Initial Setup, Step 9: Mod Import",
            "Click this button to open a file selector with which to select TTMP mod files. You can select multiple at once.\n\n"
          + "It is not recommended to import huge mod packs of all your TexTools mods, but rather import the mods themselves, otherwise you lose out on a lot of Penumbra features!\n\n"
          + "A feature to import raw texture mods for Tattoos etc. is available under Advanced Editing, but is currently a work in progress." ) // TODO
       .Register( "Advanced Help", "Click this button to get detailed information on what you can do in the mod selector.\n\n"
          + "Import and select a mod now to continue." )
       .Register( "Mod Filters", "You can filter the available mods by name, author, changed items or various attributes here." )
       .Register( "Collection Selectors", $"This row provides shortcuts to set your {SelectedCollection}.\n\n"
          + $"The first button sets it to your {DefaultCollection} (if any).\n\n"
          + "The second button sets it to the collection the settings of the currently selected mod are inherited from (if any).\n\n"
          + "The third is a regular collection selector to let you choose among all your collections." )
       .Register( "Initial Setup, Step 11: Enabling Mods",
            "Enable a mod here. Disabled mods will not apply to anything in the current collection.\n\n"
          + "Mods can be enabled or disabled in a collection, or they can be unconfigured, in which case they will use Inheritance." )
       .Register( "Initial Setup, Step 12: Priority", "If two enabled mods in one collection change the same files, there is a conflict.\n\n"
          + "Conflicts can be solved by setting a priority. The mod with the higher number will be used for all the conflicting files.\n\n"
          + "Conflicts are not a problem, as long as they are correctly resolved with priorities. Negative priorities are possible." )
       .Register( "Mod Options", "Many mods have options themselves. You can also choose those here.\n\n"
          + "Pulldown-options are mutually exclusive, whereas checkmark options can all be enabled separately." )
       .Register( "Initial Setup - Fin", "Now you should have all information to get Penumbra running and working!\n\n"
          + "If there are further questions or you need more help for the advanced features, take a look at the guide linked in the settings page." )
       .Register( "FAQ 1", "Penumbra can not easily change which items a mod applies to." )
       .Register( "FAQ 2",
            "It is advised to not use TexTools and Penumbra at the same time. Penumbra may refuse to work if TexTools broke your game indices." )
       .Register( "FAQ 3", "Penumbra can change the skin material a mod uses. This is under advanced editing." )
       .EnsureSize( Enum.GetValues< BasicTutorialSteps >().Length );
}