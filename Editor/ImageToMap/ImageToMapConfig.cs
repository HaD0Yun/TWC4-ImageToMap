/*
 * ImageToMapConfig.cs
 * 
 * ScriptableObject for configuring Image to Map generation settings.
 * Provides centralized configuration for analysis parameters, generation dimensions,
 * height-based terrain classification, and randomization options.
 * 
 * Usage:
 * 1. Create via Assets > Create > ImageToMap > Config
 * 2. Configure analysis settings (cluster count, height levels)
 * 3. Set generation dimensions and cell size
 * 4. Adjust height ranges for terrain classification
 * 5. Assign a ColorPalette for tile mapping
 */

using UnityEngine;

namespace ImageToMap
{
    /// <summary>
    /// ScriptableObject that defines all configuration settings for image-based map generation.
    /// Includes analysis parameters, generation dimensions, height ranges, and randomization options.
    /// </summary>
    [CreateAssetMenu(fileName = "ImageToMapConfig", menuName = "ImageToMap/Config")]
    public class ImageToMapConfig : ScriptableObject
    {
        #region Analysis Settings
        
        [Header("Analysis Settings")]
        [Tooltip("Number of color clusters for K-means color quantization. Higher values preserve more color detail.")]
        [Range(2, 10)]
        public int colorClusterCount = 5;
        
        [Tooltip("Number of distinct height levels for terrain classification. Affects layer generation.")]
        [Range(2, 8)]
        public int heightLevelCount = 4;
        
        [Tooltip("Enable edge detection to identify terrain boundaries and transitions.")]
        public bool useEdgeDetection = false;
        
        [Tooltip("Sensitivity threshold for edge detection. Lower values detect more edges.")]
        [Range(0.1f, 0.9f)]
        public float edgeThreshold = 0.3f;
        
        #endregion
        
        #region Generation Settings
        
        [Header("Generation Settings")]
        [Tooltip("Width of the generated map in cells.")]
        [Range(16, 256)]
        public int mapWidth = 64;
        
        [Tooltip("Height of the generated map in cells.")]
        [Range(16, 256)]
        public int mapHeight = 64;
        
        [Tooltip("Size of each cell in world units.")]
        [Range(0.5f, 4f)]
        public float cellSize = 1f;
        
        [Tooltip("Size of clusters for grouping similar cells. Used for noise reduction.")]
        [Min(1)]
        public int clusterCellSize = 8;
        
        [Tooltip("Enable dual grid generation for smoother tile transitions.")]
        public bool useDualGrid = true;
        
        #endregion
        
        #region Tile Mapping
        
        [Header("Tile Mapping")]
        [Tooltip("Color palette defining mappings from image colors to tile presets.")]
        public ColorPalette colorPalette;
        
        #endregion
        
        #region Default Height Ranges
        
        [Header("Default Height Ranges")]
        [Tooltip("Maximum normalized height (0-1) for Water terrain. Values below this threshold are classified as water.")]
        [Range(0f, 0.5f)]
        public float waterMaxHeight = 0.2f;
        
        [Tooltip("Maximum normalized height (0-1) for Beach/Sand terrain. Values between water and this threshold are classified as beach.")]
        [Range(0.1f, 0.6f)]
        public float beachMaxHeight = 0.35f;
        
        [Tooltip("Maximum normalized height (0-1) for Grass/Plains terrain. Values between beach and this threshold are classified as grass. Above this is Mountain/Snow.")]
        [Range(0.3f, 0.9f)]
        public float grassMaxHeight = 0.7f;
        
        // Note: Mountain/Snow terrain covers the range from grassMaxHeight to 1.0
        
        #endregion
        
        #region Random Seed
        
        [Header("Random Seed")]
        [Tooltip("When enabled, generates a new random seed each time. When disabled, uses the specified seed for reproducible results.")]
        public bool useRandomSeed = true;
        
        [Tooltip("Fixed seed value for reproducible generation. Only used when 'Use Random Seed' is disabled.")]
        public int seed = 12345;
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Gets the height range boundaries as an array.
        /// Array contains threshold values in ascending order: [0, waterMax, beachMax, grassMax, 1]
        /// </summary>
        /// <returns>Array of height thresholds defining terrain boundaries</returns>
        public float[] GetHeightRanges()
        {
            return new float[] { 0f, waterMaxHeight, beachMaxHeight, grassMaxHeight, 1f };
        }
        
