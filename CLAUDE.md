# FragmentsOfEternity — CLAUDE.md

Unity 2D mobile RPG. All edits must stay inside this repository root.

## Edit Scope
- **Allowed:** `C:\Users\Jorge Diogo\Documents\FragmentsOfEternity\**`
- **Forbidden:** `C:\Users\Jorge Diogo\Desktop\Ruflo\**`

Before changing any file, state exactly which files will be modified or created.
Prefer small, testable phases — one system at a time, QA sign-off before moving on.

---

## Swarm Hierarchy

| Agent ID | Role | Model | Owns |
|---|---|---|---|
| `unity-architect` | Queen / Orchestrator | Opus | Project structure, Scenes, Editor scripts, Core/ |
| `combat-systems-engineer` | Specialist | Sonnet | BattleManager, CombatController, Unit, SkillData, HeroData, status effects, cooldowns |
| `ui-ux-engineer` | Specialist | Sonnet | CombatHUD, TooltipUI, cooldown UI, target indicators, mobile readability |
| `vfx-game-feel-engineer` | Specialist | Sonnet | UnitVisual, screen shake, hit-stop, skill VFX, animation polish |
| `qa-test-engineer` | Specialist | Sonnet | Compile errors, runtime warnings, scene setup regressions, phase sign-off |
| `git-hygiene-engineer` | Specialist | Haiku | .gitignore, Editor folder rules, file placement, safe commits |

Swarm ID: `swarm-1777915743916-1fh1tu`  
Hive ID:  `hive-1777916194451-g1o9li`  
Topology: hierarchical-mesh (swarm) / hierarchical (hive)  
Consensus: byzantine

---

## Project Structure

```
Assets/
  Editor/
    CombatSceneSetup.cs       ← unity-architect
  Scenes/
    Boot.unity
    Combat.unity
    SampleScene.unity
  Scripts/
    Combat/
      BattleManager.cs        ← combat-systems-engineer
      CombatController.cs     ← combat-systems-engineer
      Unit.cs                 ← combat-systems-engineer
      UnitVisual.cs           ← vfx-game-feel-engineer
    Core/
      Bootstrap.cs            ← unity-architect
      EventBus.cs             ← unity-architect
      GameManager.cs          ← unity-architect
      SceneLoader.cs          ← unity-architect
    Data/
      HeroData.cs             ← combat-systems-engineer
      SkillData.cs            ← combat-systems-engineer
    UI/
      CombatHUD.cs            ← ui-ux-engineer
      TooltipUI.cs            ← ui-ux-engineer
  ScriptableObjects/
    Heroes/
    Skills/
  Prefabs/
  Art/
  Audio/
```

---

## Workflow Rules

1. **Phase gate:** QA/Test Engineer must sign off each phase before next phase begins.
2. **Ownership:** Only the owning agent edits its files. Cross-cutting changes need unity-architect approval.
3. **Scene automation:** Keep `CombatSceneSetup.cs` (Editor menu: RPG → Setup Combat Scene) compatible across phases.
4. **No writes outside edit scope.** Git/Hygiene Engineer audits before every commit.
5. **Paths with spaces** must always be double-quoted in shell commands.
6. **No Editor-folder scripts** outside `Assets/Editor/` — Unity will reject them at runtime builds.

---

## Phase Plan (suggested)

| # | Phase | Owner(s) |
|---|---|---|
| 1 | Audit existing scripts for compile errors | qa-test-engineer |
| 2 | Harden BattleManager turn loop + SkillData cooldowns | combat-systems-engineer |
| 3 | CombatHUD mobile layout + cooldown display | ui-ux-engineer |
| 4 | UnitVisual hit-stop + screen shake | vfx-game-feel-engineer |
| 5 | CombatSceneSetup automation validation | unity-architect |
| 6 | .gitignore audit + safe commit | git-hygiene-engineer |
