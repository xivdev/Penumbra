using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Files;
using Penumbra.Interop.ResourceTree;
using Structs = Penumbra.Interop.Structs;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private static unsafe Character* FindLocalPlayer(IObjectTable objects)
    {
        var localPlayer = objects[0];
        if (localPlayer is not Dalamud.Game.ClientState.Objects.Types.Character)
            return null;

        return (Character*)localPlayer.Address;
    }

    private static unsafe Character* FindSubActor(Character* character, int subActorType)
    {
        if (character == null)
            return null;

        switch (subActorType)
        {
            case -1:
                return character;
            case 0:
                return character->Mount.MountObject;
            case 1:
                var companion = character->Companion.CompanionObject;
                if (companion == null)
                    return null;
                return &companion->Character;
            case 2:
                var ornament = character->Ornament.OrnamentObject;
                if (ornament == null)
                    return null;
                return &ornament->Character;
            default:
                return null;
        }
    }

    private static unsafe List<(int SubActorType, int ChildObjectIndex, int ModelSlot, int MaterialSlot)> FindMaterial(CharacterBase* drawObject, int subActorType, string materialPath)
    {
        static void CollectMaterials(List<(int, int, int, int)> result, int subActorType, int childObjectIndex, CharacterBase* drawObject, string materialPath)
        {
            for (var i = 0; i < drawObject->SlotCount; ++i)
            {
                var model = drawObject->Models[i];
                if (model == null)
                    continue;

                for (var j = 0; j < model->MaterialCount; ++j)
                {
                    var material = model->Materials[j];
                    if (material == null)
                        continue;

                    var mtrlHandle = material->MaterialResourceHandle;
                    if (mtrlHandle == null)
                        continue;

                    var path = ResolveContext.GetResourceHandlePath((Structs.ResourceHandle*)mtrlHandle);
                    if (path.ToString() == materialPath)
                        result.Add((subActorType, childObjectIndex, i, j));
                }
            }
        }

        var result = new List<(int, int, int, int)>();

        if (drawObject == null)
            return result;

        materialPath = materialPath.Replace('/', '\\').ToLowerInvariant();

        CollectMaterials(result, subActorType, -1, drawObject, materialPath);

        var firstChildObject = (CharacterBase*)drawObject->DrawObject.Object.ChildObject;
        if (firstChildObject != null)
        {
            var childObject = firstChildObject;
            var childObjectIndex = 0;
            do
            {
                CollectMaterials(result, subActorType, childObjectIndex, childObject, materialPath);

                childObject = (CharacterBase*)childObject->DrawObject.Object.NextSiblingObject;
                ++childObjectIndex;
            }
            while (childObject != null && childObject != firstChildObject);
        }

        return result;
    }

    private static unsafe CharacterBase* GetChildObject(CharacterBase* drawObject, int index)
    {
        if (drawObject == null)
            return null;

        if (index >= 0)
        {
            drawObject = (CharacterBase*)drawObject->DrawObject.Object.ChildObject;
            if (drawObject == null)
                return null;
        }

        var first = drawObject;
        while (index-- > 0)
        {
            drawObject = (CharacterBase*)drawObject->DrawObject.Object.NextSiblingObject;
            if (drawObject == null || drawObject == first)
                return null;
        }

        return drawObject;
    }

    private static unsafe Material* GetDrawObjectMaterial(CharacterBase* drawObject, int modelSlot, int materialSlot)
    {
        if (drawObject == null)
            return null;

        if (modelSlot < 0 || modelSlot >= drawObject->SlotCount)
            return null;

        var model = drawObject->Models[modelSlot];
        if (model == null)
            return null;

        if (materialSlot < 0 || materialSlot >= model->MaterialCount)
            return null;

        return model->Materials[materialSlot];
    }

    private abstract unsafe class LiveMaterialPreviewerBase : IDisposable
    {
        private readonly IObjectTable _objects;

        protected readonly int SubActorType;
        protected readonly int ChildObjectIndex;
        protected readonly int ModelSlot;
        protected readonly int MaterialSlot;

        protected readonly CharacterBase* DrawObject;
        protected readonly Material*      Material;

        protected bool Valid;

        public LiveMaterialPreviewerBase(IObjectTable objects, int subActorType, int childObjectIndex, int modelSlot, int materialSlot)
        {
            _objects = objects;

            SubActorType     = subActorType;
            ChildObjectIndex = childObjectIndex;
            ModelSlot        = modelSlot;
            MaterialSlot     = materialSlot;

            var localPlayer = FindLocalPlayer(objects);
            if (localPlayer == null)
                throw new InvalidOperationException("Cannot retrieve local player object");

            var subActor = FindSubActor(localPlayer, subActorType);
            if (subActor == null)
                throw new InvalidOperationException("Cannot retrieve sub-actor (mount, companion or ornament)");

            DrawObject = GetChildObject((CharacterBase*)subActor->GameObject.GetDrawObject(), childObjectIndex);
            if (DrawObject == null)
                throw new InvalidOperationException("Cannot retrieve draw object");

            Material = GetDrawObjectMaterial(DrawObject, modelSlot, materialSlot);
            if (Material == null)
                throw new InvalidOperationException("Cannot retrieve material");

            Valid = true;
        }

        ~LiveMaterialPreviewerBase()
        {
            if (Valid)
                Dispose(false, IsStillValid());
        }

        public void Dispose()
        {
            if (Valid)
                Dispose(true, IsStillValid());
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing, bool reset)
        {
            Valid = false;
        }

        public bool CheckValidity()
        {
            if (Valid && !IsStillValid())
                Dispose(false, false);

            return Valid;
        }

        protected virtual bool IsStillValid()
        {
            var localPlayer = FindLocalPlayer(_objects);
            if (localPlayer == null)
                return false;

            var subActor = FindSubActor(localPlayer, SubActorType);
            if (subActor == null)
                return false;

            if (DrawObject != GetChildObject((CharacterBase*)subActor->GameObject.GetDrawObject(), ChildObjectIndex))
                return false;

            if (Material != GetDrawObjectMaterial(DrawObject, ModelSlot, MaterialSlot))
                return false;

            return true;
        }
    }

    private sealed unsafe class LiveMaterialPreviewer : LiveMaterialPreviewerBase
    {
        private readonly ShaderPackage* _shaderPackage;

        private readonly uint    _originalShPkFlags;
        private readonly float[] _originalMaterialParameter;
        private readonly uint[]  _originalSamplerFlags;

        public LiveMaterialPreviewer(IObjectTable objects, int subActorType, int childObjectIndex, int modelSlot, int materialSlot) : base(objects, subActorType, childObjectIndex, modelSlot, materialSlot)
        {
            var mtrlHandle = Material->MaterialResourceHandle;
            if (mtrlHandle == null)
                throw new InvalidOperationException("Material doesn't have a resource handle");

            var shpkHandle = ((Structs.MtrlResource*)mtrlHandle)->ShpkResourceHandle;
            if (shpkHandle == null)
                throw new InvalidOperationException("Material doesn't have a ShPk resource handle");

            _shaderPackage = shpkHandle->ShaderPackage;
            if (_shaderPackage == null)
                throw new InvalidOperationException("Material doesn't have a shader package");

            var material = (Structs.Material*)Material;

            _originalShPkFlags = material->ShaderPackageFlags;

            if (material->MaterialParameter->TryGetBuffer(out var materialParameter))
                _originalMaterialParameter = materialParameter.ToArray();
            else
                _originalMaterialParameter = Array.Empty<float>();

            _originalSamplerFlags = new uint[material->TextureCount];
            for (var i = 0; i < _originalSamplerFlags.Length; ++i)
                _originalSamplerFlags[i] = material->Textures[i].SamplerFlags;
        }

        protected override void Dispose(bool disposing, bool reset)
        {
            base.Dispose(disposing, reset);

            if (reset)
            {
                var material = (Structs.Material*)Material;

                material->ShaderPackageFlags = _originalShPkFlags;

                if (material->MaterialParameter->TryGetBuffer(out var materialParameter))
                    _originalMaterialParameter.AsSpan().CopyTo(materialParameter);

                for (var i = 0; i < _originalSamplerFlags.Length; ++i)
                    material->Textures[i].SamplerFlags = _originalSamplerFlags[i];
            }
        }

        public void SetShaderPackageFlags(uint shPkFlags)
        {
            if (!CheckValidity())
                return;

            ((Structs.Material*)Material)->ShaderPackageFlags = shPkFlags;
        }

        public void SetMaterialParameter(uint parameterCrc, Index offset, Span<float> value)
        {
            if (!CheckValidity())
                return;

            var cbuffer = ((Structs.Material*)Material)->MaterialParameter;
            if (cbuffer == null)
                return;

            if (!cbuffer->TryGetBuffer(out var buffer))
                return;

            for (var i = 0; i < _shaderPackage->MaterialElementCount; ++i)
            {
                ref var parameter = ref _shaderPackage->MaterialElements[i];
                if (parameter.CRC == parameterCrc)
                {
                    if ((parameter.Offset & 0x3) != 0 || (parameter.Size & 0x3) != 0 || (parameter.Offset + parameter.Size) >> 2 > buffer.Length)
                        return;

                    value.TryCopyTo(buffer.Slice(parameter.Offset >> 2, parameter.Size >> 2)[offset..]);
                    return;
                }
            }
        }

        public void SetSamplerFlags(uint samplerCrc, uint samplerFlags)
        {
            if (!CheckValidity())
                return;

            var id = 0u;
            var found = false;

            var samplers = (Structs.ShaderPackageUtility.Sampler*)_shaderPackage->Samplers;
            for (var i = 0; i < _shaderPackage->SamplerCount; ++i)
            {
                if (samplers[i].Crc == samplerCrc)
                {
                    id = samplers[i].Id;
                    found = true;
                    break;
                }
            }

            if (!found)
                return;

            var material = (Structs.Material*)Material;
            for (var i = 0; i < material->TextureCount; ++i)
            {
                if (material->Textures[i].Id == id)
                {
                    material->Textures[i].SamplerFlags = (samplerFlags & 0xFFFFFDFF) | 0x000001C0;
                    break;
                }
            }
        }

        protected override bool IsStillValid()
        {
            if (!base.IsStillValid())
                return false;

            var mtrlHandle = Material->MaterialResourceHandle;
            if (mtrlHandle == null)
                return false;

            var shpkHandle = ((Structs.MtrlResource*)mtrlHandle)->ShpkResourceHandle;
            if (shpkHandle == null)
                return false;

            if (_shaderPackage != shpkHandle->ShaderPackage)
                return false;

            return true;
        }
    }

    private sealed unsafe class LiveColorSetPreviewer : LiveMaterialPreviewerBase
    {
        public const int TextureWidth  = 4;
        public const int TextureHeight = MtrlFile.ColorSet.RowArray.NumRows;
        public const int TextureLength = TextureWidth * TextureHeight * 4;

        private readonly Framework _framework;

        private readonly Texture** _colorSetTexture;
        private readonly Texture*  _originalColorSetTexture;

        private Half[] _colorSet;
        private bool   _updatePending;

        public Half[] ColorSet => _colorSet;

        public LiveColorSetPreviewer(IObjectTable objects, Framework framework, int subActorType, int childObjectIndex, int modelSlot, int materialSlot) : base(objects, subActorType, childObjectIndex, modelSlot, materialSlot)
        {
            _framework = framework;

            var mtrlHandle = Material->MaterialResourceHandle;
            if (mtrlHandle == null)
                throw new InvalidOperationException("Material doesn't have a resource handle");

            var colorSetTextures = *(Texture***)((nint)DrawObject + 0x258);
            if (colorSetTextures == null)
                throw new InvalidOperationException("Draw object doesn't have color set textures");

            _colorSetTexture = colorSetTextures + (modelSlot * 4 + materialSlot);

            _originalColorSetTexture = *_colorSetTexture;
            if (_originalColorSetTexture == null)
                throw new InvalidOperationException("Material doesn't have a color set");
            Structs.TextureUtility.IncRef(_originalColorSetTexture);

            _colorSet = new Half[TextureLength];
            _updatePending = true;

            framework.Update += OnFrameworkUpdate;
        }

        protected override void Dispose(bool disposing, bool reset)
        {
            _framework.Update -= OnFrameworkUpdate;

            base.Dispose(disposing, reset);

            if (reset)
            {
                var oldTexture = (Texture*)Interlocked.Exchange(ref *(nint*)_colorSetTexture, (nint)_originalColorSetTexture);
                Structs.TextureUtility.DecRef(oldTexture);
            }
            else
                Structs.TextureUtility.DecRef(_originalColorSetTexture);
        }

        public void ScheduleUpdate()
        {
            _updatePending = true;
        }

        private void OnFrameworkUpdate(Framework _)
        {
            if (!_updatePending)
                return;
            _updatePending = false;

            if (!CheckValidity())
                return;

            var textureSize = stackalloc int[2];
            textureSize[0] = TextureWidth;
            textureSize[1] = TextureHeight;

            var newTexture = Structs.TextureUtility.Create2D(Device.Instance(), textureSize, 1, 0x2460, 0x80000804, 7);
            if (newTexture == null)
                return;

            bool success;
            lock (_colorSet)
                fixed (Half* colorSet = _colorSet)
                    success = Structs.TextureUtility.InitializeContents(newTexture, colorSet);

            if (success)
            {
                var oldTexture = (Texture*)Interlocked.Exchange(ref *(nint*)_colorSetTexture, (nint)newTexture);
                Structs.TextureUtility.DecRef(oldTexture);
            }
            else
                Structs.TextureUtility.DecRef(newTexture);
        }

        protected override bool IsStillValid()
        {
            if (!base.IsStillValid())
                return false;

            var colorSetTextures = *(Texture***)((nint)DrawObject + 0x258);
            if (colorSetTextures == null)
                return false;

            if (_colorSetTexture != colorSetTextures + (ModelSlot * 4 + MaterialSlot))
                return false;

            return true;
        }
    }
}