        /// <summary>
        /// Gets the names of each height level in order.
        /// Corresponds to the ranges defined by GetHeightRanges().
        /// </summary>
        /// <returns>Array of terrain level names</returns>
        public string[] GetHeightLevelNames()
        {
            return new string[] { "Water", "Beach", "Grass", "Mountain" };
        }
        
        /// <summary>
        /// Determines the terrain type for a given normalized height value.
        /// </summary>
        /// <param name="normalizedHeight">Height value between 0 and 1</param>
        /// <returns>Index of the terrain type (0=Water, 1=Beach, 2=Grass, 3=Mountain)</returns>
        public int GetTerrainIndexForHeight(float normalizedHeight)
        {
            normalizedHeight = Mathf.Clamp01(normalizedHeight);
            
            if (normalizedHeight <= waterMaxHeight)
                return 0; // Water
            if (normalizedHeight <= beachMaxHeight)
                return 1; // Beach
            if (normalizedHeight <= grassMaxHeight)
                return 2; // Grass
            
            return 3; // Mountain
        }
        
        /// <summary>
        /// Gets the terrain name for a given normalized height value.
        /// </summary>
        /// <param name="normalizedHeight">Height value between 0 and 1</param>
        /// <returns>Name of the terrain type</returns>
        public string GetTerrainNameForHeight(float normalizedHeight)
        {
            int index = GetTerrainIndexForHeight(normalizedHeight);
            string[] names = GetHeightLevelNames();
            return names[index];
        }
        
        /// <summary>
        /// Gets the effective seed value based on configuration.
        /// Returns a new random seed if useRandomSeed is true, otherwise returns the fixed seed.
        /// </summary>
        /// <returns>Seed value to use for generation</returns>
        public int GetEffectiveSeed()
        {
            if (useRandomSeed)
            {
                return System.Environment.TickCount;
            }
            return seed;
        }
        
        /// <summary>
        /// Calculates the total world size of the generated map.
        /// </summary>
        /// <returns>Vector2 containing world width and height</returns>
        public Vector2 GetWorldSize()
        {
            return new Vector2(mapWidth * cellSize, mapHeight * cellSize);
        }
        
        #endregion
        
        #region Validation
        
        /// <summary>
        /// Validates the configuration and returns whether it is valid.
        /// </summary>
        /// <param name="error">Error message if validation fails, null otherwise</param>
        /// <returns>True if configuration is valid, false otherwise</returns>
        public bool Validate(out string error)
        {
            // Validate height range ordering
            if (waterMaxHeight >= beachMaxHeight)
            {
                error = "Water max height must be less than beach max height.";
                return false;
            }
            
            if (beachMaxHeight >= grassMaxHeight)
            {
                error = "Beach max height must be less than grass max height.";
                return false;
            }
            
            if (grassMaxHeight >= 1f)
            {
                error = "Grass max height must be less than 1.0 to allow mountain terrain.";
                return false;
            }
            
            // Validate map dimensions
            if (mapWidth < 16 || mapWidth > 256)
            {
                error = "Map width must be between 16 and 256.";
                return false;
            }
            
            if (mapHeight < 16 || mapHeight > 256)
            {
                error = "Map height must be between 16 and 256.";
                return false;
            }
            
            // Validate cluster cell size
            if (clusterCellSize < 1)
            {
                error = "Cluster cell size must be at least 1.";
                return false;
            }
            
            if (clusterCellSize > Mathf.Min(mapWidth, mapHeight))
            {
                error = "Cluster cell size cannot exceed map dimensions.";
                return false;
            }
            
            // Validate cell size
            if (cellSize <= 0f)
            {
                error = "Cell size must be greater than 0.";
                return false;
            }
            
            // Validate height level count matches available terrain types
            if (heightLevelCount > 4)
            {
                // Warning: More height levels than defined terrain types
                // This is allowed but may cause unexpected behavior
            }
            
            error = null;
            return true;
        }
        
