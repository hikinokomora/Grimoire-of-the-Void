# Grimoire of the Void - AI Code Editor Guide

## Project Context & Architecture
- **Engine**: Unity 3D with C# scripts.
- **Render Pipeline**: High Definition Render Pipeline (HDRP) (`com.unity.render-pipelines.high-definition`). Ensure all graphic/shader assumptions align with HDRP.
- **Input Handling**: The project uses the *new* Unity Input System (`com.unity.inputsystem`). Do not use the legacy `Input.GetKeyDown()` etc. Use `InputSystem_Actions.inputactions` found in `Assets/`.
- **Primary Scenes**: Entry scenes and levels are typically set in `Assets/` (e.g., `OutdoorsScene.unity`). Asset dependencies often reside in specific folder structures (e.g., `BK_AlchemistHouse/`).
- **Crafting & Recipe Book System**: 
  - The project uses a physical 3D recipe book logic. Do NOT generate standard 2D UI for the recipe book. 
  - Data is driven by `OccultAspect` ScriptableObjects (SO) representing crafting recipes and ingredients. Base elements have empty ingredients text. 
  - Since SO data persists in the Editor but should reset per run, we use `[System.NonSerialized] public bool sessionUnlocked` for runtime unlock state rather than `isUnlocked`.
  - Pages are dynamically cloned via `PhysicalRecipeBook.cs` using a master prefab. They flip using Euler 3D rotations and stack with small Y-axis offsets (e.g., `-0.005f`) to prevent Z-fighting.
  - Page interactivity works via transparent `BoxCollider` overlay hitboxes mapped strictly to A/D keyboard and mouse clicks handled by custom interaction layers (like `CraftingInteractor`).
- **Table crafting & camera (FPS)**: `CraftingViewController` switches between first-person and a fixed view at the table (camera lerp, ESC to exit, cursor). `BasicMovement` gates input when the player is in the station / crafting view. `PhysicalRecipeBook` only reacts to A/D (and related keys) when `CraftingViewController.IsInCraftingView` is true so page turns do not fire during normal play.

## OccultAspectRegistry & books (authoritative for AI)
- **Single runtime catalog** (`OccultAspectRegistry` in `Assets/Scripts/Crafting/OccultAspectRegistry.cs`): ordered list + lookup by `OccultAspect.ID` + per-session revealed IDs. Used by the physical 3D book, optional 2D `RecipeBook`, and crafting unlock flow.
- **Initialization**
  - **`AspectManager` is optional**: if the scene has `AspectManager`, it runs early (`DefaultExecutionOrder(-200)`) and calls `OccultAspectRegistry.Initialize(aspectCatalog, mergeResourcesIntoCatalog)`.
  - If there is no `AspectManager` (or init must happen later), any code path that needs the registry calls **`OccultAspectRegistry.EnsureDefaultFromResources()`** — that performs `Initialize(null, true)` once, i.e. catalog from **Resources only** (see below). `UnlockForSession` and `RegisterAdHocIfMissing` also ensure default init so unlock is never “before init.”
- **Loading from `Resources` (important)**
  - Do **not** use `Resources.LoadAll<OccultAspect>("")` (empty path). Unity would scan **all** `Resources` folders in the project (e.g. TextMesh Pro, other packages), which can spuriously log **“The referenced script (Unknown) on this Behaviour is missing”** on unrelated assets.
  - The project uses a **dedicated subfolder**: constant **`OccultAspectRegistry.ResourcesCatalogSubfolder`** = `"OccultCatalog"` → only assets under e.g. **`Assets/Resources/OccultCatalog/`** are loaded (e.g. `Resources.LoadAll<OccultAspect>("OccultCatalog")` equivalent).
  - `Assets/Resources/Aspects/*.asset` may be **legacy `AspectData`** (old type), not `OccultAspect`; they are **not** merged by the `OccultAspect` loader. The main `OccultAspect` SOs in this project often live outside `Resources` (e.g. `Assets/core/Prefabs/`) and must be listed on **`AspectManager` → Aspect Catalog** if you rely on the inspector list.
