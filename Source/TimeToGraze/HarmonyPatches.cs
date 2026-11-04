using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace TimeToGraze;

// Grazing animals are kept inside the pasture through a temporary allowed area, so food search,
// wandering and pathing all respect it with no further patches.
[HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.EffectiveAreaRestrictionInPawnCurrentMap), MethodType.Getter)]
public static class Patch_PlayerSettings_EffectiveArea
{
    public static void Postfix(Pawn ___pawn, ref Area __result)
    {
        if (GrazingRegistry.IsEmpty)
        {
            return;
        }
        Area area = GrazingRegistry.GrazingAreaFor(___pawn);
        if (area != null && ___pawn.GetLord() == null)
        {
            __result = area;
        }
    }
}

// Lower wander-off chance while out at the pasture.
[HarmonyPatch(typeof(ThinkNode_ChancePerDay_Roam), "MtbHours")]
public static class Patch_Roam_MtbHours
{
    public static void Postfix(Pawn pawn, ref float __result)
    {
        if (__result <= 0f || GrazingRegistry.IsEmpty)
        {
            return;
        }
        CompGrazing comp = GrazingRegistry.OutAtPastureComp(pawn);
        if (comp == null)
        {
            return;
        }
        float multiplier = comp.RoamMultiplier;
        __result = multiplier <= 0f ? 0f : __result / multiplier;
    }
}

// Vanilla handlers must not drag grazing animals back to the pen (or a hitching post) mid-session.
[HarmonyPatch(typeof(AnimalPenUtility), nameof(AnimalPenUtility.GetPenAnimalShouldBeTakenTo))]
public static class Patch_GetPenAnimalShouldBeTakenTo
{
    public static bool Prefix(Pawn animal, ref string jobFailReason, ref CompAnimalPenMarker __result)
    {
        if (!GrazingPatchUtility.BlocksVanillaRoping(animal))
        {
            return true;
        }
        jobFailReason = "TTG_AnimalIsGrazing".Translate();
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(WorkGiver_TakeToPen), nameof(WorkGiver_TakeToPen.JobOnThing))]
public static class Patch_TakeToPen_JobOnThing
{
    public static bool Prefix(Thing t, ref Job __result)
    {
        if (!GrazingPatchUtility.BlocksVanillaRoping(t as Pawn))
        {
            return true;
        }
        __result = null;
        return false;
    }
}

// Guarded herds are less attractive prey; a scared-off predator leaves the herd alone for a while.
[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.IsAcceptablePreyFor))]
public static class Patch_IsAcceptablePreyFor
{
    private const int DecisionWindowTicks = 7500;

    public static void Postfix(Pawn predator, Pawn prey, ref bool __result)
    {
        if (!__result || GrazingRegistry.IsEmpty)
        {
            return;
        }
        CompGrazing comp = GrazingRegistry.OutAtPastureComp(prey);
        if (comp == null)
        {
            return;
        }
        if (comp.IsScared(predator))
        {
            __result = false;
            return;
        }
        float multiplier = comp.PredatorMultiplier;
        if (multiplier >= 1f)
        {
            return;
        }
        // Stable per predator/prey pair for a few hours, so repeated checks don't compound the odds.
        int seed = Gen.HashCombineInt(Gen.HashCombineInt(predator.thingIDNumber, prey.thingIDNumber), Find.TickManager.TicksGame / DecisionWindowTicks);
        if (Rand.ValueSeeded(seed) >= multiplier)
        {
            __result = false;
        }
    }
}

// Pasture areas are runtime-only (rebuilt from the zone on load), so saves stay loadable without the mod.
[HarmonyPatch(typeof(AreaManager), nameof(AreaManager.ExposeData))]
public static class Patch_AreaManager_ExposeData
{
    public static void Prefix(AreaManager __instance, out List<Area> __state)
    {
        __state = null;
        if (Scribe.mode != LoadSaveMode.Saving)
        {
            return;
        }
        List<Area> areas = __instance.AllAreas;
        for (int i = areas.Count - 1; i >= 0; i--)
        {
            if (areas[i] is Area_Pasture)
            {
                (__state ??= new List<Area>()).Add(areas[i]);
                areas.RemoveAt(i);
            }
        }
    }

    public static void Finalizer(AreaManager __instance, List<Area> __state)
    {
        if (__state != null)
        {
            __instance.AllAreas.AddRange(__state);
        }
    }
}

public static class GrazingPatchUtility
{
    public static bool BlocksVanillaRoping(Pawn animal)
    {
        if (animal == null || GrazingRegistry.IsEmpty || !GrazingRegistry.TryGet(animal, out GrazeEntry entry, out _))
        {
            return false;
        }
        return entry.phase != GrazePhase.ToPen && animal.MentalStateDef != MentalStateDefOf.Roaming;
    }
}
