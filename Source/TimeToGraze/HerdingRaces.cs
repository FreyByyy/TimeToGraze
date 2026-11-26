using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TimeToGraze;

// Which races may learn Herding. XML (race/trainableTags: TTG_Herding) gives the defaults; the player
// adds or removes races in mod settings, and the tag is rewritten at runtime to match.
[StaticConstructorOnStartup]
public static class HerdingRaces
{
    public const string Tag = "TTG_Herding";

    private static readonly HashSet<ThingDef> xmlDefaults = new HashSet<ThingDef>();

    private static List<ThingDef> candidates;

    static HerdingRaces()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.category == ThingCategory.Pawn && def.race?.trainableTags != null && def.race.trainableTags.Contains(Tag))
            {
                xmlDefaults.Add(def);
            }
        }
        Apply();
    }

    // Animals smart enough for Herding (it needs Obedience, an Intermediate trainable), sorted by label.
    public static List<ThingDef> Candidates => candidates ??= DefDatabase<ThingDef>.AllDefsListForReading
        .Where(d => d.category == ThingCategory.Pawn && d.race != null && d.race.Animal && !d.race.Dryad && (xmlDefaults.Contains(d) || (d.race.trainability != null && d.race.trainability.intelligenceOrder >= TrainabilityDefOf.Intermediate.intelligenceOrder)))
        .OrderBy(d => d.label)
        .ToList();

    public static bool IsDefault(ThingDef def)
    {
        return xmlDefaults.Contains(def);
    }

    public static bool Allowed(ThingDef def)
    {
        TTG_Settings s = TimeToGrazeMod.Settings;
        if (xmlDefaults.Contains(def))
        {
            return !s.herdingRemoved.Contains(def.defName);
        }
        return s.herdingAdded.Contains(def.defName);
    }

    public static void SetAllowed(ThingDef def, bool allowed)
    {
        TTG_Settings s = TimeToGrazeMod.Settings;
        s.herdingAdded.Remove(def.defName);
        s.herdingRemoved.Remove(def.defName);
        if (allowed && !xmlDefaults.Contains(def))
        {
            s.herdingAdded.Add(def.defName);
        }
        else if (!allowed && xmlDefaults.Contains(def))
        {
            s.herdingRemoved.Add(def.defName);
        }
        ApplyTo(def);
    }

    public static void ResetToDefaults()
    {
        TimeToGrazeMod.Settings.herdingAdded.Clear();
        TimeToGrazeMod.Settings.herdingRemoved.Clear();
        Apply();
    }

    public static int AllowedCount => Candidates.Count(Allowed);

    public static void Apply()
    {
        foreach (ThingDef def in Candidates)
        {
            ApplyTo(def);
        }
    }

    private static void ApplyTo(ThingDef def)
    {
        RaceProperties race = def.race;
        bool allowed = Allowed(def);
        if (allowed)
        {
            race.trainableTags ??= new List<string>();
            if (!race.trainableTags.Contains(Tag))
            {
                race.trainableTags.Add(Tag);
            }
        }
        else
        {
            race.trainableTags?.Remove(Tag);
        }
    }

    // A trained animal whose race was switched off in settings no longer herds.
    public static bool CanHerd(Pawn pawn)
    {
        return pawn.training != null && Allowed(pawn.def) && pawn.training.HasLearned(TTG_DefOf.TTG_Herding);
    }
}
