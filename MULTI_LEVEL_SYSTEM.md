# Multi-Level System Documentation

## Overview

The MonoGame project has been extended with a complete multi-level management system featuring:
- Multiple level storage and management
- Responsive level selection menu (both Play and Edit modes)
- Popup dialogs for confirmations and text input
- Best time tracking per level
- Resolution-independent UI

## Architecture

### Core Components

#### LevelMetadata
Represents metadata about a level:
- `Id`: Unique identifier (e.g., "level_1")
- `Name`: Display name (e.g., "Level 1")
- `FilePath`: Path to the level JSON file

#### LevelManager
Static class handling level management:
- `GetAllLevels()`: Scans Levels folder and returns all level metadata
- `LoadLevel(levelId)`: Loads a level from JSON
- `SaveLevel(level, levelId)`: Saves a level to JSON
- `CreateNewLevel(displayName)`: Creates a new default level
- `DeleteLevel(levelId)`: Deletes a level and its best time record
- `GetLevel(levelId)`: Gets metadata for a specific level

#### LevelSelectScene
Main UI scene for level selection with two modes:
- **PlayMode**: Select and play levels, view best times, delete highscores
- **EditMode**: Select and edit levels, delete levels, create new levels

#### Supporting Components
- **GridLayout**: Responsive grid layout system for level slots
- **Popup**: Reusable dialog system for confirmations and text input
- **TextInputComponent**: Text input field with validation
- **BestTimeStorage**: Enhanced to track best times per level

## File Structure

```
Content/
├── Levels/
│   ├── level_1.json
│   ├── level_2.json
│   └── ...
└── level.json (legacy, can be removed)

best_times.json (tracks best times)
```

## Level JSON Format

Each level is stored as a JSON file with the following structure:

```json
{
  "platforms": [
    {
      "x": 0,
      "y": 400,
      "width": 800,
      "height": 40,
      "color": "Red"
    }
  ],
  "goals": [
    {
      "x": 1216,
      "y": 356
    }
  ],
  "playerSpawn": {
    "x": 100,
    "y": 300
  }
}
```

## Best Times Format

Best times are stored in `best_times.json`:

```json
{
  "level_1": 12.53,
  "level_2": 20.11,
  "level_3": 15.75
}
```

## Navigation Flow

```
MenuScene
├─→ "Play" → LevelSelectScene (PlayMode)
│   ├─→ Select Level → GameScene (with level ID)
│   │   └─→ Back → LevelSelectScene (PlayMode)
│   └─→ Back → MenuScene
│
└─→ "Level Editor" → LevelSelectScene (EditMode)
    ├─→ Edit → EditorScene (with level ID)
    │   └─→ Back → LevelSelectScene (EditMode)
    ├─→ Create New → [Popup Text Input] → Creates new level
    ├─→ Delete → [Confirmation Popup] → Deletes level
    └─→ Back → MenuScene
```

## Usage Examples

### Loading a Level
```csharp
// In GameScene or EditorScene
Level level = LevelManager.LoadLevel("level_1");
```

### Creating a New Level
```csharp
// Creates level_3, level_4, etc. with default content
string newLevelId = LevelManager.CreateNewLevel("My Custom Level");
```

### Saving a Level
```csharp
LevelManager.SaveLevel(_level, _levelId);
```

### Getting Best Time
```csharp
if (BestTimeStorage.TryGetBestTime("level_1", out float bestTime))
{
    TimeSpan ts = TimeSpan.FromSeconds(bestTime);
    string timeStr = $"{ts.Minutes:00}:{ts.Seconds:00}:{(int)(ts.Milliseconds / 10):00}";
}
```

## UI Features

### Level Select Grid
- **Responsive**: Automatically adjusts grid columns based on viewport width
- **Centered**: Grid is centered on screen with proper margins
- **Selection**: Click level slots to select them
- **Visual Feedback**: Selected slots have blue highlight and glow effect
- **Best Time Display**: Shows best time for each level in Play mode

### Buttons
- **Back**: Returns to main menu
- **Play** (PlayMode): Launch selected level in GameScene
- **Delete Highscore** (PlayMode): Remove best time for level
- **Edit** (EditMode): Open selected level in EditorScene
- **Delete** (EditMode): Delete selected level permanently
- **Create New** (EditMode): Create new level with custom name

### Popup System
- **Fade Animation**: Popups fade in smoothly
- **Modal Overlay**: Darkened background blocks interaction with level grid
- **Text Input**: For creating new levels with custom names
- **Confirmation**: For delete actions

## Resolution Independence

All UI elements use viewport-relative positioning:
- Grid layout adapts to any resolution
- Button positions calculated as percentages of viewport
- Font sizes use scaling factors
- No hardcoded pixel positions

Tested resolutions:
- 1280x720 (default)
- 1920x1080 (recommended)
- 800x600 (minimum)
- Widescreen and ultrawide support

## Best Time Tracking

### Automatic Recording
- Times are automatically saved when a level is completed
- Times are rounded to centiseconds (0.01s precision)
- Only new records are saved (faster times replace older times)

### Display Format
- Format: `MM:SS:CS` (Minutes:Seconds:Centiseconds)
- Example: `00:12:53` means 12.53 seconds
- "Best: --" displays if no best time recorded

### Deletion
- Manual deletion via "Delete Highscore" button in PlayMode
- Automatic deletion when level is deleted via LevelManager

## Code Integration

### Extending Existing Scenes

GameScene and EditorScene now accept optional `levelId` parameters:

```csharp
// Load specific level (defaults to "level_1" if not specified)
var gameScene = new GameScene(_game, "level_2");
var editorScene = new EditorScene(_game, "level_1");

// Backward compatible - default level still works
var gameScene = new GameScene(_game);  // Loads "level_1"
```

### BestTimeStorage Enhancements

New methods added:
```csharp
// Reset best time for a level
BestTimeStorage.ResetLevelRecord("level_1");

// Delete level record
BestTimeStorage.DeleteLevelRecord("level_1");

// Existing methods still work
BestTimeStorage.SaveIfRecord("level_1", 12.53f);
BestTimeStorage.TryGetBestTime("level_1", out float time);
```

## Troubleshooting

### Levels Not Loading
- Verify `Content/Levels/` folder exists
- Check that level JSON files are properly formatted
- Ensure level files follow naming pattern: `level_N.json`

### Best Times Not Saving
- Verify `best_times.json` is writable in the application directory
- Check that level IDs match between levels and best_times

### Grid Layout Issues
- Ensure viewport width is sufficient (minimum ~800 pixels recommended)
- Check that level count isn't too large (tested up to 100+ levels)

### Text Input Not Working
- Verify keyboard input manager is updated
- Check that TextInputComponent has focus before typing

## Future Enhancements

Potential improvements:
- Level thumbnails/previews in grid
- Level difficulty indicators
- Sorting/filtering options (by name, best time, date created)
- Level leaderboards
- Level sharing/importing
- Undo/Redo in editor
- Level templates
- Performance metrics display

## Performance Considerations

- Level scanning is performed once at scene initialization
- Grid layout is recalculated only when viewport changes
- Level files are loaded on-demand (not preloaded)
- Best times are cached in memory
- JSON serialization uses efficient System.Text.Json

## Compatibility

- Backward compatible with single-level gameplay
- Legacy `LevelStorage.LoadOrCreateDefault()` still works
- Existing level data can be migrated to multi-level system
- No breaking changes to existing Game1, GameScene, or EditorScene APIs
