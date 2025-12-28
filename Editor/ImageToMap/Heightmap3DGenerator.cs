/*
 * Heightmap3DGenerator.cs
 * 
 * Generates true 3D voxel-style terrain by stacking tiles vertically based on grayscale heightmap values.
 * For each XZ position, tiles are placed from Y=0 up to Y=grayscaleValue*maxHeight.
 * 
 * Usage:
 * 1. Provide a grayscale heightmap texture
 * 2. Define height-to-tile mappings (which TilePreset to use at each height range)
 * 3. Call Generate3DHeightmap to create stacked vertical layers
 */

using UnityEngine;
using UnityEditor;
using GiantGrey.TileWorldCreator;
using System.Collections.Generic;

namespace ImageToMap
{
    /// <summary>
    /// Generates true 3D voxel-style heightmaps by stacking tiles vertically.
    /// Each XZ position has tiles from Y=0 up to Y=grayscaleValue*maxHeight.
    /// </summary>
    public class Heightmap3DGenerator
    {
        #region Height Tile Mapping
        
        [System.Serializable]
        public class HeightTileMapping
        {
            [Tooltip("Minimum height (inclusive)")]
            public int minHeight;
            
            [Tooltip("Maximum height (exclusive)")]  
            public int maxHeight;
            
            [Tooltip("TilePreset to use for this height range")]
            public TilePreset tilePreset;
            
            [Tooltip("Display name (e.g., 'Water', 'Grass', 'Rock')")]
            public string name;
        }
        
        #endregion
        
        #region Main Generation Method
        
        /// <summary>
        /// Generates a 3D heightmap with vertical tile stacking.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layers to</param>
        /// <param name="heightmap">Grayscale heightmap texture</param>
        /// <param name="maxWorldHeight">Maximum vertical height in tile units</param>
        /// <param name="mappings">Height-to-tile mappings defining which preset to use at each height</param>
        /// <param name="progressCallback">Optional callback for progress updates (0-1 progress, status message)</param>
        public void Generate3DHeightmap(
            TileWorldCreatorManager manager,
            Texture2D heightmap,
            int maxWorldHeight,
            List<HeightTileMapping> mappings,
            System.Action<float, string> progressCallback = null)
        {
            if (manager == null || manager.configuration == null)
            {
                Debug.LogError("[Heightmap3D] Invalid manager or configuration");
                return;
            }
            
            if (heightmap == null)
            {
                Debug.LogError("[Heightmap3D] Heightmap texture is null");
                return;
            }
            
            if (mappings == null || mappings.Count == 0)
            {
                Debug.LogError("[Heightmap3D] No height tile mappings provided");
                return;
            }
            
            // Set map size to match heightmap
            manager.configuration.width = heightmap.width;
            manager.configuration.height = heightmap.height;
            
            Debug.Log($"[Heightmap3D] Generating {maxWorldHeight} vertical layers for {heightmap.width}x{heightmap.height} map");
            
            int layersCreated = 0;
            
            // Create layers for each height level
            for (int y = 0; y < maxWorldHeight; y++)
            {
                float progress = (float)y / maxWorldHeight;
                progressCallback?.Invoke(progress, $"Creating layer Y={y}...");
                
                // Create mask texture for this height
                Texture2D mask = CreateMaskForHeight(heightmap, y, maxWorldHeight);
                
                // Check if mask has any white pixels (tiles to place)
                if (!HasAnyWhitePixels(mask))
                {
                    Debug.Log($"[Heightmap3D] Skipping Y={y} - no tiles at this height");
                    Object.DestroyImmediate(mask);
                    continue;
                }
                
                // Determine which tile preset to use at this height
                HeightTileMapping mapping = GetMappingForHeight(y, mappings);
                
                if (mapping == null || mapping.tilePreset == null)
                {
                    Debug.LogWarning($"[Heightmap3D] No mapping found for height {y}, using first available");
                    mapping = mappings.Count > 0 ? mappings[0] : null;
                    if (mapping == null || mapping.tilePreset == null)
                    {
                        Object.DestroyImmediate(mask);
                        continue;
                    }
                }
                
                // Create BlueprintLayer with HeightTexture
                string layerName = $"{mapping.name}_Y{y}";
                var blueprint = CreateHeightBasedLayer(manager, layerName, mask);
                
                if (blueprint == null)
                {
                    Debug.LogError($"[Heightmap3D] Failed to create blueprint for {layerName}");
                    Object.DestroyImmediate(mask);
                    continue;
                }
                
                // Create BuildLayer
                CreateBuildLayerForBlueprint(manager, blueprint, mapping.tilePreset, y);
                layersCreated++;
                
                // Clean up mask texture (already embedded in asset)
                Object.DestroyImmediate(mask);
            }
            
            progressCallback?.Invoke(0.9f, "Executing layers...");
            
            // Execute all layers
            Debug.Log($"[Heightmap3D] Created {layersCreated} layers. Executing blueprint layers...");
            manager.ExecuteBlueprintLayers();
            
            Debug.Log("[Heightmap3D] Executing build layers...");
            manager.ExecuteBuildLayers(ExecutionMode.FromScratch);
            
            progressCallback?.Invoke(1f, "Complete!");
            Debug.Log("[Heightmap3D] Generation complete!");
        }
        
