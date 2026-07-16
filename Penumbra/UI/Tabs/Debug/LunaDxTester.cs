using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImSharp;
using Lumina.Data.Files;
using Luna;
using Luna.DirectX;
using Luna.Generators;
using Penumbra.Import.Textures;
using Penumbra.UI.Classes;
using TerraFX.Interop.DirectX;

namespace Penumbra.UI.Tabs.Debug;

public class LunaDxTester(
    TextureManager textureManager,
    ITextureProvider textureProvider,
    ITextureReadbackProvider readbackProvider,
    IDataManager gameData,
    FileDialogService fileDialogService) : IUiService
{
    [NamedEnum(Utf16: false)]
    public enum GraphPreset
    {
        [Name("Resample (Scale)")]
        ResampleScale,

        [Name("Resample (Fixed Size)")]
        ResampleFixed,

        [Name("RGBA Blend")]
        Blend4,

        [Name("RGB Composite")]
        Composite,

        [Name("RGBA Composite with Control Mask")]
        CompositeControlled,

        [Name("Extract Red")]
        ExtractRed,

        [Name("Extract Green")]
        ExtractGreen,

        [Name("Extract Blue")]
        ExtractBlue,

        [Name("Extract Alpha")]
        ExtractAlpha,

        [Name("Grayscale")]
        Grayscale,

        [Name("Apply Index Map")]
        ApplyIndex,

        [Name("Dual Kawase Blur")]
        KawaseBlur,

        [Name("Refraction Raycast")]
        RefractionRaycast,

        [Name("Export Dye Gloss Overlay")]
        DyeGlossOverlay,

        [Name("Bring Your Own Pixel Shader")]
        Custom,

        [Name("Bring Your Own Compute Shader")]
        CustomCompute,
    }

    // Graph preset.
    private GraphPreset _graphPreset;

    // Input/output paths.
    private string _inputPath  = string.Empty;
    private string _input2Path = string.Empty;
    private string _input3Path = string.Empty;
    private string _input4Path = string.Empty;
    private string _shaderPath = string.Empty;
    private string _outputPath = string.Empty;

    // Save options.
    private CombinedTexture.TextureSaveType _saveType     = CombinedTexture.TextureSaveType.AsIs;
    private bool                            _saveWithMips = true;

    // Resample options.
    private float                      _optScale    = 1.0f;
    private int                        _optWidth    = 32;
    private int                        _optHeight   = 32;
    private LunaEffects.ResampleMethod _optResample = LunaEffects.ResampleMethod.Bilinear;

    // Blend/Composite options.
    private LunaShaders.CompositeFunction _optComposite = LunaShaders.CompositeFunction.Over;
    private LunaShaders.Blend             _optBlend     = LunaShaders.Blend.Source;
    private Vector4                       _optFgXform   = new(1.0f, 0.0f, 0.0f, 1.0f);
    private Vector2                       _optFgOffset  = Vector2.Zero;
    private Vector4                       _optBgCtl     = new(0.2126f, 0.7152f, 0.0722f, 0.0f);
    private Vector4                       _optFgCtl     = new(0.2126f, 0.7152f, 0.0722f, 0.0f);
    private float                         _optBgCtl0    = 0.0f;
    private float                         _optFgCtl0    = 0.0f;
    private Vector4                       _optLerp      = new(1.0f, 0.0f, 1.0f, 0.0f);

    // Apply Index Map options.
    private LunaShaders.Palette _optPalette = new();

    // Blur options.
    private Vector4 _optRounding = new(8.0f);
    private Vector2 _optUvMin    = Vector2.Zero;
    private Vector2 _optUvMax    = Vector2.One;
    private float   _optBlur     = 1.0f;
    private float   _optOpacity  = 1.0f;

    // Refraction Raycast options.
    private float _optDepth = 1.0f;
    private float _optIor   = 1.33f;
    private bool  _optNn    = false;

    // Bring Your Own Compute Shader options.
    private int _optTgcX = D3D11.D3D11_CS_THREAD_GROUP_MIN_X;
    private int _optTgcY = D3D11.D3D11_CS_THREAD_GROUP_MIN_Y;
    private int _optTgcZ = D3D11.D3D11_CS_THREAD_GROUP_MIN_Z;

    // Results.
    private EffectGraph?   _lastGraph;
    private TextureStandIn _lastOutput;
    private Task           _lastRun = Task.CompletedTask;

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("Luna.DirectX Tester"u8);
        if (!header)
            return;

        DrawGraphPreset();
        DrawInputPath();
        DrawShaderPath();
        DrawGraphOptions();
        DrawOutputPath();
        DrawOutputOptions();
        DrawActions();
        DrawResults();
    }

    private void DrawGraphPreset()
        => Im.Combo.DrawEnum("Effect Graph Preset"u8, ref _graphPreset, static i => i.ToNameU8());

    private void DrawInputPath()
    {
        if (_graphPreset is GraphPreset.DyeGlossOverlay)
            return;

        switch (_graphPreset)
        {
            case GraphPreset.Blend4 or GraphPreset.Composite:
                DrawInputPath("Background Input Path"u8, "Luna.DirectX Tester: Select Background Input", _inputPath,
                    value => _inputPath = value);
                DrawInputPath("Foreground Input Path"u8, "Luna.DirectX Tester: Select Foreground Input", _input2Path,
                    value => _input2Path = value);
                break;
            case GraphPreset.CompositeControlled:
                DrawInputPath("Background Input Path"u8, "Luna.DirectX Tester: Select Background Input", _inputPath,
                    value => _inputPath = value);
                DrawInputPath("Background Control Mask Path"u8, "Luna.DirectX Tester: Select Background Control Mask", _input3Path,
                    value => _input3Path = value);
                DrawInputPath("Foreground Input Path"u8, "Luna.DirectX Tester: Select Foreground Input", _input2Path,
                    value => _input2Path = value);
                DrawInputPath("Foreground Control Mask Path"u8, "Luna.DirectX Tester: Select Foreground Control Mask", _input4Path,
                    value => _input4Path = value);
                break;
            default: DrawInputPath("Input Path"u8, "Luna.DirectX Tester: Select Input", _inputPath, value => _inputPath = value); break;
        }
    }

    private void DrawShaderPath()
    {
        if (_graphPreset is not GraphPreset.Custom and not GraphPreset.CustomCompute)
            return;

        using var id = Im.Id.Push("Shader Path"u8);

        DrawInputPath("Shader Path"u8, "Luna.DirectX Tester: Select Shader", _shaderPath, value => _shaderPath = value);
    }

    private void DrawInputPath(ReadOnlySpan<byte> label, string pickerTitle, string value, Action<string> setter)
    {
        using var id = Im.Id.Push(label);

        if (Im.Input.Text(label, ref value))
            setter(value);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon))
            fileDialogService.OpenFilePicker(pickerTitle, ".*", (ok, files) =>
            {
                if (ok && files.Count > 0)
                    setter(files[0]);
            }, 1, null, false);
    }

    private void DrawOutputPath()
    {
        using var id = Im.Id.Push("Output Path"u8);

        Im.Input.Text("Output Path"u8, ref _outputPath);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon))
            fileDialogService.OpenSavePicker("Luna.DirectX Tester: Select Output", ".*", "texture.dds", ".dds", (ok, file) =>
            {
                if (ok)
                    _outputPath = file;
            }, null, false);
    }

    private void DrawOutputOptions()
    {
        if (Path.GetExtension(_outputPath).ToLowerInvariant() is not ".dds" and not ".tex" and not ".atex")
            return;

        Im.Combo.DrawEnum("Compression"u8, ref _saveType);
        if (_saveType is not CombinedTexture.TextureSaveType.AsIs)
            Im.Checkbox("Generate Mipmaps"u8, ref _saveWithMips);
    }

    private void DrawGraphOptions()
    {
        switch (_graphPreset)
        {
            case GraphPreset.ResampleScale:
                Im.Drag("Scale"u8, ref _optScale);
                Im.Combo.DrawEnum("Resampling Method"u8, ref _optResample, static resample => resample.ToNameU8());
                break;
            case GraphPreset.ResampleFixed:
                DrawDimensions();
                Im.Combo.DrawEnum("Resampling Method"u8, ref _optResample, static resample => resample.ToNameU8());
                break;
            case GraphPreset.Blend4:
                Im.DragN("Foreground UV Transform"u8, AsFloats(ref _optFgXform));
                Im.DragN("Foreground UV Offset"u8,    AsFloats(ref _optFgOffset));
                Im.Combo.DrawEnum("Foreground Resampling Method"u8, ref _optResample, static resample => resample.ToNameU8());
                DrawBlendOptions();
                break;
            case GraphPreset.Composite:
                Im.DragN("Foreground UV Transform"u8, AsFloats(ref _optFgXform));
                Im.DragN("Foreground UV Offset"u8,    AsFloats(ref _optFgOffset));
                Im.Combo.DrawEnum("Foreground Resampling Method"u8, ref _optResample,  static resample => resample.ToNameU8());
                Im.Combo.DrawEnum("Compositing Function"u8,         ref _optComposite, static composite => composite.ToNameU8());
                DrawBlendOptions();
                break;
            case GraphPreset.CompositeControlled:
                Im.DragN("Foreground UV Transform"u8, AsFloats(ref _optFgXform));
                Im.DragN("Foreground UV Offset"u8,    AsFloats(ref _optFgOffset));
                Im.Combo.DrawEnum("Foreground Resampling Method"u8, ref _optResample,  static resample => resample.ToNameU8());
                Im.Combo.DrawEnum("Compositing Function"u8,         ref _optComposite, static composite => composite.ToNameU8());
                Im.DragN("Background Control Weights"u8, AsFloats(ref _optBgCtl));
                Im.Drag("Background Control Bias"u8, ref _optBgCtl0);
                Im.DragN("Foreground Control Weights"u8, AsFloats(ref _optFgCtl));
                Im.Drag("Foreground Control Bias"u8, ref _optFgCtl0);
                DrawBlendOptions();
                break;
            case GraphPreset.ApplyIndex:
                for (var i = 0; i < LunaShaders.Palette.Length; ++i)
                    Im.Color.Editor($"Palette Color {(i >> 1) + 1}{((i & 1) is not 0 ? 'B' : 'A')}", ref _optPalette[i],
                        ColorEditorFlags.AlphaPreviewHalf
                      | ColorEditorFlags.Float
                      | ColorEditorFlags.Hdr
                      | ColorEditorFlags.AlphaBar
                      | ColorEditorFlags.DisplayRgb
                      | ColorEditorFlags.InputRgb);
                break;
            case GraphPreset.KawaseBlur:
                Im.DragN("Blurred Rectangle Top-Left UV"u8,           AsFloats(ref _optUvMin));
                Im.DragN("Blurred Rectangle Bottom-Right UV"u8,       AsFloats(ref _optUvMax));
                Im.DragN("Blurred Rectangle Corner Rounding Radii"u8, AsFloats(ref _optRounding));
                Im.Drag("Blur Strength"u8,                ref _optBlur);
                Im.Drag("Opacity of Unblurred Regions"u8, ref _optOpacity);
                break;
            case GraphPreset.RefractionRaycast:
                Im.Drag("Depth"u8,               ref _optDepth);
                Im.Drag("Index of Refraction"u8, ref _optIor);
                break;
            case GraphPreset.DyeGlossOverlay:
                DrawDimensions();
                Im.DragN("Corner Rounding Radii"u8, AsFloats(ref _optRounding));
                break;
            case GraphPreset.Custom:
                Im.Drag("Scale"u8, ref _optScale);
                Im.Checkbox("Use Nearest-Neighbor Sampling"u8, ref _optNn);
                break;
            case GraphPreset.CustomCompute:
                DrawDimensions();
                DrawThreadGroupCount();
                Im.Checkbox("Use Nearest-Neighbor Sampling"u8, ref _optNn);
                break;
        }
    }

    private bool DrawDimensions()
    {
        Span<int> dimensions = stackalloc int[2];
        dimensions[0] = _optWidth;
        dimensions[1] = _optHeight;
        if (!Im.DragN("Dimensions"u8, dimensions))
            return false;

        _optWidth  = dimensions[0];
        _optHeight = dimensions[1];
        return true;
    }

    private void DrawBlendOptions()
    {
        var blend = (uint)(_optBlend & ~LunaShaders.Blend.SwapInputs);
        if (Im.Combo.DrawItems("Blend Function"u8, ref blend, ComboFlags.None, static blend => ((LunaShaders.Blend)blend).ToNameU8(),
                LunaShaders.Blend.WellKnownFunctions.Select(static blend => (uint)blend)))
            _optBlend = (_optBlend & LunaShaders.Blend.SwapInputs) | (LunaShaders.Blend)blend;
        if (!_optBlend.Commutative)
        {
            Im.Line.Same();
            var blendSwap = _optBlend.HasFlag(LunaShaders.Blend.SwapInputs);
            if (Im.Checkbox("Swap Inputs"u8, ref blendSwap))
                _optBlend = blendSwap ? _optBlend | LunaShaders.Blend.SwapInputs : _optBlend & ~LunaShaders.Blend.SwapInputs;
        }

        switch (_optBlend & LunaShaders.Blend.FunctionMask)
        {
            case LunaShaders.Blend.Lerp: Im.DragN("Interpolation Weights"u8, AsFloats(ref _optLerp)); break;
        }
    }

    private bool DrawThreadGroupCount()
    {
        Span<int> tgc = stackalloc int[3];
        tgc[0] = _optTgcX;
        tgc[1] = _optTgcY;
        tgc[2] = _optTgcZ;
        if (!Im.DragN("Thread Group Count"u8, tgc))
            return false;

        _optTgcX = Math.Clamp(tgc[0], D3D11.D3D11_CS_THREAD_GROUP_MIN_X, D3D11.D3D11_CS_THREAD_GROUP_MAX_X);
        _optTgcY = Math.Clamp(tgc[1], D3D11.D3D11_CS_THREAD_GROUP_MIN_Y, D3D11.D3D11_CS_THREAD_GROUP_MAX_Y);
        _optTgcZ = Math.Clamp(tgc[2], D3D11.D3D11_CS_THREAD_GROUP_MIN_Z, D3D11.D3D11_CS_THREAD_GROUP_MAX_Z);
        return true;
    }

    private void DrawActions()
    {
        using var disabled = Im.Disabled(!_lastRun.IsCompleted);

        if (ImEx.Icon.LabeledButton(FontAwesomeIcon.Play.Icon(), "Run"u8))
        {
            _lastGraph?.Dispose();
            try
            {
                (_lastGraph, _lastOutput) = BuildGraph();
            }
            catch (Exception e)
            {
                _lastGraph  = null;
                _lastOutput = default;
                _lastRun    = Task.FromException(e);
            }

            if (_lastGraph is not null)
                _lastRun = textureManager.RunEffectGraph(_lastGraph);
        }

        Im.Line.Same();
        using (Im.Disabled(_lastGraph is null))
        {
            if (Im.Button("Re-run"u8) && _lastGraph is not null)
                _lastRun = textureManager.RunEffectGraph(_lastGraph);
        }

        Im.Tooltip.OnHover(
            "This will re-run the effect graph as it was last built by the \"Run\" action, without taking into account any changes in the settings.\nThis action is only here to test the Luna.DirectX framework itself."u8);

        Im.Line.Same();
        if (Im.Button("Clear"u8))
        {
            _lastOutput = default;
            _lastGraph?.Dispose();
            _lastGraph = null;
            _lastRun   = Task.CompletedTask;
        }
    }

    private void DrawResults()
    {
        if (!_lastRun.IsCompleted)
        {
            Im.Text("Cooking..."u8);
            return;
        }

        if (!_lastRun.IsCompletedSuccessfully)
        {
            using var color = ImGuiColor.Text.Push(ImGuiColors.ErrorForeground);
            Im.Text(_lastRun.Exception?.ToString() ?? "An unspecified error occurred.");
            return;
        }

        var output = _lastOutput.Id;
        if (output.IsNull)
            return;

        var dimensions = output.Dimensions;
        var imageSize  = new Vector2(dimensions.Width, dimensions.Height);
        var iconSize   = imageSize;
        if (iconSize.X > 320.0f || iconSize.Y > 320.0f)
            iconSize *= 320.0f / MathF.Max(iconSize.X, iconSize.Y);
        Im.Image.DrawScaled(output, iconSize, imageSize);
    }

    private (EffectGraph, TextureStandIn) BuildGraph()
    {
        switch (_graphPreset)
        {
            case GraphPreset.ResampleScale:
            {
                var input  = BuildLoadInput();
                var resize = BuildResampleScale(input);
                var output = BuildSaveOutput(resize);
                return ([input, resize, output], new TextureStandIn(resize, 0));
            }
            case GraphPreset.ResampleFixed:
            {
                var input  = BuildLoadInput();
                var resize = BuildResampleFixed(input);
                var output = BuildSaveOutput(resize);
                return ([input, resize, output], new TextureStandIn(resize, 0));
            }
            case GraphPreset.Blend4:
            {
                var (input1, input2) = BuildLoadInputs2();
                var blend  = BuildBlend4(input1, input2);
                var output = BuildSaveOutput(blend);
                return ([input1, input2, blend, output], new TextureStandIn(blend, 0));
            }
            case GraphPreset.Composite:
            {
                var (input1, input2) = BuildLoadInputs2();
                var composite = BuildComposite(input1, input2);
                var output    = BuildSaveOutput(composite);
                return ([input1, input2, composite, output], new TextureStandIn(composite, 0));
            }
            case GraphPreset.CompositeControlled:
            {
                var (input1, input2, input3, input4) = BuildLoadInputs4();
                var composite = BuildCompositeControlled(input1, input2, input3, input4);
                var output    = BuildSaveOutput(composite);
                return ([input1, input2, composite, output], new TextureStandIn(composite, 0));
            }
            case >= GraphPreset.ExtractRed and <= GraphPreset.Grayscale:
            {
                var input     = BuildLoadInput();
                var transform = BuildColorTransform(input);
                var output    = BuildSaveOutput(transform);
                return ([input, transform, output], new TextureStandIn(transform, 0));
            }
            case GraphPreset.ApplyIndex:
            {
                var input  = BuildLoadInput();
                var result = BuildApplyIndex(input);
                var output = BuildSaveOutput(result);
                return ([input, result, output], new TextureStandIn(result, 0));
            }
            case GraphPreset.KawaseBlur:
            {
                var input  = BuildLoadInput();
                var blur   = BuildKawaseBlur(input);
                var output = BuildSaveOutput(blur);
                return ([input, blur, output], new TextureStandIn(blur, 0));
            }
            case GraphPreset.RefractionRaycast:
            {
                var input      = BuildLoadInput();
                var projection = BuildRefractionRaycast(input);
                var output     = BuildSaveOutput(projection);
                return ([input, projection, output], new TextureStandIn(projection, 0));
            }
            case GraphPreset.DyeGlossOverlay:
            {
                var generate = BuildDyeGlossOverlay();
                var output   = BuildSaveOutput(generate);
                return ([generate, output], new TextureStandIn(generate, 0));
            }
            case GraphPreset.Custom:
            {
                var input  = BuildLoadInput();
                var filter = BuildCustom(input);
                var output = BuildSaveOutput(filter);
                return ([input, filter, output], new TextureStandIn(filter, 0));
            }
            case GraphPreset.CustomCompute:
            {
                var input  = BuildLoadInput();
                var filter = BuildCustomCompute(input);
                var output = BuildSaveOutput(filter);
                return ([input, filter, output], new TextureStandIn(filter, 0));
            }
            default: throw new NotImplementedException();
        }
    }

    private IEffect BuildLoadInput()
        => BuildLoadInput(_inputPath);

    private (IEffect, IEffect) BuildLoadInputs2()
    {
        var input1 = BuildLoadInput();
        var input2 = string.Equals(_inputPath, _input2Path, StringComparison.Ordinal) ? input1 : BuildLoadInput(_input2Path);
        return (input1, input2);
    }

    private (IEffect, IEffect, IEffect) BuildLoadInputs3()
    {
        var (input1, input2) = BuildLoadInputs2();
        var input3 = string.Equals(_inputPath, _input3Path, StringComparison.Ordinal) ? input1 :
            string.Equals(_input2Path,         _input3Path, StringComparison.Ordinal) ? input2 : BuildLoadInput(_input3Path);
        return (input1, input2, input3);
    }

    private (IEffect, IEffect, IEffect, IEffect) BuildLoadInputs4()
    {
        var (input1, input2, input3) = BuildLoadInputs3();
        var input4 = string.Equals(_inputPath, _input4Path, StringComparison.Ordinal) ? input1 :
            string.Equals(_input2Path,         _input4Path, StringComparison.Ordinal) ? input2 :
            string.Equals(_input3Path,         _input4Path, StringComparison.Ordinal) ? input3 : BuildLoadInput(_input4Path);
        return (input1, input2, input3, input4);
    }

    private IEffect BuildLoadInput(string path)
        => Path.IsPathRooted(path)
            ? new LoadEffect(textureProvider.CreateFromImageAsync(File.OpenRead(path)))
            : new LoadEffect(LoadInputGameFile(path));

    private async Task<IDalamudTextureWrap> LoadInputGameFile(string path)
        => await textureProvider.CreateFromTexFileAsync(await gameData.GetFileAsync<TexFile>(path, CancellationToken.None));

    private IEffect BuildResampleScale(IEffect input)
        => LunaEffects.Resample(new TextureStandIn(input, 0), _optScale, _optResample);

    private IEffect BuildResampleFixed(IEffect input)
        => LunaEffects.Resample(new TextureStandIn(input, 0), (_optWidth, _optHeight), _optResample);

    private IEffect BuildBlend4(IEffect input, IEffect input2)
        => LunaEffects.Blend4(new TextureStandIn(input, 0), new TextureStandIn(input2, 0), out _, new LunaShaders.Blend4Uniforms
            {
                ForegroundTransform = _optFgXform,
                ForegroundOffset    = _optFgOffset,
                Blend               = _optBlend,
                BlendParameters     = GetBlendParameters(),
            },
            _optResample);

    private IEffect BuildComposite(IEffect input, IEffect input2)
        => LunaEffects.Composite(new TextureStandIn(input, 0), new TextureStandIn(input2, 0), out _, new LunaShaders.CompositeUniforms
            {
                ForegroundTransform   = _optFgXform,
                ForegroundOffset      = _optFgOffset,
                Blend                 = _optBlend,
                ColorCompositeWeights = _optComposite.Weights,
                AlphaCompositeWeights = _optComposite.Weights,
                BlendParameters       = GetBlendParameters(),
            },
            _optResample);

    private IEffect BuildCompositeControlled(IEffect input, IEffect input2, IEffect input3, IEffect input4)
        => LunaEffects.CompositeControlled(new TextureStandIn(input, 0), new TextureStandIn(input3, 0), new TextureStandIn(input2, 0),
            new TextureStandIn(input4,                               0), out _, new LunaShaders.CompositeControlledUniforms
            {
                ForegroundTransform      = _optFgXform,
                ForegroundOffset         = _optFgOffset,
                Blend                    = _optBlend,
                CompositeWeights         = _optComposite.Weights,
                BackgroundControl0       = _optBgCtl0,
                ControlCompositeWeights  = _optComposite.Weights,
                ForegroundControl0       = _optFgCtl0,
                BackgroundControlWeights = _optBgCtl,
                ForegroundControlWeights = _optFgCtl,
                BlendParameters          = GetBlendParameters(),
            },
            _optResample);

    private LunaShaders.BlendParameters GetBlendParameters()
        => (_optBlend & LunaShaders.Blend.FunctionMask) switch
        {
            LunaShaders.Blend.Lerp => new LunaShaders.BlendParameters
            {
                LerpWeights = _optLerp,
            },
            _ => default,
        };

    private IEffect BuildApplyIndex(IEffect input)
        => LunaEffects.ApplyIndex(new TextureStandIn(input, 0), out _, new LunaShaders.ApplyIndexUniforms
        {
            Exponent = Vector4.One,
            Palette  = _optPalette,
        });

    private IEffect BuildKawaseBlur(IEffect input)
        => LunaEffects.KawaseBlur(new TextureStandIn(input, 0), out _, new LunaShaders.KawaseUniforms
        {
            BlurRectRounding = _optRounding,
            BlurRectUvMin    = _optUvMin,
            BlurRectUvMax    = _optUvMax,
            BlurStrength     = _optBlur,
            UnblurredOpacity = _optOpacity,
        });

    private IEffect BuildColorTransform(IEffect input)
        => LunaEffects.ColorTransform(new TextureStandIn(input, 0), out _, new LunaShaders.ColorTransformUniforms
        {
            BasisRed = _graphPreset switch
            {
                GraphPreset.ExtractRed => Vector4.One - Vector4.UnitW,
                GraphPreset.Grayscale  => new Vector4(0.2126f, 0.2126f, 0.2126f, 0.0f),
                _                      => Vector4.Zero,
            },
            BasisGreen = _graphPreset switch
            {
                GraphPreset.ExtractGreen => Vector4.One - Vector4.UnitW,
                GraphPreset.Grayscale    => new Vector4(0.7152f, 0.7152f, 0.7152f, 0.0f),
                _                        => Vector4.Zero,
            },
            BasisBlue = _graphPreset switch
            {
                GraphPreset.ExtractBlue => Vector4.One - Vector4.UnitW,
                GraphPreset.Grayscale   => new Vector4(0.0722f, 0.0722f, 0.0722f, 0.0f),
                _                       => Vector4.Zero,
            },
            BasisAlpha = _graphPreset switch
            {
                GraphPreset.ExtractAlpha => Vector4.One - Vector4.UnitW,
                GraphPreset.Grayscale    => Vector4.UnitW,
                _                        => Vector4.Zero,
            },
            Origin = _graphPreset switch
            {
                GraphPreset.Grayscale => Vector4.Zero,
                _                     => Vector4.UnitW,
            },
        });

    private IEffect BuildRefractionRaycast(IEffect input)
        => LunaEffects.RefractionRaycast(new TextureStandIn(input, 0), out _, new LunaShaders.RefractionRaycastUniforms
        {
            Depth             = _optDepth,
            IndexOfRefraction = _optIor,
        });

    private IEffect BuildSaveOutput(IEffect input)
        => string.IsNullOrEmpty(_outputPath)
            ? IEffect.Null
            : Path.GetExtension(_outputPath).ToLowerInvariant() switch
            {
                ".dds" or ".tex" or ".atex" => new SaveToDdsTexFileEffect(readbackProvider, textureManager, _outputPath, _saveType,
                    _saveType is CombinedTexture.TextureSaveType.AsIs ? null : _saveWithMips)
                {
                    Input = new TextureStandIn(input, 0),
                },
                _ => new SaveToFileEffect(readbackProvider, _outputPath)
                {
                    Input = new TextureStandIn(input, 0),
                },
            };

    private IEffect BuildDyeGlossOverlay()
        => new ShaderFilterEffect(LunaShaders.DyeGlossOverlay, new ConstantBuffer<LunaShaders.DyeGlossOverlayUniforms>(
            new LunaShaders.DyeGlossOverlayUniforms
            {
                Rounding = _optRounding,
            }), "Dye Gloss Overlay")
        {
            Dimensions = (_optWidth, _optHeight),
        };

    private IEffect BuildCustom(IEffect input)
    {
        var effect = new ShaderFilterEffect(new PixelShader(File.ReadAllBytes(_shaderPath), Path.GetFileName(_shaderPath)), null,
            $"Bring Your Own Pixel Shader ({Path.GetFileName(_shaderPath)})");
        effect.DimensionsStrategy = ShaderFilterEffect.ScaleLargestInput(_optScale);
        effect.Textures.Add(new TextureStandIn(input, 0));
        effect.Samplers.Add(_optNn ? Sampler.ClampNearestNeighbor : Sampler.ClampBilinear);
        return effect;
    }

    private IEffect BuildCustomCompute(IEffect input)
    {
        var effect = new ComputeFilterEffect(new ComputeShader(File.ReadAllBytes(_shaderPath), Path.GetFileName(_shaderPath)), null,
            $"Bring Your Own Compute Shader ({Path.GetFileName(_shaderPath)})");
        effect.ThreadGroupCount = (_optTgcX, _optTgcY, _optTgcZ);
        effect.Textures.Add(new TextureStandIn(input, 0));
        effect.Outputs.Add(new RwImage((_optWidth, _optHeight), FullScreenQuad.DefaultOutputFormat));
        effect.Samplers.Add(_optNn ? Sampler.ClampNearestNeighbor : Sampler.ClampBilinear);
        return effect;
    }

    private static Span<float> AsFloats<T>(ref T container) where T : unmanaged
        => MemoryMarshal.Cast<T, float>(new Span<T>(ref container));
}
