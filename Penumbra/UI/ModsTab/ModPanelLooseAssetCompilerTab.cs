using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.DragDrop;
using FFXIVLooseTextureCompiler;
using FFXIVLooseTextureCompiler.Export;
using FFXIVLooseTextureCompiler.PathOrganization;
using FFXIVLooseTextureCompiler.Racial;
using ImGuiNET;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Widgets;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using TypingConnector;

namespace Penumbra.UI.ModsTab;

public class ModPanelLooseAssetCompilerTab : ITab
{
    public ReadOnlySpan<byte> Label
    => "Asset Compiler"u8;

    #region Variables
    private string _xNormalPath;
    private Configuration _config;
    private FileDialogService _fileDialog;
    private TextureProcessor _textureProcessor;
    private Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
    private bool _lockDuplicateGeneration;

    private List<TextureSet> _textureSets = new List<TextureSet>();

    private Dictionary<string, int> _groupOptionTypes = new Dictionary<string, int>();
    private List<string> _textureSetNames = new List<string>();
    private Color _originalDiffuseBoxColour;
    private Color _originalNormalBoxColour;
    private Color _originalMultiBoxColour;
    private ModManager _manager;
    private RedrawService _redrawService;
    private ModFileSystemSelector _selector;

    private string[] _choiceTypes;
    private string[] _bodyNames;
    private string[] _bodyNamesSimplified;
    private string[] _genders;
    private string[] _races;
    private string[] _subRaces;
    private string[] _faceTypes;
    private string[] _faceParts;
    private string[] _faceScales;
    private string[] _tails;
    private string[] _simpleModeNormalChoices;
    private string[] _faceExtraValues;


    private LTCFilePicker _diffuse;
    private LTCFilePicker _normal;
    private LTCFilePicker _multi;
    private LTCFilePicker _glow;
    private LTCFilePicker _mask;

    private LTCFilePicker _skin;
    private LTCFilePicker _face;
    private LTCFilePicker _eyes;

    private LTCComboBox _baseBodyList;
    private LTCComboBox _baseBodyListSimplified;
    private LTCComboBox _genderList;
    private LTCComboBox _raceList;
    private LTCComboBox _subRaceList;
    private LTCComboBox _faceTypeList;
    private LTCComboBox _facePartList;
    private LTCComboBox _faceExtraList;
    private LTCComboBox _auraFaceScalesDropdown;
    private LTCComboBox _tailList;
    private LTCComboBox _choiceTypeList;
    private LTCComboBox _simpleModeNormalComboBox;

    private LTCCheckBox _bakeNormals;
    private LTCCheckBox _generateMulti;
    private LTCCheckBox _asymCheckbox;
    private LTCCheckBox _uniqueAuRa;

    private LTCCustomPathConfigurator _customPathConfigurator;
    private LTCFindAndReplace _ltcFindAndReplace;
    private LTCTemplateConfigurator _ltcTemplateConfigurator;
    private LTCBulkNameReplacement _ltcBulkNameReplacement;


    private TextureSet _skinTextureSet;
    private TextureSet _faceTextureSet;
    private TextureSet _eyesTextureSet;

    private int _currentTextureSet = -1;
    private int _lastTextureSet = -1;

    private string _currentEditLabel;
    private int _lastRaceIndex;
    private bool _isSimpleMode;
    private Mod? _currentMod;
    private bool _editingInternalValues;
    private bool _bulkReplacingValues;
    private string _exportStatus = "";
    private bool _configuringTemplate;
    private bool _showOmniExportPrompt;
    private bool _addingCustomValues;
    private bool _bulkNameReplacingValue;
    private const int _textureSetLimit = 10510;

    #endregion
    public ModPanelLooseAssetCompilerTab(RedrawService redrawService, ModManager manager,
    ModFileSystemSelector selector, FileDialogService fileDialog, Configuration config, IDragDropManager dragDrop)
    {
        // This will be used for underlay textures.
        // The user will need to download a mod pack with the following path until there is a better way to aquire underlay assets.
        string underlayTexturePath = manager.BasePath.FullName + @"\LooseTextureCompilerDLC\";

        // This should reference the xNormal install no matter where its been installed.
        // If this path is not found xNormal reliant functions will be disabled until xNormal is installed.
        _xNormalPath = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\xNormal\3.19.3\xNormal (x64).lnk";

        #region Initialization
        _config = config;
        _fileDialog = fileDialog;
        _textureProcessor = new TextureProcessor(underlayTexturePath);
        _textureProcessor.OnStartedProcessing += TextureProcessor_OnStartedProcessing;
        _textureProcessor.OnLaunchedXnormal += TextureProcessor_OnLaunchedXnormal;

        _manager = manager;
        _redrawService = redrawService;
        _selector = selector;
        _choiceTypes = new string[] { "Detailed", "Simple", "Dropdown", "Group Is Checkbox" };
        _bodyNames = new string[] { "Vanilla and Gen2", "BIBO+", "EVE", "Gen3 and T&F3", "SCALES+", "TBSE and HRBODY", "TAIL", "Otopop" };
        _bodyNamesSimplified = new string[] { "BIBO+ Based", "Gen3 Based", "TBSE and HRBODY", "Otopop" };
        _genders = new string[] { "Masculine", "Feminine" };
        _races = new string[] { "Midlander", "Highlander", "Elezen", "Miqo'te", "Roegadyn", "Lalafell", "Raen", "Xaela", "Hrothgar", "Viera" };
        _subRaces = new string[] { "Midlander", "Highlander", "Wildwood", "Duskwight", "Seeker", "Keeper", "Sea Wolf", "Hellsguard",
        "Plainsfolk", "Dunesfolk", "Raen", "Xaela", "Helions", "The Lost", "Rava", "Veena" };
        _faceTypes = new string[] { "Face 1", "Face 2", "Face 3", "Face 4", "Face 5", "Face 6", "Face 7", "Face 8", "Face 9" };
        _faceParts = new string[] { "Face", "Eyebrows", "Eyes", "Ears", "Face Paint", "Hair", "Face B", "Etc B" };
        _faceScales = new string[] { "Vanilla Scales", "Scaleless Vanilla", "Scaleless Varied" };
        _tails = new string[8];
        _simpleModeNormalChoices = new string[] { "No Bumps On Skin", "Bumps On Skin", "Inverted Bumps On Skin" };
        _faceExtraValues = new string[999];

        for (int i = 0; i < _tails.Length; i++)
        {
            _tails[i] = (i + 1) + "";
        }
        for (int i = 0; i < _faceExtraValues.Length; i++)
        {
            _faceExtraValues[i] = (i + 1) + "";
        }

        _baseBodyList = new LTCComboBox("bodyTypeList", _bodyNames, 0, 150);
        _baseBodyList.OnSelectedIndexChanged += BaseBodyList_OnSelectedIndexChanged;
        _baseBodyList.SelectedIndex = 0;

        _baseBodyListSimplified = new LTCComboBox("bodyTypeListSimplified", _bodyNamesSimplified, 0, 150);
        _baseBodyListSimplified.OnSelectedIndexChanged += BaseBodyListSimplified_OnSelectedIndexChanged;
        _baseBodyListSimplified.SelectedIndex = 0;

        _genderList = new LTCComboBox("genderList", _genders, 0, 100);

        _raceList = new LTCComboBox("raceList", _races, 0, 100);
        _raceList.OnSelectedIndexChanged += RaceList_OnSelectedIndexChanged;

        _subRaceList = new LTCComboBox("subRaceList", _subRaces, 0, 100);
        _subRaceList.OnSelectedIndexChanged += SubRaceList_OnSelectedIndexChanged;
        _subRaceList.SelectedIndex = 0;

        _faceTypeList = new LTCComboBox("faceTypeList", _faceTypes, 0, 70);
        _faceTypeList.OnSelectedIndexChanged += FaceTypeList_OnSelectedIndexChanged;

        _facePartList = new LTCComboBox("facePartList", _faceParts, 0, 90);
        _facePartList.OnSelectedIndexChanged += FacePartList_OnSelectedIndexChanged;

        _auraFaceScalesDropdown = new LTCComboBox("faceScaleList", _faceScales, 0, 135);
        _auraFaceScalesDropdown.Enabled = false;

        _faceExtraList = new LTCComboBox("faceExtraList", _faceExtraValues, 0, 70);
        _faceExtraList.Enabled = false;

        _tailList = new LTCComboBox("tailList", _tails, 0, 62);
        _tailList.Enabled = false;

        _choiceTypeList = new LTCComboBox("choiceTypeList", _choiceTypes, 0, 135);

        _simpleModeNormalComboBox = new LTCComboBox("simpleModeNormalChoices", _simpleModeNormalChoices, 0, 230);
        _simpleModeNormalComboBox.OnSelectedIndexChanged += SimpleModeNormalComboBox_OnSelectedIndexChanged;

        _currentEditLabel = "Please highlight a texture set to start importing";

        _diffuse = new LTCFilePicker("Diffuse", _config, fileDialog, dragDrop);
        _normal = new LTCFilePicker("Normal", _config, fileDialog, dragDrop);
        _multi = new LTCFilePicker("Multi", _config, fileDialog, dragDrop);
        _glow = new LTCFilePicker("Glow", _config, fileDialog, dragDrop);
        _mask = new LTCFilePicker("Mask", _config, fileDialog, dragDrop);

        _skin = new LTCFilePicker("Skin", _config, fileDialog, dragDrop);
        _face = new LTCFilePicker("Face", _config, fileDialog, dragDrop);
        _eyes = new LTCFilePicker("Eyes", _config, fileDialog, dragDrop);

        _customPathConfigurator = new LTCCustomPathConfigurator(_choiceTypes);
        _customPathConfigurator.OnWantsToClose += CustomPathConfiguration_OnWantsToClose;

        _ltcFindAndReplace = new LTCFindAndReplace(_textureSets, config, fileDialog, dragDrop);
        _ltcFindAndReplace.OnWantsToClose += LtcFindAndReplace_OnWantsToClose;

        _ltcTemplateConfigurator = new LTCTemplateConfigurator(this, _textureSets);
        _ltcTemplateConfigurator.OnWantsToClose += LtcTemplateConfigurator_OnWantsToClose;

        _ltcBulkNameReplacement = new LTCBulkNameReplacement(_textureSets);
        _ltcBulkNameReplacement.OnWantsToClose += _ltcBulkNameReplacement_OnWantsToClose;

        _diffuse.OnFileSelected += TextureSelection_OnFileSelected;
        _normal.OnFileSelected += TextureSelection_OnFileSelected;
        _multi.OnFileSelected += TextureSelection_OnFileSelected;
        _glow.OnFileSelected += TextureSelection_OnFileSelected;
        _mask.OnFileSelected += TextureSelection_OnFileSelected;

        _skin.OnFileSelected += Skin_OnFileSelected;
        _face.OnFileSelected += Face_OnFileSelected;
        _eyes.OnFileSelected += Eyes_OnFileSelected;

        _diffuse.Enabled = false;
        _normal.Enabled = false;
        _multi.Enabled = false;
        _mask.Enabled = false;
        _glow.Enabled = false;

        _bakeNormals = new LTCCheckBox("Bake Normals", 90);
        _bakeNormals.OnCheckedChanged += BakeNormals_OnCheckedChanged;

        _generateMulti = new LTCCheckBox("Bake Multi", 70);
        _asymCheckbox = new LTCCheckBox("Asym", 50);
        _uniqueAuRa = new LTCCheckBox("Unique Au Ra", 90);
        _uniqueAuRa.Enabled = false;

        _originalDiffuseBoxColour = _diffuse.BackColor = Color.Pink;
        _originalNormalBoxColour = _normal.BackColor = Color.AliceBlue;
        _originalMultiBoxColour = _multi.BackColor = Color.Orange;
        #endregion
    }

