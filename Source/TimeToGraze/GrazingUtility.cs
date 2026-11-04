using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TimeToGraze;

public static class GrazingUtility
{
    public const float DarkSkyGlow = 0.3f;

    private static GameConditionDef noxiousHazeDef;

    private static bool noxiousHazeLooked;

    private static readonly List<Pawn> tmpPredators = new List<Pawn>();

    public static IEnumerable<CompGrazing> CompsOn(Map map)
    {
        if (map == null)
        {
            yield break;
        }
        foreach (Building marker in map.listerBuildings.allBuildingsAnimalPenMarkers)
        {
            CompGrazing comp = marker.TryGetComp<CompGrazing>();
            if (comp != null)
            {
                yield return comp;
            }
        }
    }

    public static bool IsValidPasture(Zone_Pasture zone, Map map)
    {
        return zone != null && zone.zoneManager != null && zone.zoneManager.map == map && zone.cells.Count > 0 && map.zoneManager.AllZones.Contains(zone);
    }

    public static bool IsGrazablePlant(Plant plant)
    {
        ThingDef def = plant.def;
        return def.plant != null && !def.plant.IsTree && def.ingestible != null && (def.ingestible.foodType & FoodTypeFlags.Plant) != 0;
    }

    public static float GrassNutritionIn(List<IntVec3> cells, Map map)
    {
        float sum = 0f;
        for (int i = 0; i < cells.Count; i++)
        {
            Plant plant = cells[i].GetPlant(map);
            if (plant != null && IsGrazablePlant(plant) && plant.IngestibleNow)
            {
                sum += plant.Growth * plant.GetStatValue(StatDefOf.Nutrition);
            }
        }
        return sum;
    }

    public static float GrassNutritionIn(IEnumerable<Region> regions)
    {
        float sum = 0f;
        foreach (Region region in regions)
        {
            foreach (Thing thing in region.ListerThings.ThingsInGroup(ThingRequestGroup.Plant))
            {
                if (thing is Plant plant && IsGrazablePlant(plant) && plant.IngestibleNow)
                {
                    sum += plant.Growth * plant.GetStatValue(StatDefOf.Nutrition);
                }
            }
        }
        return sum;
    }

    public static float NutritionPerDay(IEnumerable<Pawn> animals)
    {
        float sum = 0f;
        foreach (Pawn animal in animals)
        {
            sum += SimplifiedPastureNutritionSimulator.NutritionConsumedPerDay(animal);
        }
        return sum;
    }

    public static bool IsDark(Map map)
    {
        return map.skyManager.CurSkyGlow < DarkSkyGlow;
    }

    public static bool InHourWindow(int hour, IntRange window)
    {
        if (window.min == window.max)
        {
            return false;
        }
        if (window.min < window.max)
        {
            return hour >= window.min && hour < window.max;
        }
        return hour >= window.min || hour < window.max;
    }

    public static bool ToxicConditionActive(Map map)
    {
        if (map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout))
        {
            return true;
        }
        if (!noxiousHazeLooked)
        {
            noxiousHazeDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("NoxiousHaze");
            noxiousHazeLooked = true;
        }
        return noxiousHazeDef != null && map.gameConditionManager.ConditionIsActive(noxiousHazeDef);
    }

    // Returns a translated reason when it is unsafe to keep (or take) the herd at the pasture.
    // Enemies on the map are not here: the player is asked instead (see CompGrazing).
    public static string DangerReason(Map map, CompGrazing comp, IEnumerable<Pawn> herd, Zone_Pasture pasture)
    {
        if (PredatorsHuntingHerd(map, comp).Count > 0)
        {
            return "TTG_Reason_Predator".Translate();
        }
        if (ToxicConditionActive(map))
        {
            return "TTG_Reason_Toxic".Translate();
        }
        if (pasture != null && pasture.cells.Count > 0)
        {
            float temperature = GenTemperature.GetTemperatureForCell(pasture.cells[0], map);
            foreach (Pawn animal in herd)
            {
                if (!animal.SafeTemperatureRange().Includes(temperature))
                {
                    return "TTG_Reason_Temperature".Translate();
                }
            }
        }
        return null;
    }

    public static List<Pawn> PredatorsHuntingHerd(Map map, CompGrazing comp)
    {
        tmpPredators.Clear();
        if (comp == null || !comp.SessionActive)
        {
            return tmpPredators;
        }
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p.CurJobDef == JobDefOf.PredatorHunt && p.CurJob.targetA.Thing is Pawn prey && GrazingRegistry.OutAtPastureComp(prey) == comp)
            {
                tmpPredators.Add(p);
            }
        }
        return tmpPredators;
    }

    public static IntVec3 HerdCenter(CompGrazing comp)
    {
        int count = 0;
        int x = 0;
        int z = 0;
        foreach (GrazeEntry entry in comp.Entries)
        {
            if (entry.phase != GrazePhase.ToPasture && entry.animal.Spawned)
            {
                x += entry.animal.Position.x;
                z += entry.animal.Position.z;
                count++;
            }
        }
        if (count == 0)
        {
            return IntVec3.Invalid;
        }
        return new IntVec3(x / count, 0, z / count);
    }

    public static IntVec3 FindWatchSpot(Pawn watcher, CompGrazing comp)
    {
        IntVec3 center = HerdCenter(comp);
        if (!center.IsValid)
        {
            return IntVec3.Invalid;
        }
        Map map = watcher.Map;
        if (CellFinder.TryFindRandomCellNear(center, map, 6, c => c.Standable(map) && !c.IsForbidden(watcher) && watcher.CanReach(c, PathEndMode.OnCell, Danger.Some), out IntVec3 spot))
        {
            return spot;
        }
        return IntVec3.Invalid;
    }

    public static float ShepherdSkillFactor(Pawn shepherd)
    {
        if (shepherd?.skills == null)
        {
            return 1f;
        }
        // Skill 10 gives exactly the configured value; 0 is weaker, 20 much stronger.
        return Mathf.Clamp(0.6f + 0.04f * shepherd.skills.GetSkill(SkillDefOf.Animals).Level, 0.6f, 1.4f);
    }
}
