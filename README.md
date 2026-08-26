# Parlance for Unity

A C# runtime for [Parlance](https://github.com/Orbitope/parlance), a git-native narrative design tool for story-driven games.

**Parlance is the authoring tool; this is the Unity runtime for what it produces.** You write your story in Parlance's visual editor — dialogue and quest canvases, a searchable reference index, live playtest — and it saves as human-readable JSON directly in your repo. No database, no import/export step, git as the single source of truth.

This package reads that JSON and runs it in Unity: dialogues, conditions, effects, skill checks, character dialogue ladders, quests, endings. It is verified against Parlance's published conformance vectors rather than against its author's confidence.

## Compatibility

| parlance-unity | Parlance spec | Families |
|---|---|---|
| `main` (unreleased) | v0.9.0 — pre-tag, pinned to [`1a4e657`](conformance/PIN) | 7 of 10 |

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

// 1. Initialize your project JSON and Game State
var projectDict = ParseMyJson(myParlanceJsonTextAsset.text);
var state = State.FromDict(mySavedGameStateJson);

// 2. Present a node: filters choices by showIf, interpolates {placeholders}.
var step = Runtime.StepDialogue(projectDict, "node_start", state);
Debug.Log(step["node"]["text"]);
foreach (var choice in (IEnumerable<object>)step["visibleChoices"])
{
    Debug.Log(choice["text"]);
}

// 3. onEnter effects are RETURNED, not applied. You decide when they fire —
// on first arrival, not on replay.
state = Runtime.ApplyEffects(step["onEnterEffects"], state, projectDict);

// 4. Take a choice. Pass a seeded RNG so checks are reproducible.
var outcome = Runtime.ChooseChoice(
    projectDict, 
    "node_start", 
    "ch_ask", 
    state, 
    Rng.ForStep(mySeed, stepIndex)
);
state = outcome["newState"];

if (outcome.ContainsKey("checkResult"))
{
    Debug.Log(outcome["checkResult"]); // passed, roll, total, skillValue, dice
}
var nextNodeId = outcome.GetValueOrDefault("nextNodeId"); // null on a terminal choice
```

### Important Notes
- **`onEnter` effects are not applied for you.** `StepDialogue` returns them; firing them is the caller's job, on first arrival only. Applying them on every render double-counts on a rewind.
- **A node with no choices is not necessarily over.** If it has `next`, call `AdvanceNode` — that's a listen-only beat. Treating it as the end silently truncates ambient chains.

## Verify it yourself

You can verify the runtime against the exact same conformance vectors used by the GDScript implementation. Since it's a pure C# library, you can run the NUnit tests from the command line without launching Unity:

```bash
cd Tests
dotnet test
```

## A worked example

[Mistfall Inn](https://github.com/Orbitope/mistfall-inn) is a small, complete murder mystery built on the Parlance engine. It's a useful reference for seeing the runtime driving real authored content rather than test fixtures.

## Licence

MIT, including the vendored spec files.
