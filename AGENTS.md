# Grimoire of the Void - AI Code Editor Guide

## Project Context & Architecture
- **Engine**: Unity 3D with C# scripts.
- **Render Pipeline**: High Definition Render Pipeline (HDRP) (`com.unity.render-pipelines.high-definition`). Ensure all graphic/shader assumptions align with HDRP.
- **Input Handling**: The project uses the *new* Unity Input System (`com.unity.inputsystem`). Do not use the legacy `Input.GetKeyDown()` etc. Use `InputSystem_Actions.inputactions` found in `Assets/`.
- **Primary Scenes**: Entry scenes and levels are typically set in `Assets/` (e.g., `OutdoorsScene.unity`). Asset dependencies often reside in specific folder structures (e.g., `BK_AlchemistHouse/`).

## Agent Workflow & Guidelines
- **Script Location**: C# Scripts generally go into `Assets/Scripts/` (though some asset-specific scripts live inside their vendor folders like `Assets/BK_AlchemistHouse/Scripts/`). Favor `Assets/Scripts` for new gameplay logic.
- **Component Design**: 
  - Keep Unity components modular (prefer single responsibility per `MonoBehaviour`).
  - Cache references in `Awake()` or `Start()` to avoid `GetComponent()` calls in `Update()`.
- **Packages & Dependencies**: The project uses standard Unity modules and Visual Scripting. Always check if a system could be handled via Visual Scripting before generating extensive boilerplate.
- **Scene and Asset Changes**: AI agents should inform the developer if a configuration needs to be mapped in Unity's Inspector, since scripts alone can't wire scene references. Provide step-by-step instructions for any required Inspector configurations when creating or modifying `SerializedField` components.

## Commands
- **Build / Run**: Controlled via the Unity Editor interface. When prompting tests, tell the developer to run via the Editor Play button rather than CLI.

