/*
 * ImageToMapGenerator.cs
 * 
 * Generates TileWorldCreator 4 layers from image analysis results.
 * Creates BlueprintLayers with HeightTexture modifiers and BuildLayers with TilePreset assignments.
 * 
 * Usage:
 * 1. Analyze an image using ImageAnalyzer
 * 2. Create a ColorPalette with TilePreset mappings
 * 3. Use GenerateFromHeightLevels or GenerateFromColorClusters to create TWC4 layers
 */

using UnityEngine;
using UnityEditor;
using GiantGrey.TileWorldCreator;
using System.Collections.Generic;

namespace ImageToMap
{
    /// <summary>
    /// Generates TileWorldCreator 4 layers from image analysis results.
    /// Creates BlueprintLayers with HeightTexture modifiers and corresponding BuildLayers.
    /// </summary>
    public class ImageToMapGenerator
    {
        #region Constants
        
        private const string UNDO_GROUP_NAME = "ImageToMap Generate Layers";
        
        // Default layer colors for height-based generation
        private static readonly Color[] DefaultHeightColors = new Color[]
        {
            new Color(0.2f, 0.3f, 0.8f, 1f),  // Deep blue (lowest)
            new Color(0.3f, 0.7f, 0.3f, 1f),  // Green (low-mid)
            new Color(0.7f, 0.6f, 0.3f, 1f),  // Brown/tan (mid)
            new Color(0.6f, 0.6f, 0.6f, 1f),  // Gray (high)
            new Color(0.9f, 0.9f, 0.95f, 1f)  // White (peaks)
        };
        
        #endregion

        #region Generation Result
        
        /// <summary>
        /// Result of layer generation containing all created layers.
        /// </summary>
        public class GenerationResult
        {
            public List<BlueprintLayer> blueprintLayers = new List<BlueprintLayer>();
            public List<BuildLayer> buildLayers = new List<BuildLayer>();
            public int totalLayersCreated;
            public string message;
            public bool success;
            
            public GenerationResult()
            {
                success = true;
                message = string.Empty;
            }
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Find a mapping in ColorPalette by name (case-insensitive partial match)
        /// </summary>
        private ColorPalette.TileMapping FindMappingByName(ColorPalette palette, string levelName)
        {
            if (palette == null || palette.mappings == null || string.IsNullOrEmpty(levelName))
                return null;
            
            string nameLower = levelName.ToLower();
            
            // Exact match first
            foreach (var mapping in palette.mappings)
            {
                if (mapping.name.ToLower() == nameLower && mapping.tilePreset != null)
                    return mapping;
            }
            
            // Partial match
            foreach (var mapping in palette.mappings)
            {
                if ((mapping.name.ToLower().Contains(nameLower) || nameLower.Contains(mapping.name.ToLower())) 
                    && mapping.tilePreset != null)
                    return mapping;
            }
            
            // Try common aliases
            string[] waterAliases = { "water", "ocean", "sea", "river", "lake", "level_0", "height_0" };
            string[] sandAliases = { "sand", "beach", "desert", "shore", "level_1", "height_1" };
            string[] grassAliases = { "grass", "plains", "field", "meadow", "level_2", "height_2" };
            string[] rockAliases = { "rock", "stone", "mountain", "cliff", "level_3", "height_3" };
            string[] snowAliases = { "snow", "ice", "peak", "summit", "level_4", "height_4" };
            
            foreach (var mapping in palette.mappings)
            {
                string mapNameLower = mapping.name.ToLower();
                
                if (ContainsAny(nameLower, waterAliases) && ContainsAny(mapNameLower, waterAliases))
                    if (mapping.tilePreset != null) return mapping;
                    
                if (ContainsAny(nameLower, sandAliases) && ContainsAny(mapNameLower, sandAliases))
                    if (mapping.tilePreset != null) return mapping;
                    
                if (ContainsAny(nameLower, grassAliases) && ContainsAny(mapNameLower, grassAliases))
                    if (mapping.tilePreset != null) return mapping;
                    
                if (ContainsAny(nameLower, rockAliases) && ContainsAny(mapNameLower, rockAliases))
                    if (mapping.tilePreset != null) return mapping;
                    
                if (ContainsAny(nameLower, snowAliases) && ContainsAny(mapNameLower, snowAliases))
                    if (mapping.tilePreset != null) return mapping;
            }
            
            return null;
        }
        
