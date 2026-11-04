using RimWorld;
using Verse;

namespace TimeToGraze;

[DefOf]
public static class TTG_DefOf
{
    public static JobDef TTG_RopeToPasture;

    public static JobDef TTG_RopeHerdToPen;

    public static JobDef TTG_WatchHerd;

    public static JobDef TTG_GuardHerd;

    public static TrainableDef TTG_Herding;

    public static ThoughtDef TTG_PeacefulGrazing;

    static TTG_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(TTG_DefOf));
    }
}
