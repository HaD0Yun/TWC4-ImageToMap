/*
 * ImageToMapQuickSetup.cs
 * 
 * One-click setup for Image To Map Generator.
 * Automatically creates TileWorldCreatorManager, Configuration, and opens the editor window.
 */

using UnityEngine;
using UnityEditor;
using GiantGrey.TileWorldCreator;

namespace ImageToMap
{
    public static class ImageToMapQuickSetup
    {
        [MenuItem("Tools/TileWorldCreator/Image To Map - Quick Setup", false, 100)]
        public static void QuickSetup()
        {
            Debug.Log("[ImageToMap] Starting Quick Setup...");

            // Step 1: Find or create TileWorldCreatorManager
            TileWorldCreatorManager manager = Object.FindObjectOfType<TileWorldCreatorManager>();
            
            if (manager == null)
            {
                Debug.Log("[ImageToMap] Creating TileWorldCreatorManager...");
                
                GameObject managerGO = new GameObject("TileWorldCreatorManager");
                manager = managerGO.AddComponent<TileWorldCreatorManager>();
                
                Undo.RegisterCreatedObjectUndo(managerGO, "Create TWC Manager");
                
                Debug.Log("[ImageToMap] TileWorldCreatorManager created!");
            }
            else
            {
                Debug.Log("[ImageToMap] TileWorldCreatorManager already exists in scene.");
            }

            // Step 2: Check if Configuration exists
            if (manager.configuration == null)
            {
                Debug.Log("[ImageToMap] Creating Configuration asset...");
                
                // Create folder if it doesn't exist
                if (!AssetDatabase.IsValidFolder("Assets/ImageToMap"))
                {
                    AssetDatabase.CreateFolder("Assets", "ImageToMap");
                }

                // Create Configuration asset
                string configPath = "Assets/ImageToMap/ImageToMapConfiguration.asset";
                
                // Check if already exists
                Configuration existingConfig = AssetDatabase.LoadAssetAtPath<Configuration>(configPath);
                
                if (existingConfig == null)
                {
                    existingConfig = ScriptableObject.CreateInstance<Configuration>();
                    AssetDatabase.CreateAsset(existingConfig, configPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[ImageToMap] Configuration created at: {configPath}");
                }
                
                // Assign to manager
                Undo.RecordObject(manager, "Assign Configuration");
                manager.configuration = existingConfig;
                EditorUtility.SetDirty(manager);
                
                Debug.Log("[ImageToMap] Configuration assigned to manager!");
            }
            else
            {
                Debug.Log("[ImageToMap] Manager already has a Configuration.");
            }

            // Step 3: Create default ColorPalette with TilePresets
            string palettePath = "Assets/ImageToMap/DefaultColorPalette.asset";
            ColorPalette palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(palettePath);
            
            // Find TilePresets in project
            TilePreset waterPreset = FindTilePreset("Water", "Blue", "River");
            TilePreset sandPreset = FindTilePreset("Sand");
            TilePreset grassPreset = FindTilePreset("Grass");
            TilePreset rockPreset = FindTilePreset("Cliff", "Rock", "Stone");
            TilePreset snowPreset = FindTilePreset("Snow", "Ice");
            
            // Use grass as fallback if specific preset not found
            TilePreset fallbackPreset = grassPreset ?? waterPreset ?? sandPreset;
            
            if (palette == null)
            {
                Debug.Log("[ImageToMap] Creating default ColorPalette with TilePresets...");
                
                palette = ScriptableObject.CreateInstance<ColorPalette>();
                palette.mappings = new System.Collections.Generic.List<ColorPalette.TileMapping>();
                
                AssetDatabase.CreateAsset(palette, palettePath);
            }
            
            // Update palette mappings with found TilePresets
            palette.mappings = new System.Collections.Generic.List<ColorPalette.TileMapping>
            {
                new ColorPalette.TileMapping 
                { 
                    name = "Water", 
                    targetColor = new Color(0.2f, 0.4f, 0.8f), 
                    colorTolerance = 0.3f,
                    yOffset = -0.5f,
                    tilePreset = waterPreset ?? fallbackPreset
                },
                new ColorPalette.TileMapping 
                { 
                    name = "Sand", 
                    targetColor = new Color(0.9f, 0.85f, 0.6f), 
                    colorTolerance = 0.25f,
                    yOffset = 0f,
                    tilePreset = sandPreset ?? fallbackPreset
                },
                new ColorPalette.TileMapping 
                { 
                    name = "Grass", 
                    targetColor = new Color(0.3f, 0.7f, 0.3f), 
                    colorTolerance = 0.3f,
                    yOffset = 0.5f,
                    tilePreset = grassPreset ?? fallbackPreset
                },
                new ColorPalette.TileMapping 
                { 
                    name = "Rock", 
                    targetColor = new Color(0.5f, 0.5f, 0.5f), 
                    colorTolerance = 0.3f,
                    yOffset = 1f,
                    tilePreset = rockPreset ?? fallbackPreset
                },
                new ColorPalette.TileMapping 
                { 
                    name = "Snow", 
                    targetColor = new Color(0.95f, 0.95f, 1f), 
                    colorTolerance = 0.2f,
                    yOffset = 1.5f,
                    tilePreset = snowPreset ?? grassPreset ?? fallbackPreset
                }
            };
            
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            
            int presetCount = 0;
            foreach (var m in palette.mappings) if (m.tilePreset != null) presetCount++;
            Debug.Log($"[ImageToMap] ColorPalette updated with {presetCount} TilePresets");

            // Step 4: Create default Config if not exists
            string configAssetPath = "Assets/ImageToMap/DefaultImageToMapConfig.asset";
            ImageToMapConfig imgConfig = AssetDatabase.LoadAssetAtPath<ImageToMapConfig>(configAssetPath);
            
            if (imgConfig == null)
            {
                Debug.Log("[ImageToMap] Creating default ImageToMapConfig...");
                
                imgConfig = ScriptableObject.CreateInstance<ImageToMapConfig>();
                imgConfig.colorPalette = palette;
                
                AssetDatabase.CreateAsset(imgConfig, configAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ImageToMap] ImageToMapConfig created at: {configAssetPath}");
            }

            // Step 5: Select the manager in hierarchy
            Selection.activeGameObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager);

            // Step 6: Open the Image To Map Generator window
            ImageToMapEditorWindow window = EditorWindow.GetWindow<ImageToMapEditorWindow>("Image To Map");
            window.Show();
            window.Focus();

            // Show success message
            EditorUtility.DisplayDialog(
                "Quick Setup Complete!",
                "✓ TileWorldCreatorManager - Ready\n" +
                "✓ Configuration - Assigned\n" +
                "✓ ColorPalette - Created\n" +
                "✓ ImageToMapConfig - Created\n\n" +
                "Now just drag an image to the window and click 'Generate Map'!",
                "OK"
            );

            Debug.Log("[ImageToMap] Quick Setup complete! Ready to generate maps.");
        }

