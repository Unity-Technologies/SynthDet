using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental;
using UnityEngine;

namespace Unity.Scenes.Editor
{
    static class LiveLinkBuildPipeline
    {
        const string k_TempBuildPath = "Temp/LLBP";
        private const int AssetBundleBuildVersion = 8;

        internal static GUID k_UnityEditorResources = new GUID("0000000000000000d000000000000000");
        internal static GUID k_UnityBuiltinResources = new GUID("0000000000000000e000000000000000");
        internal static GUID k_UnityBuiltinExtraResources = new GUID("0000000000000000f000000000000000");

        // TODO: This should be part of the IDeterministicIdentifiers api.
        static string GenerateAssetBundleInternalFileName(this IDeterministicIdentifiers generator, string name)
        {
            var internalName = generator.GenerateInternalFileName(name);
            return $"archive:/{internalName}/{internalName}";
        }

        // TODO: This should be part of the IDeterministicIdentifiers api.
        static string GenerateSceneBundleInternalFileName(this IDeterministicIdentifiers generator, string scenePath)
        {
            // 1 scene per bundle, so don't worry about special cases
            var internalName = generator.GenerateInternalFileName(scenePath);
            return $"archive:/{internalName}/{internalName}.sharedAssets";
        }

        // TODO: This should be part of the IDeterministicIdentifiers api.
        static string GenerateSceneInternalFileName(this IDeterministicIdentifiers generator, string scenePath)
        {
            // 1 scene per bundle, so don't worry about special cases
            var internalName = generator.GenerateInternalFileName(scenePath);
            return $"{internalName}.sharedAssets";
        }

