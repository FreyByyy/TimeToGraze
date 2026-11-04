using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TimeToGraze;

// Rope jobs that take the whole herd at once. Vanilla caps a roper at 10 animals within 10 cells;
// Patch_RopeToDestination_FindAnotherAnimalToRope routes these drivers here instead.
public interface IHerdRoper
{
    bool TryTargetNextHerdAnimal();
}

public static class HerdRopingUtility
{
    // Closest reachable animal of the pen's session in the given phase, with no count or distance cap.
    public static bool TryTargetNext(JobDriver driver, CompGrazing comp, GrazePhase phase)
    {
        if (comp == null)
        {
            return false;
        }
        Pawn roper = driver.pawn;
        Pawn best = null;
        int bestDist = int.MaxValue;
        foreach (GrazeEntry entry in comp.Entries)
        {
            Pawn animal = entry.animal;
            if (entry.phase != phase || animal == roper || !animal.Spawned || animal.roping.IsRoped || animal.Downed || !roper.CanReserve(animal))
            {
                continue;
            }
            int dist = animal.Position.DistanceToSquared(roper.Position);
            if (dist < bestDist && roper.CanReach(animal, PathEndMode.Touch, Danger.Deadly))
            {
                best = animal;
                bestDist = dist;
            }
        }
        if (best == null)
        {
            return false;
        }
        driver.job.SetTarget(TargetIndex.A, best);
        return true;
    }
}

// Ropes the herd in the pen and leads it to the session's pasture.
public class JobDriver_RopeToPasture : JobDriver_RopeToDestination, IHerdRoper
{
    private CompGrazing Comp => job.targetC.Thing?.TryGetComp<CompGrazing>();

    public bool TryTargetNextHerdAnimal()
    {
        return HerdRopingUtility.TryTargetNext(this, Comp, GrazePhase.ToPasture);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.C);
        this.FailOn(() => Comp == null || !Comp.SessionActive || Comp.Returning);
        return base.MakeNewToils();
    }

    protected override bool HasRopeeArrived(Pawn ropee, bool roperWaitingAtDest)
    {
        CompGrazing comp = Comp;
        return comp?.ActivePasture != null && ropee.Map.zoneManager.ZoneAt(ropee.Position) == comp.ActivePasture;
    }

    protected override void ProcessArrivedRopee(Pawn ropee)
    {
        Comp?.Notify_ArrivedAtPasture(ropee);
    }

    protected override bool ShouldOpportunisticallyRopeAnimal(Pawn animal)
    {
        if (animal == pawn || animal.roping.IsRoped || animal.Downed)
        {
            return false;
        }
        return GrazingRegistry.TryGet(animal, out GrazeEntry entry, out CompGrazing comp) && comp == Comp && entry.phase == GrazePhase.ToPasture && pawn.CanReserve(animal);
    }
}

// Vanilla rope-to-pen, but gathers every animal of the session that is waiting to come home.
public class JobDriver_RopeHerdToPen : JobDriver_RopeToPen, IHerdRoper
{
    private CompGrazing Comp => job.targetC.Thing?.TryGetComp<CompGrazing>();

    public bool TryTargetNextHerdAnimal()
    {
        return HerdRopingUtility.TryTargetNext(this, Comp, GrazePhase.ToPen);
    }
}

// Shared "stand near the herd and keep an eye on it" behaviour for shepherds and dogs.
public abstract class JobDriver_HerdDuty : JobDriver
{
    private const int CheckInterval = 250;

    protected int ticksOnDuty;

    protected abstract int DutyDuration { get; }

    protected CompGrazing Comp => job.targetB.Thing?.TryGetComp<CompGrazing>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ticksOnDuty, "ticksOnDuty");
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
        return true;
    }

    protected abstract bool StillAssigned(CompGrazing comp);

    protected virtual void DutyTick(int delta)
    {
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => Comp == null || !Comp.HasGrazingAnimals || !StillAssigned(Comp));
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        Toil watch = ToilMaker.MakeToil("WatchHerd");
        watch.defaultCompleteMode = ToilCompleteMode.Delay;
        watch.defaultDuration = DutyDuration;
        watch.handlingFacing = true;
        watch.socialMode = RandomSocialMode.Normal;
        watch.tickIntervalAction = delta =>
        {
            ticksOnDuty += delta;
            DutyTick(delta);
            if (pawn.IsHashIntervalTick(CheckInterval, delta))
            {
                IntVec3 center = GrazingUtility.HerdCenter(Comp);
                if (!center.IsValid)
                {
                    return;
                }
                pawn.rotationTracker.FaceCell(center);
                if (!pawn.Position.InHorDistOf(center, 14f))
                {
                    // Herd drifted away: finish and get a fresh spot next to it.
                    ReadyForNextToil();
                }
            }
        };
        yield return watch;
    }
}

public class JobDriver_WatchHerd : JobDriver_HerdDuty
{
    private const float XpPerTick = 0.02f;

    private const int TicksForThought = 1500;

    protected override int DutyDuration => 2500;

    protected override bool StillAssigned(CompGrazing comp)
    {
        return comp.shepherd == pawn && !comp.Returning;
    }

    protected override void DutyTick(int delta)
    {
        pawn.skills?.Learn(SkillDefOf.Animals, XpPerTick * delta);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(_ =>
        {
            if (ticksOnDuty >= TicksForThought && TimeToGrazeMod.Settings.shepherdThought && pawn.needs?.mood != null)
            {
                bool passion = pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Animals).passion != Passion.None;
                pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(TTG_DefOf.TTG_PeacefulGrazing, passion ? 1 : 0));
            }
        });
        return base.MakeNewToils();
    }
}

public class JobDriver_GuardHerd : JobDriver_HerdDuty
{
    protected override int DutyDuration => 900;

    protected override bool StillAssigned(CompGrazing comp)
    {
        return comp.dog == pawn;
    }
}
