using FFXIVClientStructs.Havok.Common.Base.System.IO.OStream;
using FFXIVClientStructs.Havok.Common.Base.Types;
using FFXIVClientStructs.Havok.Common.Serialize.Resource;
using FFXIVClientStructs.Havok.Common.Serialize.Util;

namespace Penumbra.Import.Models;

public static unsafe class HavokConverter
{
    /// <summary> Creates a temporary file and returns its path. </summary>
    private static string CreateTempFile()
    {
        var stream = File.Create(Path.GetTempFileName());
        stream.Close();
        return stream.Name;
    }

    /// <summary> Converts a .hkx file to a .xml file. </summary>
    /// <param name="hkx"> A byte array representing the .hkx file. </param> 
    public static string HkxToXml(byte[] hkx)
    {
        const hkSerializeUtil.SaveOptionBits options = hkSerializeUtil.SaveOptionBits.SerializeIgnoredMembers
          | hkSerializeUtil.SaveOptionBits.TextFormat
          | hkSerializeUtil.SaveOptionBits.WriteAttributes;

        var tempHkx = CreateTempFile();
        File.WriteAllBytes(tempHkx, hkx);

        var resource = Read(tempHkx);
        File.Delete(tempHkx);

        if (resource == null)
            throw new Exception("Failed to read havok file.");

        var file = Write(resource, options);
        file.Close();

        var bytes = File.ReadAllText(file.Name);
        File.Delete(file.Name);

        return bytes;
    }

    /// <summary> Converts an .xml file to a .hkx file. </summary>
    /// <param name="xml"> A string representing the .xml file. </param>
    public static byte[] XmlToHkx(string xml)
    {
        const hkSerializeUtil.SaveOptionBits options = hkSerializeUtil.SaveOptionBits.SerializeIgnoredMembers
          | hkSerializeUtil.SaveOptionBits.WriteAttributes;

        var tempXml = CreateTempFile();
        File.WriteAllText(tempXml, xml);

        var resource = Read(tempXml);
        File.Delete(tempXml);

        if (resource == null)
            throw new Exception("Failed to read havok file.");

        var file = Write(resource, options);
        file.Close();

        var bytes = File.ReadAllBytes(file.Name);
        File.Delete(file.Name);

        return bytes;
    }

    /// <summary>
    /// Parses a serialized file into an hkResource*.
    /// The type is guessed automatically by Havok.
    /// This pointer might be null - you should check for that.
    /// </summary>
    /// <param name="filePath"> Path to a file on the filesystem. </param>
    private static hkResource* Read(string filePath)
    {
        var path                = Encoding.UTF8.GetBytes(filePath);
        var builtinTypeRegistry = hkBuiltinTypeRegistry.Instance();

        var loadOptions = stackalloc hkSerializeUtil.LoadOptions[1];
        loadOptions->Flags = new hkFlags<hkSerializeUtil.LoadOptionBits, int> { Storage = (int)hkSerializeUtil.LoadOptionBits.Default };
        loadOptions->ClassNameRegistry = builtinTypeRegistry->GetClassNameRegistry();
        loadOptions->TypeInfoRegistry = builtinTypeRegistry->GetTypeInfoRegistry();

        // TODO: probably can use LoadFromBuffer for this.
        return hkSerializeUtil.LoadFromFile(path, null, loadOptions);
    }

    /// <summary> Serializes an hkResource* to a temporary file. </summary>
    /// <param name="resource"> A pointer to the hkResource, opened through Read(). </param>
    /// <param name="optionBits"> Flags representing how to serialize the file. </param>
    private static FileStream Write(
        hkResource* resource,
        hkSerializeUtil.SaveOptionBits optionBits
    )
    {
        var tempFile = CreateTempFile();
        var path     = Encoding.UTF8.GetBytes(tempFile);
        var oStream  = new hkOstream();
        oStream.Ctor(path);

        var result = stackalloc hkResult[1];

        var saveOptions = new hkSerializeUtil.SaveOptions()
        {
            Flags = new hkFlags<hkSerializeUtil.SaveOptionBits, int> { Storage = (int)optionBits },
        };

        var builtinTypeRegistry = hkBuiltinTypeRegistry.Instance();
        var classNameRegistry   = builtinTypeRegistry->GetClassNameRegistry();
        var typeInfoRegistry    = builtinTypeRegistry->GetTypeInfoRegistry();

        try
        {
            const string name = "hkRootLevelContainer";

            var resourcePtr = (hkRootLevelContainer*)resource->GetContentsPointer(name, typeInfoRegistry);
            if (resourcePtr == null)
                throw new Exception("Failed to retrieve havok root level container resource.");

            var hkRootLevelContainerClass = classNameRegistry->GetClassByName(name);
            if (hkRootLevelContainerClass == null)
                throw new Exception("Failed to retrieve havok root level container type.");

            hkSerializeUtil.Save(result, resourcePtr, hkRootLevelContainerClass, oStream.StreamWriter.ptr, saveOptions);
        }
        finally
        {
            oStream.Dtor();
        }

        if (result->Result == hkResult.hkResultEnum.Failure)
            throw new Exception("Failed to serialize havok file.");

        return new FileStream(tempFile, FileMode.Open);
    }
}
