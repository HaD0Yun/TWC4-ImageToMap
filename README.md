# TWC4-ImageToMap

Unity Editor tool that converts reference images into **TileWorldCreator 4** maps automatically.

![Unity](https://img.shields.io/badge/Unity-2021.3+-black?logo=unity)
![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **K-Means Color Clustering** - Groups image colors into terrain types
- **Height Level Extraction** - Converts grayscale to elevation layers  
- **Sobel Edge Detection** - Identifies terrain boundaries
- **Auto TilePreset Detection** - Finds and assigns TilePresets automatically
- **Drag & Drop UI** - Intuitive Unity Editor interface

## Requirements

- Unity 2021.3+ 
- [TileWorldCreator 4](https://assetstore.unity.com/packages/tools/level-design/tileworldcreator-4-320067)

## Installation

1. Copy the `Editor/ImageToMap` folder to your Unity project's `Assets/` directory
2. Unity will compile the scripts automatically

## Quick Start

```
1. Menu: Tools > TileWorldCreator > Image To Map - Quick Setup

2. Menu: Tools > TileWorldCreator > Image To Map Generator

3. Drag & Drop your image (PNG, JPG, TGA)

4. Click "Generate Map"

5. Click "Execute All Layers"
```

## Files

| File | Description |
|------|-------------|
| `ImageAnalyzer.cs` | K-Means clustering, height extraction, edge detection |
| `ColorPalette.cs` | ScriptableObject for color-to-tile mapping |
| `ImageToMapGenerator.cs` | TWC4 BlueprintLayer & BuildLayer generation |
| `ImageToMapConfig.cs` | Configuration ScriptableObject |
| `ImageToMapEditorWindow.cs` | Unity Editor UI with drag & drop |
| `ImageToMapQuickSetup.cs` | One-click setup utility |
| `TilePresetDetector.cs` | Auto-detection of TilePresets |

## How It Works

```
[Input Image]
     ↓
┌─────────────────────────────┐
│ ImageAnalyzer               │
│ ├─ K-Means Clustering       │
│ ├─ Height Extraction        │
│ └─ Edge Detection           │
└─────────────────────────────┘
     ↓
┌─────────────────────────────┐
│ ImageToMapGenerator         │
│ ├─ Create BlueprintLayers   │
│ ├─ Add HeightTexture Mods   │
│ └─ Create BuildLayers       │
└─────────────────────────────┘
     ↓
[TileWorldCreator 4 Map]
```

## Tips

1. **Match Resolution**: Set TWC4 Configuration size to match your image dimensions
2. **Heightmaps**: Grayscale images work best (black=low, white=high)
3. **Color Images**: Use distinct colors for different terrain types

## License

MIT License
