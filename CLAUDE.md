# Claude Working Guidelines — stacktower_game

## Language
- All code, comments, variable names, file names, and documentation: **English**
- Conversation with the user: **Turkish**

## Git
- **NEVER run git commands** (commit, push, pull, fetch, merge, rebase, etc.)
- When the user explicitly asks for a commit: run `git diff` only, then write the commit message text in chat for the user to copy

## Planning & Research
- For any non-trivial task: **plan first, code second**
- Present options/trade-offs to the user before starting implementation
- When uncertain about Unity APIs, third-party packages, or architecture: **use WebSearch before proposing a solution**
- Never start a large implementation without user approval of the plan

## Code Principles
- **KISS / YAGNI**: simple, focused code — no speculative abstractions
- **Single Responsibility**: one script = one job; split when a script grows beyond its purpose
- **State Pattern** for complex behaviors (enemies, game flow, UI flows)
- **C# Events / UnityEvents** for cross-system communication; avoid direct references between unrelated systems
- **ScriptableObjects** for all configurable data (stats, rewards, wave configs, shop items, etc.)
- **TextMeshPro** for all UI text — never use legacy Text component
- No error handling for impossible scenarios; validate only at system boundaries
- No backwards-compatibility shims for removed code — delete cleanly

## Portability
- **No hardcoded paths** — never use absolute or machine-specific paths anywhere in code or editor scripts
- Use `Application.dataPath`, `Application.persistentDataPath`, `Resources.Load<>()`, or `AssetDatabase` relative paths
- Asset references must be `[SerializeField]` drag-drop or loaded via `Resources`/`Addressables` — never constructed as string literals
- **No asset-dependent UI** — all UI is built in code; no prefab or sprite references that could break when assets change

## Mobile / Performance
- Target: low-end Android phones — assume 2 GB RAM, Mali/Adreno GPU, 60 fps budget
- **Object pooling** for anything spawned repeatedly (bullets, enemies, VFX, floating text) — use `UnityEngine.Pool.ObjectPool<T>`, never write custom pools
- No `FindObjectOfType` or `GameObject.Find` at runtime — cache all references in `Awake`/`Start`
- Avoid per-frame allocations: no LINQ in `Update`, no string concatenation in hot paths, use `StringBuilder` or cached strings
- Prefer `struct` over `class` for small, frequently created data (damage info, hit results)
- Texture atlases for UI sprites; keep draw calls minimal
- `[SerializeField] private` over `public` fields — avoid accidental coupling and inspector clutter

## Data & Save Integrity (Anti-Cheat)
- **All PlayerPrefs keys** must be defined as `const string` in a single `static class SaveKeys` — no inline string literals anywhere
- **All saved values** (coins, XP, high score, stones) must be written and read through integrity-checked wrappers (hash/checksum) — never raw `PlayerPrefs.SetInt`
- Critical runtime values (health, currency, score) must be **private fields** — never public, never exposed to external modification without a validated setter
- Never derive gameplay decisions from values that can be trivially patched in memory (e.g., never `if (coins > 0)` gating a purchase without server or checksum validation)
- Use **IL2CPP** scripting backend for release builds — never Mono for shipped APKs
- **No cheat codes, debug shortcuts, or admin flags** in production code — gate all debug tools behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`

## Naming Conventions
- Classes / SOs: `PascalCase`
- Private fields: `_camelCase`
- Public properties: `PascalCase`
- Constants: `UPPER_SNAKE_CASE`
- Events: `On` + PascalCase (e.g., `OnDied`, `OnWaveComplete`)
- Layer/tag constants: `static class Layers` and `static class Tags` — never raw strings or integers

## Architecture
- Systems communicate via events, not by finding each other with `FindObjectOfType`
- Managers are singletons only when truly global (GameManager, ScoreManager, AudioManager)
- Prefer composition over inheritance for game entities
- Data lives in ScriptableObjects; behaviour lives in MonoBehaviours
- **UIManager** only manages panel visibility; each major UI flow has its own script (`DailyRewardUI`, `ReviveUI`, `AbilityPickUI`, etc.)
- **Coroutines** for runtime gameplay async work; **async/await** for editor tools and I/O only

## Localization
- Use **Unity Localization package** for all user-facing strings
- No hardcoded UI text in code or ScriptableObjects — all strings go through the localization system
- Default locale: Turkish (`tr`); add others as needed

## Testing
- Pure logic (ScriptableObject data, formulas, state machines) must have **EditMode unit tests** in `Assets/_Game/Tests/EditMode/`
- Tests must not depend on scene objects or MonoBehaviour lifecycle
- User performs **manual Play Mode testing** for visual/gameplay verification
- Editor debug tools (e.g., `DailyRewardDebug.cs`) cover iteration shortcuts

## File & Folder Conventions
- Scripts: `Assets/_Game/Scripts/<Category>/ClassName.cs`
- Editor scripts: `Assets/_Game/Scripts/Editor/`
- ScriptableObjects assets: `Assets/_Game/Data/<Category>/`
- Prefabs: `Assets/_Game/Prefabs/<Category>/`
- Tests: `Assets/_Game/Tests/EditMode/`
- Localization tables: `Assets/_Game/Localization/`

## agent/ Folder
- `agent/GUIDELINES.md` — human-readable architecture & convention reference
- `agent/TASKS.md` — current active tasks / backlog (maintained by Claude when asked)
- `agent/PLAN_*.md` — per-feature plans, created before implementation begins
