using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Widgets;
using Penumbra.Mods.Manager;
using ImGuiScene;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Plugin;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using Penumbra.Mods;
using Dalamud.Interface.Internal;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly TutorialService       _tutorial;
    private readonly ModManager            _modManager;
    private readonly TagButtons            _localTags = new();
    private readonly TagButtons            _modTags   = new();
    private readonly Configuration _config;
    private DalamudPluginInterface _pi;
    private int _currentImageIndex = 0;
    private Task<IDalamudTextureWrap>? _currentImageTexture;

    public ModPanelDescriptionTab(ModFileSystemSelector selector, TutorialService tutorial, ModManager modManager, Configuration config, DalamudPluginInterface pi)
    {
        _selector = selector;
        _tutorial = tutorial;
        _modManager = modManager;
        _config = config;
        _pi = pi;
        _selector.SelectionChanged += OnSelectionChange;
    }

    public ReadOnlySpan<byte> Label
        => "Description"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##description");
        if (!child)
            return;

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        var tagIdx = _localTags.Draw("Local Tags: ",
            "Custom tags you can set personally that will not be exported to the mod data but only set for you.\n"
            + "If the mod already contains a local tag in its own tags, the local tag will be ignored.", _selector.Selected!.LocalTags,
            out var editedTag);
        _tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            _modManager.DataEditor.ChangeLocalTag(_selector.Selected!, tagIdx, editedTag);

        if (_selector.Selected!.ModTags.Count > 0)
            _modTags.Draw("Mod Tags: ", "Tags assigned by the mod creator and saved with the mod data. To edit these, look at Edit Mod.",
                _selector.Selected!.ModTags, out var _, false,
                ImGui.CalcTextSize("Local ").X - ImGui.CalcTextSize("Mod ").X);

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));

        if (_config.ShowPreviewImages == true && _selector.Selected!.PreviewImagePaths.Count > 0)
        {
            ImGui.Separator();

            List<string> previewImagePaths = _selector.Selected!.PreviewImagePaths;

            if (_currentImageTexture == null)
            {
                // Check if the current image index is within bounds
                if (_currentImageIndex < 0 || _currentImageIndex >= previewImagePaths.Count)
                {
                    _currentImageIndex = 0; // Reset to the first image if out of bounds
                }

                // Start loading the image texture asynchronously
                _currentImageTexture = LoadImageTextureAsync(previewImagePaths[_currentImageIndex]);
            }
            else if (_currentImageTexture.IsCompleted)
            {
                // Get the loaded image texture
                IDalamudTextureWrap imageTexture = _currentImageTexture.Result;

                // Display the image texture
                if (imageTexture != null)
                {
                    ImGui.Image(imageTexture.ImGuiHandle, new Vector2(imageTexture.Width, imageTexture.Height));
                    int imageWidth = imageTexture.Width;
                    Vector2 cursorPos = ImGui.GetCursorPos();

                    // Calculate the available width for the navigation buttons
                    float buttonWidth = ImGui.CalcTextSize(">").X + 2 * ImGui.GetStyle().ItemSpacing.X;

                    // Calculate the remaining width for the counter and file name label
                    float remainingWidth = imageTexture?.Width - 2 * buttonWidth ?? 0;

                    // Anchor the left button under the bottom left side of the image
                    ImGui.SetCursorPos(new Vector2(cursorPos.X, cursorPos.Y));
                    if (ImGui.ArrowButton("##PrevImage", ImGuiDir.Left))
                    {
                        // Navigate to the previous image
                        _currentImageIndex = (_currentImageIndex - 1 + previewImagePaths.Count) % previewImagePaths.Count;

                        // Reset the current image texture to load the new image
                        _currentImageTexture = null;
                    }

                    // Anchor the right button under the bottom right side of the image
                    ImGui.SetCursorPos(new Vector2(cursorPos.X + imageWidth - buttonWidth, cursorPos.Y));
                    if (ImGui.ArrowButton("##NextImage", ImGuiDir.Right))
                    {
                        // Navigate to the next image
                        _currentImageIndex = (_currentImageIndex + 1) % previewImagePaths.Count;
                        // Reset the current image texture to load the new image
                        _currentImageTexture = null;
                    }

                    // Calculate the width for the counter label
                    string counterLabel = $"{_currentImageIndex + 1} / {previewImagePaths.Count}";
                    float counterWidth = ImGui.CalcTextSize(counterLabel).X;

                    // Calculate the width and height for the file name label
                    string fileName = Path.GetFileNameWithoutExtension(previewImagePaths[_currentImageIndex]);
                    Vector2 fileNameSize = ImGui.CalcTextSize(fileName);

                    // Calculate the offset for centering the labels
                    float offset = (remainingWidth - counterWidth) * 0.5f;
                    float nameOffset = (remainingWidth - fileNameSize.X) * 0.5f;

                    // Calculate the vertical offset for centering the file name label
                    float verticalOffset = (ImGui.GetTextLineHeight() - fileNameSize.Y) * 0.5f;

                    // Anchor the counter label below the image, centered
                    ImGui.SetCursorPos(new Vector2(cursorPos.X + buttonWidth + offset, cursorPos.Y));
                    ImGui.Text(counterLabel);

                    // Anchor the file name label below the counter label, centered
                    ImGui.SetCursorPos(new Vector2(cursorPos.X + buttonWidth + nameOffset, cursorPos.Y + ImGui.GetTextLineHeight() + verticalOffset));
                    ImGui.Text(fileName);
                }
                else
                {
                    ImGui.Text("Failed to load image.");
                }
            }
        }

        ImGui.Separator();

        ImGuiUtil.TextWrapped(_selector.Selected!.Description);
    }

    private async Task<IDalamudTextureWrap> LoadImageTextureAsync(string imagePath)
    {
        try
        {
            using (FileStream fileStream = File.OpenRead(imagePath))
            {
                (Image image, _) = await Image.LoadWithFormatAsync(fileStream);

                int targetWidth = 500;
                int targetHeight = 300;

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.Stretch
                }));

                using (MemoryStream stream = new MemoryStream())
                {
                    await image.SaveAsync(stream, new PngEncoder());

                    byte[] imageData = stream.ToArray();
                    return _pi.UiBuilder.LoadImage(imageData);
                }
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error loading image: {e.Message}");
            return null!;
        }
    }
    private void OnSelectionChange(Mod? oldSelection, Mod? newSelection, in ModFileSystemSelector.ModState state)
    {
        _currentImageIndex = 0;
        _currentImageTexture = null;
    }
}
