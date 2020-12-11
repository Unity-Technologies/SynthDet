using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
#if UNITY_EDITOR
using AssetImportContext = UnityEditor.Experimental.AssetImporters.AssetImportContext;
#endif
using ConversionFlags = Unity.Entities.GameObjectConversionUtility.ConversionFlags;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities
{
    public class GameObjectConversionSettings
    {
        // forked
        public World                    DestinationWorld;
        public Hash128                  SceneGUID;
        public string                   DebugConversionName = "";
        public ConversionFlags          ConversionFlags;
#if UNITY_EDITOR
        public Build.BuildConfiguration BuildConfiguration;
        public AssetImportContext       AssetImportContext;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("BuildSettings has been renamed to BuildConfiguration. (RemovedAfter 2020-04-15) (UnityUpgradable) -> BuildConfiguration")]
        public Build.BuildConfiguration BuildSettings;
#endif

        // not carried forward into a fork
        public Type[]                   ExtraSystems = Array.Empty<Type>();
        public List<Type>               Systems;
        public byte                     NamespaceID;
        public Action<World>            ConversionWorldCreated;        // get a callback right after the conversion world is created and systems have been added to it (good for tests that want to inject something)
        public Action<World>            ConversionWorldPreDispose;     // get a callback right before the conversion world gets disposed (good for tests that want to validate world contents)

        public BlobAssetStore BlobAssetStore { get; protected internal set; }
        
        public GameObjectConversionSettings() { }

        // not a clone - only copies what makes sense for creating entities into a separate guid namespace
        public GameObjectConversionSettings Fork(byte entityGuidNamespaceID)
        {
            if (entityGuidNamespaceID == 0)
                throw new ArgumentException("0 is reserved for the default", nameof(entityGuidNamespaceID));

            return new GameObjectConversionSettings
            {
                DestinationWorld = DestinationWorld,
                SceneGUID = SceneGUID,
                DebugConversionName = $"{DebugConversionName}:{entityGuidNamespaceID:x2}",
                ConversionFlags = ConversionFlags,
                NamespaceID = entityGuidNamespaceID,
                BlobAssetStore = BlobAssetStore,
#if UNITY_EDITOR
                BuildConfiguration = BuildConfiguration,
                AssetImportContext = AssetImportContext,
#endif
            };
        }

        // ** CONFIGURATION **

        public GameObjectConversionSettings(World destinationWorld, ConversionFlags conversionFlags, BlobAssetStore blobAssetStore=null)
        {
            DestinationWorld = destinationWorld;
            ConversionFlags = conversionFlags;
            if (blobAssetStore != null)
            {
                BlobAssetStore = blobAssetStore;
            }
        }

        public static GameObjectConversionSettings FromWorld(World destinationWorld, BlobAssetStore blobAssetStore) => new GameObjectConversionSettings { DestinationWorld = destinationWorld, BlobAssetStore = blobAssetStore};
        public static GameObjectConversionSettings FromHash(Hash128 hash, BlobAssetStore blobAssetStore) => new GameObjectConversionSettings { SceneGUID = hash, BlobAssetStore = blobAssetStore};
    #if UNITY_EDITOR
        public static GameObjectConversionSettings FromGUID(UnityEditor.GUID guid, BlobAssetStore blobAssetStore) => new GameObjectConversionSettings { SceneGUID = guid, BlobAssetStore = blobAssetStore};
    #endif

        // use this to inject systems into the conversion world (good for testing)
        public GameObjectConversionSettings WithExtraSystems(params Type[] extraSystems)
        {
            if (ExtraSystems != null && ExtraSystems.Length > 0)
                throw new InvalidOperationException($"{nameof(ExtraSystems)} already initialized");
            ExtraSystems = extraSystems;
            return this;
        }

        public GameObjectConversionSettings WithExtraSystem<T>()
            => WithExtraSystems(typeof(T));

        public GameObjectConversionSettings WithExtraSystems<T1, T2>()
            => WithExtraSystems(typeof(T1), typeof(T2));

        public GameObjectConversionSettings WithExtraSystems<T1, T2, T3>()
            => WithExtraSystems(typeof(T1), typeof(T2), typeof(T3));

        // ** CONVERSION **
        
        public World CreateConversionWorld()
            => GameObjectConversionUtility.CreateConversionWorld(this);

        
        // ** EXPORTING **
        
        public bool SupportsExporting
            => GetType() == typeof(GameObjectConversionSettings); 
        
        public virtual Guid GetGuidForAssetExport(UnityObject uobject)
        {
            if (uobject == null)
                throw new ArgumentNullException(nameof(uobject));

            return Guid.Empty;
        }

        public virtual Stream TryCreateAssetExportWriter(UnityObject uobject)
        {
            if (uobject == null)
                throw new ArgumentNullException(nameof(uobject));

            return null;
        }
    }
}
