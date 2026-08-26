using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlance
{
    public static class Runtime
    {
        // ---------------------------------------------------------------- evaluate --
        public static bool Evaluate(Dictionary<string, object> condition, State state, Dictionary<string, object> project = null)
        {
            if (project == null) project = new Dictionary<string, object>();
            return _Evaluate(condition, state, project, new Dictionary<string, bool>());
        }

        private static bool _Evaluate(object conditionObj, State state, Dictionary<string, object> project, Dictionary<string, bool> visiting)
        {
            if (!(conditionObj is Dictionary<string, object> condition)) return false;
            
            string type = condition.TryGetValue("type", out var typeObj) && typeObj is string t ? t : "";
            switch (type)
            {
                case "flag":
                {
                    string flag = condition.TryGetValue("flag", out var f) && f is string fStr ? fStr : "";
                    bool expectedValue = condition.TryGetValue("value", out var v) && v is bool vBool ? vBool : true;
                    bool actualValue = state.Flags.TryGetValue(flag, out var a) ? a : false;
                    return actualValue == expectedValue;
                }
                case "reputation":
                {
                    string faction = condition.TryGetValue("faction", out var f) && f is string fStr ? fStr : "";
                    string op = condition.TryGetValue("op", out var o) && o is string oStr ? oStr : "==";
                    float val = condition.TryGetValue("value", out var v) ? Convert.ToSingle(v) : 0f;
                    float actual = state.Reputation.TryGetValue(faction, out var a) ? a : 0f;
                    return Compare(actual, op, val);
                }
                case "skill":
                {
                    string skill = condition.TryGetValue("skill", out var s) && s is string sStr ? sStr : "";
                    string op = condition.TryGetValue("op", out var o) && o is string oStr ? oStr : "==";
                    float val = condition.TryGetValue("value", out var v) ? Convert.ToSingle(v) : 0f;
                    float actual = state.Skills.TryGetValue(skill, out var a) ? a : 0f;
                    return Compare(actual, op, val);
                }
                case "counter":
                {
                    string counter = condition.TryGetValue("counter", out var c) && c is string cStr ? cStr : "";
                    string op = condition.TryGetValue("op", out var o) && o is string oStr ? oStr : "==";
                    float val = condition.TryGetValue("value", out var v) ? Convert.ToSingle(v) : 0f;
                    float actual = state.Counters.TryGetValue(counter, out var a) ? a : 0f;
                    return Compare(actual, op, val);
                }
                case "relationship":
                {
                    string character = condition.TryGetValue("character", out var c) && c is string cStr ? cStr : "";
                    string op = condition.TryGetValue("op", out var o) && o is string oStr ? oStr : "==";
                    float val = condition.TryGetValue("value", out var v) ? Convert.ToSingle(v) : 0f;
                    float actual = state.Relationships.TryGetValue(character, out var a) ? a : 0f;
                    return Compare(actual, op, val);
                }
                case "item":
                {
                    string item = condition.TryGetValue("item", out var i) && i is string iStr ? iStr : "";
                    bool has = condition.TryGetValue("has", out var h) && h is bool hBool ? hBool : true;
                    bool actual = state.Inventory.Contains(item);
                    return actual == has;
                }
                case "quest":
                    return EvaluateQuest(condition, state, project);
                case "questOutcome":
                    return EvaluateQuestOutcome(condition, state, project, visiting);
                case "all":
                {
                    if (condition.TryGetValue("of", out var ofObj) && ofObj is IEnumerable<object> ofList)
                    {
                        foreach (var sub in ofList)
                        {
                            if (!_Evaluate(sub, state, project, visiting)) return false;
                        }
                    }
                    return true;
                }
                case "any":
                {
                    if (condition.TryGetValue("of", out var ofObj) && ofObj is IEnumerable<object> ofList)
                    {
                        foreach (var sub in ofList)
                        {
                            if (_Evaluate(sub, state, project, visiting)) return true;
                        }
                    }
                    return false;
                }
                case "not":
                {
                    condition.TryGetValue("of", out var ofObj);
                    return !_Evaluate(ofObj, state, project, visiting);
                }
            }
            return false;
        }

        private static bool EvaluateQuest(Dictionary<string, object> condition, State state, Dictionary<string, object> project)
        {
            string questId = condition.TryGetValue("quest", out var q) && q is string qStr ? qStr : "";
            
            Dictionary<string, object> quests = project.TryGetValue("quests", out var qsObj) && qsObj is Dictionary<string, object> qsDict ? qsDict : new Dictionary<string, object>();
            
            if (!quests.TryGetValue(questId, out var questObj) || !(questObj is Dictionary<string, object> quest)) return false;
            
            string targetStage = condition.TryGetValue("stage", out var s) && s is string sStr ? sStr : "";
            int? targetOrder = StageOrder(quest, targetStage);
            if (targetOrder == null) return false;

            int currentOrder = -1;
            if (state.QuestStages.TryGetValue(questId, out var currentStage))
            {
                int? found = StageOrder(quest, currentStage);
                if (found != null) currentOrder = found.Value;
            }

            string op = condition.TryGetValue("op", out var o) && o is string oStr ? oStr : "==";
            return Compare(currentOrder, op, targetOrder.Value);
        }

        private static int? StageOrder(Dictionary<string, object> quest, string stageId)
        {
            if (quest.TryGetValue("stages", out var stagesObj) && stagesObj is IEnumerable<object> stagesList)
            {
                foreach (var sObj in stagesList)
                {
                    if (sObj is Dictionary<string, object> stage && stage.TryGetValue("id", out var idObj) && idObj is string idStr && idStr == stageId)
                    {
                        if (stage.TryGetValue("order", out var orderObj))
                        {
                            return Convert.ToInt32(orderObj);
                        }
                        return 0;
                    }
                }
            }
            return null;
        }

        private static bool EvaluateQuestOutcome(Dictionary<string, object> condition, State state, Dictionary<string, object> project, Dictionary<string, bool> visiting)
        {
            string questId = condition.TryGetValue("quest", out var q) && q is string qStr ? qStr : "";
            string outcomeId = condition.TryGetValue("outcome", out var o) && o is string oStr ? oStr : "";

            string key = $"{questId}/{outcomeId}";
            if (visiting.ContainsKey(key)) return false;

            Dictionary<string, object> quests = project.TryGetValue("quests", out var qsObj) && qsObj is Dictionary<string, object> qsDict ? qsDict : new Dictionary<string, object>();
            if (!quests.TryGetValue(questId, out var questObj) || !(questObj is Dictionary<string, object> quest)) return false;

            if (quest.TryGetValue("outcomes", out var outcomesObj) && outcomesObj is IEnumerable<object> outcomesList)
            {
                foreach (var ocObj in outcomesList)
                {
                    if (ocObj is Dictionary<string, object> outcome && outcome.TryGetValue("id", out var idObj) && idObj is string idStr && idStr == outcomeId)
                    {
                        if (!outcome.TryGetValue("reachedWhen", out var reachedWhenObj)) return false;
                        var guard = new Dictionary<string, bool>(visiting) { [key] = true };
                        return _Evaluate(reachedWhenObj, state, project, guard);
                    }
                }
            }
            return false;
        }

        private static bool Compare(float left, string op, float right)
        {
            switch (op)
            {
                case ">=": return left >= right;
                case "<=": return left <= right;
                case "==": return left == right;
                case ">": return left > right;
                case "<": return left < right;
            }
            return false;
        }

        // ------------------------------------------------------------- applyEffect --
        
        public static State ApplyEffect(object effectObj, State state, Dictionary<string, object> project = null)
        {
            if (project == null) project = new Dictionary<string, object>();
            State next = state.Copy();
            
            if (!(effectObj is Dictionary<string, object> effect)) return next;

            string type = effect.TryGetValue("type", out var typeObj) && typeObj is string t ? t : "";
            switch (type)
            {
                case "set_flag":
                {
                    string flag = effect.TryGetValue("flag", out var f) && f is string fStr ? fStr : "";
                    bool value = effect.TryGetValue("value", out var v) && v is bool vBool ? vBool : true;
                    next.Flags[flag] = value;
                    break;
                }
                case "adjust_reputation":
                {
                    string factionId = effect.TryGetValue("faction", out var f) && f is string fStr ? fStr : "";
                    float raw = next.Reputation.TryGetValue(factionId, out var a) ? a : 0f;
                    raw += effect.TryGetValue("delta", out var d) ? Convert.ToSingle(d) : 0f;
                    
                    Dictionary<string, object> factions = project.TryGetValue("factions", out var fObj) && fObj is Dictionary<string, object> fDict ? fDict : new Dictionary<string, object>();
                    if (factions.TryGetValue(factionId, out var factionObj) && factionObj is Dictionary<string, object> faction)
                    {
                        if (faction.TryGetValue("reputationRange", out var rrObj) && rrObj is Dictionary<string, object> r)
                        {
                            float min = r.TryGetValue("min", out var minObj) ? Convert.ToSingle(minObj) : float.NegativeInfinity;
                            float max = r.TryGetValue("max", out var maxObj) ? Convert.ToSingle(maxObj) : float.PositiveInfinity;
                            raw = Math.Max(min, Math.Min(max, raw));
                        }
                    }
                    next.Reputation[factionId] = raw;
                    break;
                }
                case "adjust_counter":
                {
                    string counterId = effect.TryGetValue("counter", out var c) && c is string cStr ? cStr : "";
                    float raw = next.Counters.TryGetValue(counterId, out var a) ? a : 0f;
                    raw += effect.TryGetValue("delta", out var d) ? Convert.ToSingle(d) : 0f;
                    next.Counters[counterId] = raw;
                    break;
                }
                case "adjust_relationship":
                {
                    string characterId = effect.TryGetValue("character", out var c) && c is string cStr ? cStr : "";
                    float raw = next.Relationships.TryGetValue(characterId, out var a) ? a : 0f;
                    raw += effect.TryGetValue("delta", out var d) ? Convert.ToSingle(d) : 0f;
                    next.Relationships[characterId] = raw;
                    break;
                }
                case "give_item":
                {
                    string itemId = effect.TryGetValue("item", out var i) && i is string iStr ? iStr : "";
                    next.Inventory.Add(itemId);
                    break;
                }
                case "take_item":
                {
                    string itemId = effect.TryGetValue("item", out var i) && i is string iStr ? iStr : "";
                    next.Inventory.Remove(itemId);
                    break;
                }
                case "advance_quest":
                {
                    string questId = effect.TryGetValue("quest", out var q) && q is string qStr ? qStr : "";
                    string toStage = effect.TryGetValue("toStage", out var s) && s is string sStr ? sStr : "";
                    next.QuestStages[questId] = toStage;
                    break;
                }
                case "grant_xp":
                {
                    float amount = effect.TryGetValue("amount", out var a) ? Convert.ToSingle(a) : 0f;
                    next.Xp += amount;
                    break;
                }
                case "set_active_dialogue":
                {
                    string characterId = effect.TryGetValue("character", out var c) && c is string cStr ? cStr : "";
                    next.Flags["active_dialogue__" + characterId] = true;
                    break;
                }
                case "play_cutscene":
                {
                    string cutscene = effect.TryGetValue("cutscene", out var c) && c is string cStr ? cStr : "";
                    next.PendingCutscene = cutscene;
                    break;
                }
                case "set_text":
                {
                    string variable = effect.TryGetValue("variable", out var v) && v is string vStr ? vStr : "";
                    string value = effect.TryGetValue("value", out var val) && val is string valStr ? valStr : "";
                    next.Texts[variable] = value;
                    break;
                }
            }
            return next;
        }

        public static State ApplyEffects(IEnumerable<object> effects, State state, Dictionary<string, object> project = null)
        {
            if (project == null) project = new Dictionary<string, object>();
            State next = state;
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    next = ApplyEffect(effect, next, project);
                }
            }
            return next;
        }
        
        // ------------------------------------------------------------ resolveCheck --

        public static Dictionary<string, object> ResolveCheck(Dictionary<string, object> check, State state, Func<float> rng, object defaultDice = null, bool criticals = false)
        {
            object diceObj = check.TryGetValue("dice", out var dObj) ? dObj : defaultDice;
            string notation = diceObj != null ? diceObj.ToString() : "1d20";
            var spec = ParseDice(notation);

            var faces = new List<int>();
            for (int i = 0; i < spec.n; i++)
            {
                faces.Add((int)Math.Floor(rng() * spec.m) + 1);
            }

            int roll = 0;
            foreach (var face in faces) roll += face;

            string skill = check.TryGetValue("skill", out var s) && s is string sStr ? sStr : "";
            float skillValue = state.Skills.TryGetValue(skill, out var sv) ? sv : 0f;
            float total = roll + skillValue;
            
            float difficulty = check.TryGetValue("difficulty", out var diffObj) ? Convert.ToSingle(diffObj) : 0f;
            bool passed = total >= difficulty;

            var result = new Dictionary<string, object>
            {
                { "passed", passed },
                { "roll", roll },
                { "total", total },
                { "skillValue", skillValue },
                { "dice", $"{spec.n}d{spec.m}" }
            };

            if (criticals)
            {
                bool allMax = true;
                bool allMin = true;
                foreach (var face in faces)
                {
                    if (face != spec.m) allMax = false;
                    if (face != 1) allMin = false;
                }
                if (allMax)
                {
                    result["passed"] = true;
                    result["critical"] = "success";
                }
                else if (allMin)
                {
                    result["passed"] = false;
                    result["critical"] = "failure";
                }
            }

            return result;
        }

        private static (int n, int m) ParseDice(string notation)
        {
            var parts = notation.Trim().ToLower().Split('d');
            if (parts.Length == 2 && int.TryParse(parts[0], out int n) && int.TryParse(parts[1], out int m))
            {
                if (n >= 1 && m >= 2) return (n, m);
            }
            return (1, 20);
        }
        
        // ----------------------------------------------------------- stepDialogue --
        
        public static Dictionary<string, object> StepDialogue(Dictionary<string, object> dialogue, string nodeId, State state, Dictionary<string, object> project = null)
        {
            if (project == null) project = new Dictionary<string, object>();
            
            var node = FindNode(dialogue, nodeId);
            if (node == null)
            {
                string diagId = dialogue.TryGetValue("id", out var dId) ? dId.ToString() : "?";
                return new Dictionary<string, object> { { "error", $"node '{nodeId}' does not exist in dialogue '{diagId}'" } };
            }

            var visible = new List<object>();
            if (node.TryGetValue("choices", out var choicesObj) && choicesObj is IEnumerable<object> choicesList)
            {
                foreach (var choiceObj in choicesList)
                {
                    if (!(choiceObj is Dictionary<string, object> choice)) continue;
                    if (choice.TryGetValue("showIf", out var showIfObj) && !Evaluate(showIfObj as Dictionary<string, object> ?? new Dictionary<string, object>(), state, project)) continue;
                    
                    string text = choice.TryGetValue("text", out var tObj) ? tObj.ToString() : "";
                    string rendered = Interpolate.Process(text, state);
                    
                    if (rendered == text)
                    {
                        visible.Add(choice);
                    }
                    else
                    {
                        var c = new Dictionary<string, object>(choice);
                        c["text"] = rendered;
                        visible.Add(c);
                    }
                }
            }
            
            var outNode = node;
            string nodeText = node.TryGetValue("text", out var nTextObj) ? nTextObj.ToString() : "";
            string renderedText = Interpolate.Process(nodeText, state);
            if (renderedText != nodeText)
            {
                outNode = new Dictionary<string, object>(node);
                outNode["text"] = renderedText;
            }

            return new Dictionary<string, object>
            {
                { "node", outNode },
                { "visibleChoices", visible },
                { "onEnterEffects", node.TryGetValue("onEnter", out var onEnter) ? onEnter : new List<object>() }
            };
        }

        // ----------------------------------------------------------- chooseChoice --
        
        public static Dictionary<string, object> ChooseChoice(Dictionary<string, object> dialogue, string nodeId, string choiceId, State state, Dictionary<string, object> project = null, Func<float> rng = null)
        {
            if (project == null) project = new Dictionary<string, object>();
            
            var node = FindNode(dialogue, nodeId);
            if (node == null)
            {
                string diagId = dialogue.TryGetValue("id", out var dId) ? dId.ToString() : "?";
                return new Dictionary<string, object> { { "error", $"node '{nodeId}' does not exist in dialogue '{diagId}'" } };
            }

            Dictionary<string, object> choice = null;
            if (node.TryGetValue("choices", out var choicesObj) && choicesObj is IEnumerable<object> choicesList)
            {
                foreach (var cObj in choicesList)
                {
                    if (cObj is Dictionary<string, object> c && c.TryGetValue("id", out var idObj) && idObj is string idStr && idStr == choiceId)
                    {
                        choice = c;
                        break;
                    }
                }
            }
            if (choice == null)
            {
                return new Dictionary<string, object> { { "error", $"choice '{choiceId}' does not exist on node '{nodeId}'" } };
            }

            IEnumerable<object> effects = choice.TryGetValue("effects", out var effObj) && effObj is IEnumerable<object> effList ? effList : new List<object>();
            State nextState = ApplyEffects(effects, state, project);

            if (choice.TryGetValue("check", out var checkObj) && checkObj is Dictionary<string, object> check)
            {
                if (check.TryGetValue("mode", out var mObj) && mObj is string mStr && mStr == "active")
                {
                    Dictionary<string, object> rules = project.TryGetValue("rules", out var rObj) && rObj is Dictionary<string, object> rDict ? rDict : new Dictionary<string, object>();
                    Dictionary<string, object> checkRules = rules.TryGetValue("check", out var crObj) && crObj is Dictionary<string, object> crDict ? crDict : new Dictionary<string, object>();
                    
                    object defaultDice = checkRules.TryGetValue("dice", out var ddObj) ? ddObj : null;
                    bool criticals = checkRules.TryGetValue("criticals", out var critObj) && critObj is bool cBool ? cBool : false;
                    
                    var result = ResolveCheck(check, nextState, rng, defaultDice, criticals);
                    bool passed = (bool)result["passed"];
                    
                    object nextNodeId = null;
                    if (passed)
                    {
                        check.TryGetValue("onSuccess", out nextNodeId);
                    }
                    else
                    {
                        check.TryGetValue("onFailure", out nextNodeId);
                    }
                    
                    return new Dictionary<string, object>
                    {
                        { "nextNodeId", nextNodeId },
                        { "newState", nextState },
                        { "checkResult", result }
                    };
                }
            }

            object gotoNodeId = null;
            choice.TryGetValue("goto", out gotoNodeId);
            return new Dictionary<string, object>
            {
                { "nextNodeId", gotoNodeId },
                { "newState", nextState }
            };
        }

        // ------------------------------------------------------------ advanceNode --
        
        public static Dictionary<string, object> AdvanceNode(Dictionary<string, object> dialogue, string nodeId, State state)
        {
            var node = FindNode(dialogue, nodeId);
            if (node == null)
            {
                string diagId = dialogue.TryGetValue("id", out var dId) ? dId.ToString() : "?";
                return new Dictionary<string, object> { { "error", $"node '{nodeId}' does not exist in dialogue '{diagId}'" } };
            }

            if (!node.ContainsKey("next"))
            {
                return new Dictionary<string, object> { { "error", $"node '{nodeId}' has no 'next' to advance from" } };
            }

            object targetIdObj = node["next"];
            string targetId = targetIdObj != null ? targetIdObj.ToString() : null;
            if (FindNode(dialogue, targetId) == null)
            {
                string diagId = dialogue.TryGetValue("id", out var dId) ? dId.ToString() : "?";
                return new Dictionary<string, object> { { "error", $"next target '{targetId}' does not exist in dialogue '{diagId}'" } };
            }

            return new Dictionary<string, object>
            {
                { "nextNodeId", targetId },
                { "newState", state }
            };
        }
        
        // ------------------------------------------- resolveCharacterDialogue (feed) --
        
        public static object ResolveCharacterDialogue(State state, Dictionary<string, object> character, Dictionary<string, object> project = null)
        {
            if (project == null) project = new Dictionary<string, object>();
            
            if (character.TryGetValue("dialogues", out var dialoguesObj) && dialoguesObj is IEnumerable<object> dialoguesList)
            {
                foreach (var rungObj in dialoguesList)
                {
                    if (!(rungObj is Dictionary<string, object> rung)) continue;
                    if (rung.TryGetValue("showIf", out var showIfObj))
                    {
                        if (!Evaluate(showIfObj as Dictionary<string, object> ?? new Dictionary<string, object>(), state, project)) continue;
                    }
                    return rung.TryGetValue("dialogue", out var diagObj) ? diagObj : null;
                }
            }
            return null;
        }

        // ------------------------------------------------------ speaker / portrait --
        
        public static object EffectiveSpeakerId(Dictionary<string, object> dialogue, Dictionary<string, object> node)
        {
            if (node.TryGetValue("speakerId", out var s1)) return s1;
            if (dialogue.TryGetValue("speakerId", out var s2)) return s2;
            return null;
        }

        public static Dictionary<string, object> ResolveSpeaker(Dictionary<string, object> project, Dictionary<string, object> dialogue, Dictionary<string, object> node)
        {
            object id = EffectiveSpeakerId(dialogue, node);
            if (id == null) return new Dictionary<string, object> { { "kind", "narration" } };
            
            string idStr = id.ToString();

            if (project.TryGetValue("characters", out var charsObj) && charsObj is Dictionary<string, object> chars)
            {
                if (chars.TryGetValue(idStr, out var cObj)) return new Dictionary<string, object> { { "kind", "character" }, { "character", cObj } };
            }

            if (project.TryGetValue("skills", out var skillsObj) && skillsObj is Dictionary<string, object> skills)
            {
                if (skills.TryGetValue(idStr, out var sObj)) return new Dictionary<string, object> { { "kind", "skill" }, { "skill", sObj } };
            }

            return new Dictionary<string, object> { { "kind", "narration" } };
        }

        public static object ResolvePortrait(Dictionary<string, object> project, Dictionary<string, object> dialogue, Dictionary<string, object> node)
        {
            if (node.TryGetValue("portrait", out var pObj)) return pObj;

            var speaker = ResolveSpeaker(project, dialogue, node);
            if (speaker.TryGetValue("kind", out var kObj) && kObj is string kStr && kStr == "character")
            {
                if (speaker.TryGetValue("character", out var cObj) && cObj is Dictionary<string, object> c)
                {
                    if (c.TryGetValue("portrait", out var cpObj)) return cpObj;
                }
            }

            return null;
        }
        
        // ----------------------------------------------------------------- shared --

        public static Dictionary<string, object> FindNode(Dictionary<string, object> dialogue, string nodeId)
        {
            if (dialogue.TryGetValue("nodes", out var nodesObj) && nodesObj is IEnumerable<object> nodesList)
            {
                foreach (var nObj in nodesList)
                {
                    if (nObj is Dictionary<string, object> n && n.TryGetValue("id", out var idObj) && idObj is string idStr && idStr == nodeId)
                    {
                        return n;
                    }
                }
            }
            return null;
        }
    }
}
