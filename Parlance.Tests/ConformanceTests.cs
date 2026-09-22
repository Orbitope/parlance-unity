using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace Parlance.Tests
{
    public class ConformanceTests
    {
        private static string VectorDir => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../conformance"));

        private List<object> Load(string filename)
        {
            string path = Path.Combine(VectorDir, filename);
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            var doc = JsonDocument.Parse(text);
            return (List<object>)doc.RootElement.ToObject();
        }

        [Test]
        public void Rng()
        {
            var vectors = Load("rng.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                uint seed = Convert.ToUInt32(v["seed"]);
                var expected = (List<object>)v["outputs"];
                var stream = Parlance.Rng.Stream(seed);
                
                for (int i = 0; i < expected.Count; i++)
                {
                    float got = stream();
                    float want = Convert.ToSingle(expected[i]);
                    Assert.AreEqual(want, got, 1e-6f, $"seed={seed} index={i}");
                }
            }
        }

        [Test]
        public void Evaluate()
        {
            var vectors = Load("evaluate.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var cond = (Dictionary<string, object>)v["condition"];
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                var project = v.ContainsKey("project") ? (Dictionary<string, object>)v["project"] : new Dictionary<string, object>();
                
                bool got = Runtime.Evaluate(cond, state, project);
                bool want = (bool)v["expected"];
                Assert.AreEqual(want, got, (string)v["description"]);
            }
        }

        [Test]
        public void ApplyEffect()
        {
            var vectors = Load("apply_effect.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var eff = v["effect"];
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                var project = v.ContainsKey("project") ? (Dictionary<string, object>)v["project"] : new Dictionary<string, object>();
                
                var outState = Runtime.ApplyEffect(eff, state, project);
                var want = (Dictionary<string, object>)v["expected"];
                AssertDeepEqual(want, outState.ToDict(), (string)v["description"]);
            }
        }

        [Test]
        public void ResolveCheck()
        {
            var vectors = Load("resolve_check.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var check = (Dictionary<string, object>)v["check"];
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                object defaultDice = v.ContainsKey("defaultDice") ? v["defaultDice"] : null;
                bool criticals = v.ContainsKey("criticals") && (bool)v["criticals"];
                
                var rngSpec = v.ContainsKey("rng") ? v["rng"] : 0.0f;
                var rng = RngFrom(rngSpec);
                
                // A check with modifiers requires a project, as a real caller always
                // has one: the vector's when it carries one (quest state), else {}.
                bool hasModifiers = check.TryGetValue("modifiers", out var mods) && mods is List<object> modList && modList.Count > 0;
                var project = v.ContainsKey("project") ? (Dictionary<string, object>)v["project"]
                    : hasModifiers ? new Dictionary<string, object>() : null;

                var got = Runtime.ResolveCheck(check, state, rng, defaultDice, criticals, project);
                var want = (Dictionary<string, object>)v["expected"];
                AssertDeepEqual(want, got, (string)v["description"]);
            }
        }

        [Test]
        public void StepDialogue()
        {
            var vectors = Load("step_dialogue.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var diag = (Dictionary<string, object>)v["dialogue"];
                var nodeId = (string)v["nodeId"];
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                var project = v.ContainsKey("project") ? (Dictionary<string, object>)v["project"] : new Dictionary<string, object>();
                
                var outDict = Runtime.StepDialogue(diag, nodeId, state, project);
                if (outDict.ContainsKey("error"))
                {
                    Assert.Fail($"unexpected error: {outDict["error"]} - {(string)v["description"]}");
                }
                
                var visibleChoices = (List<object>)outDict["visibleChoices"];
                var ids = new List<object>();
                foreach (var choiceObj in visibleChoices)
                {
                    var choice = (Dictionary<string, object>)choiceObj;
                    ids.Add(choice.ContainsKey("id") ? choice["id"] : null);
                }
                var onEnterEffects = (IEnumerable<object>)outDict["onEnterEffects"];

                var onEnterList = new List<object>();
                foreach (var e in onEnterEffects) onEnterList.Add(e);

                var outNode = (Dictionary<string, object>)outDict["node"];

                var got = new Dictionary<string, object>
                {
                    { "nodeId", outNode.TryGetValue("id", out var idObj) ? idObj : null },
                    { "visibleChoiceIds", ids },
                    { "onEnterEffectCount", onEnterList.Count },
                    { "onEnterEffects", onEnterList }
                };
                AssertDeepEqual((Dictionary<string, object>)v["expected"], got, (string)v["description"]);
            }
        }

        [Test]
        public void ChooseChoice()
        {
            var vectors = Load("choose_choice.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var diag = (Dictionary<string, object>)v["dialogue"];
                var nodeId = (string)v["nodeId"];
                var choiceId = (string)v["choiceId"];
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                var project = v.ContainsKey("project") ? (Dictionary<string, object>)v["project"] : new Dictionary<string, object>();
                
                var rngSpec = v.ContainsKey("rng") ? v["rng"] : 0.0f;
                var rng = RngFrom(rngSpec);
                
                var outDict = Runtime.ChooseChoice(diag, nodeId, choiceId, state, project, rng);
                if (outDict.ContainsKey("error"))
                {
                    Assert.Fail($"unexpected error: {outDict["error"]} - {(string)v["description"]}");
                }
                
                var actual = new Dictionary<string, object>
                {
                    { "nextNodeId", outDict["nextNodeId"] },
                    { "newState", ((State)outDict["newState"]).ToDict() }
                };
                if (outDict.ContainsKey("checkResult"))
                {
                    actual["checkResult"] = outDict["checkResult"];
                }
                
                AssertDeepEqual((Dictionary<string, object>)v["expected"], actual, (string)v["description"]);
            }
        }

        [Test]
        public void AdvanceNode()
        {
            var vectors = Load("advance.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var diag = (Dictionary<string, object>)v["dialogue"];
                var nodeId = (string)v["nodeId"];
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                
                var outDict = Runtime.AdvanceNode(diag, nodeId, state);
                
                if (v.ContainsKey("expectedError"))
                {
                    string expectedErr = (string)v["expectedError"];
                    if (!outDict.ContainsKey("error"))
                        Assert.Fail($"expected error containing {expectedErr}, but succeeded - {(string)v["description"]}");
                    string errStr = outDict["error"].ToString();
                    Assert.IsTrue(errStr.Contains(expectedErr), $"error {errStr} does not contain {expectedErr}");
                    continue;
                }
                
                if (outDict.ContainsKey("error"))
                {
                    Assert.Fail($"unexpected error: {outDict["error"]} - {(string)v["description"]}");
                }
                
                var actual = new Dictionary<string, object>
                {
                    { "nextNodeId", outDict["nextNodeId"] },
                    { "newState", ((State)outDict["newState"]).ToDict() }
                };
                AssertDeepEqual((Dictionary<string, object>)v["expected"], actual, (string)v["description"]);
            }
        }

        [Test]
        public void ResolveCharacterDialogue()
        {
            var vectors = Load("resolveCharacterDialogue.json");
            Assert.NotNull(vectors);
            foreach (var vObj in vectors)
            {
                var v = (Dictionary<string, object>)vObj;
                var state = State.FromDict((Dictionary<string, object>)v["state"]);
                var character = (Dictionary<string, object>)v["character"];
                var project = v.ContainsKey("project") ? (Dictionary<string, object>)v["project"] : new Dictionary<string, object>();
                
                var visited = v.ContainsKey("visited") ? (List<object>)v["visited"] : null;
                
                var got = Runtime.ResolveCharacterDialogue(state, character, project, visited);
                var expected = v["expected"];
                
                AssertDeepEqual(expected, got, (string)v["description"]);
            }
        }

        private Func<float> RngFrom(object spec)
        {
            List<object> values = spec is List<object> l ? l : new List<object> { spec };
            int i = 0;
            return () =>
            {
                float val = i < values.Count ? Convert.ToSingle(values[i]) : 0f;
                i++;
                return val;
            };
        }

        private void AssertDeepEqual(object expected, object actual, string message)
        {
            if (DeepEqual(expected, actual)) return;
            Assert.Fail($"{message}\ngot: {JsonSerializer.Serialize(actual)}\nwant: {JsonSerializer.Serialize(expected)}");
        }

        private bool DeepEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            
            if (a is bool b1 && b is bool b2) return b1 == b2;
            
            bool isANum = a is int || a is float || a is double || a is long;
            bool isBNum = b is int || b is float || b is double || b is long;
            if (isANum && isBNum) return Math.Abs(Convert.ToSingle(a) - Convert.ToSingle(b)) < 1e-5f;
            
            if (a is string s1 && b is string s2) return s1 == s2;
            
            if (a is System.Collections.IDictionary aDict && b is System.Collections.IDictionary bDict)
            {
                if (aDict.Count != bDict.Count) return false;
                foreach (System.Collections.DictionaryEntry kvp in aDict)
                {
                    if (!bDict.Contains(kvp.Key)) return false;
                    if (!DeepEqual(kvp.Value, bDict[kvp.Key])) return false;
                }
                return true;
            }
            
            if (a is System.Collections.IEnumerable aList && b is System.Collections.IEnumerable bList)
            {
                var aEnum = aList.GetEnumerator();
                var bEnum = bList.GetEnumerator();
                while (true)
                {
                    bool aHas = aEnum.MoveNext();
                    bool bHas = bEnum.MoveNext();
                    if (aHas != bHas) return false;
                    if (!aHas) break;
                    if (!DeepEqual(aEnum.Current, bEnum.Current)) return false;
                }
                return true;
            }
            
            return object.Equals(a, b);
        }
    }
}