- **Recipe vs image (session)**
  - **Recipe text** (ingredients) visibility uses `OccultAspectRegistry.IsRevealedForPage` (per-session recipe IDs, base aspects with empty / “Нет данных” ingredients treated as already “known” for the text line).
  - **Illustration** (`aspectIcon` on pages) is separate: `OccultAspectRegistry.IsImageRevealedForPage`, backed by `_sessionImageRevealedIds`. For crafted aspects, **`UnlockForSession` unlocks both** recipe + image so the grimoire page stays consistent.
  - **Image-only unlock** (e.g. reward without revealing the recipe): **`OccultAspectRegistry.UnlockImageForSession`**, **`RevealImageAndNotifyUI`**, and internal **`NotifyBooks`**, without calling `UnlockForSession`.
  - **`PhysicalPage`** and optional 2D **`RecipeBook`**: only show the aspect `Image` when the image is revealed and a sprite is assigned; hidden for unknown recipes until craft or an image-only unlock.
- **Unlocks and UI sync**
  - **`OccultAspectRegistry.UnlockAndNotifyUI(OccultAspect)`**: registers session unlock and calls **`SyncFromRegistry`** on every `PhysicalRecipeBook` and `RecipeBook` in loaded scenes. This is the **one** place for “reveal in book + refresh UI.”
  - **`CauldronController`** calls `UnlockAndNotifyUI` on successful craft so the book updates **without** requiring `AspectManager` in the scene.
  - **`AspectManager.UnlockAspect`** delegates to `UnlockAndNotifyUI` (the serialized `recipeBook` / `physicalBook` fields on the component are not required for that sync; discovery is by type).
- **Cauldron & table interaction**
  - **`CraftingInteractor`**: disabled until the player is in table mode; **`CraftingViewController`** enables it after the camera finishes moving and uses **`RequestSuppressNextInput`** to avoid the sit-down click also hitting the world. The interactor’s serialized **`CauldronController`** is for other flows (e.g. drop/click), not the lever.
  - **`CauldronController`**: on reset (lever), **`PlayResetVisualEffects`** — `resetSmokeEffect` + optional **`clearBurstEffects`**, even if the pot is already empty.
  - **`CauldronLever`**: ray hits require **`CauldronLever` on the same `GameObject` as the `Collider`**. Wire **`Cauldron`**. For **a single pull clip** and no separate idle: leave **`Idle State Name`** empty, set **`Pull State Name`** to the Animator state name; the script freezes at frame 0 (`speed = 0`) until `Pull()`. A **NullReference** in **`UnityEditor.Graphs.Edge`** when opening an Animator Controller usually means a **corrupted graph** — recreate the controller or reimport; editor-only, not gameplay code.
- **Books startup**: `PhysicalRecipeBook` and `RecipeBook` call `EnsureDefaultFromResources()`; if the catalog is still **empty** after init (`Count == 0`), they log a clear error (add SO under `Resources/OccultCatalog` and/or fill `AspectManager`’s catalog).
- **C# / Unity gotchas (already hit in this repo)**
  - **`Object` is ambiguous** (`System.Object` vs `UnityEngine.Object`) in files with `using System;` and `UnityEngine` — for scene queries use **`UnityEngine.Object.FindObjectsByType`**, not bare `Object`.
  - **Local variable shadowing** in a single method (e.g. two `string ing` blocks) → **CS0136**; use one name or different names in nested scopes.

## Agent Workflow & Guidelines
- **Script Location**: C# Scripts generally go into `Assets/Scripts/` (though some asset-specific scripts live inside their vendor folders like `Assets/BK_AlchemistHouse/Scripts/`). Favor `Assets/Scripts` for new gameplay logic.
- **Component Design**: 
  - Keep Unity components modular (prefer single responsibility per `MonoBehaviour`).
  - Cache references in `Awake()` or `Start()` to avoid `GetComponent()` calls in `Update()`.
- **Packages & Dependencies**: The project uses standard Unity modules and Visual Scripting. Always check if a system could be handled via Visual Scripting before generating extensive boilerplate.
- **Scene and Asset Changes**: AI agents should inform the developer if a configuration needs to be mapped in Unity's Inspector, since scripts alone can't wire scene references. Provide step-by-step instructions for any required Inspector configurations when creating or modifying `SerializedField` components.

## Commands
- **Build / Run**: Controlled via the Unity Editor interface. When prompting tests, tell the developer to run via the Editor Play button rather than CLI.