    #region UI
    public void DrawContent()
    {
        if (!_lockDuplicateGeneration)
        {
            CheckForNewMod();
            DrawCoreWindow();
        }
        else
        {
            ProgressDisplay();
        }
    }

    private void DrawCoreWindow()
    {
        if (!_editingInternalValues && !_bulkReplacingValues
            && !_configuringTemplate && !_addingCustomValues
            && !_bulkNameReplacingValue)
        {
            WarningsAndDisclaimers();
            if (!_isSimpleMode)
            {
                AdvancedMode();
            }
            else
            {
                SimpleMode();
            }
        }
        else
        {
            ConfiguratorWindows();
        }
    }

    private void SimpleMode()
    {
        _baseBodyListSimplified.Draw();
        ImGui.SameLine();
        _subRaceList.Draw();
        ImGui.SameLine();
        _faceTypeList.Draw();

        _skin.Draw();
        _face.Draw();
        _eyes.Draw();

        ImGui.SetNextItemWidth(80);
        ImGui.LabelText("##skinBumpsLabel", "Skin Bumps");
        ImGui.SameLine();
        _simpleModeNormalComboBox.Draw();
        if (ImGui.Button("Preview (For quick edits)"))
        {
            Task.Run(() => Export(false));
        }

        ImGui.SameLine();

        if (ImGui.Button("Finalize (To finish mod"))
        {
            Task.Run(() => Export(true));
        }
    }

    private void AdvancedMode()
    {
        BodySelection();
        FaceSelection();
        TextureSetManagement();
        ExportSettings();
    }

    private void MenuBar()
    {
        TemplateButtons();
    }