        static void FilterBuiltinResourcesObjectManifest(string manifestPath)
        {
            // Builtin Resources in the editor contains a mix of Editor Only & Runtime types.
            // Based on some trial an error, objects with these flags are the Runtime types.
            // TODO: This will probably break someday, and we need a more reliable method for handling this.
            // TODO: Builtin Resources should not contain Editor only types, there are Editor Builtin Resources for this
            var manifest = (AssetObjectManifest)UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(manifestPath)[0];
            var objects = manifest.Objects.Where(x => x.hideFlags == (HideFlags.HideInInspector | HideFlags.HideAndDontSave)).ToArray();
            AssetObjectManifestBuilder.BuildManifest(objects, manifest);

            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] { manifest }, manifestPath, true);
        }

        static void FilterBuiltinExtraResourcesObjectManifest(string manifestPath)
        {
            // Builtin Extra Resources in the editor contains a mix of Editor Only & Runtime types.
            // This is easier to filter as the type info is available to C# and we can just filter on UnityEngine vs UnityEditor
            // TODO: This will probably break someday, and we need a more reliable method for handling this.
            // TODO: Builtin Extra Resources should not contain Editor only types, there are Editor Builtin Resources for this
            var manifest = (AssetObjectManifest)UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(manifestPath)[0];
            var objects = manifest.Objects.Where(x => !x.GetType().FullName.Contains("UnityEditor")).ToArray();
            AssetObjectManifestBuilder.BuildManifest(objects, manifest);

            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] { manifest }, manifestPath, true);
        }

        struct BlobGlobalUsage
        {
            public uint m_LightmapModesUsed;
            public uint m_LegacyLightmapModesUsed;
            public uint m_DynamicLightmapsUsed;
            public uint m_FogModesUsed;
            public bool m_ForceInstancingStrip;
            public bool m_ForceInstancingKeep;
            public bool m_ShadowMasksUsed;
            public bool m_SubtractiveUsed;
        }

        static unsafe BuildUsageTagGlobal ForceKeepInstancingVariants(BuildUsageTagGlobal globalUsage)
        {
            BlobGlobalUsage blob = *(BlobGlobalUsage*)&globalUsage;
            blob.m_ForceInstancingStrip = false;
            blob.m_ForceInstancingKeep = true;
            return *(BuildUsageTagGlobal*)&blob;
        }

        // BuiltinExtra resources are not added to a player build and as such must be referenced and send individually.
        // To avoid having a very special case and expanding LiveLink to support a GUID and FileIdentifier, we are packing the file ident in the end of the GUID.
        // This allows us to check if the GUID unpacks into k_UnityBuiltinExtraResources (0000000000000000f000000000000000) and if so, output the fileIdent for building the AssetBundle.
        // Ideally this will be removed with future improvements, where we stop having builtins packed into a single asset.
        //
        // Example packed builtin_extra GUID: 0000000000000000f000000004820000 
        public static unsafe bool TryRemapBuiltinExtraGuid(ref GUID guid, out long fileIdent)
        {
            fixed (void* ptr = &guid)
            {
                var asHash = (Entities.Hash128*)ptr;
                var temp = asHash->Value.w;
                asHash->Value.w = 0;

                if (guid == k_UnityBuiltinExtraResources)
                {
                    fileIdent = temp;
                    return true;
                }

                asHash->Value.w = temp;
            }

            fileIdent = -1;
            return false;
        }

        public static unsafe void PackBuiltinExtraWithFileIdent(ref GUID guid, long fileIdent)
        {
            fixed (void* ptr = &guid)
            {
                var asHash = (Entities.Hash128*)ptr;
                asHash->Value.w = (uint)fileIdent;
            }
        }

        public static void RemapBuildInAssetGuid(ref string assetGUID)
        {
            var guid = new GUID(assetGUID);
            TryRemapBuiltinExtraGuid(ref guid, out _);

            if (guid == k_UnityBuiltinResources || guid == k_UnityEditorResources || guid == k_UnityBuiltinExtraResources)
            {
                var tempPath = $"Assets/TempAssetCache/{assetGUID}.txt";
                if (!File.Exists(tempPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
                    File.WriteAllText(tempPath, "");
                    AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceSynchronousImport);
                }
                assetGUID = AssetDatabase.AssetPathToGUID(tempPath);
            }
        }

        public static long RemapBuildInAssetPath(ref string assetPath)
        {
            var guid = new GUID(Path.GetFileNameWithoutExtension(assetPath));
            TryRemapBuiltinExtraGuid(ref guid, out var ident);

            // Update ADBv2 to allow extra artifacts to be generated for BuiltIn types
            if (guid == k_UnityBuiltinResources || guid == k_UnityEditorResources || guid == k_UnityBuiltinExtraResources)
            {
                var newPath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                if (!string.IsNullOrEmpty(newPath))
                {
                    assetPath = newPath;
                }
            }

            return ident;
        }

        public static bool BuildSceneBundle(GUID sceneGuid, string cacheFilePath, BuildTarget target, bool collectDependencies = false, HashSet<Entities.Hash128> dependencies = null, HashSet<System.Type> types = null)
        {
            using (new BuildInterfacesWrapper())
            using (new SceneStateCleanup())
            {
                Directory.CreateDirectory(k_TempBuildPath);

                var scene = sceneGuid.ToString();
                var scenePath = AssetDatabase.GUIDToAssetPath(scene);

                // Deterministic ID Generator
                var generator = new Unity5PackedIdentifiers();

                // Target platform settings & script information
                var settings = new BuildSettings
                {
                    buildFlags = ContentBuildFlags.None,
                    target = target,
                    group = BuildPipeline.GetBuildTargetGroup(target),
                    typeDB = null
                };

                // Inter-asset feature usage (shader features, used mesh channels)
                var usageSet = new BuildUsageTagSet();
                var dependencyResults = ContentBuildInterface.CalculatePlayerDependenciesForScene(scenePath, settings, usageSet);

                // Bundle all the needed write parameters
                var writeParams = new WriteSceneParameters
                {
                    // Target platform settings & script information
                    settings = settings,

                    // Scene / Project lighting information
                    globalUsage = dependencyResults.globalUsage,

                    // Inter-asset feature usage (shader features, used mesh channels)
                    usageSet = usageSet,

                    // Scene being written out
                    scenePath = scenePath,

                    // Serialized File Layout
                    writeCommand = new WriteCommand
                    {
                        fileName = generator.GenerateSceneInternalFileName(scenePath),
                        internalName = generator.GenerateSceneBundleInternalFileName(scenePath),
                        serializeObjects = new List<SerializationInfo>() // Populated Below
                    },

                    // External object references
                    referenceMap = new BuildReferenceMap(), // Populated Below

                    // External object preload
                    preloadInfo = new PreloadInfo(), // Populated Below

                    sceneBundleInfo = new SceneBundleInfo
                    {
                        bundleName = scene,
                        bundleScenes = new List<SceneLoadInfo> { new SceneLoadInfo {
                        asset = sceneGuid,
                        address = scenePath,
                        internalName = generator.GenerateInternalFileName(scenePath)
                    } }
                    }
                };

                // The requirement is that a single asset bundle only contains the ObjectManifest and the objects that are directly part of the asset, objects for external assets will be in their own bundles. IE: 1 asset per bundle layout
                // So this means we need to take manifestObjects & manifestDependencies and filter storing them into writeCommand.serializeObjects and/or referenceMap based on if they are this asset or other assets
                foreach (var obj in dependencyResults.referencedObjects)
                {
                    // MonoScripts need to live beside the MonoBehavior (in this case ScriptableObject) and loaded first. We could move it to it's own bundle, but this is safer and it's a lightweight object
                    var type = ContentBuildInterface.GetTypeForObject(obj);
                    if (obj.guid == k_UnityBuiltinResources)
                    {
                        // For Builtin Resources, we can reference them directly
                        // TODO: Once we switch to using GlobalObjectId for SBP, we will need a mapping for certain special cases of GUID <> FilePath for Builtin Resources
                        writeParams.referenceMap.AddMapping(obj.filePath, obj.localIdentifierInFile, obj);
                    }
                    else if (type == typeof(MonoScript))
                    {
                        writeParams.writeCommand.serializeObjects.Add(new SerializationInfo { serializationObject = obj, serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj) });
                        writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName, generator.SerializationIndexFromObjectIdentifier(obj), obj);
                    }
                    else if (collectDependencies || obj.guid == sceneGuid)
                    {
                        writeParams.writeCommand.serializeObjects.Add(new SerializationInfo { serializationObject = obj, serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj) });
                        writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName, generator.SerializationIndexFromObjectIdentifier(obj), obj);
                    }
                    else if (obj.guid == k_UnityBuiltinExtraResources)
                    {
                        GUID convGUID = obj.guid;
                        PackBuiltinExtraWithFileIdent(ref convGUID, obj.localIdentifierInFile);
                        
                        writeParams.referenceMap.AddMapping(generator.GenerateAssetBundleInternalFileName(convGUID.ToString()), generator.SerializationIndexFromObjectIdentifier(obj), obj);
                        
                        dependencies?.Add(convGUID);
                    }
                    else if (!obj.guid.Empty())
                        writeParams.referenceMap.AddMapping(generator.GenerateAssetBundleInternalFileName(obj.guid.ToString()), generator.SerializationIndexFromObjectIdentifier(obj), obj);

                    // This will be solvable after we move SBP into the asset pipeline as importers.
                    if (!obj.guid.Empty() && obj.guid != sceneGuid && type != typeof(MonoScript) && obj.guid != k_UnityBuiltinExtraResources)
                    {
                        dependencies?.Add(obj.guid);
                    }
                    
                    if (type != null)
                        types?.Add(type);
                }

                // Write the serialized file
                var result = ContentBuildInterface.WriteSceneSerializedFile(k_TempBuildPath, writeParams);
                // Archive and compress the serialized & resource files for the previous operation
                var crc = ContentBuildInterface.ArchiveAndCompress(result.resourceFiles.ToArray(), cacheFilePath, UnityEngine.BuildCompression.Uncompressed);

                // Because the shader compiler progress bar hooks are absolute shit
                EditorUtility.ClearProgressBar();

                //Debug.Log($"Wrote '{writeParams.writeCommand.fileName}' to '{k_TempBuildPath}/{writeParams.writeCommand.internalName}' resulting in {result.serializedObjects.Count} objects in the serialized file.");
                //Debug.Log($"Archived '{k_TempBuildPath}/{writeParams.writeCommand.internalName}' to '{cacheFilePath}' resulting in {crc} CRC.");

                Directory.Delete(k_TempBuildPath, true);

                return crc != 0;
            }
        }

        public static void BuildSubSceneBundle(string manifestPath, string bundleName, string bundlePath, BuildTarget target, HashSet<Entities.Hash128> dependencies)
        {
            using (new BuildInterfacesWrapper())
            {
                Directory.CreateDirectory(k_TempBuildPath);
                
                // Deterministic ID Generator
                var generator = new Unity5PackedIdentifiers();

                // Target platform settings & script information
                var buildSettings = new BuildSettings
                {
                    buildFlags = ContentBuildFlags.None,
                    target = target,
                    group = BuildPipeline.GetBuildTargetGroup(target),
                    typeDB = null
                };

#if UNITY_2020_1_OR_NEWER
                // Collect all the objects we need for this asset & bundle (returned array order is deterministic)
                var manifestObjects =
 ContentBuildInterface.GetPlayerObjectIdentifiersInSerializedFile(manifestPath, buildSettings.target);
#else
                var method = typeof(ContentBuildInterface).GetMethod("GetPlayerObjectIdentifiersInSerializedFile",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                // Collect all the objects we need for this asset & bundle (returned array order is deterministic)
                var manifestObjects =
                    (ObjectIdentifier[]) method.Invoke(null, new object[] {manifestPath, buildSettings.target});
#endif
                // Collect all the objects we need to reference for this asset (returned array order is deterministic)
                var manifestDependencies =
                    ContentBuildInterface.GetPlayerDependenciesForObjects(manifestObjects, buildSettings.target,
                        buildSettings.typeDB);

                // Scene / Project lighting information
                var globalUsage = ContentBuildInterface.GetGlobalUsageFromGraphicsSettings();
                globalUsage = ForceKeepInstancingVariants(globalUsage);

                // Inter-asset feature usage (shader features, used mesh channels)
                var usageSet = new BuildUsageTagSet();
                ContentBuildInterface.CalculateBuildUsageTags(manifestDependencies, manifestDependencies, globalUsage,
                    usageSet); // TODO: Cache & Append to the assets that are influenced by this usageTagSet, ideally it would be a nice api to extract just the data for a given asset or object from the result

                // Bundle all the needed write parameters
                var writeParams = new WriteParameters
                {
                    // Target platform settings & script information
                    settings = buildSettings,

                    // Scene / Project lighting information
                    globalUsage = globalUsage,

                    // Inter-asset feature usage (shader features, used mesh channels)
                    usageSet = usageSet,

                    // Serialized File Layout
                    writeCommand = new WriteCommand
                    {
                        fileName = generator.GenerateInternalFileName(bundleName),
                        internalName = generator.GenerateAssetBundleInternalFileName(bundleName),
                        serializeObjects = new List<SerializationInfo>() // Populated Below
                    },

                    // External object references
                    referenceMap = new BuildReferenceMap(), // Populated Below

                    // Asset Bundle object layout
                    bundleInfo = new AssetBundleInfo
                    {
                        bundleName = bundleName,
                        // What is loadable from this bundle
                        bundleAssets = new List<AssetLoadInfo>
                        {
                            // The manifest object and it's dependencies
                            new AssetLoadInfo
                            {
                                address = bundleName,
                                asset = new GUID(), // TODO: Remove this as it is unused in C++
                                includedObjects =
                                    manifestObjects
                                        .ToList(), // TODO: In our effort to modernize the public API design we over complicated it trying to take List or return ReadOnlyLists. Should have just stuck with Arrays[] in all places
                                referencedObjects = manifestDependencies.ToList()
                            }
                        }
                    }
                };

                // The requirement is that a single asset bundle only contains the ObjectManifest and the objects that are directly part of the asset, objects for external assets will be in their own bundles. IE: 1 asset per bundle layout
                // So this means we need to take manifestObjects & manifestDependencies and filter storing them into writeCommand.serializeObjects and/or referenceMap based on if they are this asset or other assets
                foreach (var obj in manifestObjects)
                {
                    writeParams.writeCommand.serializeObjects.Add(new SerializationInfo
                    {
                        serializationObject = obj,
                        serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj)
                    });
                    writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName,
                        generator.SerializationIndexFromObjectIdentifier(obj), obj);
                }

                foreach (var obj in manifestDependencies)
                {
                    // MonoScripts need to live beside the MonoBehavior (in this case ScriptableObject) and loaded first. We could move it to it's own bundle, but this is safer and it's a lightweight object
                    var type = ContentBuildInterface.GetTypeForObject(obj);
                    if (obj.guid == k_UnityBuiltinResources)
                    {
                        // For Builtin Resources, we can reference them directly
                        // TODO: Once we switch to using GlobalObjectId for SBP, we will need a mapping for certain special cases of GUID <> FilePath for Builtin Resources
                        writeParams.referenceMap.AddMapping(obj.filePath, obj.localIdentifierInFile, obj);
                    }
                    else if (type == typeof(MonoScript))
                    {
                        writeParams.writeCommand.serializeObjects.Add(new SerializationInfo { serializationObject = obj, serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj) });
                        writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName, generator.SerializationIndexFromObjectIdentifier(obj), obj);
                    }
                    else if (!obj.guid.Empty())
                        writeParams.referenceMap.AddMapping(
                            generator.GenerateAssetBundleInternalFileName(obj.guid.ToString()),
                            generator.SerializationIndexFromObjectIdentifier(obj), obj);
                    
                    // This will be solvable after we move SBP into the asset pipeline as importers.
                    if (!obj.guid.Empty() && type != typeof(MonoScript))
                    {
                        var convGUID = obj.guid;
                        if (obj.guid == k_UnityBuiltinExtraResources)
                        {
                            PackBuiltinExtraWithFileIdent(ref convGUID, obj.localIdentifierInFile);
                        }
                        
                        dependencies?.Add(convGUID);
                    }
                }

                // Write the serialized file
                var result = ContentBuildInterface.WriteSerializedFile(k_TempBuildPath, writeParams);
                // Archive and compress the serialized & resource files for the previous operation
                var crc = ContentBuildInterface.ArchiveAndCompress(result.resourceFiles.ToArray(), bundlePath,
                    UnityEngine.BuildCompression.Uncompressed);

                // Because the shader compiler progress bar hooks are absolute shit
                EditorUtility.ClearProgressBar();

                //Debug.Log($"Wrote '{writeParams.writeCommand.fileName}' to '{k_TempBuildPath}/{writeParams.writeCommand.internalName}' resulting in {result.serializedObjects.Count} objects in the serialized file.");
                //Debug.Log($"Archived '{k_TempBuildPath}/{writeParams.writeCommand.internalName}' to '{cacheFilePath}' resulting in {crc} CRC.");

                Directory.Delete(k_TempBuildPath, true);
            }
        }

        public static bool BuildAssetBundle(string manifestPath, GUID assetGuid, string cacheFilePath, BuildTarget target, bool collectDependencies = false, HashSet<Entities.Hash128> dependencies = null, HashSet<System.Type> types = null, long fileIdent = -1)
        {
            using (new BuildInterfacesWrapper())
            {
                Directory.CreateDirectory(k_TempBuildPath);

                // Used for naming
                string asset;
                if (fileIdent != -1)
                {
                    GUID convGuid = assetGuid;
                    PackBuiltinExtraWithFileIdent(ref convGuid, fileIdent);
                    asset = convGuid.ToString();
                }
                else
                {
                    asset = assetGuid.ToString();
                }

                // Deterministic ID Generator
                var generator = new Unity5PackedIdentifiers();

                // Target platform settings & script information
                var settings = new BuildSettings
                {
                    buildFlags = ContentBuildFlags.None,
                    target = target,
                    group = BuildPipeline.GetBuildTargetGroup(target),
                    typeDB = null
                };

                if (assetGuid == k_UnityBuiltinResources)
                    FilterBuiltinResourcesObjectManifest(manifestPath);

                if (assetGuid == k_UnityBuiltinExtraResources)
                    FilterBuiltinExtraResourcesObjectManifest(manifestPath);

#if UNITY_2020_1_OR_NEWER
                // Collect all the objects we need for this asset & bundle (returned array order is deterministic)
                var manifestObjects = ContentBuildInterface.GetPlayerObjectIdentifiersInSerializedFile(manifestPath, settings.target);
#else
                var method = typeof(ContentBuildInterface).GetMethod("GetPlayerObjectIdentifiersInSerializedFile", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                // Collect all the objects we need for this asset & bundle (returned array order is deterministic)
                var manifestObjects = (ObjectIdentifier[])method.Invoke(null, new object[] { manifestPath, settings.target });
#endif

                // Collect all the objects we need to reference for this asset (returned array order is deterministic)
                var manifestDependencies = ContentBuildInterface.GetPlayerDependenciesForObjects(manifestObjects, settings.target, settings.typeDB);

                // Scene / Project lighting information
                var globalUsage = ContentBuildInterface.GetGlobalUsageFromGraphicsSettings();
                globalUsage = ForceKeepInstancingVariants(globalUsage);

                // Inter-asset feature usage (shader features, used mesh channels)
                var usageSet = new BuildUsageTagSet();
                ContentBuildInterface.CalculateBuildUsageTags(manifestDependencies, manifestDependencies, globalUsage, usageSet); // TODO: Cache & Append to the assets that are influenced by this usageTagSet, ideally it would be a nice api to extract just the data for a given asset or object from the result

                // Bundle all the needed write parameters
                var writeParams = new WriteParameters
                {
                    // Target platform settings & script information
                    settings = settings,

                    // Scene / Project lighting information
                    globalUsage = globalUsage,

                    // Inter-asset feature usage (shader features, used mesh channels)
                    usageSet = usageSet,

                    // Serialized File Layout
                    writeCommand = new WriteCommand
                    {
                        fileName = generator.GenerateInternalFileName(asset),
                        internalName = generator.GenerateAssetBundleInternalFileName(asset),
                        serializeObjects = new List<SerializationInfo>() // Populated Below
                    },

                    // External object references
                    referenceMap = new BuildReferenceMap(), // Populated Below

                    // Asset Bundle object layout
                    bundleInfo = new AssetBundleInfo
                    {
                        bundleName = asset,
                        // What is loadable from this bundle
                        bundleAssets = new List<AssetLoadInfo>
                    {
                        // The manifest object and it's dependencies
                        new AssetLoadInfo
                        {
                            address = asset,
                            asset = assetGuid, // TODO: Remove this as it is unused in C++
                            includedObjects = manifestObjects.ToList(), // TODO: In our effort to modernize the public API design we over complicated it trying to take List or return ReadOnlyLists. Should have just stuck with Arrays[] in all places
                            referencedObjects = manifestDependencies.ToList()
                        }
                    }
                    }
                };

                // For Builtin Resources, we just want to reference them directly instead of pull them in.
                //if (assetGuid == k_UnityBuiltinResources)
                //    assetGuid = manifestGuid;

                // The requirement is that a single asset bundle only contains the ObjectManifest and the objects that are directly part of the asset, objects for external assets will be in their own bundles. IE: 1 asset per bundle layout
                // So this means we need to take manifestObjects & manifestDependencies and filter storing them into writeCommand.serializeObjects and/or referenceMap based on if they are this asset or other assets
                foreach (var obj in manifestObjects)
                {
                    writeParams.writeCommand.serializeObjects.Add(new SerializationInfo { serializationObject = obj, serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj) });
                    writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName, generator.SerializationIndexFromObjectIdentifier(obj), obj);
                }

                foreach (var obj in manifestDependencies)
                {
                    // MonoScripts need to live beside the MonoBehavior (in this case ScriptableObject) and loaded first. We could move it to it's own bundle, but this is safer and it's a lightweight object
                    var type = ContentBuildInterface.GetTypeForObject(obj);
                    if (obj.guid == k_UnityBuiltinResources)
                    {
                        // For Builtin Resources, we can reference them directly
                        // TODO: Once we switch to using GlobalObjectId for SBP, we will need a mapping for certain special cases of GUID <> FilePath for Builtin Resources
                        writeParams.referenceMap.AddMapping(obj.filePath, obj.localIdentifierInFile, obj);
                    }
                    else if (type == typeof(MonoScript))
                    {
                        writeParams.writeCommand.serializeObjects.Add(new SerializationInfo { serializationObject = obj, serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj) });
                        writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName, generator.SerializationIndexFromObjectIdentifier(obj), obj);
                    }
                    else if (collectDependencies || obj.guid == assetGuid)
                    {
                        // If we are a specific built-in asset, only add the built-in asset
                        if (fileIdent != -1 && obj.localIdentifierInFile != fileIdent)
                            continue;
                        
                        writeParams.writeCommand.serializeObjects.Add(new SerializationInfo { serializationObject = obj, serializationIndex = generator.SerializationIndexFromObjectIdentifier(obj) });
                        writeParams.referenceMap.AddMapping(writeParams.writeCommand.internalName, generator.SerializationIndexFromObjectIdentifier(obj), obj);
                    }
                    else if (obj.guid == k_UnityBuiltinExtraResources)
                    {
                        GUID convGUID = obj.guid;
                        PackBuiltinExtraWithFileIdent(ref convGUID, obj.localIdentifierInFile);
                        
                        writeParams.referenceMap.AddMapping(generator.GenerateAssetBundleInternalFileName(convGUID.ToString()), generator.SerializationIndexFromObjectIdentifier(obj), obj);
                        
                        dependencies?.Add(convGUID);
                    }
                    else if (!obj.guid.Empty())
                        writeParams.referenceMap.AddMapping(generator.GenerateAssetBundleInternalFileName(obj.guid.ToString()), generator.SerializationIndexFromObjectIdentifier(obj), obj);

                    // This will be solvable after we move SBP into the asset pipeline as importers.
                    if (!obj.guid.Empty() && obj.guid != assetGuid && type != typeof(MonoScript) && obj.guid != k_UnityBuiltinExtraResources)
                    {
                        dependencies?.Add(obj.guid);
                    }

                    if (type != null)
                        types?.Add(type);
                }

                // Write the serialized file
                var result = ContentBuildInterface.WriteSerializedFile(k_TempBuildPath, writeParams);
                // Archive and compress the serialized & resource files for the previous operation
                var crc = ContentBuildInterface.ArchiveAndCompress(result.resourceFiles.ToArray(), cacheFilePath, UnityEngine.BuildCompression.Uncompressed);

                // Because the shader compiler progress bar hooks are absolute shit
                EditorUtility.ClearProgressBar();

                //Debug.Log($"Wrote '{writeParams.writeCommand.fileName}' to '{k_TempBuildPath}/{writeParams.writeCommand.internalName}' resulting in {result.serializedObjects.Count} objects in the serialized file.");
                //Debug.Log($"Archived '{k_TempBuildPath}/{writeParams.writeCommand.internalName}' to '{cacheFilePath}' resulting in {crc} CRC.");

                Directory.Delete(k_TempBuildPath, true);

                return crc != 0;
            }
        }

        public static Hash128 CalculateTargetHash(GUID guid, BuildTarget target, AssetDatabaseExperimental.ImportSyncMode syncMode)
        {
            return LiveLinkBuildImporter.GetHash(guid.ToString(), target, syncMode);
        }

        public static void CalculateTargetDependencies(Entities.Hash128 artifactHash, BuildTarget target, out ResolvedAssetID[] dependencies, AssetDatabaseExperimental.ImportSyncMode syncMode)
        {
            List<Entities.Hash128> assets = new List<Entities.Hash128>(LiveLinkBuildImporter.GetDependencies(artifactHash));
            List<ResolvedAssetID> resolvedDependencies = new List<ResolvedAssetID>();

            HashSet<Entities.Hash128> visited = new HashSet<Entities.Hash128>();
            for (int i = 0; i < assets.Count; i++)
            {
                if (!visited.Add(assets[i]))
                    continue;

                var resolvedAsset = new ResolvedAssetID
                {
                    GUID = assets[i],
                    TargetHash = CalculateTargetHash(assets[i], target, syncMode),
                };
                resolvedDependencies.Add(resolvedAsset);

                if (resolvedAsset.TargetHash.IsValid)
                {
                    assets.AddRange(LiveLinkBuildImporter.GetDependencies(resolvedAsset.TargetHash));
                }
            }

            dependencies = resolvedDependencies.ToArray();
        }
    }
}
