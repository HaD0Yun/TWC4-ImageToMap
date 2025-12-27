/*
 * ImageAnalyzer.cs
 * Core image analysis class for extracting map data from images
 * 
 * Features:
 * - K-Means color clustering for terrain type identification
 * - Height level extraction from grayscale values
 * - Edge detection using Sobel filter for boundary detection
 */

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ImageToMap
{
    /// <summary>
    /// Analyzes images to extract map-relevant data including color clusters,
    /// height levels, and edge information for TileWorldCreator integration.
    /// </summary>
    public class ImageAnalyzer
    {
        #region Constants
        
        private const int DEFAULT_KMEANS_MAX_ITERATIONS = 100;
        private const float KMEANS_CONVERGENCE_THRESHOLD = 0.001f;
        
        #endregion

        #region Data Structures

        /// <summary>
        /// Represents a cluster of similar colors found via K-Means clustering.
        /// </summary>
        public struct ColorCluster
        {
            /// <summary>Centroid color representing this cluster</summary>
            public Color centroid;
            
            /// <summary>List of pixel positions belonging to this cluster</summary>
            public List<Vector2Int> pixels;
            
            /// <summary>Percentage of image covered by this cluster (0-1)</summary>
            public float coverage;
            
            /// <summary>Optional name for this cluster (e.g., "Water", "Grass")</summary>
            public string name;

            public ColorCluster(Color centroid)
            {
                this.centroid = centroid;
                this.pixels = new List<Vector2Int>();
                this.coverage = 0f;
                this.name = string.Empty;
            }
        }

        /// <summary>
        /// Represents a height level range extracted from grayscale values.
        /// </summary>
        public struct HeightLevel
        {
            /// <summary>Minimum grayscale value for this level (0-1)</summary>
            public float minHeight;
            
            /// <summary>Maximum grayscale value for this level (0-1)</summary>
            public float maxHeight;
            
            /// <summary>Descriptive name for this height level</summary>
            public string name;
            
            /// <summary>Set of pixel positions at this height level</summary>
            public HashSet<Vector2> positions;

            public HeightLevel(float min, float max, string name)
            {
                this.minHeight = min;
                this.maxHeight = max;
                this.name = name;
                this.positions = new HashSet<Vector2>();
            }
        }

        /// <summary>
        /// Combined result of all image analysis operations.
        /// </summary>
        public class AnalysisResult
        {
            /// <summary>Color clusters found via K-Means</summary>
            public List<ColorCluster> colorClusters;
            
            /// <summary>Height levels extracted from grayscale</summary>
            public List<HeightLevel> heightLevels;
            
            /// <summary>Edge detection result texture</summary>
            public Texture2D edgeMap;
            
            /// <summary>Original image width</summary>
            public int width;
            
            /// <summary>Original image height</summary>
            public int height;
            
            /// <summary>Source texture reference</summary>
            public Texture2D sourceTexture;
            
            /// <summary>Analysis timestamp</summary>
            public System.DateTime analysisTime;

            public AnalysisResult()
            {
                colorClusters = new List<ColorCluster>();
                heightLevels = new List<HeightLevel>();
                analysisTime = System.DateTime.Now;
            }
        }

        #endregion

        #region K-Means Color Clustering

        /// <summary>
        /// Analyzes the image using K-Means clustering to identify dominant color groups.
        /// </summary>
        /// <param name="texture">Source texture to analyze</param>
        /// <param name="numClusters">Number of color clusters to find</param>
        /// <param name="maxIterations">Maximum iterations for convergence</param>
        /// <param name="seed">Optional random seed for reproducible results</param>
        /// <returns>List of color clusters sorted by coverage (descending)</returns>
        public List<ColorCluster> AnalyzeColors(Texture2D texture, int numClusters, int maxIterations = DEFAULT_KMEANS_MAX_ITERATIONS, int? seed = null)
        {
            if (texture == null)
            {
                LogError("[ImageAnalyzer] AnalyzeColors: Texture is null");
                return new List<ColorCluster>();
            }

            if (numClusters <= 0)
            {
                LogError("[ImageAnalyzer] AnalyzeColors: numClusters must be positive");
                return new List<ColorCluster>();
            }

            int width = texture.width;
            int height = texture.height;
            
            // Use GetPixels32 for better memory efficiency (4 bytes vs 16 bytes per pixel)
            Color32[] pixels32 = texture.GetPixels32();
            Color[] pixels = new Color[pixels32.Length];
            for (int i = 0; i < pixels32.Length; i++)
            {
                pixels[i] = pixels32[i];
            }
            int totalPixels = pixels.Length;

            // Initialize centroids using K-Means++ initialization with optional seed
            Color[] centroids = InitializeCentroidsKMeansPlusPlus(pixels, numClusters, seed);
            int[] assignments = new int[totalPixels];

            bool converged = false;
            int iteration = 0;

            while (!converged && iteration < maxIterations)
            {
                // Assignment step: assign each pixel to nearest centroid
                for (int i = 0; i < totalPixels; i++)
                {
                    assignments[i] = FindNearestCentroid(pixels[i], centroids);
                }

                // Update step: recalculate centroids
                Color[] newCentroids = CalculateNewCentroids(pixels, assignments, numClusters);

                // Check convergence
                converged = CheckConvergence(centroids, newCentroids, KMEANS_CONVERGENCE_THRESHOLD);
                centroids = newCentroids;
                iteration++;
            }

            // Build cluster results
            List<ColorCluster> clusters = new List<ColorCluster>();
            for (int c = 0; c < numClusters; c++)
            {
                ColorCluster cluster = new ColorCluster(centroids[c]);
                clusters.Add(cluster);
            }

            // Assign pixels to clusters and calculate coverage
            // FIX: Flip Y-axis to match Unity's bottom-left origin coordinate system
            for (int i = 0; i < totalPixels; i++)
            {
                int x = i % width;
                int y = i / width;
                int flippedY = height - 1 - y;  // Flip Y for Unity coordinate system
                int clusterIdx = assignments[i];
                
                ColorCluster cluster = clusters[clusterIdx];
                cluster.pixels.Add(new Vector2Int(x, flippedY));
                clusters[clusterIdx] = cluster;
            }

            // Calculate coverage percentages
            for (int c = 0; c < numClusters; c++)
            {
                ColorCluster cluster = clusters[c];
                cluster.coverage = (float)cluster.pixels.Count / totalPixels;
                cluster.name = $"Cluster_{c + 1}";
                clusters[c] = cluster;
            }

            // Sort by coverage (largest first)
            clusters.Sort((a, b) => b.coverage.CompareTo(a.coverage));

            LogDebug($"[ImageAnalyzer] K-Means completed in {iteration} iterations, found {numClusters} clusters");
            
            return clusters;
        }

        /// <summary>
        /// K-Means++ initialization for better initial centroid selection.
        /// </summary>
        /// <param name="pixels">Array of pixel colors to cluster</param>
        /// <param name="numClusters">Number of clusters to create</param>
        /// <param name="seed">Optional random seed for reproducible results</param>
        private Color[] InitializeCentroidsKMeansPlusPlus(Color[] pixels, int numClusters, int? seed = null)
        {
            Color[] centroids = new Color[numClusters];
            System.Random rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            // Choose first centroid randomly
            centroids[0] = pixels[rng.Next(pixels.Length)];

            // Pre-allocate distances array OUTSIDE the loop to avoid repeated allocations
            float[] distances = new float[pixels.Length];

            // Choose remaining centroids with probability proportional to distance squared
            for (int c = 1; c < numClusters; c++)
            {
                float totalDistance = 0f;

                for (int i = 0; i < pixels.Length; i++)
                {
                    float minDist = float.MaxValue;
                    for (int j = 0; j < c; j++)
                    {
                        float dist = ColorDistanceSquared(pixels[i], centroids[j]);
                        if (dist < minDist) minDist = dist;
                    }
                    distances[i] = minDist;
                    totalDistance += minDist;
                }

                // Weighted random selection
                float threshold = (float)rng.NextDouble() * totalDistance;
                float cumulative = 0f;
                for (int i = 0; i < pixels.Length; i++)
                {
                    cumulative += distances[i];
                    if (cumulative >= threshold)
                    {
                        centroids[c] = pixels[i];
                        break;
                    }
                }
            }

            return centroids;
        }

        /// <summary>
        /// Finds the index of the nearest centroid to a given color.
        /// </summary>
        private int FindNearestCentroid(Color pixel, Color[] centroids)
        {
            int nearest = 0;
            float minDist = float.MaxValue;

            for (int c = 0; c < centroids.Length; c++)
            {
                float dist = ColorDistanceSquared(pixel, centroids[c]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = c;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Calculates new centroid positions based on current assignments.
        /// </summary>
        private Color[] CalculateNewCentroids(Color[] pixels, int[] assignments, int numClusters)
        {
            Color[] newCentroids = new Color[numClusters];
            int[] counts = new int[numClusters];
            float[] sumR = new float[numClusters];
            float[] sumG = new float[numClusters];
            float[] sumB = new float[numClusters];

            for (int i = 0; i < pixels.Length; i++)
            {
                int c = assignments[i];
                sumR[c] += pixels[i].r;
                sumG[c] += pixels[i].g;
                sumB[c] += pixels[i].b;
                counts[c]++;
            }

            for (int c = 0; c < numClusters; c++)
            {
                if (counts[c] > 0)
                {
                    newCentroids[c] = new Color(
                        sumR[c] / counts[c],
                        sumG[c] / counts[c],
                        sumB[c] / counts[c]
                    );
                }
                else
                {
                    // Keep old centroid if no pixels assigned
                    newCentroids[c] = Color.black;
                }
            }

            return newCentroids;
        }

        /// <summary>
        /// Checks if K-Means has converged (centroids stopped moving).
        /// </summary>
        private bool CheckConvergence(Color[] oldCentroids, Color[] newCentroids, float threshold)
        {
            for (int c = 0; c < oldCentroids.Length; c++)
            {
                float dist = ColorDistanceSquared(oldCentroids[c], newCentroids[c]);
                if (dist > threshold * threshold)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Calculates squared Euclidean distance between two colors in RGB space.
        /// </summary>
        private float ColorDistanceSquared(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        #endregion

        #region Height Level Extraction

        /// <summary>
        /// Extracts height levels from an image by converting to grayscale and dividing into ranges.
        /// </summary>
        /// <param name="texture">Source texture to analyze</param>
        /// <param name="numLevels">Number of height levels to extract</param>
        /// <returns>List of height levels with their positions</returns>
        public List<HeightLevel> ExtractHeightLevels(Texture2D texture, int numLevels)
        {
            if (texture == null)
            {
                LogError("[ImageAnalyzer] ExtractHeightLevels: Texture is null");
                return new List<HeightLevel>();
            }

            if (numLevels <= 0)
            {
                LogError("[ImageAnalyzer] ExtractHeightLevels: numLevels must be positive");
                return new List<HeightLevel>();
            }

            int width = texture.width;
            int height = texture.height;
            
            // Use GetPixels32 for better memory efficiency
            Color32[] pixels32 = texture.GetPixels32();

            // Create height levels with even distribution
            List<HeightLevel> levels = new List<HeightLevel>();
            float levelSize = 1f / numLevels;

            string[] defaultLevelNames = GenerateHeightLevelNames(numLevels);

            for (int i = 0; i < numLevels; i++)
            {
                float minH = i * levelSize;
                float maxH = (i + 1) * levelSize;
                
                // Adjust last level to ensure it captures 1.0
                if (i == numLevels - 1)
                {
                    const float HEIGHT_EPSILON = 0.001f;
                    maxH = 1.0f + HEIGHT_EPSILON; // Slightly over to include 1.0
                }

                HeightLevel level = new HeightLevel(minH, maxH, defaultLevelNames[i]);
                levels.Add(level);
            }

            // Assign pixels to height levels based on grayscale value
            // FIX: Flip Y-axis to match Unity's bottom-left origin coordinate system
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int idx = y * width + x;
                    int flippedY = height - 1 - y;  // Flip Y for Unity coordinate system
                    // Calculate grayscale from Color32 (faster than Color.grayscale)
                    Color32 c = pixels32[idx];
                    float grayscale = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;

                    // Find matching height level
                    for (int i = 0; i < numLevels; i++)
                    {
                        HeightLevel level = levels[i];
                        if (grayscale >= level.minHeight && grayscale < level.maxHeight)
                        {
                            level.positions.Add(new Vector2(x, flippedY));
                            levels[i] = level;
                            break;
                        }
                    }
                }
            }

            int totalPixels = width * height;
            for (int i = 0; i < numLevels; i++)
            {
                HeightLevel level = levels[i];
                float coverage = (float)level.positions.Count / totalPixels * 100f;
                LogDebug($"[ImageAnalyzer] Height level '{level.name}' ({level.minHeight:F2}-{level.maxHeight:F2}): {level.positions.Count} pixels ({coverage:F1}%)");
            }

            return levels;
        }

        /// <summary>
        /// Generates descriptive names for height levels.
        /// </summary>
        private string[] GenerateHeightLevelNames(int numLevels)
        {
            if (numLevels <= 0) return new string[0];
            
            string[] names = new string[numLevels];
            
            // Predefined names for common level counts
            if (numLevels == 1)
            {
                names[0] = "Ground";
            }
            else if (numLevels == 2)
            {
                names[0] = "Low";
                names[1] = "High";
            }
            else if (numLevels == 3)
            {
                names[0] = "Valley";
                names[1] = "Plains";
                names[2] = "Hills";
            }
            else if (numLevels == 4)
            {
                names[0] = "Deep";
                names[1] = "Low";
                names[2] = "Mid";
                names[3] = "High";
            }
            else if (numLevels == 5)
            {
                names[0] = "Abyss";
                names[1] = "Valley";
                names[2] = "Plains";
                names[3] = "Hills";
                names[4] = "Peaks";
            }
            else
            {
                // Generic naming for more levels
                for (int i = 0; i < numLevels; i++)
                {
                    names[i] = $"Level_{i + 1}";
                }
            }
            
            return names;
        }

        /// <summary>
        /// Gets the grayscale value at a specific pixel position.
        /// </summary>
        public float GetHeightAtPosition(Texture2D texture, int x, int y)
        {
            if (texture == null) return 0f;
            if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) return 0f;
            
            Color pixel = texture.GetPixel(x, y);
            return pixel.grayscale;
        }

        /// <summary>
        /// Converts a color texture to grayscale height map.
        /// Uses GetPixels32/SetPixels32 for better memory efficiency.
        /// </summary>
        /// <remarks>Caller is responsible for destroying the returned Texture2D when no longer needed.</remarks>
        public Texture2D ConvertToGrayscale(Texture2D texture)
        {
            if (texture == null) return null;

            int width = texture.width;
            int height = texture.height;
            Texture2D grayscaleTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            
            // Use GetPixels32 for better memory efficiency (4 bytes vs 16 bytes per pixel)
            Color32[] pixels32 = texture.GetPixels32();
            Color32[] grayscalePixels = new Color32[pixels32.Length];

            for (int i = 0; i < pixels32.Length; i++)
            {
                // Calculate grayscale using standard luminance weights
                Color32 c = pixels32[i];
                byte gray = (byte)(c.r * 0.299f + c.g * 0.587f + c.b * 0.114f);
                grayscalePixels[i] = new Color32(gray, gray, gray, 255);
            }

            grayscaleTexture.SetPixels32(grayscalePixels);
            grayscaleTexture.Apply();

            return grayscaleTexture;
        }

        #endregion

        #region Edge Detection (Sobel Filter)

        /// <summary>
        /// Detects edges in the image using the Sobel operator.
        /// </summary>
        /// <param name="texture">Source texture to analyze</param>
        /// <param name="threshold">Edge detection threshold (0-1), higher = fewer edges detected</param>
        /// <returns>Texture with detected edges (white = edge, black = no edge)</returns>
        /// <remarks>Caller is responsible for destroying the returned Texture2D when no longer needed.</remarks>
        public Texture2D DetectEdges(Texture2D texture, float threshold = 0.1f)
        {
            if (texture == null)
            {
                LogError("[ImageAnalyzer] DetectEdges: Texture is null");
                return null;
            }

            int width = texture.width;
            int height = texture.height;
            
            // First convert to grayscale for edge detection
            // Use GetPixels32 for better memory efficiency when reading
            Color32[] pixels32 = texture.GetPixels32();
            float[] grayscale = new float[pixels32.Length];
            
            for (int i = 0; i < pixels32.Length; i++)
            {
                // Calculate grayscale using standard luminance weights
                Color32 c = pixels32[i];
                grayscale[i] = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
            }

            // Sobel kernels
            // Gx kernel (horizontal gradient)
            int[,] sobelX = new int[,]
            {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };

            // Gy kernel (vertical gradient)
            int[,] sobelY = new int[,]
            {
                { -1, -2, -1 },
                {  0,  0,  0 },
                {  1,  2,  1 }
            };

            // Create output texture using Color32 for better memory efficiency
            Texture2D edgeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] edgePixels = new Color32[pixels32.Length];

            // Apply Sobel operator
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float gx = 0f;
                    float gy = 0f;

                    // Convolve with Sobel kernels
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int px = Mathf.Clamp(x + kx, 0, width - 1);
                            int py = Mathf.Clamp(y + ky, 0, height - 1);
                            int idx = py * width + px;
                            
                            float pixelValue = grayscale[idx];
                            
                            gx += pixelValue * sobelX[ky + 1, kx + 1];
                            gy += pixelValue * sobelY[ky + 1, kx + 1];
                        }
                    }

                    // Calculate gradient magnitude
                    float magnitude = Mathf.Sqrt(gx * gx + gy * gy);
                    
                    // Normalize and apply threshold
                    magnitude = Mathf.Clamp01(magnitude);
                    
                    int outputIdx = y * width + x;
                    if (magnitude > threshold)
                    {
                        byte mag = (byte)(magnitude * 255f);
                        edgePixels[outputIdx] = new Color32(mag, mag, mag, 255);
                    }
                    else
                    {
                        edgePixels[outputIdx] = new Color32(0, 0, 0, 255);
                    }
                }
            }

            edgeTexture.SetPixels32(edgePixels);
            edgeTexture.Apply();

            LogDebug($"[ImageAnalyzer] Edge detection completed with threshold {threshold}");
            
            return edgeTexture;
        }

        /// <summary>
        /// Gets edge positions as a HashSet for TWC4 integration.
        /// </summary>
        /// <param name="edgeTexture">The edge map texture from DetectEdges</param>
        /// <param name="threshold">Minimum edge intensity to include (0-1)</param>
        /// <returns>HashSet of positions where edges were detected</returns>
        public HashSet<Vector2> GetEdgePositions(Texture2D edgeTexture, float threshold = 0.5f)
        {
            HashSet<Vector2> edgePositions = new HashSet<Vector2>();
            
            if (edgeTexture == null) return edgePositions;

            int width = edgeTexture.width;
            int height = edgeTexture.height;
            
            // Use GetPixels32 for better memory efficiency
            Color32[] pixels32 = edgeTexture.GetPixels32();
            byte thresholdByte = (byte)(threshold * 255f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    // For grayscale, r=g=b so we can just check r
                    if (pixels32[idx].r > thresholdByte)
                    {
                        edgePositions.Add(new Vector2(x, y));
                    }
                }
            }

            return edgePositions;
        }

        /// <summary>
        /// Calculates gradient direction at each edge pixel (useful for boundary orientation).
        /// </summary>
        public float[,] CalculateGradientDirections(Texture2D texture)
        {
            if (texture == null) return null;

            int width = texture.width;
            int height = texture.height;
            
            // Use GetPixels32 for better memory efficiency
            Color32[] pixels32 = texture.GetPixels32();
            float[] grayscale = new float[pixels32.Length];
            
            for (int i = 0; i < pixels32.Length; i++)
            {
                Color32 c = pixels32[i];
                grayscale[i] = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
            }

            float[,] directions = new float[width, height];

            // Sobel kernels
            int[,] sobelX = new int[,] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] sobelY = new int[,] { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float gx = 0f;
                    float gy = 0f;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int px = Mathf.Clamp(x + kx, 0, width - 1);
                            int py = Mathf.Clamp(y + ky, 0, height - 1);
                            int idx = py * width + px;
                            
                            gx += grayscale[idx] * sobelX[ky + 1, kx + 1];
                            gy += grayscale[idx] * sobelY[ky + 1, kx + 1];
                        }
                    }

                    // Calculate direction in radians (-PI to PI)
                    directions[x, y] = Mathf.Atan2(gy, gx);
                }
            }

            return directions;
        }

        #endregion

        #region Adaptive Height Level Extraction

        /// <summary>
        /// Extracts height levels using adaptive thresholds based on image histogram.
        /// This provides better results than fixed thresholds for images with non-uniform brightness distribution.
        /// </summary>
        /// <param name="texture">Source texture to analyze</param>
        /// <param name="numLevels">Number of height levels to extract</param>
        /// <returns>List of height levels with adaptive boundaries</returns>
        public List<HeightLevel> ExtractHeightLevelsAdaptive(Texture2D texture, int numLevels)
        {
            if (texture == null)
            {
                LogError("[ImageAnalyzer] ExtractHeightLevelsAdaptive: Texture is null");
                return new List<HeightLevel>();
            }

            if (numLevels <= 0)
            {
                LogError("[ImageAnalyzer] ExtractHeightLevelsAdaptive: numLevels must be positive");
                return new List<HeightLevel>();
            }

            int width = texture.width;
            int height = texture.height;
            
            Color32[] pixels32 = texture.GetPixels32();
            int totalPixels = pixels32.Length;
            
            // Calculate histogram (256 bins)
            int[] histogram = new int[256];
            float[] grayscaleValues = new float[totalPixels];
            
            for (int i = 0; i < totalPixels; i++)
            {
                Color32 c = pixels32[i];
                float gray = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
                grayscaleValues[i] = gray;
                int bin = Mathf.Clamp((int)(gray * 255f), 0, 255);
                histogram[bin]++;
            }
            
            // Find adaptive thresholds using percentile-based approach
            float[] thresholds = new float[numLevels + 1];
            thresholds[0] = 0f;
            thresholds[numLevels] = 1.001f; // Slightly over 1 to include 1.0
            
            int pixelsPerLevel = totalPixels / numLevels;
            int cumulative = 0;
            int thresholdIdx = 1;
            
            for (int bin = 0; bin < 256 && thresholdIdx < numLevels; bin++)
            {
                cumulative += histogram[bin];
                if (cumulative >= pixelsPerLevel * thresholdIdx)
                {
                    thresholds[thresholdIdx] = (bin + 1) / 255f;
                    thresholdIdx++;
                }
            }
            
            // Create height levels with adaptive thresholds
            List<HeightLevel> levels = new List<HeightLevel>();
            string[] defaultLevelNames = GenerateHeightLevelNames(numLevels);
            
            for (int i = 0; i < numLevels; i++)
            {
                HeightLevel level = new HeightLevel(thresholds[i], thresholds[i + 1], defaultLevelNames[i]);
                levels.Add(level);
            }
            
            // Assign pixels to height levels (with Y-axis flip)
            for (int i = 0; i < totalPixels; i++)
            {
                int x = i % width;
                int y = i / width;
                int flippedY = height - 1 - y;
                float grayscale = grayscaleValues[i];
                
                for (int lvl = 0; lvl < numLevels; lvl++)
                {
                    HeightLevel level = levels[lvl];
                    if (grayscale >= level.minHeight && grayscale < level.maxHeight)
                    {
                        level.positions.Add(new Vector2(x, flippedY));
                        levels[lvl] = level;
                        break;
                    }
                }
            }
            
            // Log results
            for (int i = 0; i < numLevels; i++)
            {
                HeightLevel level = levels[i];
                float coverage = (float)level.positions.Count / totalPixels * 100f;
                LogDebug($"[ImageAnalyzer] Adaptive height level '{level.name}' ({level.minHeight:F2}-{level.maxHeight:F2}): {level.positions.Count} pixels ({coverage:F1}%)");
            }
            
            return levels;
        }

        #endregion

        #region Complete Analysis

        /// <summary>
        /// Performs complete image analysis including color clustering, height extraction, and edge detection.
        /// </summary>
        /// <param name="texture">Source texture to analyze</param>
        /// <param name="numColorClusters">Number of color clusters for K-Means</param>
        /// <param name="numHeightLevels">Number of height levels to extract</param>
        /// <param name="edgeThreshold">Threshold for edge detection</param>
        /// <param name="useAdaptiveHeights">Use adaptive height thresholds based on histogram</param>
        /// <returns>Complete analysis result</returns>
        public AnalysisResult AnalyzeImage(Texture2D texture, int numColorClusters = 5, int numHeightLevels = 4, float edgeThreshold = 0.1f, bool useAdaptiveHeights = true)
        {
            AnalysisResult result = new AnalysisResult();
            
            if (texture == null)
            {
                LogError("[ImageAnalyzer] AnalyzeImage: Texture is null");
                return result;
            }

            result.width = texture.width;
            result.height = texture.height;
            result.sourceTexture = texture;

            LogDebug($"[ImageAnalyzer] Starting analysis of {texture.width}x{texture.height} image...");

            // Perform color clustering
            result.colorClusters = AnalyzeColors(texture, numColorClusters);

            // Extract height levels (use adaptive method by default for better results)
            if (useAdaptiveHeights)
            {
                result.heightLevels = ExtractHeightLevelsAdaptive(texture, numHeightLevels);
                LogDebug("[ImageAnalyzer] Using adaptive height level extraction");
            }
            else
            {
                result.heightLevels = ExtractHeightLevels(texture, numHeightLevels);
            }

            // Detect edges
            result.edgeMap = DetectEdges(texture, edgeThreshold);

            result.analysisTime = System.DateTime.Now;

            LogDebug($"[ImageAnalyzer] Analysis complete. Found {result.colorClusters.Count} color clusters, {result.heightLevels.Count} height levels");

            return result;
        }

        #endregion

        #region Debug Logging
        
        /// <summary>
        /// Conditional debug logging that only executes in the Unity Editor.
        /// </summary>
        /// <param name="message">Message to log</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogDebug(string message) => Debug.Log(message);
        
        /// <summary>
        /// Conditional debug error logging that only executes in the Unity Editor.
        /// </summary>
        /// <param name="message">Error message to log</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogError(string message) => Debug.LogError(message);
        
        #endregion
        
        #region Utility Methods

        /// <summary>
        /// Resizes a texture to the specified dimensions.
        /// Uses RenderTexture.GetTemporary for proper memory management.
        /// </summary>
        /// <remarks>Caller is responsible for destroying the returned Texture2D when no longer needed.</remarks>
        public Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;

            // Use GetTemporary for automatic pooling and proper memory management
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            // Restore previous active RenderTexture and release temporary
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        /// <summary>
        /// Calculates color similarity between two colors (0 = identical, 1 = completely different).
        /// </summary>
        public float CalculateColorSimilarity(Color a, Color b)
        {
            float dist = Mathf.Sqrt(ColorDistanceSquared(a, b));
            // Max distance in RGB space is sqrt(3) ≈ 1.732
            return Mathf.Clamp01(dist / 1.732f);
        }

        /// <summary>
        /// Gets the dominant color from a texture.
        /// </summary>
        public Color GetDominantColor(Texture2D texture)
        {
            if (texture == null) return Color.black;

            var clusters = AnalyzeColors(texture, 1, 50);
            if (clusters.Count > 0)
            {
                return clusters[0].centroid;
            }
            return Color.black;
        }

        /// <summary>
        /// Calculates histogram of grayscale values.
        /// Uses GetPixels32 for better memory efficiency.
        /// </summary>
        public int[] CalculateHistogram(Texture2D texture, int bins = 256)
        {
            int[] histogram = new int[bins];
            
            if (texture == null) return histogram;

            // Use GetPixels32 for better memory efficiency
            Color32[] pixels32 = texture.GetPixels32();
            
            foreach (Color32 pixel in pixels32)
            {
                // Calculate grayscale using standard luminance weights
                float grayscale = (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) / 255f;
                int binIndex = Mathf.Clamp((int)(grayscale * (bins - 1)), 0, bins - 1);
                histogram[binIndex]++;
            }

            return histogram;
        }

        #endregion
    }
}