        #endregion
        
        #region Mask Generation
        
        /// <summary>
        /// Creates a binary mask texture for a specific height level.
        /// White pixels = place tile, Black pixels = no tile.
        /// </summary>
        /// <param name="heightmap">Source grayscale heightmap</param>
        /// <param name="targetY">Target Y level (0 to maxHeight-1)</param>
        /// <param name="maxHeight">Maximum height value</param>
        /// <returns>Binary mask texture for this height level</returns>
        private Texture2D CreateMaskForHeight(Texture2D heightmap, int targetY, int maxHeight)
        {
            int width = heightmap.width;
            int height = heightmap.height;
            Texture2D mask = new Texture2D(width, height, TextureFormat.RGBA32, false);
            mask.filterMode = FilterMode.Point; // No interpolation
            
            Color[] pixels = new Color[width * height];
            
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Read grayscale value (0-1)
                    float grayscale = heightmap.GetPixel(x, z).grayscale;
                    
                    // Convert to height (0 to maxHeight-1)
                    int pixelHeight = Mathf.RoundToInt(grayscale * (maxHeight - 1));
                    
                    // If this pixel's height >= targetY, place a tile here
                    int index = z * width + x;
                    pixels[index] = (pixelHeight >= targetY) ? Color.white : Color.black;
                }
            }
            
