/*
 * TilePresetDetector.cs
 * 
 * Editor utility for scanning project TilePreset assets and suggesting
 * intelligent color-to-tile mappings based on preset names and terrain types.
 * 
 * Features:
 * - Finds all TilePreset assets in the project
 * - Guesses terrain type from preset names (water, grass, rock, etc.)
 * - Suggests appropriate colors and Y offsets for each terrain type
 * - Auto-populates ColorPalette with detected presets
 * 
 * Usage:
 * 1. Call TilePresetDetector.FindAllTilePresets() to get all presets
 * 2. Use GuessTerrainType() to classify presets
 * 3. Use AutoPopulatePalette() to fill a ColorPalette automatically
 */

using UnityEngine;
using UnityEditor;
using GiantGrey.TileWorldCreator;
using System.Collections.Generic;
using System.Linq;

namespace ImageToMap
{
    /// <summary>
    /// Terrain classification types for intelligent preset mapping.
    /// Used to suggest colors and Y offsets based on preset names.
    /// </summary>
    public enum TerrainType
    {
        Unknown,
        Water,
        Sand,
        Grass,
        Dirt,
        Rock,
        Snow,
        Forest,
        Path,
        Building,
        Lava,
        Swamp,
        Ice
    }

    /// <summary>
    /// Static utility class for scanning and analyzing TilePreset assets.
    /// Provides intelligent suggestions for color-to-tile mappings.
    /// </summary>
    public static class TilePresetDetector
    {
        #region Preset Discovery

        /// <summary>
        /// Finds all TilePreset assets in the project.
        /// Scans entire Assets folder using AssetDatabase.
        /// </summary>
        /// <returns>List of all TilePreset ScriptableObjects found in the project</returns>
        public static List<TilePreset> FindAllTilePresets()
        {
            var guids = AssetDatabase.FindAssets("t:TilePreset");
            return guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<TilePreset>(path))
                .Where(preset => preset != null)
                .ToList();
        }

