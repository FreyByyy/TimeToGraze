using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TimeToGraze;

public abstract class WorkGiver_GrazingAnimals : WorkGiver_InteractAnimal
{
    protected abstract GrazePhase Phase { get; }

    public WorkGiver_GrazingAnimals()
    {
        canInteractWhileSleeping = true;
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        foreach (CompGrazing comp in GrazingUtility.CompsOn(pawn.Map))
        {
            foreach (GrazeEntry entry in comp.Entries)
            {
                if (entry.phase == Phase)
                {
                    yield return entry.animal;
                }
            }
        }
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        if (GrazingRegistry.IsEmpty)
        {
            return true;
        }
        foreach (CompGrazing comp in GrazingUtility.CompsOn(pawn.Map))
        {
            foreach (GrazeEntry entry in comp.Entries)
            {
                if (entry.phase == Phase && !entry.animal.roping.IsRoped)
                {
                    return false;
                }
            }
        }
        return true;
    }

    protected bool TryGetAnimal(Pawn pawn, Thing t, bool forced, out Pawn animal, out CompGrazing comp)
    {
        animal = t as Pawn;
        comp = null;
        if (animal == null || !GrazingRegistry.TryGet(animal, out GrazeEntry entry, out comp) || entry.phase != Phase)
        {
            return false;
        }
        if (animal.roping.IsRoped || comp.parent.Map != pawn.Map)
        {
            return false;
        }
        if (animal.Position.IsForbidden(pawn))
        {
            JobFailReason.Is("ForbiddenOutsideAllowedAreaLower".Translate().CapitalizeFirst());
            return false;
        }
        if (!CanInteractWithAnimal(pawn, animal, out string reason, forced, canInteractWhileSleeping: true, ignoreSkillRequirements: true))
        {
            if (reason != null)
            {
                JobFailReason.Is(reason);
            }
            return false;
        }
        return true;
    }
}

public class WorkGiver_LeadToPasture : WorkGiver_GrazingAnimals
{
    protected override GrazePhase Phase => GrazePhase.ToPasture;

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!TryGetAnimal(pawn, t, forced, out Pawn animal, out CompGrazing comp) || comp.Returning)
        {
            return null;
        }
        if (!comp.MayLeadOut(pawn, forced))
        {
            JobFailReason.Is("TTG_ShepherdLeadsFirst".Translate(comp.shepherd.LabelShort));
            return null;
        }
        IntVec3 dest = comp.FindPastureDestCell(pawn, animal);
        if (!dest.IsValid)
        {
            JobFailReason.Is("TTG_PastureUnreachable".Translate());
            return null;
        }
        return JobMaker.MakeJob(TTG_DefOf.TTG_RopeToPasture, animal, dest, comp.parent);
    }
}

public class WorkGiver_ReturnFromPasture : WorkGiver_GrazingAnimals
{
    protected override GrazePhase Phase => GrazePhase.ToPen;

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!TryGetAnimal(pawn, t, forced, out Pawn animal, out CompGrazing comp))
        {
            return null;
        }
        CompAnimalPenMarker marker = comp.Marker;
        if (marker == null || marker.parent.IsForbidden(pawn) || !marker.PenState.Enclosed)
        {
            // Let vanilla roping pick any suitable pen instead.
            return null;
        }
        Job job = WorkGiver_TakeToPen.MakeJob(pawn, animal, marker, allowUnenclosedPens: false, RopingPriority.Closest, out string reason);
        if (job == null && reason != null)
        {
            JobFailReason.Is(reason);
        }
        return job;
    }
}

// The assigned shepherd stands watch next to the grazing herd (emergency-priority Handling work).
public class WorkGiver_WatchHerd : WorkGiver
{
    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return FindComp(pawn) == null;
    }

    public override Job NonScanJob(Pawn pawn)
    {
        CompGrazing comp = FindComp(pawn);
        if (comp == null)
        {
            return null;
        }
        IntVec3 spot = GrazingUtility.FindWatchSpot(pawn, comp);
        if (!spot.IsValid)
        {
            return null;
        }
        Job job = JobMaker.MakeJob(TTG_DefOf.TTG_WatchHerd, spot, comp.parent);
        job.expiryInterval = 2500;
        return job;
    }

    private static CompGrazing FindComp(Pawn pawn)
    {
        if (GrazingRegistry.IsEmpty || !pawn.Spawned)
        {
            return null;
        }
        foreach (CompGrazing comp in GrazingUtility.CompsOn(pawn.Map))
        {
            if (comp.shepherd == pawn && !comp.Returning && comp.HasGrazingAnimals)
            {
                return comp;
            }
        }
        return null;
    }
}

// Vanilla animals only eat once they get hungry; out at the pasture they keep nibbling grass
// until they reach the pen's "fed" threshold. Inserted before idle wandering.
public class JobGiver_GrazeAtPasture : ThinkNode_JobGiver
{
    private const float SearchRadius = 40f;

    private const float FullWhenNoThreshold = 0.95f;

    protected override Job TryGiveJob(Pawn pawn)
    {
        if (GrazingRegistry.IsEmpty || !GrazingRegistry.TryGet(pawn, out GrazeEntry entry, out CompGrazing comp) || entry.phase != GrazePhase.Grazing)
        {
            return null;
        }
        Zone_Pasture pasture = comp.ActivePasture;
        Need_Food food = pawn.needs?.food;
        if (pasture == null || food == null || pawn.roping.IsRoped)
        {
            return null;
        }
        float target = comp.returnWhenFed ? comp.fedThreshold : FullWhenNoThreshold;
        if (food.CurLevelPercentage >= target)
        {
            return null;
        }
        Map map = pawn.Map;
        Thing plant = GenClosest.ClosestThingReachable(pawn.Position, map, ThingRequest.ForGroup(ThingRequestGroup.Plant), PathEndMode.Touch, TraverseParms.For(pawn), SearchRadius, t =>
            t is Plant p
            && map.zoneManager.ZoneAt(p.Position) == pasture
            && GrazingUtility.IsGrazablePlant(p)
            && p.IngestibleNow
            && pawn.WillEat(p)
            && !p.IsForbidden(pawn)
            && pawn.CanReserve(p));
        if (plant == null)
        {
            return null;
        }
        Job job = JobMaker.MakeJob(JobDefOf.Ingest, plant);
        job.count = 1;
        return job;
    }
}

// Herding dog: inserted into the animal think tree before idle wandering.
public class JobGiver_GuardHerd : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if (GrazingRegistry.IsEmpty || !pawn.Spawned || pawn.Downed || pawn.roping.IsRoped || pawn.training == null || !pawn.training.HasLearned(TTG_DefOf.TTG_Herding))
        {
            return null;
        }
        foreach (CompGrazing comp in GrazingUtility.CompsOn(pawn.Map))
        {
            if (comp.dog != pawn || !comp.HasGrazingAnimals)
            {
                continue;
            }
            IntVec3 spot = GrazingUtility.FindWatchSpot(pawn, comp);
            if (!spot.IsValid)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(TTG_DefOf.TTG_GuardHerd, spot, comp.parent);
            job.expiryInterval = 1200;
            return job;
        }
        return null;
    }
}
