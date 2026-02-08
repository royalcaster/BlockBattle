# BlockBattle

A VR block-building and destruction game where players replicate structures from a 3D reference, then destroy them with a slingshot.

## Project Overview

BlockBattle is a VR game built with Unity and the XR Interaction Toolkit. Players grab blocks from a shelf, build structures matching a reference, validate their build, then destroy it with a slingshot. The game features physics-based interactions, relative position validation, and a complete game loop with level progression.

## Gameplay Loop

1. **Building**: Replicate a vorgegebene Struktur aus bunten Blöcken (replicate a given structure from colorful blocks)
2. **Validating**: Echtzeit-Prüfung der Baugenauigkeit (real-time validation of build accuracy)
3. **Destroying**: Nutze eine Steinschleuder (Slingshot), um alles abzuräumen (use a slingshot to clear everything)
4. **Cleaning Up**: Physisches Zurücklegen der Blöcke beendet das Spiel (physically returning blocks ends the game)

## Documentation Structure

### Module Documentation (Close to Code)

Module-specific documentation is located in `Assets/BlockBattle/Scripts/`:

- **[Main Scripts Overview](Assets/BlockBattle/Scripts/README.md)** - System architecture and module overview
- **[Validation System](Assets/BlockBattle/Scripts/Validation/README.md)** - Build validation with relative positioning
- **[Shelf System](Assets/BlockBattle/Scripts/Shelf/README.md)** - Block spawning and door-triggered ejection
- **[Destruction System](Assets/BlockBattle/Scripts/Destruction/README.md)** - VR slingshot and destruction phase
- **[Game Flow](Assets/BlockBattle/Scripts/GameFlow/README.md)** - Level progression and phase management
- **[Block System](Assets/BlockBattle/Scripts/Blocks/README.md)** - Block spawning and configuration
- **[UI System](Assets/BlockBattle/Scripts/UI/README.md)** - HUDs and user interface

Each module README includes:
- Purpose statement
- Main classes/components
- How to use/test the module
- Key technical decisions
- Known limitations

### Personal Project Diaries

Team member diaries are located in `Docs/`:

- **[Gabriel Pechstein](Docs/GabrielPechstein.md)** - Core systems implementation
- **[Philip Poplutz](Docs/PhilipPoplutz.md)** - Environment, levels, and polish

Each diary documents:
- Development progress and results
- Problems encountered and solutions
- Technical decisions with rationale
- References to relevant module documentation

### Additional Documentation

Detailed technical documentation in `Docs/`:

- [Core Implementation](Docs/core_implementation.md) - Overall system architecture
- [Shelf System](Docs/shelf_system.md) - Detailed shelf mechanics
- [Game Flow](Docs/game_flow_shelf_wall.md) - Game loop documentation
- [Validation HUD API](Docs/validation_hud_api.md) - Validation UI reference
- [Build Validator API](Docs/build_validator_api.md) - Validation system reference
- [Reference Structure API](Docs/reference_structure_api.md) - Reference system reference

## Getting Started

### Prerequisites

- Unity 2022.3 LTS or later
- XR Interaction Toolkit package
- Oculus Quest 2/3 (or compatible VR headset)

### Setup

1. Clone the repository
2. Open project in Unity
3. Open `Assets/Scenes/BlockBattleScene.unity`
4. Configure XR settings for your headset
5. Build and deploy to Quest

### Scene Setup

The main scene (`BlockBattleScene`) includes:
- XR Rig with hand controllers
- Build zone and table
- Shelf with doors
- Slingshot
- UI canvases
- LevelManager (orchestrates game flow)

## Key Features

### Relative Position Validation
The validation system uses relative positioning, allowing the build table to be rotated in VR space without breaking validation logic.

### Physics-Based Interactions
- Motor-driven shelf doors with haptic feedback
- Pull-distance-based slingshot mechanics
- Realistic block physics and ejection

### Phase-Based Game Flow
- Building phase with real-time validation
- Destruction phase with slingshot
- Block return phase for level completion
- Automatic level progression

## Project Structure

```
BlockBattle/
├── Assets/
│   ├── BlockBattle/          # Main game code
│   │   ├── Scripts/         # Core gameplay scripts
│   │   ├── Prefabs/         # Block and game object prefabs
│   │   ├── Materials/      # Block materials
│   │   └── Structures/      # Level configurations
│   ├── Scenes/              # Unity scenes
│   └── ...
├── Docs/                     # Documentation
│   ├── GabrielPechstein.md  # Personal diary
│   ├── PhilipPoplutz.md     # Personal diary
│   └── ...                  # Technical docs
├── .github/                  # GitHub templates
│   └── pull_request_template.md
└── README.md                 # This file
```

## Development Guidelines

### Code Style
- Follow SUIT technical guidelines (see repository rules)
- Use PascalCase for methods
- Use XML comments (`///`) for classes and methods
- Use descriptive variable names (no unnecessary abbreviations)

### Git Workflow
- Branch naming: `<category>/<surname>/<YYYYMMDD>_<description>`
- Commit messages: `feat:`, `fix:`, or `chore:` prefix
- Pull requests must include documentation updates

### Documentation Requirements
- **Module READMEs**: Required for new modules or significant changes
- **Personal Diaries**: Required entry for each PR
- **PR Template**: Use `.github/pull_request_template.md`

See [PR Template](.github/pull_request_template.md) for detailed requirements.

## Building and Deployment

### Quest Build
1. Switch platform to Android
2. Configure Oculus settings
3. Build APK
4. Deploy via ADB or Oculus Developer Hub

### Build Settings
- Target API Level: Android 12 (API 31) or later
- Minimum API Level: Android 7.0 (API 24)
- Graphics API: Vulkan (recommended) or OpenGL ES 3.0

## Known Issues

- Tolerance values may need per-level tuning
- Validation runs on main thread (could be optimized)
- Some hardcoded values (door angles, teleport positions) could be configurable

## Contributors

- **Gabriel Pechstein** - Core systems, validation, shelf, destruction, game flow
- **Philip Poplutz** - Environment, levels, UI polish, bug fixes

## License

[Add license information if applicable]

## References

- [Unity XR Interaction Toolkit](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@latest)
