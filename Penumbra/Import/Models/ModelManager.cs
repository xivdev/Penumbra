using Penumbra.GameData.Files;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace Penumbra.Import.Models;

public sealed class ModelManager
{
    public ModelManager()
    {
        //
    }

    // TODO: Consider moving import/export onto an async queue, check ../textures/texturemanager

    public void ExportToGltf(/* MdlFile mdl, */string path)
    {
        var mesh = new MeshBuilder<VertexPosition>("mesh");

        var material1 = new MaterialBuilder()
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 0, 0, 1));
        var primitive1 = mesh.UsePrimitive(material1);
        primitive1.AddTriangle(new VertexPosition(-10, 0, 0), new VertexPosition(10, 0, 0), new VertexPosition(0, 10, 0));
        primitive1.AddTriangle(new VertexPosition(10, 0, 0), new VertexPosition(-10, 0, 0), new VertexPosition(0, -10, 0));
        
        var material2 = new MaterialBuilder()
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 0, 1, 1));
        var primitive2 = mesh.UsePrimitive(material2);
        primitive2.AddQuadrangle(new VertexPosition(-5, 0, 3), new VertexPosition(0, -5, 3), new VertexPosition(5, 0, 3), new VertexPosition(0, 5, 3));

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity);

        var model = scene.ToGltf2();
        model.SaveGLTF(path);

        // TODO: Draw the rest of the owl.
    }
}
