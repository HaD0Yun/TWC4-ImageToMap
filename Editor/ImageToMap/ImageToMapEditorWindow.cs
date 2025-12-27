/*
 * ImageToMapEditorWindow.cs
 * 
 * Main Unity Editor Window for the Image to Map Generator tool.
 * Provides a user-friendly interface for converting images into TileWorldCreator 4 maps.
 * 
 * Features:
 * - Drag & drop image support with preview
 * - Real-time image analysis visualization (color clusters, height levels)
 * - Configuration management and quick settings
 * - Map generation with progress feedback
 * - TWC4 manager selection and validation
 */

using UnityEngine;
using UnityEditor;
using GiantGrey.TileWorldCreator;
using System.Collections.Generic;

namespace ImageToMap
{
    /// <summary>
    /// Unity Editor Window for converting reference images into TileWorldCreator 4 maps.
    /// Provides complete workflow from image import through analysis to map generation.
    /// </summary>
    public class ImageToMapEditorWindow : EditorWindow
    {
        #region Menu Item

        [MenuItem("Tools/TileWorldCreator/Image To Map Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<ImageToMapEditorWindow>("Image To Map");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }

        #endregion

        #region Fields

        // Source image
        private Texture2D sourceImage;
        private Texture2D preprocessedImage;  // Cached preprocessed image for generation

        // Configuration
        private ImageToMapConfig config;
        private ColorPalette colorPalette;

        // Analysis
        private ImageAnalyzer analyzer;
        private ImageAnalyzer.AnalysisResult analysisResult;
        private bool isAnalyzing;

        // Generation
        private ImageToMapGenerator generator;
        private TileWorldCreatorManager targetManager;
        private bool isGenerating;
        private float generationProgress;
        private string generationStatus;

        // UI State
        private Vector2 scrollPosition;
        private bool showAnalysisPreview = true;
        private bool showSettingsPanel = true;
        private bool showAdvancedSettings = false;
        private bool showColorClusters = true;
        private bool showHeightLevels = true;

        // Quick Settings (overrides config when no config is assigned)
        private int quickColorClusters = 5;
        private int quickHeightLevels = 8; // Increased from 4 for better terrain detail
        private float quickEdgeThreshold = 0.3f;
        private bool quickUseEdgeDetection = false;
        
        // Preprocessing settings for better height fidelity
        private bool usePreprocessing = true;  // Enable by default for better results
        private int blurRadius = 2;  // Gaussian blur radius (0 = no blur)
        private bool normalizeContrast = true;  // Normalize to full 0-1 range

        // Generation Mode
        private enum GenerationMode
        {
            HeightBased,
            ColorBased
        }
        private GenerationMode generationMode = GenerationMode.HeightBased;

        // Styles (cached for performance)
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle foldoutStyle;
        private bool stylesInitialized;

        #endregion

        #region Unity Callbacks

        private void OnEnable()
        {
            analyzer = new ImageAnalyzer();
            generator = new ImageToMapGenerator();
            
            // Enable drag and drop support
            wantsMouseMove = true;
            
            // Try to find existing manager in scene
            if (targetManager == null)
            {
                targetManager = FindObjectOfType<TileWorldCreatorManager>();
            }
            
            // Auto-load default ColorPalette if not set
            if (colorPalette == null)
            {
                colorPalette = AssetDatabase.LoadAssetAtPath<ColorPalette>("Assets/ImageToMap/DefaultColorPalette.asset");
                if (colorPalette != null)
                {
                    Debug.Log("[ImageToMapEditorWindow] Auto-loaded DefaultColorPalette");
                }
            }
            
            // Auto-load default Config if not set
            if (config == null)
            {
                config = AssetDatabase.LoadAssetAtPath<ImageToMapConfig>("Assets/ImageToMap/DefaultImageToMapConfig.asset");
                if (config != null)
                {
                    Debug.Log("[ImageToMapEditorWindow] Auto-loaded DefaultImageToMapConfig");
                    // Sync quick settings from config
                    quickColorClusters = config.colorClusterCount;
                    quickHeightLevels = config.heightLevelCount;
                    quickEdgeThreshold = config.edgeThreshold;
                    quickUseEdgeDetection = config.useEdgeDetection;
                    if (config.colorPalette != null)
                    {
                        colorPalette = config.colorPalette;
                    }
                }
            }
        }

        private void OnDisable()
        {
            // Cleanup
            if (analysisResult != null && analysisResult.edgeMap != null)
            {
                // Don't destroy if it's an asset
                if (!AssetDatabase.Contains(analysisResult.edgeMap))
                {
                    DestroyImmediate(analysisResult.edgeMap);
                }
            }
            
            // Clean up preprocessed image
            if (preprocessedImage != null && preprocessedImage != sourceImage)
            {
                DestroyImmediate(preprocessedImage);
                preprocessedImage = null;
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawImageDropZone();
            EditorGUILayout.Space(10);

            DrawSettingsPanel();
            EditorGUILayout.Space(10);

            DrawAnalysisPreview();
            EditorGUILayout.Space(10);

            DrawGenerationPanel();
            EditorGUILayout.Space(10);

            DrawGenerateButton();

            EditorGUILayout.EndScrollView();

            // Handle repaint during analysis/generation
            if (isAnalyzing || isGenerating)
            {
                Repaint();
            }
        }

        #endregion

        #region Style Initialization

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };

            stylesInitialized = true;
        }

        #endregion

        #region Header

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.LabelField("Image To Map Generator", headerStyle);
            EditorGUILayout.LabelField("Convert reference images into TileWorldCreator 4 maps", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Image Drop Zone

        private void DrawImageDropZone()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.LabelField("Reference Image", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 150, GUILayout.ExpandWidth(true));
            
            // Draw background
            Color bgColor = EditorGUIUtility.isProSkin 
                ? new Color(0.2f, 0.2f, 0.2f, 1f) 
                : new Color(0.8f, 0.8f, 0.8f, 1f);
            EditorGUI.DrawRect(dropArea, bgColor);
            
            // Draw border
            Handles.BeginGUI();
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            Handles.DrawSolidRectangleWithOutline(
                dropArea, 
                Color.clear, 
                Handles.color
            );
            Handles.EndGUI();

            if (sourceImage != null)
            {
                // Draw texture preview
                Rect texRect = new Rect(
                    dropArea.x + 5, 
                    dropArea.y + 5, 
                    dropArea.width - 10, 
                    dropArea.height - 10
                );
                GUI.DrawTexture(texRect, sourceImage, ScaleMode.ScaleToFit);

                // Draw image info overlay
                string imageInfo = $"{sourceImage.width} x {sourceImage.height}";
                Rect infoRect = new Rect(dropArea.x + 5, dropArea.y + dropArea.height - 20, dropArea.width - 10, 20);
                EditorGUI.DropShadowLabel(infoRect, imageInfo, EditorStyles.miniLabel);
            }
            else
            {
                // Draw placeholder text
                GUIStyle placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12,
                    wordWrap = true
                };
                GUI.Label(dropArea, "Drop Image Here\n(PNG, JPG, TGA)", placeholderStyle);
            }

            // Handle drag and drop
            HandleDragAndDrop(dropArea);

            EditorGUILayout.Space(5);

            // Object field as alternative
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Or Select:", GUILayout.Width(70));
            Texture2D newImage = (Texture2D)EditorGUILayout.ObjectField(
                sourceImage, 
                typeof(Texture2D), 
                false
            );
            EditorGUILayout.EndHorizontal();

            // Handle image change
            if (newImage != sourceImage)
            {
                if (newImage != null)
                {
                    // Try to make texture readable
                    if (TryMakeTextureReadable(newImage))
                    {
                        sourceImage = newImage;
                        AnalyzeImage();
                    }
                }
                else
                {
                    sourceImage = null;
                    analysisResult = null;
                }
            }

            // Clear button
            if (sourceImage != null)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Clear Image"))
                {
                    sourceImage = null;
                    analysisResult = null;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (evt.GetTypeForControl(controlID))
            {
                case EventType.DragExited:
                    HandleUtility.Repaint();
                    break;

                case EventType.DragUpdated:
                case EventType.DragPerform:
                    // Check if mouse is over the drop area
                    if (!dropArea.Contains(evt.mousePosition))
                    {
                        return;
                    }

                    // Check if any dragged object is a Texture2D
                    bool hasValidTexture = false;
                    Texture2D draggedTexture = null;
                    
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        foreach (Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Texture2D tex)
                            {
                                hasValidTexture = true;
                                draggedTexture = tex;
                                break;
                            }
                        }
                    }

                    if (hasValidTexture)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            GUI.changed = true;

                            if (draggedTexture != null)
                            {
                                // Try to make texture readable (auto-enable if user agrees)
                                if (TryMakeTextureReadable(draggedTexture))
                                {
                                    sourceImage = draggedTexture;
                                    AnalyzeImage();
                                }
                            }

                            GUIUtility.ExitGUI();
                        }
                        