            mask.SetPixels(pixels);
            mask.Apply();
            return mask;
        }
        
        /// <summary>
        /// Checks if mask has any white pixels (tiles to place).
        /// </summary>
        /// <param name="mask">Mask texture to check</param>
        /// <returns>True if at least one white pixel exists</returns>
        private bool HasAnyWhitePixels(Texture2D mask)
        {
            Color[] pixels = mask.GetPixels();
            foreach (var pixel in pixels)
            {
                if (pixel.grayscale > 0.5f)
                    return true;
            }
            return false;
        }
        
        #endregion
        
        #region Height Mapping
        
        /// <summary>
        /// Finds the appropriate tile mapping for a given height.
        /// </summary>
        /// <param name="height">Height level to find mapping for</param>
        /// <param name="mappings">List of available mappings</param>
        /// <returns>Matching HeightTileMapping or null if not found</returns>
        private HeightTileMapping GetMappingForHeight(int height, List<HeightTileMapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                if (height >= mapping.minHeight && height < mapping.maxHeight)
                {
                    return mapping;
                }
            }
            // Fallback to last mapping for heights beyond defined ranges
            if (mappings.Count > 0)
            {
                var lastMapping = mappings[mappings.Count - 1];
                if (height >= lastMapping.minHeight)
                    return lastMapping;
            }
            return null;
        }
        
        #endregion
        
        #region Layer Creation
        
        /// <summary>
        /// Creates a BlueprintLayer with HeightTexture modifier.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layer to</param>
        /// <param name="name">Layer name</param>
        /// <param name="texture">Mask texture for HeightTexture modifier</param>
        /// <returns>Created BlueprintLayer or null on failure</returns>
        private BlueprintLayer CreateHeightBasedLayer(
            TileWorldCreatorManager manager,
            string name,
            Texture2D texture)
        {
            var blueprintLayer = manager.AddNewBlueprintLayer(name);
            blueprintLayer.layerColor = new Color(
                UnityEngine.Random.Range(0.3f, 1f),
                UnityEngine.Random.Range(0.3f, 1f),
                UnityEngine.Random.Range(0.3f, 1f)
            );
            
            // Create HeightTexture modifier
            HeightTexture heightTexture = ScriptableObject.CreateInstance<HeightTexture>();
            heightTexture.SetTexture(texture);
            heightTexture.SetGrayscaleRange(0.5f, 1.0f); // White pixels only
            heightTexture.hideFlags = HideFlags.HideInHierarchy;
            
            AssetDatabase.AddObjectToAsset(heightTexture, manager.configuration);
            blueprintLayer.tileMapModifiers.Add(heightTexture);
            
            return blueprintLayer;
        }
        
        /// <summary>
        /// Creates a BuildLayer linked to a BlueprintLayer.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layer to</param>
        /// <param name="blueprintLayer">BlueprintLayer to link to</param>
        /// <param name="tilePreset">TilePreset to assign</param>
        /// <param name="yOffset">Vertical offset for tile stacking</param>
        /// <returns>Created TilesBuildLayer or null on failure</returns>
        private TilesBuildLayer CreateBuildLayerForBlueprint(
            TileWorldCreatorManager manager,
            BlueprintLayer blueprintLayer,
            TilePreset tilePreset,
            int yOffset)
        {
            string buildLayerName = $"{blueprintLayer.layerName}_Build";
            var buildLayer = manager.AddNewBuildLayer<TilesBuildLayer>(buildLayerName);
            
            // Link to blueprint using GUID
            string guid = "";
            foreach (var bp in manager.configuration.blueprintLayers)
            {
                if (bp.layerName == blueprintLayer.layerName)
                {
                    guid = bp.guid;
                    break;
                }
            }
            buildLayer.assignedBlueprintLayerGuid = guid;
            
            // Set tile preset
            buildLayer.tilePresetsTop.Add(new TilesBuildLayer.TilePresetSelection 
            { 
                preset = tilePreset, 
                weight = 1f 
            });
            
            // Set Y offset for vertical stacking
            buildLayer.useDualGrid = true;
            buildLayer.layerYOffset = yOffset;
            
            return buildLayer;
        }
        
        #endregion
        
        #region Default Mappings
        
        /// <summary>
        /// Creates default height-to-tile mappings with four terrain zones.
        /// </summary>
        /// <param name="maxHeight">Maximum height value</param>
        /// <returns>List of default HeightTileMappings (presets are null and must be assigned)</returns>
        public static List<HeightTileMapping> CreateDefaultMappings(int maxHeight)
        {
            var mappings = new List<HeightTileMapping>();
            
            int quarterHeight = maxHeight / 4;
            
            mappings.Add(new HeightTileMapping
            {
                minHeight = 0,
                maxHeight = quarterHeight,
                name = "DeepWater",
                tilePreset = null
            });
            
            mappings.Add(new HeightTileMapping
            {
                minHeight = quarterHeight,
                maxHeight = quarterHeight * 2,
                name = "Grass",
                tilePreset = null
            });
            
            mappings.Add(new HeightTileMapping
            {
                minHeight = quarterHeight * 2,
                maxHeight = quarterHeight * 3,
                name = "Rock",
                tilePreset = null
            });
            
            mappings.Add(new HeightTileMapping
            {
                minHeight = quarterHeight * 3,
                maxHeight = maxHeight,
                name = "Snow",
                tilePreset = null
            });
            
            return mappings;
        }
        
        #endregion
    }
}
