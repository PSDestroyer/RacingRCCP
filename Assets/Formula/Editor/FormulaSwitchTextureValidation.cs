using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only validation of the authored Switch texture overrides.</summary>
public static class FormulaSwitchTextureValidation
{
    private const string Folder = "Docs/AI/SwitchMemory";
    private const string Platform = "Nintendo Switch";

    [MenuItem("Tools/Formula Career/Validate Switch Texture Compression")]
    public static void Validate()
    {
        var manifest = JObject.Parse(File.ReadAllText(Folder + "/TextureCompression.json"));
        bool moduleInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Switch, BuildTarget.Switch);
        var failures = new List<string>();
        int checkedTextures = 0;
        foreach (var row in (JArray)manifest["textures"])
        {
            string path = (string)row["path"];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { failures.Add(path + ": texture importer missing"); continue; }
            checkedTextures++;
            if (AssetDatabase.AssetPathToGUID(path) != (string)row["guid"])
                failures.Add(path + ": GUID changed");
            var settings = importer.GetPlatformTextureSettings(Platform);
            if (!settings.overridden || settings.maxTextureSize != (int)row["afterMaxSize"] ||
                (int)settings.format != (int)row["serializedFormat"] ||
                (int)settings.textureCompression != (int)row["serializedCompression"] ||
                settings.crunchedCompression || settings.compressionQuality != 80)
                failures.Add(path + ": Switch override differs from the manifest");
            if (importer.isReadable || importer.mipmapEnabled != (bool)row["mipmaps"])
                failures.Add(path + ": CPU readability or mipmap settings changed");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != (int)row["sourceWidth"] || height != (int)row["sourceHeight"])
                failures.Add(path + ": source dimensions changed; refresh the memory estimate");
            var format = settings.format == TextureImporterFormat.Automatic
                ? importer.GetAutomaticFormat(Platform) : settings.format;
            if (format.ToString() != (string)row["afterFormat"])
                failures.Add(path + ": resolved format is " + format);
            // Installed platform support is required for authoritative format validation.
            if (moduleInstalled && !TextureImporter.IsPlatformTextureFormatValid(importer.textureType, BuildTarget.Switch, format))
                failures.Add(path + ": format is unsupported by the installed Switch module");
        }
        var report = new
        {
            utc = DateTime.UtcNow.ToString("O"),
            unity = Application.unityVersion,
            status = failures.Count == 0 ? "passed_import_settings" : "failed",
            checkedTextures,
            switchModuleInstalled = moduleInstalled,
            targetFormatValidationPerformed = moduleInstalled,
            switchBuildPerformed = false,
            hardwareMemoryMeasured = false,
            failures
        };
        File.WriteAllText(Folder + "/ImportValidation.json", JsonConvert.SerializeObject(report, Formatting.Indented) + "\n");
        if (failures.Count > 0) Debug.LogError("Switch texture validation failed: " + string.Join("; ", failures));
        else Debug.Log("Switch texture overrides validated: " + checkedTextures +
            (moduleInstalled ? ". Target formats supported." : ". Install the Switch module to validate target format support and build."));
    }
}