                        evt.Use();
                    }
                    else
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    }
                    break;
            }
        }

        private bool IsTextureReadable(Texture2D texture)
        {
            try
            {
                texture.GetPixel(0, 0);
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        /// <summary>
        /// Automatically enables Read/Write on a texture if needed
        /// </summary>
        private bool TryMakeTextureReadable(Texture2D texture)
        {
            if (IsTextureReadable(texture))
                return true;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return false;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return false;

            // Ask user permission
            bool shouldEnable = EditorUtility.DisplayDialog(
                "Enable Read/Write",
                $"The texture '{texture.name}' needs Read/Write enabled to be analyzed.\n\nDo you want to enable it automatically?",
                "Yes, Enable",
                "Cancel"
            );

            if (!shouldEnable)
                return false;

            // Enable Read/Write
            importer.isReadable = true;
            importer.SaveAndReimport();

            Debug.Log($"[ImageToMap] Enabled Read/Write on texture: {texture.name}");
            return true;
        }

        #endregion

        #region Settings Panel

        private void DrawSettingsPanel()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            showSettingsPanel = EditorGUILayout.Foldout(showSettingsPanel, "Settings", true, foldoutStyle);

            if (showSettingsPanel)
            {
                EditorGUI.indentLevel++;

                // Config asset field
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
                
                ImageToMapConfig newConfig = (ImageToMapConfig)EditorGUILayout.ObjectField(
                    "Config Asset",
                    config,
                    typeof(ImageToMapConfig),
                    false
                );

                if (newConfig != config)
                {
                    config = newConfig;
                    if (config != null)
                    {
                        // Copy settings from config
                        quickColorClusters = config.colorClusterCount;
                        quickHeightLevels = config.heightLevelCount;
                        quickEdgeThreshold = config.edgeThreshold;
                        quickUseEdgeDetection = config.useEdgeDetection;
                        colorPalette = config.colorPalette;
                    }
                    // Re-analyze if image is loaded
                    if (sourceImage != null)
                    {
                        AnalyzeImage();
                    }
                }

                // Create config button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create New Config", GUILayout.Width(150)))
                {
                    CreateNewConfig();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                // Quick settings (used when no config is assigned)
                EditorGUILayout.LabelField("Quick Settings", EditorStyles.boldLabel);
                
                if (config == null)
                {
                    EditorGUILayout.HelpBox(
                        "No config assigned. Using quick settings below.", 
                        MessageType.Info
                    );
                }

                EditorGUI.BeginDisabledGroup(config != null);

                int newColorClusters = EditorGUILayout.IntSlider("Color Clusters", quickColorClusters, 2, 10);
                int newHeightLevels = EditorGUILayout.IntSlider("Height Levels", quickHeightLevels, 2, 16); // Increased max from 8 to 16
                bool newUseEdgeDetection = EditorGUILayout.Toggle("Use Edge Detection", quickUseEdgeDetection);
                
                if (quickUseEdgeDetection || newUseEdgeDetection)
                {
                    float newEdgeThreshold = EditorGUILayout.Slider("Edge Threshold", quickEdgeThreshold, 0.1f, 0.9f);
                    if (Mathf.Abs(newEdgeThreshold - quickEdgeThreshold) > 0.01f)
                    {
                        quickEdgeThreshold = newEdgeThreshold;
                    }
                }

                // Check for changes and re-analyze
                bool settingsChanged = newColorClusters != quickColorClusters || 
                                       newHeightLevels != quickHeightLevels ||
                                       newUseEdgeDetection != quickUseEdgeDetection;

                if (settingsChanged)
                {
                    quickColorClusters = newColorClusters;
                    quickHeightLevels = newHeightLevels;
                    quickUseEdgeDetection = newUseEdgeDetection;

                    if (sourceImage != null && !isAnalyzing)
                    {
                        AnalyzeImage();
                    }
                }

                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(10);
                
                // Preprocessing settings (for better height fidelity)
                EditorGUILayout.LabelField("Image Preprocessing", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Preprocessing helps the generated map better match the source image by reducing noise and maximizing height differentiation.",
                    MessageType.Info
                );
                
                bool newUsePreprocessing = EditorGUILayout.Toggle("Enable Preprocessing", usePreprocessing);
                
                if (usePreprocessing)
                {
                    EditorGUI.indentLevel++;
                    int newBlurRadius = EditorGUILayout.IntSlider("Blur Radius", blurRadius, 0, 5);
                    bool newNormalizeContrast = EditorGUILayout.Toggle("Normalize Contrast", normalizeContrast);
                    EditorGUI.indentLevel--;
                    
                    if (newBlurRadius != blurRadius || newNormalizeContrast != normalizeContrast)
                    {
                        blurRadius = newBlurRadius;
                        normalizeContrast = newNormalizeContrast;
                        if (sourceImage != null && !isAnalyzing)
                        {
                            AnalyzeImage();
                        }
                    }
                }
                
                if (newUsePreprocessing != usePreprocessing)
                {
                    usePreprocessing = newUsePreprocessing;
                    if (sourceImage != null && !isAnalyzing)
                    {
                        AnalyzeImage();
                    }
                }

                EditorGUILayout.Space(5);

                // Color palette
                EditorGUILayout.LabelField("Tile Mapping", EditorStyles.boldLabel);
                ColorPalette newPalette = (ColorPalette)EditorGUILayout.ObjectField(
                    "Color Palette",
                    colorPalette,
                    typeof(ColorPalette),
                    false
                );

                if (newPalette != colorPalette)
                {
                    colorPalette = newPalette;
                }

                // Create palette button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create New Palette", GUILayout.Width(150)))
                {
                    CreateNewPalette();
                }
                EditorGUILayout.EndHorizontal();

                // Validation warnings
                if (colorPalette != null)
                {
                    var warnings = colorPalette.Validate();
                    if (warnings.Count > 0)
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.HelpBox(
                            $"Palette has {warnings.Count} warning(s). Check console for details.",
                            MessageType.Warning
                        );
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateNewConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Image To Map Config",
                "NewImageToMapConfig",
                "asset",
                "Choose a location to save the config asset"
            );

            if (string.IsNullOrEmpty(path)) return;

            ImageToMapConfig newConfig = CreateInstance<ImageToMapConfig>();
            AssetDatabase.CreateAsset(newConfig, path);
            AssetDatabase.SaveAssets();
            
            config = newConfig;
            EditorGUIUtility.PingObject(newConfig);
        }

        private void CreateNewPalette()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Color Palette",
                "NewColorPalette",
                "asset",
                "Choose a location to save the palette asset"
            );

            if (string.IsNullOrEmpty(path)) return;

            ColorPalette newPalette = CreateInstance<ColorPalette>();
            
            // Add default mappings
            newPalette.mappings = new List<ColorPalette.TileMapping>
            {
                new ColorPalette.TileMapping { name = "Water", targetColor = new Color(0f, 0f, 1f), colorTolerance = 0.3f },
                new ColorPalette.TileMapping { name = "Beach", targetColor = new Color(0.76f, 0.7f, 0.5f), colorTolerance = 0.2f },
                new ColorPalette.TileMapping { name = "Grass", targetColor = new Color(0.2f, 0.6f, 0.2f), colorTolerance = 0.3f },
                new ColorPalette.TileMapping { name = "Mountain", targetColor = new Color(0.5f, 0.5f, 0.5f), colorTolerance = 0.3f }
            };
            
            AssetDatabase.CreateAsset(newPalette, path);
            AssetDatabase.SaveAssets();
            
            colorPalette = newPalette;
            EditorGUIUtility.PingObject(newPalette);
        }

        #endregion

        #region Analysis Preview

        private void DrawAnalysisPreview()
        {
            if (analysisResult == null) return;

            EditorGUILayout.BeginVertical(boxStyle);

            showAnalysisPreview = EditorGUILayout.Foldout(showAnalysisPreview, "Analysis Preview", true, foldoutStyle);

            if (showAnalysisPreview)
            {
                EditorGUI.indentLevel++;

                // Analysis info
                EditorGUILayout.LabelField(
                    $"Analyzed: {analysisResult.width} x {analysisResult.height}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"Analysis Time: {analysisResult.analysisTime:HH:mm:ss}",
                    EditorStyles.miniLabel
                );

                EditorGUILayout.Space(10);

                // Color Clusters Section
                DrawColorClustersSection();

                EditorGUILayout.Space(10);

                // Height Levels Section
                DrawHeightLevelsSection();

                // Edge Map Preview
                if (analysisResult.edgeMap != null)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Edge Detection", EditorStyles.boldLabel);
                    
                    Rect edgeRect = GUILayoutUtility.GetRect(100, 80, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(edgeRect, analysisResult.edgeMap, ScaleMode.ScaleToFit);
                }

                // Re-analyze button
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Re-Analyze Image"))
                {
                    AnalyzeImage();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawColorClustersSection()
        {
            if (analysisResult.colorClusters == null || analysisResult.colorClusters.Count == 0)
                return;

            showColorClusters = EditorGUILayout.Foldout(
                showColorClusters, 
                $"Color Clusters ({analysisResult.colorClusters.Count})", 
                true
            );

            if (!showColorClusters) return;

            EditorGUI.indentLevel++;

            foreach (var cluster in analysisResult.colorClusters)
            {
                EditorGUILayout.BeginHorizontal();

                // Color swatch
                Rect colorRect = GUILayoutUtility.GetRect(30, 18, GUILayout.Width(30));
                EditorGUI.DrawRect(colorRect, cluster.centroid);
                
                // Border around swatch
                Handles.BeginGUI();
                Handles.color = Color.black;
                Handles.DrawSolidRectangleWithOutline(colorRect, Color.clear, Handles.color);
                Handles.EndGUI();

                // Cluster info
                string colorHex = ColorUtility.ToHtmlStringRGB(cluster.centroid);
                EditorGUILayout.LabelField(
                    $"{cluster.name} - #{colorHex} ({cluster.coverage:P1})",
                    GUILayout.ExpandWidth(true)
                );

                // Show matched preset if palette is assigned
                if (colorPalette != null)
                {
                    var mapping = colorPalette.FindBestMatch(cluster.centroid);
                    if (mapping != null && mapping.tilePreset != null)
                    {
                        EditorGUILayout.LabelField(
                            $"→ {mapping.tilePreset.name}",
                            EditorStyles.miniLabel,
                            GUILayout.Width(100)
                        );
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawHeightLevelsSection()
        {
            if (analysisResult.heightLevels == null || analysisResult.heightLevels.Count == 0)
                return;

            showHeightLevels = EditorGUILayout.Foldout(
                showHeightLevels, 
                $"Height Levels ({analysisResult.heightLevels.Count})", 
                true
            );

            if (!showHeightLevels) return;

            EditorGUI.indentLevel++;

            for (int i = 0; i < analysisResult.heightLevels.Count; i++)
            {
                var level = analysisResult.heightLevels[i];

                EditorGUILayout.BeginHorizontal();

                // Height bar (progress bar style)
                Rect barRect = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
                
                // Calculate fill based on height range
                float fillAmount = (level.minHeight + level.maxHeight) / 2f;
                Color barColor = Color.Lerp(
                    new Color(0.2f, 0.3f, 0.8f),  // Blue (low)
                    new Color(0.9f, 0.9f, 0.9f),  // White (high)
                    fillAmount
                );

                // Draw background
                EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f, 1f));

                // Draw filled portion
                Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * fillAmount, barRect.height);
                EditorGUI.DrawRect(fillRect, barColor);

                // Draw label
                string label = $"{level.name} ({level.minHeight:F2} - {level.maxHeight:F2})";
                GUI.Label(barRect, label, EditorStyles.centeredGreyMiniLabel);

                // Coverage info
                if (level.positions != null)
                {
                    float coverage = (float)level.positions.Count / (analysisResult.width * analysisResult.height);
                    EditorGUILayout.LabelField($"{coverage:P0}", GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        #endregion

        #region Generation Panel

        private void DrawGenerationPanel()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Target manager
            TileWorldCreatorManager newManager = (TileWorldCreatorManager)EditorGUILayout.ObjectField(
                "Target Manager",
                targetManager,
                typeof(TileWorldCreatorManager),
                true
            );

            if (newManager != targetManager)
            {
                targetManager = newManager;
            }

            // Find manager button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Find in Scene", GUILayout.Width(100)))
            {
                targetManager = FindObjectOfType<TileWorldCreatorManager>();
                if (targetManager == null)
                {
                    EditorUtility.DisplayDialog(
                        "Manager Not Found",
                        "No TileWorldCreatorManager found in the scene. Please add one to your scene first.",
                        "OK"
                    );
                }
            }
            EditorGUILayout.EndHorizontal();

            // Manager validation
            if (targetManager != null)
            {
                if (targetManager.configuration == null)
                {
                    EditorGUILayout.HelpBox(
                        "The selected manager has no configuration assigned. Please assign a TWC4 configuration first.",
                        MessageType.Warning
                    );
                }
            }

            EditorGUILayout.Space(10);

            // Generation mode
            EditorGUILayout.LabelField("Generation Mode", EditorStyles.boldLabel);
            generationMode = (GenerationMode)EditorGUILayout.EnumPopup("Mode", generationMode);

            EditorGUILayout.HelpBox(
                generationMode == GenerationMode.HeightBased
                    ? "Height-Based: Creates layers based on grayscale height values. Best for heightmaps and grayscale images."
                    : "Color-Based: Creates layers based on color clusters. Best for colored reference images. Requires a Color Palette.",
                MessageType.Info
            );

            // Color-based mode requires palette
            if (generationMode == GenerationMode.ColorBased && colorPalette == null)
            {
                EditorGUILayout.HelpBox(
                    "Color-Based mode requires a Color Palette to be assigned in the Settings panel.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.Space(5);

            // Advanced settings
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Options", true);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Existing Layers"))
                {
                    if (targetManager != null && targetManager.configuration != null)
                    {
                        if (EditorUtility.DisplayDialog(
                            "Clear Layers",
                            "This will remove all existing blueprint and build layers from the configuration. This action can be undone.",
                            "Clear",
                            "Cancel"))
                        {
                            generator.ClearExistingLayers(targetManager);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Generate Button

        private void DrawGenerateButton()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            // Validation checks
            bool canGenerate = true;
            string validationMessage = "";

            if (sourceImage == null)
            {
                canGenerate = false;
                validationMessage = "Please select a source image.";
            }
            else if (analysisResult == null)
            {
                canGenerate = false;
                validationMessage = "Please wait for analysis to complete.";
            }
            else if (targetManager == null)
            {
                canGenerate = false;
                validationMessage = "Please select a target TileWorldCreator Manager.";
            }
            else if (targetManager.configuration == null)
            {
                canGenerate = false;
                validationMessage = "The target manager has no configuration assigned.";
            }
            else if (generationMode == GenerationMode.ColorBased && colorPalette == null)
            {
                canGenerate = false;
                validationMessage = "Color-Based mode requires a Color Palette.";
            }
            else if (isGenerating)
            {
                canGenerate = false;
                validationMessage = "Generation in progress...";
            }
            else if (isAnalyzing)
            {
                canGenerate = false;
                validationMessage = "Analysis in progress...";
            }

            if (!canGenerate && !string.IsNullOrEmpty(validationMessage))
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Info);
            }

            // Progress bar during generation
            if (isGenerating)
            {
                Rect progressRect = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progressRect, generationProgress, generationStatus);
            }

            // Generate button
            EditorGUI.BeginDisabledGroup(!canGenerate);
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };

            if (GUILayout.Button("Generate Map", buttonStyle))
            {
                GenerateMap();
            }

            EditorGUI.EndDisabledGroup();

            // Execute layers button (if layers already exist)
            if (targetManager != null && targetManager.configuration != null)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Execute All Layers"))
                {
                    generator.ExecuteLayers(targetManager);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Image Analysis

        private void AnalyzeImage()
        {
            if (sourceImage == null) return;
            if (isAnalyzing) return;

            isAnalyzing = true;

            try
            {
                // Clean up previous preprocessed image
                if (preprocessedImage != null && preprocessedImage != sourceImage)
                {
                    DestroyImmediate(preprocessedImage);
                    preprocessedImage = null;
                }
                
                // Get analysis parameters
                int colorClusters = config != null ? config.colorClusterCount : quickColorClusters;
                int heightLevels = config != null ? config.heightLevelCount : quickHeightLevels;
                float edgeThreshold = config != null ? config.edgeThreshold : quickEdgeThreshold;

                // Apply preprocessing if enabled (reduces noise, improves height fidelity)
                Texture2D imageToAnalyze = sourceImage;
                if (usePreprocessing)
                {
                    EditorUtility.DisplayProgressBar("Analyzing Image", "Preprocessing image...", 0.1f);
                    preprocessedImage = analyzer.PreprocessForHeightMap(sourceImage, blurRadius, normalizeContrast);
                    if (preprocessedImage != null)
                    {
                        imageToAnalyze = preprocessedImage;
                        Debug.Log($"[ImageToMapEditorWindow] Applied preprocessing (blur={blurRadius}, normalizeContrast={normalizeContrast})");
                    }
                }
                else
                {
                    preprocessedImage = null;  // No preprocessing
                }

                // Perform analysis
                // IMPORTANT: useAdaptiveHeights=false ensures height levels correspond to actual grayscale values
                // This is critical for proper height representation - adaptive levels break the correlation
                // between grayscale brightness and tile elevation
                EditorUtility.DisplayProgressBar("Analyzing Image", "Running K-Means clustering...", 0.3f);
                
                analysisResult = analyzer.AnalyzeImage(
                    imageToAnalyze,
                    colorClusters,
                    heightLevels,
                    edgeThreshold,
                    useAdaptiveHeights: false  // Use fixed height ranges for proper height correlation
                );
                
                // Store the texture to use for generation (preprocessed if available)
                analysisResult.sourceTexture = imageToAnalyze;

                EditorUtility.DisplayProgressBar("Analyzing Image", "Analysis complete!", 1f);
                
                Debug.Log($"[ImageToMapEditorWindow] Analysis complete. Found {analysisResult.colorClusters.Count} color clusters, {analysisResult.heightLevels.Count} height levels.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ImageToMapEditorWindow] Analysis failed: {ex.Message}\n{ex.StackTrace}");
                analysisResult = null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                isAnalyzing = false;
                Repaint();
            }
        }

        #endregion

        #region Map Generation

        private void GenerateMap()
        {
            if (targetManager == null || targetManager.configuration == null) return;
            if (analysisResult == null) return;
            if (isGenerating) return;

            isGenerating = true;
            generationProgress = 0f;
            generationStatus = "Initializing...";

            try
            {
                // ===== FIX 1: Auto-adjust TWC4 Configuration size to match image =====
                // NOTE: Large maps (512+) can cause very slow execution. Start with 128 for testing.
                const int MAX_MAP_SIZE = 128; // Reduced from 512 for faster execution
                
                if (sourceImage != null)
                {
                    var twcConfig = targetManager.configuration;
                    
                    // Calculate appropriate map size (cap at MAX_MAP_SIZE for performance)
                    int targetWidth = Mathf.Min(sourceImage.width, MAX_MAP_SIZE);
                    int targetHeight = Mathf.Min(sourceImage.height, MAX_MAP_SIZE);
                    
                    // Maintain aspect ratio if image is larger
                    if (sourceImage.width > MAX_MAP_SIZE || sourceImage.height > MAX_MAP_SIZE)
                    {
                        float aspect = (float)sourceImage.width / sourceImage.height;
                        if (aspect > 1f)
                        {
                            targetWidth = MAX_MAP_SIZE;
                            targetHeight = Mathf.RoundToInt(MAX_MAP_SIZE / aspect);
                        }
                        else
                        {
                            targetHeight = MAX_MAP_SIZE;
                            targetWidth = Mathf.RoundToInt(MAX_MAP_SIZE * aspect);
                        }
                    }
                    
                    // Set configuration size
                    twcConfig.width = targetWidth;
                    twcConfig.height = targetHeight;
                    
                    EditorUtility.SetDirty(twcConfig);
                    Debug.Log($"[ImageToMapEditorWindow] Set TWC4 Configuration size to {targetWidth}x{targetHeight} (image: {sourceImage.width}x{sourceImage.height})");
                }
                
                // Ensure colorPalette is loaded
                if (colorPalette == null)
                {
                    colorPalette = AssetDatabase.LoadAssetAtPath<ColorPalette>("Assets/ImageToMap/DefaultColorPalette.asset");
                    Debug.Log($"[ImageToMapEditorWindow] Loaded ColorPalette: {(colorPalette != null ? colorPalette.name : "NULL")}");
                }
                
                if (colorPalette == null)
                {
                    Debug.LogWarning("[ImageToMapEditorWindow] No ColorPalette assigned! Build layers won't be created.");
                }
                else
                {
                    int presetCount = 0;
                    foreach (var m in colorPalette.mappings)
                        if (m.tilePreset != null) presetCount++;
                    Debug.Log($"[ImageToMapEditorWindow] Using ColorPalette '{colorPalette.name}' with {presetCount} TilePresets");
                }

                ImageToMapGenerator.GenerationResult result;
                
                // Use the texture that was analyzed (preprocessed if enabled)
                // This ensures HeightTexture uses the same data as the analysis
                Texture2D textureForGeneration = analysisResult.sourceTexture ?? sourceImage;

                if (generationMode == GenerationMode.HeightBased)
                {
                    generationStatus = "Generating from height levels...";
                    generationProgress = 0.3f;
                    Repaint();

                    result = generator.GenerateFromHeightLevels(
                        targetManager,
                        analysisResult.heightLevels,
                        textureForGeneration,  // Use preprocessed texture for consistent results
                        colorPalette
                    );
                }
                else
                {
                    generationStatus = "Generating from color clusters...";
                    generationProgress = 0.3f;
                    Repaint();

                    result = generator.GenerateFromColorClusters(
                        targetManager,
                        analysisResult.colorClusters,
                        textureForGeneration,  // Use preprocessed texture for consistent results
                        colorPalette
                    );
                }

                generationProgress = 0.7f;
                generationStatus = "Executing layers...";
                Repaint();

                // Execute the generated layers
                if (result.success && result.totalLayersCreated > 0)
                {
                    generator.ExecuteLayers(targetManager);
                }

                generationProgress = 1f;
                generationStatus = "Complete!";
                Repaint();

                // Show result dialog
                if (result.success)
                {
                    EditorUtility.DisplayDialog(
                        "Generation Complete",
                        result.message,
                        "OK"
                    );
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Generation Failed",
                        result.message,
                        "OK"
                    );
                }

                // Refresh scene view
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ImageToMapEditorWindow] Generation failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Generation Error",
                    $"An error occurred during generation:\n{ex.Message}",
                    "OK"
                );
            }
            finally
            {
                isGenerating = false;
                generationProgress = 0f;
                generationStatus = "";
                Repaint();
            }
        }

        #endregion
    }
}
