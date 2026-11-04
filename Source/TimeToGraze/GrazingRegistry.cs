using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TimeToGraze;

public enum GrazePhase : byte
{
    ToPasture,
    Grazing,
    ToPen
}

public class GrazeEntry : IExposable
{
    public Pawn animal;

    public GrazePhase phase;

    public int phaseTick;

    public void ExposeData()
    {
        Scribe_References.Look(ref animal, "animal");
        Scribe_Values.Look(ref phase, "phase", GrazePhase.ToPasture);
        Scribe_Values.Look(ref phaseTick, "phaseTick");
    }
}

// Fast animal -> session lookup for the Harmony patches. Copy-on-write, because the area getter
// may be read while path requests are being prepared.
public static class GrazingRegistry
{
    private struct Record
    {
        public GrazeEntry entry;

        public CompGrazing comp;
    }

    private static volatile Dictionary<Pawn, Record> records = new Dictionary<Pawn, Record>();

    public static bool IsEmpty => records.Count == 0;

    public static void Clear()
    {
        records = new Dictionary<Pawn, Record>();
    }

    public static void Register(CompGrazing comp, GrazeEntry entry)
    {
        if (entry?.animal == null)
        {
            return;
        }
        Dictionary<Pawn, Record> copy = new Dictionary<Pawn, Record>(records);
        copy[entry.animal] = new Record { entry = entry, comp = comp };
        records = copy;
    }

    public static void Unregister(Pawn animal)
    {
        if (animal == null || !records.ContainsKey(animal))
        {
            return;
        }
        Dictionary<Pawn, Record> copy = new Dictionary<Pawn, Record>(records);
        copy.Remove(animal);
        records = copy;
    }

    public static void UnregisterAll(CompGrazing comp)
    {
        Dictionary<Pawn, Record> copy = new Dictionary<Pawn, Record>(records);
        List<Pawn> toRemove = new List<Pawn>();
        foreach (KeyValuePair<Pawn, Record> kv in copy)
        {
            if (kv.Value.comp == comp)
            {
                toRemove.Add(kv.Key);
            }
        }
        if (toRemove.Count == 0)
        {
            return;
        }
        foreach (Pawn p in toRemove)
        {
            copy.Remove(p);
        }
        records = copy;
    }

    public static bool Contains(Pawn animal)
    {
        return animal != null && records.ContainsKey(animal);
    }

    public static bool TryGet(Pawn animal, out GrazeEntry entry, out CompGrazing comp)
    {
        if (animal != null && records.TryGetValue(animal, out Record r))
        {
            entry = r.entry;
            comp = r.comp;
            return true;
        }
        entry = null;
        comp = null;
        return false;
    }

    // Pasture area the animal must stay in, or null if it should behave normally.
    public static Area GrazingAreaFor(Pawn animal)
    {
        if (!TryGet(animal, out GrazeEntry entry, out CompGrazing comp) || entry.phase == GrazePhase.ToPasture)
        {
            return null;
        }
        if (!animal.Spawned || animal.roping == null || animal.roping.IsRoped)
        {
            return null;
        }
        Area area = comp.ActivePasture?.GrazingAreaIfBuilt;
        if (area == null || area.TrueCount == 0)
        {
            return null;
        }
        return area;
    }

    // Grazing animals still counted as "at the pasture" (for wander-off and predator odds).
    public static CompGrazing OutAtPastureComp(Pawn animal)
    {
        if (TryGet(animal, out GrazeEntry entry, out CompGrazing comp) && entry.phase != GrazePhase.ToPasture)
        {
            return comp;
        }
        return null;
    }
}

public class GameComponent_TimeToGraze : GameComponent
{
    public GameComponent_TimeToGraze(Game game)
    {
        GrazingRegistry.Clear();
    }
}