        /// <summary>
        /// Gets a list of validation warnings (non-critical issues).
        /// </summary>
        /// <returns>List of warning messages</returns>
        public System.Collections.Generic.List<string> GetValidationWarnings()
        {
            var warnings = new System.Collections.Generic.List<string>();
            
            // Check for missing color palette
            if (colorPalette == null)
            {
                warnings.Add("No color palette assigned. Tile mapping will not be available.");
            }
            else
            {
                // Validate the color palette if present
                var paletteWarnings = colorPalette.Validate();
                if (paletteWarnings.Count > 0)
                {
                    warnings.Add($"Color palette has {paletteWarnings.Count} warning(s).");
                    warnings.AddRange(paletteWarnings);
                }
            }
            
            // Check for very small height ranges
            float waterRange = waterMaxHeight;
            float beachRange = beachMaxHeight - waterMaxHeight;
            float grassRange = grassMaxHeight - beachMaxHeight;
            float mountainRange = 1f - grassMaxHeight;
            
            float minRecommendedRange = 0.1f;
            
            if (waterRange < minRecommendedRange)
            {
                warnings.Add($"Water height range ({waterRange:F2}) is very small. Consider increasing waterMaxHeight.");
            }
            
            if (beachRange < minRecommendedRange)
            {
                warnings.Add($"Beach height range ({beachRange:F2}) is very small. Consider adjusting height thresholds.");
            }
            
            if (grassRange < minRecommendedRange)
            {
                warnings.Add($"Grass height range ({grassRange:F2}) is very small. Consider adjusting height thresholds.");
            }
            
            if (mountainRange < minRecommendedRange)
            {
                warnings.Add($"Mountain height range ({mountainRange:F2}) is very small. Consider decreasing grassMaxHeight.");
            }
            
            // Check edge detection settings
            if (useEdgeDetection && edgeThreshold < 0.2f)
            {
                warnings.Add("Edge detection threshold is very low. This may result in noisy edge detection.");
            }
            
            // Check cluster cell size relative to map size
            if (clusterCellSize > mapWidth / 4 || clusterCellSize > mapHeight / 4)
            {
                warnings.Add("Cluster cell size is large relative to map size. This may result in low detail clustering.");
            }
            
            return warnings;
        }
        
        /// <summary>
        /// Performs full validation and logs results to Unity console.
        /// </summary>
        /// <returns>True if configuration passes all critical validation</returns>
        public bool ValidateAndLog()
        {
            bool isValid = Validate(out string error);
            
            if (!isValid)
            {
                Debug.LogError($"[ImageToMapConfig] Validation failed: {error}");
                return false;
            }
            
            var warnings = GetValidationWarnings();
            foreach (var warning in warnings)
            {
                Debug.LogWarning($"[ImageToMapConfig] {warning}");
            }
            
            if (warnings.Count == 0)
            {
                Debug.Log("[ImageToMapConfig] Configuration is valid with no warnings.");
            }
            else
            {
                Debug.Log($"[ImageToMapConfig] Configuration is valid with {warnings.Count} warning(s).");
            }
            
            return true;
        }
        
        #endregion
        
        #region Preset Methods
        
        /// <summary>
        /// Applies default island-style height ranges.
        /// Creates a typical island with water, beach, grass, and mountain peaks.
        /// </summary>
        public void ApplyIslandPreset()
        {
            waterMaxHeight = 0.25f;
            beachMaxHeight = 0.35f;
            grassMaxHeight = 0.75f;
        }
        
        /// <summary>
        /// Applies default continental-style height ranges.
        /// Creates terrain with less water and more land area.
        /// </summary>
        public void ApplyContinentalPreset()
        {
            waterMaxHeight = 0.15f;
            beachMaxHeight = 0.25f;
            grassMaxHeight = 0.65f;
        }
        
        /// <summary>
        /// Applies default mountainous-style height ranges.
        /// Creates terrain dominated by mountains with less low-lying areas.
        /// </summary>
        public void ApplyMountainousPreset()
        {
            waterMaxHeight = 0.1f;
            beachMaxHeight = 0.2f;
            grassMaxHeight = 0.5f;
        }
        
        /// <summary>
        /// Applies default archipelago-style height ranges.
        /// Creates terrain with lots of water and small islands.
        /// </summary>
        public void ApplyArchipelagoPreset()
        {
            waterMaxHeight = 0.4f;
            beachMaxHeight = 0.5f;
            grassMaxHeight = 0.8f;
        }
        
        #endregion
    }
}
