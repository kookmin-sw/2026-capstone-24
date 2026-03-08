@AGENTS.md

# Claude Project Notes

## Purpose
- This file adds Claude-specific guidance on top of the shared project rules in `AGENTS.md`.
- Keep shared project instructions in `AGENTS.md`; only keep Claude-specific workflow notes here.

## Claude Usage
- Use Unity MCP tools first for scene, GameObject, component, asset, and console inspection when that is faster or safer than editing serialized files manually.
- Prefer reading only the files needed for the current task to keep context small.
- When a task becomes repetitive or highly specialized, create a focused skill or subagent instead of expanding this file with long procedural instructions.

## Candidate Claude Specializations
- Unity scene inspection and hierarchy review.
- XR rig and interaction wiring checks.
- Console-driven debugging after scene or prefab changes.
- Test execution and failure triage when Unity tests are added.

## Keep Out Of Repo Memory
- Personal preferences, machine-specific paths, temporary experiments, and one-off debugging notes should stay in local Claude memory, not in this file.
