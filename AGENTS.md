# VirtualMusicStudio Unity Agent Guide

## Project Summary
- Unity project for a VR/XR music studio prototype.
- Current stack is centered on Universal Render Pipeline, Input System, OpenXR, XR Hands, and XR Interaction Toolkit.
- The main working scene is `Assets/Scenes/SampleScene.unity`.
- The project currently contains mostly Unity sample assets and prefabs; custom gameplay code is minimal.

## Source of Truth
- Treat this file as the shared project instruction set for coding agents.
- Put tool-specific additions in their own files, but keep project facts and engineering rules here.
- Prefer updating this file instead of duplicating the same rules in multiple places.

## Project Layout
- `Assets/Scenes`: scene assets. `SampleScene.unity` is currently the only build scene.
- `Assets/Samples`: imported Unity sample content for XR Hands and XR Interaction Toolkit.
- `Assets/XR`, `Assets/XRI`, `Assets/Settings`: XR-related assets and configuration.
- `Packages/manifest.json`: package dependencies.
- `ProjectSettings/`: Unity editor, render pipeline, input, and XR configuration.
- `.claude/settings.local.json`: Claude local permissions for this repository.

## Environment Facts
- Unity version: `6000.3.10f1`.
- Render pipeline: URP.
- Input stack: Input System with `Assets/InputSystem_Actions.inputactions`.
- XR stack: OpenXR + XR Interaction Toolkit + XR Hands.
- Test package is installed via `com.unity.test-framework`.
- Unity MCP is installed via `com.coplaydev.unity-mcp`.

## Working Rules
- Inspect the current scene, hierarchy, and package setup before changing behavior.
- Prefer preserving existing Unity sample wiring unless the task explicitly replaces it.
- Do not move or rename sample assets casually. Many prefab and scene references are GUID-sensitive.
- Keep edits narrow and compatible with Unity serialization. Avoid unnecessary churn in `.unity`, `.prefab`, and `.asset` files.
- When editing scenes or prefabs, verify the target object path and component assignments before changing serialized data.
- Treat user changes as authoritative. The worktree may already be dirty.
- Validate Unity MCP tool parameter enums against the tool schema before calling them. Do not guess values such as search modes or refresh flags.

## Scene And Asset Safety
- Check whether `Assets/Scenes/SampleScene.unity` already contains unsaved or unrelated user edits before making scene changes.
- Prefer prefab or script changes over broad direct scene rewrites when either approach would work.
- If a task requires generated screenshots or captures, keep them under ignored paths such as `Assets/Screenshots`.
- Preserve `.meta` files and never regenerate assets by deleting and reimporting unless explicitly required.
- When Unity object references cannot be assigned reliably through MCP property setters, inspect actual serialization or package code first and then patch the minimal YAML needed.
- After Unity domain reload or compile, do not reuse old scene instance IDs. Re-query the scene objects before continuing validation.

## Coding Conventions
- Use C# for runtime/editor logic under a clear folder such as `Assets/Scripts` when new code is needed.
- Match existing Unity conventions: serialized fields for inspector wiring, explicit component dependencies, and minimal magic strings.
- Keep MonoBehaviour responsibilities narrow. Extract reusable logic from scene-bound scripts when complexity grows.
- Add brief comments only where Unity behavior or XR wiring is non-obvious.
- Avoid speculative abstraction in a project that is still sample-heavy.

## XR-Specific Guidance
- Assume the player rig is based on `XROrigin` and XR Interaction Toolkit providers.
- When changing locomotion, input, hand interaction, or controller behavior, validate references on the XR rig and interaction manager.
- Prefer Input System actions and existing XRI components over custom polling logic.
- Be careful with OpenXR and XR Hands sample objects because missing bindings often fail at runtime rather than compile time.
- Before adding an XRI component, check for `RequireComponent` relationships in package code or existing serialized data to avoid duplicate components.
- When wiring XRI locomotion providers from starter assets, do not assume sample presets match the requested control scheme. Verify left/right action slots explicitly and clear unused hand bindings.
- For locomotion/input changes, inspect the actual package scripts or presets to confirm serialized field names, expected action IDs, and whether references are inferred at runtime versus serialized.

## Verification
- For code changes, run the narrowest available validation first.
- For scene or prefab changes, inspect hierarchy/component state and check the Unity console for serialization or missing-reference errors.
- If tests are added, prefer Unity Edit Mode or Play Mode tests that can run headlessly.
- If validation is skipped or blocked, state that explicitly.

## Git And Change Management
- Expect unrelated user modifications. Do not revert them.
- Keep commits focused and avoid incidental formatting-only edits across Unity YAML files.
- Note any files that are generated, local-only, or ignored by `.gitignore`.

## Agent Workflow
1. Read this file and inspect the target area before editing.
2. Confirm scene, asset, and package context that could affect the task.
3. Make the smallest viable change.
4. Validate with console checks, scene inspection, or tests as appropriate.
5. Report changed files, validation performed, and any remaining risk.