        /// <summary>
        /// Finds all TilePreset assets within a specific folder.
        /// </summary>
        /// <param name="folderPath">Asset folder path (e.g., "Assets/TileWorldCreator/Tiles URP")</param>
        /// <returns>List of TilePresets found in the specified folder and subfolders</returns>
        public static List<TilePreset> FindTilePresetsInFolder(string folderPath)
        {
            var guids = AssetDatabase.FindAssets("t:TilePreset", new[] { folderPath });
            return guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<TilePreset>(path))
                .Where(preset => preset != null)
                .ToList();
        }

        /// <summary>
        /// Gets the asset path for a TilePreset.
        /// </summary>
        /// <param name="preset">The TilePreset to locate</param>
        /// <returns>Asset path or null if not found</returns>
        public static string GetPresetPath(TilePreset preset)
        {
            if (preset == null) return null;
            return AssetDatabase.GetAssetPath(preset);
        }

        #endregion

        #region Terrain Type Detection

        /// <summary>
        /// Guesses the terrain type based on the preset's name.
        /// Uses keyword matching for intelligent classification.
        /// </summary>
        /// <param name="preset">The TilePreset to analyze</param>
        /// <returns>Best-guess TerrainType based on name keywords</returns>
        public static TerrainType GuessTerrainType(TilePreset preset)
        {
            if (preset == null) return TerrainType.Unknown;
            
            var name = preset.name.ToLower();
            
            // Water-related
            if (name.Contains("water") || name.Contains("ocean") || name.Contains("sea") || 
                name.Contains("river") || name.Contains("lake") || name.Contains("pond"))
                return TerrainType.Water;
            
            // Lava/volcanic
            if (name.Contains("lava") || name.Contains("magma") || name.Contains("volcanic") ||
                name.Contains("molten"))
                return TerrainType.Lava;
            
            // Ice-related
            if (name.Contains("ice") || name.Contains("frozen") || name.Contains("glacier"))
                return TerrainType.Ice;
            
            // Snow-related
            if (name.Contains("snow") || name.Contains("frost") || name.Contains("winter") ||
                name.Contains("arctic") || name.Contains("tundra"))
                return TerrainType.Snow;
            
            // Swamp/marsh
            if (name.Contains("swamp") || name.Contains("marsh") || name.Contains("bog") ||
                name.Contains("wetland"))
                return TerrainType.Swamp;
            
            // Sand/beach/desert
            if (name.Contains("sand") || name.Contains("beach") || name.Contains("desert") ||
                name.Contains("dune"))
                return TerrainType.Sand;
            
            // Grass/plains
            if (name.Contains("grass") || name.Contains("plains") || name.Contains("field") ||
                name.Contains("meadow") || name.Contains("lawn"))
                return TerrainType.Grass;
            
            // Forest/trees
            if (name.Contains("forest") || name.Contains("tree") || name.Contains("wood") ||
                name.Contains("jungle"))
                return TerrainType.Forest;
            
            // Rock/stone/mountain
            if (name.Contains("rock") || name.Contains("stone") || name.Contains("mountain") ||
                name.Contains("cliff") || name.Contains("boulder") || name.Contains("granite"))
                return TerrainType.Rock;
            
            // Dirt/earth/mud
            if (name.Contains("dirt") || name.Contains("mud") || name.Contains("earth") ||
                name.Contains("soil") || name.Contains("ground"))
                return TerrainType.Dirt;
            
            // Path/road
            if (name.Contains("path") || name.Contains("road") || name.Contains("trail") ||
                name.Contains("cobble") || name.Contains("brick"))
                return TerrainType.Path;
            
            // Building/structure
            if (name.Contains("building") || name.Contains("floor") || name.Contains("wall") ||
                name.Contains("house") || name.Contains("castle") || name.Contains("dungeon"))
                return TerrainType.Building;
            
            return TerrainType.Unknown;
        }

        /// <summary>
        /// Attempts to guess terrain type from any string (name, tag, etc.)
        /// </summary>
        /// <param name="text">Text to analyze for terrain keywords</param>
        /// <returns>Best-guess TerrainType</returns>
        public static TerrainType GuessTerrainTypeFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return TerrainType.Unknown;
            
            // Create a temporary preset-like object for reuse of existing logic
            var tempPreset = ScriptableObject.CreateInstance<TilePreset>();
            tempPreset.name = text;
            var result = GuessTerrainType(tempPreset);
            Object.DestroyImmediate(tempPreset);
            return result;
        }

        #endregion

        #region Color Suggestions

        /// <summary>
        /// Gets the default/suggested color for a terrain type.
        /// Colors are chosen to be visually distinctive and intuitive.
        /// </summary>
        /// <param name="type">The terrain type</param>
        /// <returns>Suggested Color for the terrain type</returns>
        public static Color GetDefaultColorForTerrain(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Water:    return new Color(0.2f, 0.4f, 0.8f);      // Deep blue
                case TerrainType.Sand:     return new Color(0.9f, 0.85f, 0.6f);     // Sandy beige
                case TerrainType.Grass:    return new Color(0.3f, 0.7f, 0.3f);      // Green
                case TerrainType.Rock:     return new Color(0.5f, 0.5f, 0.5f);      // Gray
                case TerrainType.Snow:     return new Color(0.95f, 0.95f, 1f);      // Near white
                case TerrainType.Dirt:     return new Color(0.6f, 0.4f, 0.2f);      // Brown
                case TerrainType.Forest:   return new Color(0.1f, 0.4f, 0.1f);      // Dark green
                case TerrainType.Path:     return new Color(0.65f, 0.55f, 0.4f);    // Tan
                case TerrainType.Building: return new Color(0.4f, 0.35f, 0.3f);     // Dark brown
                case TerrainType.Lava:     return new Color(1f, 0.3f, 0f);          // Orange-red
                case TerrainType.Swamp:    return new Color(0.3f, 0.4f, 0.2f);      // Murky green
                case TerrainType.Ice:      return new Color(0.7f, 0.9f, 1f);        // Light cyan
                default:                   return Color.white;
            }
        }

        /// <summary>
        /// Gets a hex color string for a terrain type.
        /// Useful for UI display and debugging.
        /// </summary>
        /// <param name="type">The terrain type</param>
        /// <returns>Hex color string (e.g., "#3366CC")</returns>
        public static string GetHexColorForTerrain(TerrainType type)
        {
            Color c = GetDefaultColorForTerrain(type);
            return ColorUtility.ToHtmlStringRGB(c);
        }

        #endregion

        #region Y Offset Suggestions

        /// <summary>
        /// Gets the default Y offset for a terrain type.
        /// Higher values create elevated terrain (mountains, buildings).
        /// Lower/negative values create recessed terrain (water, trenches).
        /// </summary>
        /// <param name="type">The terrain type</param>
        /// <returns>Suggested Y offset value</returns>
        public static float GetDefaultYOffset(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Water:    return -0.5f;   // Below ground level
                case TerrainType.Lava:     return -0.3f;   // Slightly recessed
                case TerrainType.Swamp:    return -0.2f;   // Slightly recessed
                case TerrainType.Sand:     return 0f;      // Ground level
                case TerrainType.Dirt:     return 0f;      // Ground level
                case TerrainType.Path:     return 0.05f;   // Slightly raised
                case TerrainType.Grass:    return 0.5f;    // Slight elevation
                case TerrainType.Forest:   return 0.6f;    // Similar to grass
                case TerrainType.Ice:      return 0.3f;    // Slight elevation
                case TerrainType.Rock:     return 1.5f;    // Elevated
                case TerrainType.Snow:     return 2f;      // High elevation (mountain peaks)
                case TerrainType.Building: return 1f;      // Elevated platform
                default:                   return 0f;
            }
        }

        #endregion

        #region Palette Auto-Population

        /// <summary>
        /// Auto-populates a ColorPalette with all detected TilePresets.
        /// Clears existing mappings and creates new ones based on preset analysis.
        /// </summary>
        /// <param name="palette">The ColorPalette to populate</param>
        /// <param name="skipUnknown">If true, presets with Unknown terrain type are skipped</param>
        public static void AutoPopulatePalette(ColorPalette palette, bool skipUnknown = true)
        {
            if (palette == null)
            {
                Debug.LogError("[TilePresetDetector] Cannot populate null palette.");
                return;
            }
            
            var presets = FindAllTilePresets();
            palette.mappings.Clear();
            
            foreach (var preset in presets)
            {
                var terrainType = GuessTerrainType(preset);
                
                if (skipUnknown && terrainType == TerrainType.Unknown)
                {
                    Debug.Log($"[TilePresetDetector] Skipping preset '{preset.name}' - terrain type unknown");
                    continue;
                }
                
                palette.mappings.Add(new ColorPalette.TileMapping
                {
                    name = preset.name,
                    targetColor = GetDefaultColorForTerrain(terrainType),
                    colorTolerance = 0.25f,
                    tilePreset = preset,
                    yOffset = GetDefaultYOffset(terrainType)
                });
            }
            
            EditorUtility.SetDirty(palette);
            Debug.Log($"[TilePresetDetector] Auto-populated palette with {palette.mappings.Count} mappings");
        }

        /// <summary>
        /// Adds suggested mappings to an existing palette without clearing it.
        /// Only adds presets that aren't already mapped.
        /// </summary>
        /// <param name="palette">The ColorPalette to augment</param>
        /// <param name="skipUnknown">If true, presets with Unknown terrain type are skipped</param>
        /// <returns>Number of new mappings added</returns>
        public static int AddMissingPresetsToPalette(ColorPalette palette, bool skipUnknown = true)
        {
            if (palette == null)
            {
                Debug.LogError("[TilePresetDetector] Cannot augment null palette.");
                return 0;
            }
            
            var presets = FindAllTilePresets();
            var existingPresets = new HashSet<TilePreset>(
                palette.mappings
                    .Where(m => m != null && m.tilePreset != null)
                    .Select(m => m.tilePreset)
            );
            
            int added = 0;
            
            foreach (var preset in presets)
            {
                if (existingPresets.Contains(preset))
                    continue;
                
                var terrainType = GuessTerrainType(preset);
                
                if (skipUnknown && terrainType == TerrainType.Unknown)
                    continue;
                
                palette.mappings.Add(new ColorPalette.TileMapping
                {
                    name = preset.name,
                    targetColor = GetDefaultColorForTerrain(terrainType),
                    colorTolerance = 0.25f,
                    tilePreset = preset,
                    yOffset = GetDefaultYOffset(terrainType)
                });
                
                added++;
            }
            
            if (added > 0)
            {
                EditorUtility.SetDirty(palette);
                Debug.Log($"[TilePresetDetector] Added {added} new mappings to palette");
            }
            
            return added;
        }

        #endregion

        #region Preset Analysis

        /// <summary>
        /// Gets all detected presets grouped by their guessed terrain type.
        /// Useful for UI display and selection.
        /// </summary>
        /// <returns>Dictionary mapping TerrainType to list of matching TilePresets</returns>
        public static Dictionary<TerrainType, List<TilePreset>> GetPresetsByTerrainType()
        {
            var result = new Dictionary<TerrainType, List<TilePreset>>();
            
            foreach (var preset in FindAllTilePresets())
            {
                var type = GuessTerrainType(preset);
                
                if (!result.ContainsKey(type))
                    result[type] = new List<TilePreset>();
                    
                result[type].Add(preset);
            }
            
            return result;
        }

        /// <summary>
        /// Gets a summary of all detected presets for logging/display.
        /// </summary>
        /// <returns>Formatted string with preset counts by terrain type</returns>
        public static string GetPresetSummary()
        {
            var byType = GetPresetsByTerrainType();
            var lines = new List<string>();
            
            lines.Add($"=== TilePreset Detection Summary ===");
            lines.Add($"Total presets found: {byType.Values.Sum(list => list.Count)}");
            lines.Add("");
            
            foreach (var kvp in byType.OrderBy(k => k.Key.ToString()))
            {
                var color = GetHexColorForTerrain(kvp.Key);
                var yOffset = GetDefaultYOffset(kvp.Key);
                lines.Add($"  {kvp.Key}: {kvp.Value.Count} preset(s) [Color: #{color}, Y: {yOffset:F1}]");
                
                foreach (var preset in kvp.Value.Take(3))
                {
                    lines.Add($"    - {preset.name}");
                }
                
                if (kvp.Value.Count > 3)
                {
                    lines.Add($"    ... and {kvp.Value.Count - 3} more");
                }
            }
            
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Creates a mapping suggestion for a single preset.
        /// </summary>
        /// <param name="preset">The preset to create a suggestion for</param>
        /// <returns>A new TileMapping with suggested values</returns>
        public static ColorPalette.TileMapping CreateSuggestedMapping(TilePreset preset)
        {
            if (preset == null) return null;
            
            var terrainType = GuessTerrainType(preset);
            
            return new ColorPalette.TileMapping
            {
                name = preset.name,
                targetColor = GetDefaultColorForTerrain(terrainType),
                colorTolerance = 0.25f,
                tilePreset = preset,
                yOffset = GetDefaultYOffset(terrainType)
            };
        }

        /// <summary>
        /// Validates that a preset has all necessary tile references.
        /// Useful for checking preset completeness before use.
        /// </summary>
        /// <param name="preset">The preset to validate</param>
        /// <returns>True if preset has at least one tile configured</returns>
        public static bool IsPresetValid(TilePreset preset)
        {
            if (preset == null) return false;
            
            // Check dual grid tiles
            if (preset.gridtype == TilePreset.GridType.dual)
            {
                return preset.DUALGRD_cornerTile != null ||
                       preset.DUALGRD_invertedCornerTile != null ||
                       preset.DUALGRD_edgeTile != null ||
                       preset.DUALGRD_fillTile != null ||
                       preset.DUALGRD_doubleInteriorCornerTile != null;
            }
            
            // Check normal grid tiles
            return preset.NRMGRD_fillTile != null ||
                   preset.NRMGRD_cornerFillTile != null ||
                   preset.NRMGRD_edgeFillTile != null;
        }

        /// <summary>
        /// Gets a list of valid (properly configured) presets only.
        /// </summary>
        /// <returns>List of TilePresets that have at least one tile configured</returns>
        public static List<TilePreset> FindValidTilePresets()
        {
            return FindAllTilePresets()
                .Where(IsPresetValid)
                .ToList();
        }

        #endregion

        #region Editor Menu Integration

        /// <summary>
        /// Logs a summary of all detected presets to the console.
        /// Accessible via menu: Tools > ImageToMap > Log Preset Summary
        /// </summary>
        [MenuItem("Tools/ImageToMap/Log Preset Summary")]
        public static void LogPresetSummary()
        {
            Debug.Log(GetPresetSummary());
        }

        /// <summary>
        /// Shows a dialog with the number of detected presets.
        /// Accessible via menu: Tools > ImageToMap > Count Presets
        /// </summary>
        [MenuItem("Tools/ImageToMap/Count Presets")]
        public static void CountPresets()
        {
            var presets = FindAllTilePresets();
            var valid = FindValidTilePresets();
            
            EditorUtility.DisplayDialog(
                "TilePreset Detection",
                $"Found {presets.Count} TilePreset asset(s).\n" +
                $"Valid (with tiles configured): {valid.Count}\n\n" +
                $"Check console for detailed summary.",
                "OK"
            );
            
            LogPresetSummary();
        }

        #endregion
    }
}
