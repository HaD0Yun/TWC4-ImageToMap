using UnityEngine;
using System.Collections.Generic;
using GiantGrey.TileWorldCreator;

namespace ImageToMap
{
    /// <summary>
    /// ScriptableObject that defines height-to-tile mappings for 3D heightmap generation.
    /// Create via: Assets > Create > ImageToMap > Height Tile Mappings
    /// </summary>
    [CreateAssetMenu(fileName = "HeightTileMappings", menuName = "ImageToMap/Height Tile Mappings")]
    public class HeightTileMappings : ScriptableObject
    {
        [Tooltip("Maximum world height in tiles (e.g., 8, 16, 32)")]
        public int maxWorldHeight = 16;
        
        [Tooltip("Height-to-tile mappings. Define which TilePreset to use for each height range.")]
        public List<Heightmap3DGenerator.HeightTileMapping> mappings = new List<Heightmap3DGenerator.HeightTileMapping>();
        
        /// <summary>
        /// Creates default mappings based on maxWorldHeight.
        /// </summary>
        public void CreateDefaultMappings()
        {
            mappings.Clear();
            mappings = Heightmap3DGenerator.CreateDefaultMappings(maxWorldHeight);
        }
        
        /// <summary>
        /// Validates mappings and returns any issues.
        /// </summary>
        public List<string> ValidateMappings()
        {
            var issues = new List<string>();
            
            if (mappings == null || mappings.Count == 0)
            {
                issues.Add("No mappings defined");
                return issues;
            }
            
            foreach (var mapping in mappings)
            {
                if (mapping.tilePreset == null)
                {
                    issues.Add($"'{mapping.name}' has no TilePreset assigned");
                }
                if (mapping.minHeight >= mapping.maxHeight)
                {
                    issues.Add($"'{mapping.name}' has invalid height range ({mapping.minHeight} >= {mapping.maxHeight})");
                }
            }
            
            return issues;
        }
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// Custom editor for HeightTileMappings with validation and helper buttons.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(HeightTileMappings))]
    public class HeightTileMappingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var mappings = (HeightTileMappings)target;
            
            UnityEditor.EditorGUILayout.HelpBox(
                "Define height-to-tile mappings for 3D heightmap generation.\n" +
                "Each mapping specifies which TilePreset to use for a range of heights.",
                UnityEditor.MessageType.Info
            );
            
            DrawDefaultInspector();
            
            UnityEditor.EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Create Default Mappings"))
            {
                UnityEditor.Undo.RecordObject(mappings, "Create Default Mappings");
                mappings.CreateDefaultMappings();
                UnityEditor.EditorUtility.SetDirty(mappings);
            }
            
            // Validate and show issues
            var issues = mappings.ValidateMappings();
            if (issues.Count > 0)
            {
                UnityEditor.EditorGUILayout.Space(5);
                UnityEditor.EditorGUILayout.HelpBox(
                    "Issues found:\n• " + string.Join("\n• ", issues),
                    UnityEditor.MessageType.Warning
                );
            }
            else if (mappings.mappings.Count > 0)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "All mappings are valid!",
                    UnityEditor.MessageType.None
                );
            }
        }
    }
    #endif
}
