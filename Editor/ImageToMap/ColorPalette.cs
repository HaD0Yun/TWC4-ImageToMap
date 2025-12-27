/*
 * ColorPalette.cs
 * 
 * ScriptableObject for mapping image colors to TWC4 TilePresets.
 * Used by ImageToMapGenerator to convert pixel colors into tile layers.
 * 
 * Usage:
 * 1. Create via Assets > Create > ImageToMap > Color Palette
 * 2. Add mappings for each color you want to detect
 * 3. Assign appropriate TilePresets from TWC4
 * 
 * Default Preset Suggestions:
 * - Green (0, 128, 0): Grass/Ground tiles
 * - Blue (0, 0, 255): Water tiles
 * - Brown (139, 69, 19): Dirt/Path tiles
 * - Gray (128, 128, 128): Stone/Rock tiles
 * - White (255, 255, 255): Snow tiles
 * - Black (0, 0, 0): Void/Empty (no tile)
 */

using UnityEngine;
using System.Collections.Generic;
using GiantGrey.TileWorldCreator;

namespace ImageToMap
{
    /// <summary>
    /// ScriptableObject that defines color-to-tile mappings for image-based map generation.
    /// Each mapping associates a color with a TWC4 TilePreset for procedural tile placement.
    /// </summary>
    [CreateAssetMenu(fileName = "ColorPalette", menuName = "ImageToMap/Color Palette")]
    public class ColorPalette : ScriptableObject
    {
        /// <summary>
        /// Defines a mapping between a target color and its corresponding tile configuration.
        /// </summary>
        [System.Serializable]
        public class TileMapping
        {
            [Tooltip("Display name for this mapping (e.g., 'Grass', 'Water', 'Stone')")]
            public string name = "New Mapping";
            
            [Tooltip("The target color to match in the source image")]
            public Color targetColor = Color.white;
            
            [Tooltip("How much color variance is allowed (0 = exact match, 1 = any color)")]
            [Range(0f, 1f)]
            public float colorTolerance = 0.2f;
            
            [Tooltip("The TWC4 TilePreset to use for this color")]
            public TilePreset tilePreset;
            
            [Tooltip("Vertical offset for this tile layer (for stacking/elevation)")]
            public float yOffset = 0f;
            
            /// <summary>
            /// Checks if a given color falls within this mapping's tolerance range.
            /// </summary>
            /// <param name="color">The color to check</param>
            /// <returns>True if the color is within tolerance</returns>
            public bool IsWithinTolerance(Color color)
            {
                float distance = ColorPalette.ColorDistance(targetColor, color);
                return distance <= colorTolerance;
            }
        }
        
        [Tooltip("List of color-to-tile mappings. Order matters for priority when colors overlap.")]
        public List<TileMapping> mappings = new List<TileMapping>();
        
        /// <summary>
        /// Finds the best matching TileMapping for a given color using Euclidean distance in RGB space.
        /// Returns null if no mapping is found within tolerance.
        /// </summary>
        /// <param name="color">The color to match</param>
        /// <returns>The closest matching TileMapping, or null if none within tolerance</returns>
        public TileMapping FindBestMatch(Color color)
        {
            if (mappings == null || mappings.Count == 0)
            {
                return null;
            }
            
            TileMapping bestMatch = null;
            float bestDistance = float.MaxValue;
            
            foreach (var mapping in mappings)
            {
                if (mapping == null || mapping.tilePreset == null)
                {
                    continue;
                }
                
                float distance = ColorDistance(color, mapping.targetColor);
                
                // Only consider mappings within their defined tolerance
                if (distance <= mapping.colorTolerance && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = mapping;
                }
            }
            
            return bestMatch;
        }
        
        /// <summary>
        /// Finds all mappings that match a given color within their respective tolerances.
        /// Useful for generating multiple overlapping layers.
        /// </summary>
        /// <param name="color">The color to match</param>
        /// <returns>List of all matching TileMappings, sorted by distance (closest first)</returns>
        public List<TileMapping> FindAllMatches(Color color)
        {
            var matches = new List<TileMapping>();
            
            if (mappings == null || mappings.Count == 0)
            {
                return matches;
            }
            
            // Collect all matches with their distances for sorting
            var matchesWithDistance = new List<(TileMapping mapping, float distance)>();
            
            foreach (var mapping in mappings)
            {
                if (mapping == null || mapping.tilePreset == null)
                {
                    continue;
                }
                
                float distance = ColorDistance(color, mapping.targetColor);
                
                if (distance <= mapping.colorTolerance)
                {
                    matchesWithDistance.Add((mapping, distance));
                }
            }
            
            // Sort by distance (closest first)
            matchesWithDistance.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            foreach (var match in matchesWithDistance)
            {
                matches.Add(match.mapping);
            }
            
            return matches;
        }
        
        /// <summary>
        /// Calculates the Euclidean distance between two colors in RGB space.
        /// Alpha channel is ignored for matching purposes.
        /// </summary>
        /// <param name="a">First color</param>
        /// <param name="b">Second color</param>
        /// <returns>Distance value between 0 (identical) and ~1.73 (maximum difference)</returns>
        public static float ColorDistance(Color a, Color b)
        {
            // Calculate Euclidean distance in RGB space (ignoring alpha)
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
        
        /// <summary>
        /// Calculates the normalized Euclidean distance between two colors.
        /// Result is normalized to 0-1 range for easier tolerance comparison.
        /// </summary>
        /// <param name="a">First color</param>
        /// <param name="b">Second color</param>
        /// <returns>Normalized distance value between 0 (identical) and 1 (maximum difference)</returns>
        public static float ColorDistanceNormalized(Color a, Color b)
        {
            // Maximum possible distance in RGB space is sqrt(3) ≈ 1.732
            const float maxDistance = 1.732050808f;
            return ColorDistance(a, b) / maxDistance;
        }
        
        /// <summary>
        /// Gets the count of valid mappings (those with assigned TilePresets).
        /// </summary>
        public int ValidMappingCount
        {
            get
            {
                int count = 0;
                if (mappings != null)
                {
                    foreach (var mapping in mappings)
                    {
                        if (mapping != null && mapping.tilePreset != null)
                        {
                            count++;
                        }
                    }
                }
                return count;
            }
        }
        
        /// <summary>
        /// Validates the palette and returns any issues found.
        /// </summary>
        /// <returns>List of validation warning messages</returns>
        public List<string> Validate()
        {
            var warnings = new List<string>();
            
            if (mappings == null || mappings.Count == 0)
            {
                warnings.Add("Palette has no mappings defined.");
                return warnings;
            }
            
            for (int i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                
                if (mapping == null)
                {
                    warnings.Add($"Mapping at index {i} is null.");
                    continue;
                }
                
                if (string.IsNullOrEmpty(mapping.name))
                {
                    warnings.Add($"Mapping at index {i} has no name.");
                }
                
                if (mapping.tilePreset == null)
                {
                    warnings.Add($"Mapping '{mapping.name}' has no TilePreset assigned.");
                }
                
                // Check for overlapping colors with same tolerance
                for (int j = i + 1; j < mappings.Count; j++)
                {
                    var other = mappings[j];
                    if (other == null) continue;
                    
                    float distance = ColorDistance(mapping.targetColor, other.targetColor);
                    float combinedTolerance = Mathf.Min(mapping.colorTolerance, other.colorTolerance);
                    
                    if (distance < combinedTolerance)
                    {
                        warnings.Add($"Mappings '{mapping.name}' and '{other.name}' have overlapping color ranges.");
                    }
                }
            }
            
            return warnings;
        }
    }
}
