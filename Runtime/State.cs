using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlance
{
    public class State
    {
        public Dictionary<string, bool> Flags { get; private set; } = new Dictionary<string, bool>();
        public Dictionary<string, float> Reputation { get; private set; } = new Dictionary<string, float>();
        public Dictionary<string, float> Skills { get; private set; } = new Dictionary<string, float>();
        public Dictionary<string, float> Counters { get; private set; } = new Dictionary<string, float>();
        public HashSet<string> Inventory { get; private set; } = new HashSet<string>();
        public Dictionary<string, string> QuestStages { get; private set; } = new Dictionary<string, string>();
        public Dictionary<string, float> Relationships { get; private set; } = new Dictionary<string, float>();
        public float Xp { get; set; } = 0.0f;
        public Dictionary<string, float> SkillPointsSpent { get; private set; } = new Dictionary<string, float>();
        public Dictionary<string, string> Texts { get; private set; } = new Dictionary<string, string>();
        public HashSet<string> QuestFired { get; private set; } = new HashSet<string>();
        public string PendingCutscene { get; set; } = null;

        public static State FromDict(Dictionary<string, object> d)
        {
            var s = new State();
            s.Flags = ExtractDict<bool>(d, "flags");
            s.Reputation = ExtractDict<float>(d, "reputation");
            s.Skills = ExtractDict<float>(d, "skills");
            s.Counters = ExtractDict<float>(d, "counters");
            s.QuestStages = ExtractDict<string>(d, "questStages");
            s.Relationships = ExtractDict<float>(d, "relationships");
            s.SkillPointsSpent = ExtractDict<float>(d, "skillPointsSpent");
            s.Texts = ExtractDict<string>(d, "texts");
            
            s.Xp = ExtractFloat(d, "xp", 0.0f);
            
            if (d.TryGetValue("inventory", out object invObj) && invObj is IEnumerable<object> invList)
            {
                foreach (var item in invList)
                {
                    if (item is string str) s.Inventory.Add(str);
                }
            }
            
            if (d.TryGetValue("questFired", out object qfObj) && qfObj is IEnumerable<object> qfList)
            {
                foreach (var item in qfList)
                {
                    if (item is string str) s.QuestFired.Add(str);
                }
            }
            
            if (d.TryGetValue("pendingCutscene", out object pcObj) && pcObj is string pcStr)
            {
                s.PendingCutscene = pcStr;
            }

            return s;
        }

        public Dictionary<string, object> ToDict()
        {
            var inv = Inventory.ToList();
            inv.Sort((a, b) => string.CompareOrdinal(a, b));

            var outDict = new Dictionary<string, object>
            {
                { "flags", new Dictionary<string, bool>(Flags) },
                { "reputation", new Dictionary<string, float>(Reputation) },
                { "skills", new Dictionary<string, float>(Skills) },
                { "counters", new Dictionary<string, float>(Counters) },
                { "inventory", inv },
                { "questStages", new Dictionary<string, string>(QuestStages) },
                { "xp", Xp },
                { "skillPointsSpent", new Dictionary<string, float>(SkillPointsSpent) }
            };

            if (Texts.Count > 0)
                outDict["texts"] = new Dictionary<string, string>(Texts);
            
            if (Relationships.Count > 0)
                outDict["relationships"] = new Dictionary<string, float>(Relationships);
            
            if (QuestFired.Count > 0)
            {
                var fired = QuestFired.ToList();
                fired.Sort((a, b) => string.CompareOrdinal(a, b));
                outDict["questFired"] = fired;
            }

            if (PendingCutscene != null)
                outDict["pendingCutscene"] = PendingCutscene;

            return outDict;
        }

        public State Copy()
        {
            var s = new State();
            s.Flags = new Dictionary<string, bool>(Flags);
            s.Reputation = new Dictionary<string, float>(Reputation);
            s.Skills = new Dictionary<string, float>(Skills);
            s.Counters = new Dictionary<string, float>(Counters);
            s.Inventory = new HashSet<string>(Inventory);
            s.QuestStages = new Dictionary<string, string>(QuestStages);
            s.Relationships = new Dictionary<string, float>(Relationships);
            s.Xp = Xp;
            s.SkillPointsSpent = new Dictionary<string, float>(SkillPointsSpent);
            s.Texts = new Dictionary<string, string>(Texts);
            s.QuestFired = new HashSet<string>(QuestFired);
            s.PendingCutscene = PendingCutscene;
            return s;
        }

        private static Dictionary<string, T> ExtractDict<T>(Dictionary<string, object> d, string key)
        {
            var result = new Dictionary<string, T>();
            if (d.TryGetValue(key, out object val) && val is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    try 
                    {
                        result[kvp.Key] = (T)Convert.ChangeType(kvp.Value, typeof(T));
                    }
                    catch 
                    {
                        // Ignore unconvertible types silently to match GDScript loose typing resilience
                    }
                }
            }
            return result;
        }

        private static float ExtractFloat(Dictionary<string, object> d, string key, float defaultVal)
        {
            if (d.TryGetValue(key, out object val))
            {
                try 
                {
                    return (float)Convert.ChangeType(val, typeof(float));
                }
                catch
                {
                }
            }
            return defaultVal;
        }
    }
}
