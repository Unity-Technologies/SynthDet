using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine.SimViz.Content.Pipeline;
using UnityEngine.SimViz.Content.Taxonomy;

namespace UnityEngine.SimViz.Content
{

    [Serializable] 
    struct CategoryLocation 
    {
        public string type;
        public string[] path; 
    }

    // Taxonomy is imported from a serialized CategoryLocation object
   [Serializable]
    class CategoryLocationList
    {
        public List<CategoryLocation> placements = new List<CategoryLocation>();
        public List<CategoryLocation> labels = new List<CategoryLocation>();
    }
    
    [ScriptedImporter(version:1, ext:"txn")]
    public class TaxonomyImporter : ScriptedImporter
    {
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            Debug.Log("Parsing taxonomy " + ctx.assetPath);
            var taxonomy = ScriptableObject.CreateInstance<PlacementDictionary>();
            var taxonomyName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            CategoryLocationList locationList;
            try
            {
                locationList = JsonUtility.FromJson<CategoryLocationList>(File.ReadAllText(ctx.assetPath));
            } catch (Exception e)
            {
                Debug.LogError("Unable to parse taxonomy: " + e.Message);
                return;
            }

            if (locationList == null)
            {
                Debug.LogWarning("Unable to parse taxonomy " + ctx.assetPath);
                return;
            }

            var placements = new Dictionary<string, HashSet<GameObject>>();

            void AddToPlacements(string category, IEnumerable<string> guids)
            {
                foreach (var guid in guids)
                {
                    if (!placements.ContainsKey(category)) placements[category] = new HashSet<GameObject>();
                    placements[category].Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
                }
            }

            foreach (var location in locationList.placements)
            {
                var category = location.type;
                AddToPlacements(category,AssetDatabase.FindAssets("t:Prefab", location.path));
                AddToPlacements(category,AssetDatabase.FindAssets("t:Model", location.path));
            }
            
            foreach (var entry in placements)
            { 
                var cat = ScriptableObject.CreateInstance<PlacementCategory>();
                cat.prefabs = entry.Value.ToArray();
                cat.name = taxonomyName + "-" + entry.Key;
                ctx.AddObjectToAsset(entry.Key, cat);
            }

            ctx.AddObjectToAsset("PlacementTaxonomy", taxonomy);
            ctx.SetMainObject(taxonomy);
        }
    }
}
