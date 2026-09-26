# Parlance for Unity

A C# runtime for [Parlance](https://github.com/Orbitope/parlance), a git-native narrative design tool for story-driven games.

**Parlance is the authoring tool; this is the Unity runtime for what it produces.** You write your story in Parlance's visual editor — dialogue and quest canvases, a searchable reference index, live playtest — and it saves as human-readable JSON directly in your repo. No database, no import/export step, git as the single source of truth.

This package reads that JSON and runs it in Unity: dialogues (conditional lines, fallback and locked choices, line and choice tags, engine commands), conditions, effects, skill checks (with conditional modifiers), character dialogue offers, quests, endings. It is verified against Parlance's published conformance vectors rather than against its author's confidence.

## Compatibility

| parlance-unity | Parlance spec | Families |
|---|---|---|
| `main` (unreleased) | v0.15.0 — pinned to [`a3c6454`](conformance/PIN) | 11 of 11 — 207 / 207 vectors |

[`conformance/PIN`](conformance/PIN) is the authoritative record of which upstream ref the vectors came from.

## Install

Install this package via the Unity Package Manager (UPM).

1. In Unity, open **Window > Package Manager**.
2. Click the **+** icon in the top left and select **Add package from git URL...**
3. Enter the URL of this repository (e.g., `https://github.com/Orbitope/parlance-unity.git`).

Since the runtime is composed of pure C# static classes, there are no `MonoBehaviour` singletons to manage or add to your scene.

## Use

Load your JSON using Unity's `TextAsset` or `System.IO.File`, parse it into a `Dictionary<string, object>` (using your preferred JSON library, such as `System.Text.Json` or `Newtonsoft.Json`), and pass it to the runtime. 

State is immutable: every entry point returns a new state and never mutates its input.

```csharp
using Parlance;

// 1. Initialize your project JSON and Game State. The project is the whole
// data set (rules, factions, quests, …); a dialogue is one entry in it.
var projectDict = ParseMyJson(myParlanceJsonTextAsset.text);
var dialogues = (Dictionary<string, object>)projectDict["dialogues"];
var dialogue = (Dictionary<string, object>)dialogues["d_intro"];
var state = State.FromDict(mySavedGameStateJson);

// 2. Present a node: resolves conditional nodes, partitions the choices,
// interpolates {placeholders}. Every entry point returns { "error": … }
// instead of throwing when the call is invalid.
var step = Runtime.StepDialogue(dialogue, "node_start", state, projectDict);
var node = (Dictionary<string, object>)step["node"];
string nodeId = (string)node["id"];              // the RESOLVED node: may differ from the id you asked for
if (!(bool)step["textHidden"] && node.TryGetValue("text", out var line))
{
    Debug.Log(line);                             // no line when hidden, or on a text-less choice node
}
foreach (Dictionary<string, object> choice in (List<object>)step["visibleChoices"])
{
    Debug.Log(choice["text"]);                   // selectable
}
foreach (Dictionary<string, object> choice in (List<object>)step["lockedChoices"])
{
    // Opt-in: failing choices authored whenLocked: "show". Render greyed out,
    // with lockedText in place of text when set. Never selectable.
    Debug.Log(choice.TryGetValue("lockedText", out var lt) ? lt : choice["text"]);
}

// 3. onEnter effects are RETURNED, not applied. You decide when they fire —
// on first arrival, not on replay. Walk them in order and dispatch any
// `engine` effect yourself: ApplyEffect leaves state unchanged for it.
var onEnter = (IEnumerable<object>)step["onEnterEffects"];
foreach (Dictionary<string, object> effect in onEnter)
{
    if ((string)effect["type"] == "engine") MyEngine.Dispatch((string)effect["command"], effect.GetValueOrDefault("args"));
}
state = Runtime.ApplyEffects(onEnter, state, projectDict);

// 4. Take a choice. Pass a seeded RNG so checks are reproducible.
var outcome = Runtime.ChooseChoice(
    dialogue,
    nodeId,
    "ch_ask",
    state,
    projectDict,
    Rng.ForStep(mySeed, stepIndex)
);
if (outcome.ContainsKey("error")) throw new InvalidOperationException((string)outcome["error"]);
state = (State)outcome["newState"];

if (outcome.ContainsKey("checkResult"))
{
    Debug.Log(outcome["checkResult"]); // passed, roll, total, skillValue, dice (+ bonus, appliedModifiers)
}
var nextNodeId = outcome.GetValueOrDefault("nextNodeId"); // null on a terminal choice; resolve it with the next StepDialogue
```

### Important Notes
- **A gated node with choices or `isEnd` is not skipped — only its line is hidden.** `StepDialogue` returns it with `textHidden: true` and `node.text == ""`; its choices are offered, an `isEnd` node still ends the dialogue, and its `onEnter` still fires. Only an interstitial gated node (no choices, not `isEnd`) is skipped, through `next`. `StepDialogue` and `AdvanceNode` share one `ResolveNode` walk.
- **Resolve once per arrival, on the arrival state.** If you apply `onEnter` before presenting, resolve with `ResolveNode` and present with `StepResolvedNode(node, postEffectState, project, arrivalState)` so `textHidden` is judged on arrival while choices filter on the post-effect state. Never re-resolve a node the player has already seen.
- **`ChooseChoice` only accepts a choice that was offered.** A hidden choice, a locked choice, or a `fallback` while a non-fallback choice is visible returns an `error` containing `not selectable`. A fallback is offered only when no other choice is visible.
- **Tags pass through untouched.** `node["tags"]` and each visible choice's `tags` come back exactly as authored; hang animation, audio or camera cues off them.
- **`onEnter` effects are not applied for you.** `StepDialogue` returns them; firing them is the caller's job, on first arrival only. Applying them on every render double-counts on a rewind.
- **A node with no choices is not necessarily over.** If it has `next`, call `AdvanceNode` — that's a listen-only beat. Treating it as the end silently truncates ambient chains.
- **Passive checks never roll.** Show or hide a passive-check choice with `PassiveCheckPasses` (`skill + Σbonus >= difficulty`), so a check modifier means the same thing in both modes.
- **Run `ResolveQuests` after every state change.** It fires quest stage `onComplete` and outcome `effects` whose condition holds, once each, and records them in `QuestFired`. It never advances a stage for you.
- **Levelling is derived, not stored.** `Xp` is total-earned; `LevelForXp`, `PointsEarned` and `AvailablePoints` derive from it. `InvestSkillPoint` is a guarded player action, not an effect; call `RecomputeSkills` on load so checks read preset + invested skill.
- **When a scene ends, ask `NextContinuations`.** A pending cutscene comes first, then any character routed with `set_active_dialogue`, otherwise each character's best offer. Consume them with `ClearPendingCutscene` / `ClearActiveDialogue`.
- **Which dialogue a character opens is `ResolveCharacterDialogue`.** It ranks the dialogues whose `offer` names that character (priority tier, then condition specificity, then id); pass your visited set so non-replayable dialogues are not offered twice.

## Verify it yourself

You can verify the runtime against the exact same conformance vectors used by the GDScript implementation. Since it's a pure C# library, you can run the NUnit tests from the command line without launching Unity:

```bash
cd Parlance.Tests
dotnet test
```

## A worked example

[Mistfall Inn](https://github.com/Orbitope/mistfall-inn) is a small, complete murder mystery built on the Parlance engine. It's a useful reference for seeing the runtime driving real authored content rather than test fixtures.

## Licence

MIT, including the vendored spec files.