    private void WarningsAndDisclaimers()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - 180);
        ImGui.LabelText("##disclaimer", "This tab will overwrite any previously autogenerated assets.");
        ImGui.SameLine();
        if (ImGui.Button(_isSimpleMode ? "Enable Advanced Mode" : "Enable Simple Mode", new Vector2(170, 20)))
        {
            if (!_isSimpleMode)
            {
                if (_textureSets.Count == 3 || _textureSets.Count == 0)
                {
                    SimpleModeSwitch();
                }
                else
                {
                    Penumbra.Messager.NotificationMessage("This project is too complex to switch to simple mode",
                    Dalamud.Interface.Internal.Notifications.NotificationType.Error);
                }
            }
            else
            {
                _isSimpleMode = false;
            }
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - 410);
        if (!Directory.Exists(_textureProcessor.BasePath))
        {
            ImGui.LabelText("##dlcInstall", "Loose Texture Compiler DLC is not installed. Underlay support will not function.");
            ImGui.SameLine();
            if (ImGui.Button("Download DLC"))
            {
                looseTextureDLCDownload();
            }
        }
        if (!File.Exists(_xNormalPath) || IsRunningUnderWine())
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - 540);
            ImGui.LabelText("##xNormalInstall", IsRunningUnderWine() ?
            "Universal mode is not supported on Wine. Universal options will be unavailable" :
            "xNormal is not installed. Universal options will be unavailable");
            ImGui.SameLine();
            if (!IsRunningUnderWine())
            {
                if (ImGui.Button("Download xNormal"))
                {
                    xNormalDownload();
                }
            }
        }
    }

    private void xNormalDownload()
    {
        try
        {
            Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "https://xnormal.net/",
                UseShellExecute = true,
                Verb = "OPEN"
            });
        }
        catch
        {

        }
    }

    private void looseTextureDLCDownload()
    {
        try
        {
            Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "https://drive.google.com/uc?id=1qUExzMshqVovo2cP5jkSpkkcC4pLpfHs",
                UseShellExecute = true,
                Verb = "OPEN"
            });
        }
        catch
        {

        }
    }

    private void ConfiguratorWindows()
    {
        if (_editingInternalValues || _addingCustomValues)
        {
            _customPathConfigurator.Draw();
        }
        else if (_bulkReplacingValues)
        {
            _ltcFindAndReplace?.Draw();
        }
        else if (_configuringTemplate)
        {
            _ltcTemplateConfigurator?.Draw();
        }
        else if (_bulkNameReplacingValue)
        {
            _ltcBulkNameReplacement?.Draw();
        }
    }

    private void ProgressDisplay()
    {
        ImGui.LabelText("##progressLabel", "This tab is unavailable until the previous operation completes.");
        ImGui.LabelText("##progressStatus", _exportStatus + " " + _currentMod.Name);
        ImGui.ProgressBar((float)_textureProcessor.ExportCompletion / (float)_textureProcessor.ExportMax,
            new System.Numerics.Vector2(ImGui.GetContentRegionMax().X, 20));
    }

    private void CheckForNewMod()
    {
        if (!_lockDuplicateGeneration)
        {
            if (_selector.Selected != _currentMod)
            {
                string projectPath = "";
                if (_currentMod != null && _textureSets.Count > 0)
                {
                    if (Directory.Exists(_currentMod.ModPath.FullName))
                    {
                        projectPath = Path.Combine(_manager.BasePath.FullName,
                            (_currentMod.Name.Text.Replace("/", null).Replace(@"\", null)) + ".ffxivtp");
                        SaveProject(projectPath);
                    }
                }
                if (_selector.Selected != null)
                {
                    projectPath = Path.Combine(_manager.BasePath.FullName,
                        _selector.Selected.Name.Text.Replace("/", null).Replace(@"\", null) + ".ffxivtp");
                    NewProject();
                    ExitConfigurators();
                    if (File.Exists(projectPath))
                    {
                        OpenProject(projectPath);
                    }
                    _currentMod = _selector.Selected;
                }
            }
        }
    }

    private void ExitConfigurators()
    {
        _editingInternalValues = false;
        _bulkReplacingValues = false;
        _configuringTemplate = false;
        _addingCustomValues = false;
        _bulkNameReplacingValue = false;
    }

    private void ExportSettings()
    {
        ImGui.SetNextItemWidth(80);
        ImGui.LabelText("##choiceTypeLabel", "Choice Type");
        ImGui.SameLine();
        _choiceTypeList.Draw();
        ImGui.SameLine();

        _bakeNormals.Draw();
        ImGui.SameLine();

        _generateMulti.Draw();
        ImGui.SameLine();
        if (ImGui.Button("Preview"))
        {
            Task.Run(() => Export(false));
        }
        ImGui.SameLine();
        if (ImGui.Button("Finalize"))
        {
            Task.Run(() => Export(true));
        }
    }

    private void TextureSetManagement()
    {
        _textureSetNames.Clear();
        foreach (TextureSet textureSet in _textureSets)
        {
            _textureSetNames.Add(textureSet.ToString());
        }
        ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionMax().X - 105);
        ImGui.LabelText("##textureSetsLabel", "Texture Sets");
        ImGui.SameLine();

        if (ImGui.Button("Custom Path"))
        {
            OnCustomPath();
        }
        ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionMax().X - 10);
        ImGui.ListBox("##textureSets", ref _currentTextureSet, _textureSetNames.ToArray(), _textureSetNames.Count, 6);

        if (_currentTextureSet != _lastTextureSet)
        {
            SelectedIndexChanged();
        }

        _lastTextureSet = _currentTextureSet;

        TextureSetContextMenu();
        TextureSetManagementButtons();
        ImGui.SameLine();
        MenuBar();
        TextureSetManagementFileEntries();
    }

    private void TextureSetManagementFileEntries()
    {
        ImGui.LabelText("##currentEditLabelText" + _currentEditLabel, _currentEditLabel);

        _diffuse.Draw();
        _normal.Draw();
        _multi.Draw();
        _glow.Draw();
        _mask.Draw();
    }

    private void TextureSetManagementButtons()
    {
        if (!_config.DeleteModModifier.IsActive())
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("Delete"))
        {
            OnRemoveSelection();
        }

        ImGui.SameLine();

        if (ImGui.Button("Clear List"))
        {
            OnClearList();
        }
        if (!_config.DeleteModModifier.IsActive())
        {
            ImGui.EndDisabled();
        }

        ImGui.SameLine();

        if (ImGui.Button("Move Up"))
        {
            OnMoveUp();
        }

        ImGui.SameLine();

        if (ImGui.Button("Move Down"))
        {
            OnMoveDown();
        }

    }

    private void OnCustomPath()
    {
        if (_textureSets.Count < _textureSetLimit)
        {

            _customPathConfigurator.TextureSet = new TextureSet();
            _addingCustomValues = true;
        }
        else
        {
            Penumbra.Messager.NotificationMessage("You have hit the cap of " + _textureSetLimit + " texture sets",
                Dalamud.Interface.Internal.Notifications.NotificationType.Error);
        }
    }

    private void TextureSetContextMenu()
    {
        if (_currentTextureSet > -1)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.IsItemHovered())
            {
                ImGui.OpenPopup("##textureSetContextMenu");
            }
            if (ImGui.BeginPopup("##textureSetContextMenu"))
            {
                if (ImGui.MenuItem("Edit Internal Values##context"))
                {
                    OnEditInternalValues();
                }
                if (File.Exists(_xNormalPath))
                {
                    if (ImGui.MenuItem(!_textureSets[_currentTextureSet].OmniExportMode
                ? "Enable Universal Mode##context" : "Disable Universal Mode##context"))
                    {
                        ToggleUniversalModeOnCurrentTextureSet();
                    }
                }
                if (ImGui.MenuItem("Bulk Name Replacement##context"))
                {
                    OnBulkReplaceName();
                }
                if (ImGui.MenuItem("Bulk Replace Values##context"))
                {
                    OnBulkReplace();
                }

                if (ImGui.MenuItem("Move Up##context"))
                {
                    OnMoveUp();
                }

                if (ImGui.MenuItem("Move Down##context"))
                {
                    OnMoveDown();
                }
                if (ImGui.MenuItem("Duplicate##context"))
                {
                    OnDuplicateSelection();
                }
                if (!_config.DeleteModModifier.IsActive())
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.MenuItem("Delete##context"))
                {
                    OnRemoveSelection();
                }
                if (!_config.DeleteModModifier.IsActive())
                {
                    ImGui.EndDisabled();
                }
                ImGui.EndPopup();
            }
        }
        if (_showOmniExportPrompt)
        {
            ImGui.OpenPopup("UniversalModeNotice");
            _showOmniExportPrompt = false;
        }
        UniversalModeDialog();
    }

    private void OnDuplicateSelection()
    {
        TextureSet newTextureSet = new TextureSet();
        TextureSet textureSet = _textureSets[_currentTextureSet];
        newTextureSet.Diffuse = textureSet.Diffuse;
        newTextureSet.Normal = textureSet.Normal;
        newTextureSet.Multi = textureSet.Multi;
        newTextureSet.Glow = newTextureSet.Glow;
        newTextureSet.NormalCorrection = newTextureSet.NormalCorrection;
        newTextureSet.InternalDiffusePath = textureSet.InternalDiffusePath;
        newTextureSet.InternalNormalPath = textureSet.InternalNormalPath;
        newTextureSet.InternalMultiPath = textureSet.InternalMultiPath;
        newTextureSet.BackupTexturePaths = textureSet.BackupTexturePaths;
        newTextureSet.ChildSets = textureSet.ChildSets;
        newTextureSet.GroupName = textureSet.GroupName;
        newTextureSet.TextureSetName = textureSet.GroupName;
        newTextureSet.InvertNormalGeneration = textureSet.InvertNormalGeneration;
        newTextureSet.IgnoreNormalGeneration = textureSet.IgnoreNormalGeneration;
        newTextureSet.IgnoreMultiGeneration = textureSet.IgnoreMultiGeneration;
        newTextureSet.OmniExportMode = textureSet.OmniExportMode;
        _textureSets.Add(newTextureSet);
       _currentTextureSet = _textureSets.Count - 1;
    }

    private void OnBulkReplaceName()
    {
        _bulkNameReplacingValue = true;
    }

    private void TemplateButtons()
    {
        ImGui.Dummy(new System.Numerics.Vector2(ImGui.GetContentRegionMax().X - 510, 5));
        ImGui.SameLine();
        if (ImGui.Button("Templates"))
        {
            ImGui.OpenPopup("##templateContextMenu");
        }
        if (ImGui.BeginPopup("##templateContextMenu"))
        {
            if (ImGui.MenuItem("Import Template##context"))
            {
                _fileDialog.OpenFilePicker("Import Template",
                  "Template Project {.ffxivtp}", (s, f) =>
                  {
                      if (!s)
                      {
                          return;
                      }
                      OpenTemplate(f[0]);
                  }, 1, "", false);
            }
            if (Directory.Exists(_textureProcessor.BasePath))
            {
                foreach (string item in Directory.EnumerateFiles(_textureProcessor.BasePath + @"\res\templates"))
                {
                    if (item.EndsWith(".ffxivtp"))
                    {
                        if (ImGui.MenuItem(Path.GetFileNameWithoutExtension(item) + "##context"))
                        {
                            OpenTemplate(item);
                        }
                    }
                }
            }
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Export Template"))
        {
            _fileDialog.OpenSavePicker("Save Template", "Template Project{.ffxivtp}",
                  "New template.ffxivtp", "{.ffxivtp}", (s, f) =>
                  {
                      if (!s)
                      {
                          return;
                      }
                      SaveProject(f.Replace(".ffxivtp", "") + ".ffxivtp");
                  }, "", false);
        }
    }

    private void OnEditInternalValues()
    {
        _editingInternalValues = true;
        _customPathConfigurator.TextureSet = _textureSets[_currentTextureSet];
        _customPathConfigurator.GroupingType.SelectedIndex = (
           _groupOptionTypes.ContainsKey(_customPathConfigurator.TextureSet.GroupName) ?
           _groupOptionTypes[_customPathConfigurator.TextureSet.GroupName] : 0);
    }

    private void OnBulkReplace()
    {
        if (_currentTextureSet > -1)
        {
            TextureSet sourceTextureSet = _textureSets[_currentTextureSet];
            Tokenizer tokenizer = new Tokenizer(sourceTextureSet.TextureSetName);
            _ltcFindAndReplace.TextureSetSearchString = tokenizer.GetToken();
            _ltcFindAndReplace.GroupSearchString = sourceTextureSet.GroupName
                != sourceTextureSet.TextureSetName ? sourceTextureSet.GroupName : "";
            _ltcFindAndReplace.Diffuse.CurrentPath = _diffuse.CurrentPath;
            _ltcFindAndReplace.Normal.CurrentPath = _normal.CurrentPath;
            _ltcFindAndReplace.Multi.CurrentPath = _multi.CurrentPath;
            _ltcFindAndReplace.Mask.CurrentPath = _mask.CurrentPath;
            _ltcFindAndReplace.Glow.CurrentPath = _glow.CurrentPath;
            _ltcFindAndReplace.IsForEyes = sourceTextureSet.InternalMultiPath.ToLower().Contains("catchlight");
            _bulkReplacingValues = true;
        }
    }

    private void OnMoveDown()
    {
        if (_currentTextureSet + 1 < _textureSets.Count && _currentTextureSet != -1)
        {
            TextureSet object1 = _textureSets[_currentTextureSet + 1];
            TextureSet object2 = _textureSets[_currentTextureSet];

            _textureSets[_currentTextureSet] = object1;
            _textureSets[_currentTextureSet + 1] = object2;
            _currentTextureSet += 1;
        }
    }

    private void OnMoveUp()
    {
        if (_currentTextureSet > 0)
        {
            TextureSet object1 = _textureSets[_currentTextureSet - 1];
            TextureSet object2 = _textureSets[_currentTextureSet];

            _textureSets[_currentTextureSet] = object1;
            _textureSets[_currentTextureSet - 1] = object2;
            _currentTextureSet -= 1;
        }
    }

    private void OnClearList()
    {
        _textureSets.Clear();
        _diffuse.CurrentPath = "";
        _normal.CurrentPath = "";
        _multi.CurrentPath = "";
        _mask.CurrentPath = "";
        _glow.CurrentPath = "";

        _diffuse.Enabled = false;
        _normal.Enabled = false;
        _multi.Enabled = false;
        _mask.Enabled = false;
        _glow.Enabled = false;

        _currentEditLabel = "Please highlight a texture set to start importing";

        _currentTextureSet = -1;
    }

    private void OnRemoveSelection()
    {
        if (_currentTextureSet > -1)
        {
            _textureSets.RemoveAt(_currentTextureSet);
            _diffuse.CurrentPath = "";
            _normal.CurrentPath = "";
            _multi.CurrentPath = "";
            _glow.CurrentPath = "";
            _currentEditLabel = "Please highlight a texture set to start importing";

            _currentTextureSet = -1;
        }
    }

    private void FaceSelection()
    {
        #region Face Selection
        if (_subRaceList.Enabled)
        {
            _subRaceList.Draw();
            ImGui.SameLine();
        }

        if (_faceTypeList.Enabled)
        {
            _faceTypeList.Draw();
            ImGui.SameLine();
        }

        _facePartList.Draw();
        ImGui.SameLine();

        _faceExtraList.Draw();
        ImGui.SameLine();

        _auraFaceScalesDropdown.Draw();
        ImGui.SameLine();

        _asymCheckbox.Draw();
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(ImGui.GetWindowContentRegionMax().X -
            _subRaceList.Width - _faceTypeList.Width -
            _facePartList.Width - _faceExtraList.Width -
            _auraFaceScalesDropdown.Width - _asymCheckbox.Width - 100, 1));
        ImGui.SameLine();
        if (ImGui.Button("Add Face"))
        {
            AddFace();
        }
        #endregion
    }

    private void BodySelection()
    {
        #region Body Selection
        _baseBodyList.Draw();
        ImGui.SameLine();

        _genderList.Draw();
        ImGui.SameLine();

        _raceList.Draw();
        ImGui.SameLine();

        _tailList.Draw();
        ImGui.SameLine();

        _uniqueAuRa.Draw();
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(ImGui.GetWindowContentRegionMax().X -
          _baseBodyList.Width - _genderList.Width -
          _raceList.Width - _tailList.Width -
          _uniqueAuRa.Width - 110, 1));
        ImGui.SameLine();
        if (ImGui.Button("Add Body"))
        {
            AddBody();
        }
        #endregion
    }

    private void SelectedIndexChanged()
    {
        if (_currentTextureSet == -1)
        {
            _currentEditLabel = "Please highlight a texture set to start importing";
            SetControlsEnabled(false);
        }
        else
        {
            TextureSet textureSet = _textureSets[_currentTextureSet];
            _currentEditLabel = "Editing: " + textureSet.TextureSetName;
            SetControlsEnabled(true, textureSet);
            SetControlsPaths(textureSet);
            SetControlsColors(textureSet);
        }
    }

    private void SetControlsEnabled(bool enabled, TextureSet textureSet = null)
    {
        if (textureSet == null)
        {
            enabled = false;
        }
        _diffuse.Enabled = enabled && !string.IsNullOrEmpty(textureSet.InternalDiffusePath);
        _normal.Enabled = enabled && !string.IsNullOrEmpty(textureSet.InternalNormalPath);
        _multi.Enabled = enabled && !string.IsNullOrEmpty(textureSet.InternalMultiPath);
        _mask.Enabled = enabled && _bakeNormals.Checked;
        _glow.Enabled = enabled && !textureSet.TextureSetName.ToLower().Contains("face paint")
                && !textureSet.TextureSetName.ToLower().Contains("hair") && _diffuse.Enabled;
    }

    private void SetControlsPaths(TextureSet textureSet)
    {
        _diffuse.CurrentPath = textureSet.Diffuse;
        _normal.CurrentPath = textureSet.Normal;
        _multi.CurrentPath = textureSet.Multi;
        _mask.CurrentPath = textureSet.NormalMask;
        _glow.CurrentPath = textureSet.Glow;
    }

    private void SetControlsColors(TextureSet textureSet)
    {
        if (textureSet.InternalMultiPath != null && textureSet.InternalMultiPath.ToLower().Contains("catchlight"))
        {
            _diffuse.LabelName = "Normal";
            _normal.LabelName = "Multi";
            _multi.LabelName = "Catchlight";
            _diffuse.BackColor = _originalNormalBoxColour;
            _normal.BackColor = Color.Lavender;
            _multi.BackColor = Color.LightGray;
        }
        else
        {
            _diffuse.LabelName = "Diffuse";
            _normal.LabelName = "Normal";
            _multi.LabelName = "Multi";
            _diffuse.BackColor = _originalDiffuseBoxColour;
            _normal.BackColor = _originalNormalBoxColour;
            _multi.BackColor = _originalMultiBoxColour;
        }
    }
    #endregion
    #region Event Callbacks
    private void SimpleModeNormalComboBox_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        switch (_simpleModeNormalComboBox.SelectedIndex)
        {
            case 0:
                _bakeNormals.Checked = false;
                _skinTextureSet.InvertNormalGeneration = false;
                _faceTextureSet.InvertNormalGeneration = false;
                break;
            case 1:
                _bakeNormals.Checked = true;
                _skinTextureSet.InvertNormalGeneration = false;
                _faceTextureSet.InvertNormalGeneration = false;
                break;
            case 2:
                _bakeNormals.Checked = true;
                _skinTextureSet.InvertNormalGeneration = true;
                _faceTextureSet.InvertNormalGeneration = true;
                break;
        }
        SaveState();
    }

    private void Eyes_OnFileSelected(object? sender, EventArgs e)
    {
        _eyesTextureSet.Normal = _eyes.FilePath;
        AddWatcher(_eyesTextureSet.Diffuse);
        SaveState();
    }

    private void Face_OnFileSelected(object? sender, EventArgs e)
    {
        _faceTextureSet.Diffuse = _face.FilePath;
        AddWatcher(_faceTextureSet.Diffuse);
        SaveState();
    }

    private void Skin_OnFileSelected(object? sender, EventArgs e)
    {
        _skinTextureSet.Diffuse = _skin.FilePath;
        AddWatcher(_skinTextureSet.Diffuse);
        SaveState();
    }

    private void FaceTypeList_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isSimpleMode)
        {
            _facePartList.SelectedIndex = 0;
            AddFacePaths(_faceTextureSet);
            _facePartList.SelectedIndex = 2;
            AddEyePaths(_eyesTextureSet);
            SaveState();
        }
    }

    private void BaseBodyListSimplified_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        switch (_baseBodyListSimplified.SelectedIndex)
        {
            case 0:
                _baseBodyList.SelectedIndex = 1;
                break;
            case 1:
                _baseBodyList.SelectedIndex = 3;
                break;
            case 2:
                _baseBodyList.SelectedIndex = 5;
                break;
            case 3:
                _baseBodyList.SelectedIndex = 7;
                break;
        }
        AddBodyPaths(_skinTextureSet);
        SaveState();
    }
    private void _ltcBulkNameReplacement_OnWantsToClose(object? sender, EventArgs e)
    {
        _bulkNameReplacingValue = false;
    }
    private void LtcTemplateConfigurator_OnWantsToClose(object? sender, EventArgs e)
    {
        _configuringTemplate = false;
    }

    private void TextureProcessor_OnLaunchedXnormal(object? sender, EventArgs e)
    {
        _exportStatus = "Waiting For XNormal To Generate Assets For";
    }

    private void TextureProcessor_OnStartedProcessing(object? sender, EventArgs e)
    {
        _exportStatus = "Compiling Assets For";
    }

    private void LtcFindAndReplace_OnWantsToClose(object? sender, EventArgs e)
    {
        if (_ltcFindAndReplace.WasValidated)
        {
            foreach (TextureSet textureSet in _textureSets)
            {
                AddWatcher(textureSet.Diffuse);
                AddWatcher(textureSet.Normal);
                AddWatcher(textureSet.Multi);
                AddWatcher(textureSet.NormalMask);
                AddWatcher(textureSet.Glow);
            }
            _currentTextureSet = -1;
            _ltcFindAndReplace.WasValidated = false;
        }
        _bulkReplacingValues = false;
    }

    private void CustomPathConfiguration_OnWantsToClose(object? sender, EventArgs e)
    {
        if (_customPathConfigurator.WasValidated)
        {
            _groupOptionTypes[_customPathConfigurator.TextureSet.GroupName] = _customPathConfigurator.GroupingType.SelectedIndex;
            _customPathConfigurator.WasValidated = false;
        }
        if (_editingInternalValues)
        {
            _editingInternalValues = false;
        }
        if (_addingCustomValues)
        {
            _textureSets.Add(_customPathConfigurator.TextureSet);
            _addingCustomValues = false;
            SaveState();
        }
    }

    private void TextureSelection_OnFileSelected(object? sender, EventArgs e)
    {
        SetPaths();
        SaveState();
    }

    private void BakeNormals_OnCheckedChanged(object? sender, EventArgs e)
    {
        _mask.Enabled = _bakeNormals.Checked && _currentTextureSet > -1;
    }

    private void FacePartList_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_facePartList.SelectedIndex == 4)
        {
            _auraFaceScalesDropdown.Enabled = _asymCheckbox.Enabled = _faceTypeList.Enabled = _subRaceList.Enabled = false;
            _faceExtraList.Enabled = true;
        }
        else if (_facePartList.SelectedIndex == 5)
        {
            _auraFaceScalesDropdown.Enabled = false;
            _asymCheckbox.Enabled = _faceTypeList.Enabled;
            _faceExtraList.Enabled = true;
        }
        else
        {
            _asymCheckbox.Enabled = _faceTypeList.Enabled = _subRaceList.Enabled = true;
            if (_subRaceList.SelectedIndex == 10 || _subRaceList.SelectedIndex == 11)
            {
                _auraFaceScalesDropdown.Enabled = true;
            }
            _faceExtraList.Enabled = false;
        }
    }

    private void SubRaceList_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_subRaceList.SelectedIndex == 10 || _subRaceList.SelectedIndex == 11)
        {
            _auraFaceScalesDropdown.Enabled = true;
        }
        else
        {
            _auraFaceScalesDropdown.Enabled = false;
        }
        _raceList.SelectedIndex = RaceInfo.SubRaceToMainRace(_subRaceList.SelectedIndex);
    }

    private void RaceList_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_baseBodyList.SelectedIndex == 6)
        {
            if (_raceList.SelectedIndex != 3 && _raceList.SelectedIndex != 6 && _raceList.SelectedIndex != 7)
            {
                _raceList.SelectedIndex = 3;
                Penumbra.Messager.NotificationMessage("Tail is only compatible with Miqo'te Xaela, and Raen", Dalamud.Interface.Internal.Notifications.NotificationType.Error);
            }
        }
        else if (_baseBodyList.SelectedIndex == 4)
        {
            if (_raceList.SelectedIndex != 6 && _raceList.SelectedIndex != 7)
            {
                _raceList.SelectedIndex = 6;
                Penumbra.Messager.NotificationMessage("Scales+ is only compatible with Xaela, and Raen", Dalamud.Interface.Internal.Notifications.NotificationType.Error);
            }
        }
        else if (_baseBodyList.SelectedIndex > 0 && _baseBodyList.SelectedIndex < 7)
        {
            if (_raceList.SelectedIndex == 5)
            {
                _raceList.SelectedIndex = _lastRaceIndex;
                Penumbra.Messager.NotificationMessage("Lalafells are not compatible with the selected body", Dalamud.Interface.Internal.Notifications.NotificationType.Error);
            }
        }
        else if (_baseBodyList.SelectedIndex > 6)
        {
            if (_raceList.SelectedIndex != 5)
            {
                _raceList.SelectedIndex = 5;
                Penumbra.Messager.NotificationMessage("Only Lalafells are compatible with the selected body", Dalamud.Interface.Internal.Notifications.NotificationType.Error);
            }
        }
        _lastRaceIndex = _raceList.SelectedIndex;
    }

    private void BaseBodyList_OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        switch (_baseBodyList.SelectedIndex)
        {
            case 0:
                _genderList.Enabled = true;
                _tailList.Enabled = false;
                _uniqueAuRa.Enabled = false;
                break;
            case 1:
            case 2:
            case 3:
                _genderList.SelectedIndex = 1;
                _genderList.Enabled = false;
                _tailList.Enabled = false;
                if (_raceList.SelectedIndex == 5)
                {
                    _raceList.SelectedIndex = 0;
                }
                _uniqueAuRa.Enabled = false;
                break;
            case 4:
                _raceList.SelectedIndex = 6;
                _genderList.SelectedIndex = 1;
                _genderList.Enabled = false;
                _tailList.Enabled = false;
                _uniqueAuRa.Enabled = false;
                break;
            case 5:
                _genderList.SelectedIndex = 0;
                _genderList.Enabled = false;
                _tailList.Enabled = false;
                if (_raceList.SelectedIndex == 5)
                {
                    _raceList.SelectedIndex = 0;
                }
                _uniqueAuRa.Enabled = true;
                break;
            case 6:
                _raceList.SelectedIndex = 6;
                _genderList.Enabled = true;
                _tailList.Enabled = true;
                _uniqueAuRa.Enabled = false;
                break;
            case 7:
                _genderList.Enabled = false;
                _raceList.SelectedIndex = 5;
                _tailList.Enabled = false;
                break;
        }
    }
    #endregion
    #region File Persistence
    private void NewProject()
    {
        _lockDuplicateGeneration = true;
        _textureSets.Clear();
        _diffuse.CurrentPath = "";
        _normal.CurrentPath = "";
        _multi.CurrentPath = "";
        _glow.CurrentPath = "";

        _diffuse.Enabled = false;
        _normal.Enabled = false;
        _multi.Enabled = false;
        _mask.Enabled = false;
        _glow.Enabled = false;

        _currentTextureSet = -1;

        foreach (FileSystemWatcher watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
        _currentEditLabel = "Please highlight a texture set to start importing";
        _lockDuplicateGeneration = false;
        _isSimpleMode = false;
    }
    private void OpenProject(string path)
    {
        using (StreamReader file = File.OpenText(path))
        {
            JsonSerializer serializer = new JsonSerializer();
            ProjectFile projectFile = (ProjectFile)serializer.Deserialize(file, typeof(ProjectFile));
            _textureSets.Clear();
            _choiceTypeList.SelectedIndex = projectFile.ExportType;
            _bakeNormals.Checked = projectFile.BakeMissingNormals;
            _generateMulti.Checked = projectFile.GenerateMulti;
            if (projectFile.GroupOptionTypes != null)
            {
                _groupOptionTypes = projectFile.GroupOptionTypes;
            }
            _textureSets.AddRange(projectFile.TextureSets?.ToArray());
            if (projectFile.SimpleMode)
            {
                SimpleModeSwitch();
            }
            else
            {
                _isSimpleMode = false;
            }
            _baseBodyList.SelectedIndex = projectFile.SimpleBodyType;
            _faceTypeList.SelectedIndex = projectFile.SimpleFaceType;
            _subRaceList.SelectedIndex = projectFile.SimpleSubRaceType;
            _simpleModeNormalComboBox.SelectedIndex = projectFile.SimpleNormalGeneration;
            foreach (TextureSet textureSet in projectFile.TextureSets)
            {
                AddWatcher(textureSet.Diffuse);
                AddWatcher(textureSet.Normal);
                AddWatcher(textureSet.Multi);
                AddWatcher(textureSet.NormalMask);
                AddWatcher(textureSet.Glow);
                BackupTexturePaths.AddBackupPaths(_genderList.SelectedIndex, _raceList.SelectedIndex, textureSet);
            }
        }
    }

    private void OpenTemplate(string path)
    {
        _configuringTemplate = true;
        _ltcTemplateConfigurator.OpenTemplate(path, _genderList.SelectedIndex, _raceList.SelectedIndex);
        SaveState();
    }
    private void SaveProject(string path)
    {
        using (StreamWriter writer = new StreamWriter(path))
        {
            JsonSerializer serializer = new JsonSerializer();
            ProjectFile projectFile = new ProjectFile();
            projectFile.Name = _currentMod.Name;
            projectFile.Author = _currentMod.Author;
            projectFile.Version = _currentMod.Description;
            projectFile.Description = _currentMod.Description;
            projectFile.Website = _currentMod.Website;
            projectFile.GroupOptionTypes = _groupOptionTypes;
            projectFile.TextureSets = new List<TextureSet>();
            projectFile.ExportType = _choiceTypeList.SelectedIndex;
            projectFile.BakeMissingNormals = _bakeNormals.Checked;
            projectFile.GenerateMulti = _generateMulti.Checked;
            projectFile.SimpleMode = _isSimpleMode;

            projectFile.SimpleBodyType = _baseBodyList.SelectedIndex;
            projectFile.SimpleFaceType = _faceTypeList.SelectedIndex;
            projectFile.SimpleSubRaceType = _subRaceList.SelectedIndex;
            projectFile.SimpleNormalGeneration = _simpleModeNormalComboBox.SelectedIndex;
            foreach (TextureSet textureSet in _textureSets)
            {
                projectFile.TextureSets.Add(textureSet);
            }
            serializer.Serialize(writer, projectFile);
        }
        //Penumbra.Messager.NotificationMessage("Save successfull", "Changes Saved");
    }
    private void SaveState()
    {
        string projectPath = Path.Combine(_manager.BasePath.FullName, _currentMod.Name + ".ffxivtp");
        SaveProject(projectPath);
    }
    #endregion
    #region Texture Set Management
    public void SimpleModeSwitch()
    {
        _isSimpleMode = true;
        _baseBodyList.SelectedIndex = 1;
        _simpleModeNormalComboBox.SelectedIndex = 0;
        if (_textureSets.Count == 0)
        {
            _skinTextureSet = new TextureSet();
            _faceTextureSet = new TextureSet();
            _eyesTextureSet = new TextureSet();
            _textureSets.Add(_skinTextureSet);
            _textureSets.Add(_faceTextureSet);
            _textureSets.Add(_eyesTextureSet);
        }
        else if (_textureSets.Count == 3)
        {
            _skinTextureSet = _textureSets[0];
            _faceTextureSet = _textureSets[1];
            _eyesTextureSet = _textureSets[2];
        }
        else
        {
            return;
        }

        // Enable universal export if XNormal is installed and not running under wine
        if (!IsRunningUnderWine() && File.Exists(_xNormalPath))
        {
            _skinTextureSet.OmniExportMode = true;
        }

        _skinTextureSet.MaterialSetName = "Skin";
        _faceTextureSet.MaterialSetName = "Face";
        _eyesTextureSet.MaterialSetName = "Eyes";

        _skinTextureSet.MaterialGroupName = "Character Customization";
        _faceTextureSet.MaterialGroupName = "Character Customization";
        _eyesTextureSet.MaterialGroupName = "Character Customization";

        _skin.FilePath = _skinTextureSet.Diffuse;
        _face.FilePath = _faceTextureSet.Diffuse;
        _eyes.FilePath = _eyesTextureSet.Normal;

        AddBodyPaths(_skinTextureSet);
        _facePartList.SelectedIndex = 0;
        AddFacePaths(_faceTextureSet);
        _facePartList.SelectedIndex = 2;
        AddEyePaths(_eyesTextureSet);
        _choiceTypeList.SelectedIndex = 1;
    }
    private void ToggleUniversalModeOnCurrentTextureSet()
    {
        TextureSet textureSet = _textureSets[_currentTextureSet];
        if (textureSet != null)
        {
            if (!textureSet.OmniExportMode)
            {
                UniversalTextureSetCreator.ConfigureOmniConfiguration(textureSet);
                _showOmniExportPrompt = true;
            }
            else
            {
                textureSet.OmniExportMode = false;
                textureSet.ChildSets.Clear();
                _showOmniExportPrompt = false;
            }
        }
    }

    public void UniversalModeDialog()
        => ImGuiUtil.HelpPopup("UniversalModeNotice", new Vector2(500 * UiHelpers.Scale, 10 * ImGui.GetTextLineHeightWithSpacing()), () =>
        {
            ImGui.NewLine();
            ImGui.TextWrapped(
            "Enabling universal compatibility mode allows your currently selected body or face textures to be compatible with other body/face configurations on a best effort basis." +
            "\r\n\r\nWarning: this slows down the generation process, so you will need to click the finalize button to update changes on bodies that arent this one.");
        });
    private void SetPaths()
    {
        if (_currentTextureSet != -1)
        {
            TextureSet textureSet = _textureSets[_currentTextureSet];
            DisposeWatcher(textureSet.Diffuse, _diffuse);
            DisposeWatcher(textureSet.Normal, _normal);
            DisposeWatcher(textureSet.Multi, _multi);
            DisposeWatcher(textureSet.NormalMask, _mask);
            DisposeWatcher(textureSet.Glow, _glow);

            if (!string.IsNullOrWhiteSpace(textureSet.Glow))
            {
                _generateMulti.Checked = true;
            }

            textureSet.Diffuse = _diffuse.CurrentPath;
            textureSet.Normal = _normal.CurrentPath;
            textureSet.Multi = _multi.CurrentPath;
            textureSet.NormalMask = _mask.CurrentPath;
            textureSet.Glow = _glow.CurrentPath;

            AddWatcher(textureSet.Diffuse);
            AddWatcher(textureSet.Normal);
            AddWatcher(textureSet.Multi);
            AddWatcher(textureSet.NormalMask);
            AddWatcher(textureSet.Glow);
        }
    }
    private void AddFace()
    {
        if (_textureSets.Count < _textureSetLimit)
        {
            TextureSet textureSet = new TextureSet();
            textureSet.TextureSetName = _facePartList.Text + (_facePartList.SelectedIndex == 4 ? " "
                + (_faceExtraList.SelectedIndex + 1) : "") + ", " + (_facePartList.SelectedIndex != 4 ? _genderList.Text : "Unisex")
                + ", " + (_facePartList.SelectedIndex != 4 ? _subRaceList.Text : "Multi Race") + ", "
                + (_facePartList.SelectedIndex != 4 ? _faceTypeList.Text : "Multi Face");
            switch (_facePartList.SelectedIndex)
            {
                default:
                    AddFacePaths(textureSet);
                    break;
                case 2:
                    AddEyePaths(textureSet);
                    break;
                case 4:
                    AddDecalPath(textureSet);
                    break;
                case 5:
                    AddHairPaths(textureSet);
                    break;
            }
            textureSet.IgnoreMultiGeneration = true;
            textureSet.BackupTexturePaths = null;
            _textureSets.Add(textureSet);
            _currentTextureSet = _textureSets.Count - 1;
            SaveState();
        }
        else
        {
            Penumbra.Messager.NotificationMessage("You have hit the cap of " + _textureSetLimit + " texture sets",
                Dalamud.Interface.Internal.Notifications.NotificationType.Error);
        }
    }

    private void AddBody()
    {
        if (_textureSets.Count < _textureSetLimit)
        {
            TextureSet textureSet = new TextureSet();
            textureSet.TextureSetName = _baseBodyList.Text + (_baseBodyList.Text.ToLower().Contains("tail") ? " " +
                (_tailList.SelectedIndex + 1) : "") + ", " + (_raceList.SelectedIndex == 5 ? "Unisex" : _genderList.Text)
                + ", " + _raceList.Text;
            AddBodyPaths(textureSet);
            _textureSets.Add(textureSet);
            _currentTextureSet = _textureSets.Count - 1;
            SaveState();
        }
        else
        {
            Penumbra.Messager.NotificationMessage("You have hit the cap of " + _textureSetLimit + " texture sets",
                Dalamud.Interface.Internal.Notifications.NotificationType.Error);
        }
    }

    private void AddBodyPaths(TextureSet textureSet)
    {
        if (_raceList.SelectedIndex != 3 || _baseBodyList.SelectedIndex != 6)
        {
            textureSet.InternalDiffusePath = RacePaths.GetBodyTexturePath(0, _genderList.SelectedIndex,
               _baseBodyList.SelectedIndex, _raceList.SelectedIndex, _tailList.SelectedIndex, _uniqueAuRa.Checked);
        }
        textureSet.InternalNormalPath = RacePaths.GetBodyTexturePath(1, _genderList.SelectedIndex,
               _baseBodyList.SelectedIndex, _raceList.SelectedIndex, _tailList.SelectedIndex, _uniqueAuRa.Checked);

        textureSet.InternalMultiPath = RacePaths.GetBodyTexturePath(2, _genderList.SelectedIndex,
               _baseBodyList.SelectedIndex, _raceList.SelectedIndex, _tailList.SelectedIndex, _uniqueAuRa.Checked);
        BackupTexturePaths.AddBackupPaths(_genderList.SelectedIndex, _raceList.SelectedIndex, textureSet);
    }

    private void AddDecalPath(TextureSet textureSet)
    {
        textureSet.InternalDiffusePath = RacePaths.GetFaceTexturePath(_faceExtraList.SelectedIndex);
    }

    private void AddHairPaths(TextureSet textureSet)
    {
        textureSet.TextureSetName = _faceParts[_facePartList.SelectedIndex] + " " + (_faceExtraList.SelectedIndex + 1)
            + ", " + _genderList.Text + ", " + _races[_raceList.SelectedIndex];

        textureSet.InternalNormalPath = RacePaths.GetHairTexturePath(1, _faceExtraList.SelectedIndex,
            _genderList.SelectedIndex, _raceList.SelectedIndex, _subRaceList.SelectedIndex);

        textureSet.InternalMultiPath = RacePaths.GetHairTexturePath(2, _faceExtraList.SelectedIndex,
            _genderList.SelectedIndex, _raceList.SelectedIndex, _subRaceList.SelectedIndex);
    }

    private void AddEyePaths(TextureSet textureSet)
    {
        textureSet.InternalDiffusePath = RacePaths.GetFaceTexturePath(1, _genderList.SelectedIndex, _subRaceList.SelectedIndex,
        2, _faceTypeList.SelectedIndex, _auraFaceScalesDropdown.SelectedIndex, _asymCheckbox.Checked);

        textureSet.InternalNormalPath = RacePaths.GetFaceTexturePath(2, _genderList.SelectedIndex, _subRaceList.SelectedIndex,
        2, _faceTypeList.SelectedIndex, _auraFaceScalesDropdown.SelectedIndex, _asymCheckbox.Checked);

        textureSet.InternalMultiPath = RacePaths.GetFaceTexturePath(3, _genderList.SelectedIndex, _subRaceList.SelectedIndex,
        2, _faceTypeList.SelectedIndex, _auraFaceScalesDropdown.SelectedIndex, _asymCheckbox.Checked);
    }

    private void AddFacePaths(TextureSet textureSet)
    {
        if (_facePartList.SelectedIndex != 1)
        {
            textureSet.InternalDiffusePath = RacePaths.GetFaceTexturePath(0, _genderList.SelectedIndex, _subRaceList.SelectedIndex,
                _facePartList.SelectedIndex, _faceTypeList.SelectedIndex, _auraFaceScalesDropdown.SelectedIndex, _asymCheckbox.Checked);
        }

        textureSet.InternalNormalPath = RacePaths.GetFaceTexturePath(1, _genderList.SelectedIndex, _subRaceList.SelectedIndex,
        _facePartList.SelectedIndex, _faceTypeList.SelectedIndex, _auraFaceScalesDropdown.SelectedIndex, _asymCheckbox.Checked);

        textureSet.InternalMultiPath = RacePaths.GetFaceTexturePath(2, _genderList.SelectedIndex, _subRaceList.SelectedIndex,
        _facePartList.SelectedIndex, _faceTypeList.SelectedIndex, _auraFaceScalesDropdown.SelectedIndex, _asymCheckbox.Checked);

        if (_facePartList.SelectedIndex == 0)
        {
            if (_subRaceList.SelectedIndex == 10 || _subRaceList.SelectedIndex == 11)
            {
                if (_auraFaceScalesDropdown.SelectedIndex > 0)
                {
                    if (_faceTypeList.SelectedIndex < 4)
                    {
                        if (_asymCheckbox.Checked)
                        {
                            textureSet.NormalCorrection = Path.Combine(_textureProcessor.BasePath,
                                  @"res\textures\s" + (_genderList.SelectedIndex == 0 ? "m" : "f") + _faceTypeList.SelectedIndex + "a.png");
                        }
                        else
                        {
                            textureSet.NormalCorrection = Path.Combine(_textureProcessor.BasePath,
                                @"res\textures\s" + (_genderList.SelectedIndex == 0 ? "m" : "f") + _faceTypeList.SelectedIndex + ".png");
                        }
                    }
                }
            }
        }
    }

    private bool IsRunningUnderWine()
    {
        return Dalamud.Utility.Util.IsWine();
    }


    /// <summary>
    /// Allows file changes to be autmatically pulled in game while the mod tab is open. Function is ignored while running on Wine.
    /// </summary>
    /// <param name="path"></param>
    private void AddWatcher(string path)
    {
        // Check to see if we are running under wine before attempting to watch for files changes.
        if (!IsRunningUnderWine())
        {
            string directory = Path.GetDirectoryName(path);
            if (Directory.Exists(directory) && !string.IsNullOrWhiteSpace(path))
            {
                FileSystemWatcher fileSystemWatcher = _watchers.ContainsKey(path) ? _watchers[path] : new FileSystemWatcher();
                fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileSystemWatcher.Changed += delegate (object sender, FileSystemEventArgs e)
                {
                    if (e.Name.Contains(Path.GetFileName(path)))
                    {
                        Task.Run(() => Export(false));
                    }
                    return;
                };
                fileSystemWatcher.Path = directory;
                fileSystemWatcher.EnableRaisingEvents = !string.IsNullOrEmpty(path);
                _watchers[path] = fileSystemWatcher;
            }
        }
    }

    /// <summary>
    /// Untracks files from being automatically pulled in for changes. Function is ignored while running on Wine.
    /// </summary>
    private void DisposeWatcher(string path, LTCFilePicker filePicker)
    {
        // Check to see if we are running under wine.
        if (!IsRunningUnderWine())
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (_watchers.ContainsKey(path))
                {
                    if (path != filePicker.CurrentPath)
                    {
                        _watchers[path].Dispose();
                        _watchers.Remove(path);
                    }
                }
            }
        }
    }
    #endregion
    #region Export
    public async Task<bool> Export(bool finalize)
    {
        if (!_lockDuplicateGeneration)
        {
            _exportStatus = "Initializing";
            _lockDuplicateGeneration = true;
            List<TextureSet> textureSets = new List<TextureSet>();
            foreach (TextureSet item in _textureSets)
            {
                if (item.OmniExportMode)
                {
                    UniversalTextureSetCreator.ConfigureOmniConfiguration(item);
                }
                textureSets.Add(item);
            }
            _textureProcessor.CleanGeneratedAssets(_selector.Selected.ModPath.FullName);
            await _textureProcessor.Export(
                textureSets,
                _groupOptionTypes,
                _selector.Selected.ModPath.FullName,
                _choiceTypeList.SelectedIndex,
                _bakeNormals.Checked,
                _generateMulti.Checked,
                File.Exists(_xNormalPath) && finalize,
                _xNormalPath);

            _manager.ReloadMod(_currentMod);
            _redrawService.RedrawObject(0, Api.Enums.RedrawType.Redraw);
            _lockDuplicateGeneration = false;
        }
        return true;
    }
    #endregion
    #region Classes
    internal class LTCCheckBox
    {
        string _label = "";
        bool _isChecked = false;
        bool _lastValue = false;
        bool _enabled = true;
        int _width = 100;
        public LTCCheckBox(string _label, int width = 100)
        {
            this._label = _label;
            this._width = width;
        }

        public bool Enabled { get => _enabled; set => _enabled = value; }
        public bool Checked { get => _isChecked; set => _isChecked = value; }
        public int Width { get => (_enabled ? (45 + _width) : 0); set => _width = value; }

        public void Draw()
        {
            if (_enabled)
            {
                ImGui.Checkbox("##" + _label, ref _isChecked);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(_width);
                ImGui.LabelText("##labelText" + _label, _label);
            }
            if (_isChecked != _lastValue)
            {
                if (OnCheckedChanged != null)
                {
                    OnCheckedChanged.Invoke(this, EventArgs.Empty);
                }
            }
            _lastValue = _isChecked;
        }

        public event EventHandler OnCheckedChanged;
    }
    internal class LTCComboBox
    {
        string _label;
        int _width;
        int index = -1;
        int _lastIndex;
        bool _enabled = true;
        string[] _contents;
        public event EventHandler OnSelectedIndexChanged;
        public string Text { get { return index > -1 ? _contents[index] : ""; } }
        public LTCComboBox(string _label, string[] contents, int index, int width = 100)
        {
            this._label = _label;
            this._width = width;
            this.index = index;
            this._contents = contents;
        }

        public string[] Contents { get => _contents; set => _contents = value; }
        public int SelectedIndex { get => index; set => index = value; }
        public int Width { get => (_enabled ? _width : 0); set => _width = value; }
        public string Label { get => _label; set => _label = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }

        public void Draw()
        {
            if (_enabled)
            {
                ImGui.SetNextItemWidth(_width);
                ImGui.Combo("##" + _label, ref index, _contents, _contents.Length);
            }
            if (index != _lastIndex)
            {
                if (OnSelectedIndexChanged != null)
                {
                    OnSelectedIndexChanged.Invoke(this, EventArgs.Empty);
                }
            }
            _lastIndex = index;
        }
    }
    internal class LTCFilePicker
    {
        string _label = "";
        private Configuration _config;
        string _filePath = "";
        private string _currentPath;
        bool _enabled = true;
        private string _lastText;
        private FileDialogService _fileDialog;
        private IDragDropManager _dragDrop;

        public LTCFilePicker(string label, Configuration config, FileDialogService fileDialog, IDragDropManager dragDrop)
        {
            _label = label;
            _config = config;
            _fileDialog = fileDialog;
            _dragDrop = dragDrop;
            OnTextChanged += LTCFilePicker_OnTextChanged;
        }

        private void LTCFilePicker_OnTextChanged(object? sender, EventArgs e)
        {
            if (_filePath.ToLower().Contains("basetexbaked"))
            {
                _currentPath = "";
                _filePath = "";
                Penumbra.Messager.NotificationMessage("Please remove the prefix 'baseTexBaked' from the file name! "
                    + "\r\n\r\nAlternatively, please use the source image that was used to generate this texture.", Dalamud.Interface.Internal.Notifications.NotificationType.Error);
            }
            else if (_filePath.Contains("."))
            {
                if (CheckExtentions(_filePath))
                {
                    if (!_filePath.Contains("_generated") && !_filePath.Contains("-generated"))
                    {
                        _currentPath = _filePath;
                        if (OnFileSelected != null)
                        {
                            OnFileSelected.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        _currentPath = "";
                        _filePath = "";
                        Penumbra.Messager.NotificationMessage("Do not use autogenerated files here.",
                            Dalamud.Interface.Internal.Notifications.NotificationType.Error);
                    }
                }
                else
                {
                    Penumbra.Messager.NotificationMessage("This is not a file this tool supports.",
                        Dalamud.Interface.Internal.Notifications.NotificationType.Error);
                }
            }
            else if (string.IsNullOrEmpty(_filePath))
            {
                _currentPath = "";
                _filePath = "";
                if (OnFileSelected != null)
                {
                    OnFileSelected.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string CurrentPath
        {
            get => _currentPath; set
            {
                string newValue = value != null ? value : "";
                _currentPath = newValue;
                _filePath = newValue;
            }
        }

        public Color BackColor { get; internal set; }
        public string LabelName { get => _label; set => _label = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }
        public string FilePath { get => _filePath; set => _filePath = value; }
        public static bool FileDialogInUse { get; private set; }

        public event EventHandler OnFileSelected;
        public event EventHandler OnTextChanged;
        public void Draw()
        {
            if (_enabled)
            {
                _dragDrop.CreateImGuiSource("TextureDragDrop", m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m =>
                {
                    ImGui.TextUnformatted($"Dragging texture for import:\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
                    return true;
                });
                ImGui.SetNextItemWidth(80);
                ImGui.LabelText("##labelText" + _label, _label);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionMax().X - 180);
                ImGui.InputText("##" + _label, ref _filePath, 256);
                if (_dragDrop.CreateImGuiTarget("TextureDragDrop", out var files, out _))
                {
                    if (ValidTextureExtensions.Contains(Path.GetExtension(files[0])))
                    {
                        _filePath = files[0];
                    }
                }
                ImGui.SameLine();

                if (!_config.DeleteModModifier.IsActive())
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button("X" + "##" + _label))
                {
                    _filePath = "";
                }
                if (!_config.DeleteModModifier.IsActive())
                {
                    ImGui.EndDisabled();
                }
                ImGui.SameLine();
                if (_filePath != _currentPath)
                {
                    OnTextChanged?.Invoke(this, EventArgs.Empty);
                }

                if (ImGui.Button("Select" + "##" + LabelName))
                {
                    if (_fileDialog != null)
                    {
                        _fileDialog.OpenFilePicker("Import Texture",
                        "Textures{.png,.dds,.bmp,.tex}", (s, f) =>
                        {
                            if (!s)
                            {
                                return;
                            }
                            string value = f[0];
                            _filePath = value != null ? value : "";
                            OnFileSelected?.Invoke(this, EventArgs.Empty);
                        }, 1, "", false);
                    }
                }
                _lastText = _filePath;
            }
        }
        public static bool CheckExtentions(string file)
        {
            string[] extentions = new string[] { ".png", ".dds", ".bmp", ".tex" };
            foreach (string extention in extentions)
            {
                if (file.EndsWith(extention))
                {
                    return true;
                }
            }
            return false;
        }
        private static readonly string[] ValidTextureExtensions = new[]
        {
          ".png",
          ".dds",
          ".bmp",
          ".tex",
        };

    }


    internal class LTCCustomPathConfigurator
    {
        TextureSet textureSet = new TextureSet();
        string _group = "";
        string _textureSetName = "";
        string _internalDiffusePath = "";
        string _internalNormalPath = "";
        string _internalMultiPath = "";
        LTCCheckBox _ignoreNormals;
        LTCCheckBox _ignoreMulti;
        LTCCheckBox _invertNormals;
        string _normalCorrection = "";
        private LTCComboBox _groupChoiceType = null;
        private string _diffuseLabel;
        private string _normalLabel;
        private string _multiLabel;
        private string _errorText = "";

        private bool _wasValidated;
        public event EventHandler OnWantsToClose;
        public LTCCustomPathConfigurator(string[] newGroupChoices)
        {
            List<string> groupChoices = new List<string>();
            groupChoices.Add("Use Global Setting");
            groupChoices.AddRange(newGroupChoices);

            _groupChoiceType = new LTCComboBox("Group Choice Type", groupChoices.ToArray(), 0, 150);
            _ignoreNormals = new LTCCheckBox("Ignore Normals", 120);
            _ignoreMulti = new LTCCheckBox("Ignore Multi", 120);
            _invertNormals = new LTCCheckBox("Invert Normals", 120);
        }

        public LTCComboBox GroupingType
        {
            get { return _groupChoiceType; }
        }
        public TextureSet TextureSet
        {
            get => textureSet;
            set
            {
                textureSet = value;
                if (textureSet != null)
                {
                    _group = textureSet.GroupName;
                    _textureSetName = textureSet.TextureSetName;

                    _internalDiffusePath = textureSet.InternalDiffusePath != null
                        ? textureSet.InternalDiffusePath : "";

                    _internalNormalPath = textureSet.InternalNormalPath != null
                        ? textureSet.InternalNormalPath : "";

                    _internalMultiPath = textureSet.InternalMultiPath != null
                        ? textureSet.InternalMultiPath : "";

                    _ignoreNormals.Checked = textureSet.IgnoreNormalGeneration;
                    _ignoreMulti.Checked = textureSet.IgnoreMultiGeneration;
                    _invertNormals.Checked = textureSet.InvertNormalGeneration;

                    _normalCorrection = textureSet.NormalCorrection != null ? textureSet.NormalCorrection : "";
                }
                _wasValidated = false;
            }
        }

        public bool WasValidated { get => _wasValidated; set => _wasValidated = value; }

        public void Draw()
        {
            int labelWidth = 120;
            RefreshLabels();
            ImGui.SetNextItemWidth(labelWidth);
            ImGui.LabelText("##groupLabelText" + _group, "Group");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - labelWidth);
            ImGui.InputText("##group", ref _group, 256);

            ImGui.SetNextItemWidth(labelWidth);
            ImGui.LabelText("##groupChoiceType" + _group, "Group Choice Type");
            ImGui.SameLine();
            _groupChoiceType.Draw();

            ImGui.Dummy(new System.Numerics.Vector2(0, 20));

            ImGui.SetNextItemWidth(labelWidth);
            ImGui.LabelText("##textureSetNameLabel" + _textureSetName, "Texture Set Name");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - labelWidth);
            ImGui.InputText("##textureSet", ref _textureSetName, 256);

            ImGui.SetNextItemWidth(labelWidth);
            ImGui.LabelText("##diffuseLabel" + _diffuseLabel, _diffuseLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - labelWidth);
            ImGui.InputText("##internalDiffuse", ref _internalDiffusePath, 256);

            ImGui.SetNextItemWidth(labelWidth);
            ImGui.LabelText("##normalLabel" + _normalLabel, _normalLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - labelWidth);
            ImGui.InputText("##internalNormal", ref _internalNormalPath, 256);

            ImGui.SetNextItemWidth(labelWidth);
            ImGui.LabelText("##multiLabel" + _multiLabel, _multiLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - labelWidth);
            ImGui.InputText("##internalMulti", ref _internalMultiPath, 256);

            if (!textureSet.InternalMultiPath.ToLower().Contains("catchlight"))
            {
                ImGui.SetNextItemWidth(labelWidth);
                ImGui.LabelText("##normalCorrection", "Normal Correction");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X - labelWidth);
                ImGui.InputText("##normalCorrection", ref _normalCorrection, 256);
                _invertNormals.Draw();
                _ignoreNormals.Draw();
                _ignoreMulti.Draw();
            }
            if (ImGui.Button("Confirm Changes"))
            {
                AcceptChanges();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                Cancel();
            }

            ImGui.LabelText("##errorText", _errorText);
        }

        private void Cancel()
        {
            _errorText = "";
            OnWantsToClose?.Invoke(this, EventArgs.Empty);
        }

        public void AcceptChanges()
        {
            if (!string.IsNullOrWhiteSpace(_textureSetName))
            {
                textureSet.GroupName = _group;
                textureSet.TextureSetName = _textureSetName;
                int validationCount = 0;
                if (IsValidGamePathFormat(_internalDiffusePath))
                {
                    textureSet.InternalDiffusePath = _internalDiffusePath;
                    validationCount++;
                }
                else
                {
                    _errorText = "Internal diffuse path is invalid. Make sure an in game path format is being used and that it points to a .tex file!";
                }
                if (IsValidGamePathFormat(_internalNormalPath))
                {
                    textureSet.InternalNormalPath = _internalNormalPath;
                    validationCount++;
                }
                else
                {
                    _errorText = "Internal normal path is invalid. Make sure an in game path format is being used and that it points to a .tex file!";
                }
                if (IsValidGamePathFormat(_internalMultiPath))
                {
                    textureSet.InternalMultiPath = _internalMultiPath;
                    validationCount++;
                }
                else
                {
                    _errorText = "Internal multi path is invalid. Make sure an in game path format is being used and that it points to a .tex file!";
                }
                if (File.Exists(_normalCorrection) || string.IsNullOrEmpty(_normalCorrection))
                {
                    textureSet.NormalCorrection = _normalCorrection;
                    validationCount++;
                }
                else
                {
                    _errorText = "Normal correction path is invalid.";
                }
                if (validationCount == 4)
                {
                    textureSet.IgnoreNormalGeneration = _ignoreNormals.Checked;
                    textureSet.IgnoreMultiGeneration = _ignoreMulti.Checked;
                    textureSet.InvertNormalGeneration = _invertNormals.Checked;
                    _wasValidated = true;
                    _errorText = "";
                    OnWantsToClose?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _errorText = "Please enter a name for your texture set!";
            }
        }
        public void RefreshLabels()
        {
            if (textureSet.InternalMultiPath != null
             && textureSet.InternalMultiPath.ToLower().Contains("catchlight"))
            {
                _diffuseLabel = "Internal Normal";
                _normalLabel = "Internal Multi";
                _multiLabel = "Internal Catchlight";
            }
            else
            {
                _diffuseLabel = "Internal Diffuse";
                _normalLabel = "Internal Normal";
                _multiLabel = "Internal Multi";
            }
        }
        public bool IsValidGamePathFormat(string input)
        {
            if ((input.Contains(@"\") || !input.Contains(".tex")) && !string.IsNullOrWhiteSpace(input))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
    internal class LTCBulkNameReplacement
    {
        public event EventHandler OnWantsToClose;
        private List<TextureSet> _textureSets = new List<TextureSet>();
        private LTCComboBox _replacementTypeComboBox;

        private string _findBoxText = "";
        private string _replaceBoxText = "";
        private string _errorText = "";

        private bool _wasValidated = false;

        private string[] _replacementOptions = new string[]
        {
            "Search And Replace Inside Name",
            "Search And Replace Inside Group",
            "Find In Name Then Change Whole Group",
            "Find In Group Then Change Whole Name",
            "Find In Name Then Replace Whole Name ",
            "Find In Group Then Change Whole Group"
        };

        public bool WasValidated { get => _wasValidated; set => _wasValidated = value; }

        public LTCBulkNameReplacement(List<TextureSet> textureSets)
        {
            _textureSets = textureSets;
            _replacementTypeComboBox = new LTCComboBox("replacementTypeCheckbox", _replacementOptions, 0, 300);
        }
        public void Draw()
        {
            ImGui.LabelText("##label1", "Find this");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##textureSetSearchString", ref _findBoxText, 256);
            ImGui.LabelText("##label2", "Replace With this");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##groupSearchString", ref _replaceBoxText, 256);
            _replacementTypeComboBox.Draw();
            ImGui.LabelText("##label3", _errorText);
            if (ImGui.Button("Replace All"))
            {
                ReplaceNames();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                OnWantsToClose?.Invoke(this, EventArgs.Empty);
            }
        }
        private void ReplaceNames()
        {
            if (!string.IsNullOrEmpty(_findBoxText))
            {
                switch (_replacementTypeComboBox.SelectedIndex)
                {
                    case 0:
                        foreach (TextureSet textureSet in _textureSets)
                        {
                            if (textureSet.TextureSetName.Contains(_findBoxText))
                            {
                                textureSet.TextureSetName = textureSet.TextureSetName.Replace(_findBoxText, _replaceBoxText);
                            }
                        }
                        break;
                    case 1:
                        foreach (TextureSet textureSet in _textureSets)
                        {
                            if (textureSet.GroupName.Contains(_findBoxText))
                            {
                                textureSet.GroupName = textureSet.GroupName.Replace(_findBoxText, _replaceBoxText);
                            }
                        }
                        break;
                    case 2:
                        foreach (TextureSet textureSet in _textureSets)
                        {
                            if (textureSet.TextureSetName.Contains(_findBoxText))
                            {
                                textureSet.GroupName = _replaceBoxText;
                            }
                        }
                        break;
                    case 3:
                        foreach (TextureSet textureSet in _textureSets)
                        {
                            if (textureSet.GroupName.Contains(_findBoxText))
                            {
                                textureSet.TextureSetName = textureSet.TextureSetName = _replaceBoxText;
                            }
                        }
                        break;
                    case 4:
                        foreach (TextureSet textureSet in _textureSets)
                        {
                            if (textureSet.TextureSetName.Contains(_findBoxText))
                            {
                                textureSet.TextureSetName = textureSet.TextureSetName = _replaceBoxText;
                            }
                        }
                        break;
                    case 5:
                        foreach (TextureSet textureSet in _textureSets)
                        {
                            if (textureSet.GroupName.Contains(_findBoxText))
                            {
                                textureSet.GroupName = _replaceBoxText;
                            }
                        }
                        break;
                }
                _wasValidated = true;
                OnWantsToClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _errorText = "Find text cannot be empty!";
            }
        }
    }

    internal class LTCFindAndReplace
    {
        List<TextureSet> _textureSets = new List<TextureSet>();
        string _textureSetSearchString = "";
        string _groupSearchString = "";

        LTCFilePicker _diffuse;
        LTCFilePicker _normal;
        LTCFilePicker _multi;
        LTCFilePicker _glow;
        LTCFilePicker _mask;

        private bool _wasValidated;
        private string _errorText = "";

        public event EventHandler OnWantsToClose;

        public LTCFindAndReplace(List<TextureSet> textureSets, Configuration config, FileDialogService fileDialog, IDragDropManager dragDrop)
        {
            _diffuse = new LTCFilePicker("Diffuse", config, fileDialog, dragDrop);
            _normal = new LTCFilePicker("Normal", config, fileDialog, dragDrop);
            _multi = new LTCFilePicker("Multi", config, fileDialog, dragDrop);
            _glow = new LTCFilePicker("Glow", config, fileDialog, dragDrop);
            _mask = new LTCFilePicker("Mask", config, fileDialog, dragDrop);
            _textureSets = textureSets;
        }

        public bool IsForEyes { get; internal set; }
        public List<TextureSet> TextureSets
        {
            get => _textureSets;
            set
            {
                _textureSets = value;
            }
        }

        public bool WasValidated { get => _wasValidated; set => _wasValidated = value; }
        public string TextureSetSearchString { get => _textureSetSearchString; set => _textureSetSearchString = value; }
        public string GroupSearchString { get => _groupSearchString; set => _groupSearchString = value; }
        public LTCFilePicker Diffuse { get => _diffuse; set => _diffuse = value; }
        public LTCFilePicker Normal { get => _normal; set => _normal = value; }
        public LTCFilePicker Multi { get => _multi; set => _multi = value; }
        public LTCFilePicker Glow { get => _glow; set => _glow = value; }
        public LTCFilePicker Mask { get => _mask; set => _mask = value; }

        public void Draw()
        {
            RefreshLabels();

            ImGui.LabelText("##label1", "Find Texture Sets Containing This Name");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##textureSetSearchString", ref _textureSetSearchString, 256);
            ImGui.LabelText("##label2", "And In This Group");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionMax().X);
            ImGui.InputText("##groupSearchString", ref _groupSearchString, 256);
            ImGui.LabelText("##label3", "Then Replace Files With These");

            _diffuse.Draw();
            _normal.Draw();
            _multi.Draw();
            _glow.Draw();
            _mask.Draw();

            if (ImGui.Button("Confirm Replacement"))
            {
                AcceptChanges();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                OnWantsToClose?.Invoke(this, EventArgs.Empty);
            }

            ImGui.LabelText("##errorText", _errorText);
        }
        public void AcceptChanges()
        {
            if (!string.IsNullOrEmpty(_textureSetSearchString))
            {
                foreach (TextureSet textureSet in _textureSets)
                {
                    if (textureSet.TextureSetName.ToLower().Contains(_textureSetSearchString.ToLower())
                        && textureSet.GroupName.ToLower().Contains(_groupSearchString.ToLower()))
                    {
                        if (!string.IsNullOrEmpty(_diffuse.FilePath))
                        {
                            textureSet.Diffuse = _diffuse.FilePath;
                        }
                        if (!string.IsNullOrEmpty(_normal.FilePath))
                        {
                            textureSet.Normal = _normal.FilePath;
                        }
                        if (!string.IsNullOrEmpty(_multi.FilePath))
                        {
                            textureSet.Multi = _multi.FilePath;
                        }
                        if (!string.IsNullOrEmpty(_mask.FilePath))
                        {
                            textureSet.NormalMask = _mask.FilePath;
                        }
                        if (!string.IsNullOrEmpty(_glow.FilePath))
                        {
                            textureSet.Glow = _glow.FilePath;
                        }
                    }
                }
                _wasValidated = true;
                OnWantsToClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _errorText = "Search name value cannot be empty!";
            }
        }
        public void RefreshLabels()
        {
            if (IsForEyes)
            {
                _diffuse.LabelName = "Normal";
                _normal.LabelName = "Multi";
                _multi.LabelName = "Catchlight";
            }
            else
            {
                _diffuse.LabelName = "Diffuse";
                _normal.LabelName = "Normal";
                _multi.LabelName = "Multi";
            }
        }
    }
    internal class LTCTemplateConfigurator
    {
        ModPanelLooseAssetCompilerTab _compilerTab;
        private bool _wasValidated;
        string _groupName = "Default";
        private List<TextureSet> _textureSets;
        string _currentPath = "";
        private int _currentGenderIndex;
        private int _currentRaceIndex;

        public LTCTemplateConfigurator(ModPanelLooseAssetCompilerTab compilerTab, List<TextureSet> textureSets)
        {
            _compilerTab = compilerTab;
            _textureSets = textureSets;
        }

        public string GroupName { get => _groupName; set => _groupName = value; }
        public bool WasValidated { get => _wasValidated; set => _wasValidated = value; }

        public event EventHandler OnWantsToClose;
        public void OpenTemplate(string path, int genderIndex, int raceIndex)
        {
            _groupName = "Default";
            _currentPath = path;
            _currentGenderIndex = genderIndex;
            _currentRaceIndex = raceIndex;
        }
        public void Draw()
        {
            ImGui.LabelText("##groupNameLabel", "Enter Group Name For Template");
            ImGui.InputText("##groupName", ref _groupName, 256);
            if (ImGui.Button("Confirm"))
            {
                using (StreamReader file = File.OpenText(_currentPath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    ProjectFile projectFile = (ProjectFile)serializer.Deserialize(file, typeof(ProjectFile));
                    if (_textureSets.Count + projectFile.TextureSets.Count < 10510)
                    {
                        foreach (TextureSet textureSet in projectFile.TextureSets)
                        {
                            if (!_groupName.Contains("Default"))
                            {
                                textureSet.GroupName = _groupName;
                            }
                            _compilerTab.AddWatcher(textureSet.Diffuse);
                            _compilerTab.AddWatcher(textureSet.Normal);
                            _compilerTab.AddWatcher(textureSet.Multi);
                            _compilerTab.AddWatcher(textureSet.NormalMask);
                            _compilerTab.AddWatcher(textureSet.Glow);
                            BackupTexturePaths.AddBackupPaths(_currentGenderIndex, _currentRaceIndex, textureSet);
                        }
                        _textureSets.AddRange(projectFile.TextureSets?.ToArray());
                    }
                    else
                    {
                        Penumbra.Messager.NotificationMessage("Importing this template will go above the limit of 10510 texture sets",
                            Dalamud.Interface.Internal.Notifications.NotificationType.Error);
                    }
                }
                _wasValidated = true;
                OnWantsToClose?.Invoke(this, EventArgs.Empty);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                OnWantsToClose?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    #endregion
}