        private bool ContainsAny(string text, string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword))
                    return true;
            }
            return false;
        }
        
        #endregion

        #region Height-Based Generation

        /// <summary>
        /// Generates TWC4 layers from height level analysis results.
        /// Creates one BlueprintLayer per height level with HeightTexture modifier.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layers to</param>
        /// <param name="heightLevels">Height levels from ImageAnalyzer</param>
        /// <param name="sourceTexture">Source texture for HeightTexture modifier</param>
        /// <param name="palette">ColorPalette for TilePreset mapping (optional)</param>
        /// <returns>Generation result with created layers</returns>
        public GenerationResult GenerateFromHeightLevels(
            TileWorldCreatorManager manager,
            List<ImageAnalyzer.HeightLevel> heightLevels,
            Texture2D sourceTexture,
            ColorPalette palette = null)
        {
            var result = new GenerationResult();
            
            // Validation
            if (manager == null)
            {
                result.success = false;
                result.message = "TileWorldCreatorManager is null";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            if (manager.configuration == null)
            {
                result.success = false;
                result.message = "Manager has no configuration assigned";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            if (heightLevels == null || heightLevels.Count == 0)
            {
                result.success = false;
                result.message = "No height levels provided";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            if (sourceTexture == null)
            {
                result.success = false;
                result.message = "Source texture is null";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            // Begin undo group
            Undo.SetCurrentGroupName(UNDO_GROUP_NAME);
            int undoGroup = Undo.GetCurrentGroup();
            
            try
            {
                // Record manager for undo
                Undo.RecordObject(manager, "Generate Height Layers");
                Undo.RecordObject(manager.configuration, "Generate Height Layers");
                
                Debug.Log($"[ImageToMapGenerator] Generating {heightLevels.Count} layers from height levels...");
                
                for (int i = 0; i < heightLevels.Count; i++)
                {
                    var level = heightLevels[i];
                    
                    // Get layer color
                    Color layerColor = GetColorForLevel(i, heightLevels.Count);
                    
                    // Create blueprint layer with HeightTexture modifier
                    var blueprintLayer = CreateHeightBasedLayer(
                        manager,
                        level.name,
                        sourceTexture,
                        level.minHeight,
                        level.maxHeight,
                        layerColor
                    );
                    
                    if (blueprintLayer != null)
                    {
                        result.blueprintLayers.Add(blueprintLayer);
                        
                        // Try to find matching TilePreset from palette
                        TilePreset tilePreset = null;
                        
                        // FIXED: Always calculate Y offset based on level index for distinct heights
                        // Each level gets progressively higher (0.5 units per level)
                        float yOffset = i * 0.5f;
                        
                        if (palette != null && palette.mappings != null)
                        {
                            // First try: Match by level NAME (more reliable)
                            var mapping = FindMappingByName(palette, level.name);
                            
                            // Second try: Match by height index (Water=0, Beach=1, Grass=2, etc.)
                            if (mapping == null && i < palette.mappings.Count)
                            {
                                mapping = palette.mappings[i];
                            }
                            
                            // Third try: Use first available mapping with a TilePreset (cycle through)
                            if (mapping == null || mapping.tilePreset == null)
                            {
                                // Cycle through palette mappings for levels beyond palette count
                                int paletteIdx = i % palette.mappings.Count;
                                for (int p = 0; p < palette.mappings.Count; p++)
                                {
                                    int checkIdx = (paletteIdx + p) % palette.mappings.Count;
                                    if (palette.mappings[checkIdx].tilePreset != null)
                                    {
                                        mapping = palette.mappings[checkIdx];
                                        break;
                                    }
                                }
                            }
                            
                            if (mapping != null && mapping.tilePreset != null)
                            {
                                tilePreset = mapping.tilePreset;
                                // NOTE: Do NOT use mapping.yOffset - always use index-based yOffset for distinct heights
                                Debug.Log($"[ImageToMapGenerator] Level {i} '{level.name}' → TilePreset '{tilePreset.name}' (Y: {yOffset})");
                            }
                        }
                        
                        // Create build layer if we have a preset
                        if (tilePreset != null)
                        {
                            var buildLayer = CreateBuildLayerForBlueprint(
                                manager,
                                blueprintLayer,
                                tilePreset,
                                yOffset
                            );
                            
                            if (buildLayer != null)
                            {
                                result.buildLayers.Add(buildLayer);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[ImageToMapGenerator] No TilePreset found for level '{level.name}'. BuildLayer not created.");
                        }
                    }
                }
                
                // Mark assets as dirty and save
                EditorUtility.SetDirty(manager.configuration);
                AssetDatabase.SaveAssets();
                
                result.totalLayersCreated = result.blueprintLayers.Count + result.buildLayers.Count;
                result.message = $"Created {result.blueprintLayers.Count} blueprint layers and {result.buildLayers.Count} build layers";
                
                Debug.Log($"[ImageToMapGenerator] {result.message}");
            }
            catch (System.Exception ex)
            {
                result.success = false;
                result.message = $"Error during generation: {ex.Message}";
                Debug.LogError($"[ImageToMapGenerator] {result.message}\n{ex.StackTrace}");
                
                // Clean up partially created resources on failure
                CleanupPartialGenerationResults(result);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
            
            return result;
        }

        #endregion

        #region Color-Based Generation

        /// <summary>
        /// Generates TWC4 layers from color cluster analysis results.
        /// Creates one BlueprintLayer per color cluster.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layers to</param>
        /// <param name="clusters">Color clusters from ImageAnalyzer</param>
        /// <param name="sourceTexture">Source texture for reference</param>
        /// <param name="palette">ColorPalette for TilePreset mapping</param>
        /// <returns>Generation result with created layers</returns>
        public GenerationResult GenerateFromColorClusters(
            TileWorldCreatorManager manager,
            List<ImageAnalyzer.ColorCluster> clusters,
            Texture2D sourceTexture,
            ColorPalette palette)
        {
            var result = new GenerationResult();
            
            // Validation
            if (manager == null)
            {
                result.success = false;
                result.message = "TileWorldCreatorManager is null";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            if (manager.configuration == null)
            {
                result.success = false;
                result.message = "Manager has no configuration assigned";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            if (clusters == null || clusters.Count == 0)
            {
                result.success = false;
                result.message = "No color clusters provided";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            if (palette == null)
            {
                result.success = false;
                result.message = "ColorPalette is required for color cluster generation";
                Debug.LogError($"[ImageToMapGenerator] {result.message}");
                return result;
            }
            
            // Begin undo group
            Undo.SetCurrentGroupName(UNDO_GROUP_NAME);
            int undoGroup = Undo.GetCurrentGroup();
            
            try
            {
                // Record manager for undo
                Undo.RecordObject(manager, "Generate Color Layers");
                Undo.RecordObject(manager.configuration, "Generate Color Layers");
                
                Debug.Log($"[ImageToMapGenerator] Generating layers from {clusters.Count} color clusters...");
                
                int layerIndex = 0;
                
                foreach (var cluster in clusters)
                {
                    // Find matching TilePreset from palette
                    var mapping = palette.FindBestMatch(cluster.centroid);
                    
                    if (mapping == null || mapping.tilePreset == null)
                    {
                        Debug.LogWarning($"[ImageToMapGenerator] No matching preset found for cluster color {cluster.centroid}");
                        continue;
                    }
                    
                    // Create mask texture from cluster pixels
                    Texture2D maskTexture = CreateMaskTextureFromCluster(cluster, sourceTexture.width, sourceTexture.height);
                    
                    if (maskTexture == null)
                    {
                        Debug.LogWarning($"[ImageToMapGenerator] Failed to create mask texture for cluster {cluster.name}");
                        continue;
                    }
                    
                    // Determine layer name
                    string layerName = !string.IsNullOrEmpty(mapping.name) ? mapping.name : $"ColorLayer_{layerIndex}";
                    
                    // Create blueprint layer with HeightTexture modifier (using mask as grayscale 0-1)
                    var blueprintLayer = CreateHeightBasedLayer(
                        manager,
                        layerName,
                        maskTexture,
                        0.5f,  // Min threshold for mask
                        1.0f,  // Max threshold for mask
                        cluster.centroid
                    );
                    
                    // Clean up maskTexture if blueprint layer creation failed
                    if (blueprintLayer == null)
                    {
                        Object.DestroyImmediate(maskTexture);
                        Debug.LogWarning($"[ImageToMapGenerator] Failed to create blueprint layer for cluster {cluster.name}");
                        continue;
                    }
                    
                    if (blueprintLayer != null)
                    {
                        result.blueprintLayers.Add(blueprintLayer);
                        
                        // Create build layer
                        var buildLayer = CreateBuildLayerForBlueprint(
                            manager,
                            blueprintLayer,
                            mapping.tilePreset,
                            mapping.yOffset
                        );
                        
                        if (buildLayer != null)
                        {
                            result.buildLayers.Add(buildLayer);
                        }
                    }
                    
                    layerIndex++;
                }
                
                // Mark assets as dirty and save
                EditorUtility.SetDirty(manager.configuration);
                AssetDatabase.SaveAssets();
                
                result.totalLayersCreated = result.blueprintLayers.Count + result.buildLayers.Count;
                result.message = $"Created {result.blueprintLayers.Count} blueprint layers and {result.buildLayers.Count} build layers from color clusters";
                
                Debug.Log($"[ImageToMapGenerator] {result.message}");
            }
            catch (System.Exception ex)
            {
                result.success = false;
                result.message = $"Error during generation: {ex.Message}";
                Debug.LogError($"[ImageToMapGenerator] {result.message}\n{ex.StackTrace}");
                
                // Clean up partially created resources on failure
                CleanupPartialGenerationResults(result);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
            
            return result;
        }
        
        /// <summary>
        /// Cleans up partially created resources when generation fails mid-way.
        /// </summary>
        /// <param name="result">The generation result containing partial resources</param>
        private void CleanupPartialGenerationResults(GenerationResult result)
        {
            if (result == null) return;
            
            // Note: BlueprintLayers and BuildLayers are managed by the configuration asset
            // and will be cleaned up through the Undo system when the user undoes the operation.
            // This method is a placeholder for any additional cleanup that might be needed.
            
            result.blueprintLayers.Clear();
            result.buildLayers.Clear();
            result.totalLayersCreated = 0;
            
            Debug.Log("[ImageToMapGenerator] Cleaned up partial generation results due to failure");
        }

        #endregion

        #region Blueprint Layer Creation

        /// <summary>
        /// Creates a BlueprintLayer with HeightTexture modifier for a specific height range.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layer to</param>
        /// <param name="name">Layer name</param>
        /// <param name="texture">Texture for HeightTexture modifier</param>
        /// <param name="minHeight">Minimum grayscale value (0-1)</param>
        /// <param name="maxHeight">Maximum grayscale value (0-1)</param>
        /// <param name="layerColor">Color for layer visualization</param>
        /// <returns>Created BlueprintLayer or null on failure</returns>
        private BlueprintLayer CreateHeightBasedLayer(
            TileWorldCreatorManager manager,
            string name,
            Texture2D texture,
            float minHeight,
            float maxHeight,
            Color layerColor)
        {
            if (manager == null || manager.configuration == null)
            {
                Debug.LogError("[ImageToMapGenerator] CreateHeightBasedLayer: Invalid manager or configuration");
                return null;
            }
            
            HeightTexture heightTexture = null;
            
            try
            {
                // Create the blueprint layer
                var blueprintLayer = manager.AddNewBlueprintLayer(name);
                
                if (blueprintLayer == null)
                {
                    Debug.LogError($"[ImageToMapGenerator] Failed to create blueprint layer '{name}'");
                    return null;
                }
                
                // Set layer properties
                blueprintLayer.layerColor = layerColor;
                
                // Create HeightTexture modifier
                heightTexture = ScriptableObject.CreateInstance<HeightTexture>();
                
                if (heightTexture == null)
                {
                    Debug.LogError("[ImageToMapGenerator] Failed to create HeightTexture modifier");
                    return blueprintLayer;
                }
                
                // Configure HeightTexture
                heightTexture.SetTexture(texture);
                heightTexture.SetGrayscaleRange(minHeight, maxHeight);
                
                // Set hideFlags so it doesn't show in hierarchy
                heightTexture.hideFlags = HideFlags.HideInHierarchy;
                
                // Add modifier as sub-asset of the configuration
                AssetDatabase.AddObjectToAsset(heightTexture, manager.configuration);
                
                // Add modifier to the layer
                blueprintLayer.tileMapModifiers.Add(heightTexture);
                
                // Clear reference since it's now owned by the asset database
                heightTexture = null;
                
                Debug.Log($"[ImageToMapGenerator] Created blueprint layer '{name}' with HeightTexture ({minHeight:F2} - {maxHeight:F2})");
                
                return blueprintLayer;
            }
            catch (System.Exception ex)
            {
                // Clean up the ScriptableObject if it wasn't added to the asset database
                if (heightTexture != null)
                {
                    Object.DestroyImmediate(heightTexture);
                }
                
                Debug.LogError($"[ImageToMapGenerator] Error creating blueprint layer '{name}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Build Layer Creation

        /// <summary>
        /// Creates a BuildLayer for an existing BlueprintLayer with TilePreset assignment.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to add layer to</param>
        /// <param name="blueprintLayer">BlueprintLayer to associate with</param>
        /// <param name="tilePreset">TilePreset to assign</param>
        /// <param name="yOffset">Vertical offset for the layer</param>
        /// <returns>Created TilesBuildLayer or null on failure</returns>
        private BuildLayer CreateBuildLayerForBlueprint(
            TileWorldCreatorManager manager,
            BlueprintLayer blueprintLayer,
            TilePreset tilePreset,
            float yOffset)
        {
            if (manager == null || manager.configuration == null)
            {
                Debug.LogError("[ImageToMapGenerator] CreateBuildLayerForBlueprint: Invalid manager or configuration");
                return null;
            }
            
            if (blueprintLayer == null)
            {
                Debug.LogError("[ImageToMapGenerator] CreateBuildLayerForBlueprint: BlueprintLayer is null");
                return null;
            }
            
            if (tilePreset == null)
            {
                Debug.LogWarning($"[ImageToMapGenerator] CreateBuildLayerForBlueprint: TilePreset is null for layer '{blueprintLayer.layerName}'");
                return null;
            }
            
            try
            {
                // Create build layer name based on blueprint layer
                string buildLayerName = $"{blueprintLayer.layerName}_Tiles";
                
                // Create the TilesBuildLayer
                var buildLayer = manager.AddNewBuildLayer<TilesBuildLayer>(buildLayerName);
                
                if (buildLayer == null)
                {
                    Debug.LogError($"[ImageToMapGenerator] Failed to create build layer '{buildLayerName}'");
                    return null;
                }
                
                // Assign to blueprint layer using GUID
                buildLayer.assignedBlueprintLayerGuid = manager.configuration.GetBlueprintLayerGuid(blueprintLayer.layerName);
                
                // Add tile preset with weight
                buildLayer.tilePresetsTop.Add(new TilesBuildLayer.TilePresetSelection 
                { 
                    preset = tilePreset, 
                    weight = 1f 
                });
                
                // Set layer properties
                buildLayer.useDualGrid = true;
                buildLayer.layerYOffset = yOffset;
                
                Debug.Log($"[ImageToMapGenerator] Created build layer '{buildLayerName}' with preset '{tilePreset.name}' (Y offset: {yOffset})");
                
                return buildLayer;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ImageToMapGenerator] Error creating build layer for '{blueprintLayer.layerName}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets a color for a specific height level index.
        /// </summary>
        private Color GetColorForLevel(int index, int totalLevels)
        {
            if (index < DefaultHeightColors.Length)
            {
                return DefaultHeightColors[index];
            }
            
            // Generate a color based on hue for additional levels
            float hue = (float)index / totalLevels;
            return Color.HSVToRGB(hue, 0.7f, 0.8f);
        }

        /// <summary>
        /// Creates a binary mask texture from a color cluster's pixel positions.
        /// White pixels represent the cluster, black pixels are empty.
        /// </summary>
        /// <param name="cluster">Color cluster with pixel positions</param>
        /// <param name="width">Texture width</param>
        /// <param name="height">Texture height</param>
        /// <returns>Mask texture or null on failure</returns>
        /// <remarks>Caller is responsible for destroying the returned Texture2D when no longer needed.</remarks>
        private Texture2D CreateMaskTextureFromCluster(ImageAnalyzer.ColorCluster cluster, int width, int height)
        {
            if (cluster.pixels == null || cluster.pixels.Count == 0)
            {
                return null;
            }
            
            // Validate texture dimensions
            if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
            {
                throw new System.ArgumentException($"Invalid texture dimensions: {width}x{height}. Must be between 1 and 8192.");
            }
            
            Texture2D maskTexture = null;
            try
            {
                maskTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                
                // Initialize all pixels to black
                Color[] pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.black;
                }
                
                // Set cluster pixels to white
                foreach (var pixelPos in cluster.pixels)
                {
                    int x = pixelPos.x;
                    int y = pixelPos.y;
                    
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        int index = y * width + x;
                        pixels[index] = Color.white;
                    }
                }
                
                maskTexture.SetPixels(pixels);
                maskTexture.Apply();
                
                return maskTexture;
            }
            catch (System.Exception ex)
            {
                // Clean up texture on failure to prevent memory leak
                if (maskTexture != null)
                {
                    Object.DestroyImmediate(maskTexture);
                }
                Debug.LogError($"[ImageToMapGenerator] Error creating mask texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Executes all blueprint and build layers after generation.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to execute</param>
        public void ExecuteLayers(TileWorldCreatorManager manager)
        {
            if (manager == null)
            {
                Debug.LogError("[ImageToMapGenerator] Cannot execute layers: manager is null");
                return;
            }
            
            Undo.RecordObject(manager, "Execute Generated Layers");
            
            manager.ExecuteBlueprintLayers();
            manager.ExecuteBuildLayers(ExecutionMode.FromScratch);
            
            Debug.Log("[ImageToMapGenerator] Layer execution complete");
        }

        /// <summary>
        /// Clears all existing layers from the manager before generation.
        /// </summary>
        /// <param name="manager">TileWorldCreatorManager to clear</param>
        public void ClearExistingLayers(TileWorldCreatorManager manager)
        {
            if (manager == null || manager.configuration == null)
            {
                return;
            }
            
            Undo.RecordObject(manager.configuration, "Clear Existing Layers");
            
            manager.ClearConfiguration();
            
            Debug.Log("[ImageToMapGenerator] Cleared existing layers");
        }

        #endregion
    }
}