        [MenuItem("Tools/TileWorldCreator/Image To Map - Quick Setup", true)]
        public static bool QuickSetupValidate()
        {
            // Always enabled
            return true;
        }

        /// <summary>
        /// Find a TilePreset by name keywords
        /// </summary>
        private static TilePreset FindTilePreset(params string[] keywords)
        {
            string[] guids = AssetDatabase.FindAssets("t:TilePreset");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
                
                foreach (string keyword in keywords)
                {
                    if (fileName.Contains(keyword.ToLower()))
                    {
                        TilePreset preset = AssetDatabase.LoadAssetAtPath<TilePreset>(path);
                        if (preset != null)
                        {
                            Debug.Log($"[ImageToMap] Found TilePreset for '{keyword}': {path}");
                            return preset;
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Re-assign TilePresets to existing ColorPalette
        /// </summary>
        [MenuItem("Tools/TileWorldCreator/Image To Map - Refresh TilePresets", false, 101)]
        public static void RefreshTilePresets()
        {
            string palettePath = "Assets/ImageToMap/DefaultColorPalette.asset";
            ColorPalette palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(palettePath);
            
            if (palette == null)
            {
                EditorUtility.DisplayDialog("Error", "DefaultColorPalette not found. Run Quick Setup first.", "OK");
                return;
            }

            // Find and assign presets
            TilePreset waterPreset = FindTilePreset("Water", "Blue", "River");
            TilePreset sandPreset = FindTilePreset("Sand");
            TilePreset grassPreset = FindTilePreset("Grass");
            TilePreset rockPreset = FindTilePreset("Cliff", "Rock", "Stone");
            TilePreset snowPreset = FindTilePreset("Snow", "Ice");
            
            TilePreset fallbackPreset = grassPreset ?? sandPreset ?? waterPreset;

            foreach (var mapping in palette.mappings)
            {
                string nameLower = mapping.name.ToLower();
                
                if (nameLower.Contains("water")) mapping.tilePreset = waterPreset ?? fallbackPreset;
                else if (nameLower.Contains("sand")) mapping.tilePreset = sandPreset ?? fallbackPreset;
                else if (nameLower.Contains("grass")) mapping.tilePreset = grassPreset ?? fallbackPreset;
                else if (nameLower.Contains("rock")) mapping.tilePreset = rockPreset ?? fallbackPreset;
                else if (nameLower.Contains("snow")) mapping.tilePreset = snowPreset ?? fallbackPreset;
                else if (mapping.tilePreset == null) mapping.tilePreset = fallbackPreset;
            }

            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();

            int count = 0;
            foreach (var m in palette.mappings) if (m.tilePreset != null) count++;
            
            EditorUtility.DisplayDialog(
                "TilePresets Refreshed",
                $"Assigned {count} TilePresets to ColorPalette mappings.",
                "OK"
            );
        }
    }
}
