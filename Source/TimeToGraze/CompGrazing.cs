using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace TimeToGraze;

public class CompProperties_Grazing : CompProperties
{
    public CompProperties_Grazing()
    {
        compClass = typeof(CompGrazing);
    }
}

public class CompGrazing : ThingComp
{
    private const int RestartCooldownTicks = 2500;

    private const int ScheduleOnlyRestartTicks = 30000;

    private const int LeadOutTimeoutTicks = 2500 * 6;

    private const int StuckReturnTimeoutTicks = 60000;

    private const int GuardMemoryTicks = 1000;

    private const int ScaredTicks = 30000;

    // ---- Settings shown in the "Grazing" tab ----
    public bool enabled;

    public List<Zone_Pasture> pastures = new List<Zone_Pasture>();

    public bool triggerHunger = true;

    public float hungerThreshold = 0.3f;

    public bool triggerPenFood;

    public float penFoodDays = 2f;

    public bool useSchedule = true;

    public IntRange hours = new IntRange(7, 19);

    public bool returnWhenFed = true;

    public float fedThreshold = 0.9f;

    public bool returnAtNight = true;

    public bool returnOnDanger = true;

    public bool excludeSick = true;

    public bool excludePregnant;

    public bool excludeYoung;

    public List<Pawn> excludedAnimals = new List<Pawn>();

    public Pawn shepherd;

    public Pawn dog;

    // ---- Session state ----
    private List<GrazeEntry> entries = new List<GrazeEntry>();

    private Zone_Pasture activePasture;

    private int sessionStartTick = -1;

    private bool returning;

    private string returnReason;

    private int lastStartTick = -99999;

    private int lastEndTick = -99999;

    // ---- Runtime only ----
    private int shepherdSeenTick = -99999;

    private int dogSeenTick = -99999;

    private bool hostilesPresent;

    private bool hostileDialogShown;

    private bool holdForHostiles;

    private readonly Dictionary<int, int> announcedPredators = new Dictionary<int, int>();

    private readonly Dictionary<int, int> scaredPredators = new Dictionary<int, int>();

    private CompAnimalPenMarker markerCached;

    private static TTG_Settings S => TimeToGrazeMod.Settings;

    public CompAnimalPenMarker Marker => markerCached ??= parent.TryGetComp<CompAnimalPenMarker>();

    public string PenLabel => Marker?.label ?? parent.LabelCap;

    public List<GrazeEntry> Entries => entries;

    public Zone_Pasture ActivePasture => activePasture;

    public bool SessionActive => sessionStartTick >= 0;

    public bool Returning => returning;

    public string ReturnReason => returnReason;

    public bool HasGrazingAnimals => entries.Any(e => e.phase == GrazePhase.Grazing);

    public bool ShepherdPresent => shepherd != null && Find.TickManager.TicksGame - shepherdSeenTick < GuardMemoryTicks;

    public bool DogPresent => dog != null && Find.TickManager.TicksGame - dogSeenTick < GuardMemoryTicks;

    public float RoamMultiplier => GuardMultiplier(S.roamNone, S.roamDog, S.roamShepherd, S.roamBoth);

    public float PredatorMultiplier => GuardMultiplier(S.predatorNone, S.predatorDog, S.predatorShepherd, S.predatorBoth);

    private Map Map => parent.Map;

    private int Now => Find.TickManager.TicksGame;

    private float GuardMultiplier(float none, float dogOnly, float shepherdOnly, float both)
    {
        bool hasShepherd = ShepherdPresent;
        bool hasDog = DogPresent;
        if (!hasShepherd && !hasDog)
        {
            return none;
        }
        if (!hasShepherd)
        {
            return dogOnly;
        }
        float value = hasDog ? both : shepherdOnly;
        if (S.shepherdSkillScaling)
        {
            float reduction = (1f - value) * GrazingUtility.ShepherdSkillFactor(shepherd);
            value = Mathf.Clamp(1f - reduction, 0.01f, 1f);
        }
        if (hasDog)
        {
            value = Mathf.Min(value, dogOnly);
        }
        return Mathf.Min(value, none);
    }

    // ================= Periodic logic (called from MapComponent_Grazing) =================

    public void GrazingTick()
    {
        if (!parent.Spawned)
        {
            return;
        }
        ValidateConfig();
        UpdateHostileState();
        if (SessionActive)
        {
            UpdateSession();
            if (SessionActive)
            {
                AskAboutHostilesDuringSession();
            }
        }
        else if (enabled && Now - lastEndTick > RestartCooldownTicks)
        {
            TryAutoStart();
        }
    }

    // ================= Enemies on the map: ask the player instead of forbidding =================

    private void UpdateHostileState()
    {
        hostilesPresent = GenHostility.AnyHostileActiveThreatToPlayer(Map);
        if (!hostilesPresent)
        {
            hostileDialogShown = false;
            holdForHostiles = false;
        }
    }

    private void AskAboutHostilesDuringSession()
    {
        if (!returnOnDanger || !hostilesPresent || returning || hostileDialogShown)
        {
            return;
        }
        hostileDialogShown = true;
        string text = "TTG_Hostiles_DuringGrazing".Translate(PenLabel, activePasture?.label ?? "?");
        Find.WindowStack.Add(new Dialog_MessageBox(text, "TTG_Hostiles_Return".Translate(), () => BeginReturn("TTG_Reason_Hostiles".Translate()), "TTG_Hostiles_KeepGrazing".Translate(), null, "TTG_Hostiles_Title".Translate()));
    }

    // Auto start with enemies around: one question per enemy episode; "wait" holds until they are gone.
    private bool HoldAutoStartForHostiles()
    {
        if (!returnOnDanger || !hostilesPresent)
        {
            return false;
        }
        if (!holdForHostiles && !hostileDialogShown)
        {
            hostileDialogShown = true;
            string text = "TTG_Hostiles_BeforeStart".Translate(PenLabel);
            Find.WindowStack.Add(new Dialog_MessageBox(text, "TTG_Hostiles_GoOut".Translate(), () => StartNow(silentReject: true), "TTG_Hostiles_Wait".Translate(), () => holdForHostiles = true, "TTG_Hostiles_Title".Translate()));
        }
        return true;
    }

    private void ValidateConfig()
    {
        pastures.RemoveAll(z => !GrazingUtility.IsValidPasture(z, Map));
        excludedAnimals.RemoveAll(p => p == null || p.Dead || p.Faction != Faction.OfPlayer);
        if (shepherd != null && (shepherd.Dead || shepherd.Faction != Faction.OfPlayer || !shepherd.RaceProps.Humanlike))
        {
            shepherd = null;
        }
        if (dog != null && (dog.Dead || dog.Faction != Faction.OfPlayer))
        {
            dog = null;
        }
    }

    private void TryAutoStart()
    {
        PenMarkerState penState = Marker?.PenState;
        if (penState == null || !penState.Enclosed)
        {
            return;
        }
        if (useSchedule && !InSchedule())
        {
            return;
        }
        if (!useSchedule && returnAtNight && GrazingUtility.IsDark(Map))
        {
            return;
        }
        List<Pawn> candidates = GetCandidates();
        if (candidates.Count == 0)
        {
            return;
        }
        bool triggered;
        if (!triggerHunger && !triggerPenFood)
        {
            // Schedule-only mode: graze once per day.
            triggered = Now - lastStartTick > ScheduleOnlyRestartTicks;
        }
        else
        {
            triggered = (triggerHunger && candidates.Any(a => a.needs.food.CurLevelPercentage < hungerThreshold)) || (triggerPenFood && PenFoodDays() < penFoodDays);
        }
        if (!triggered)
        {
            return;
        }
        Zone_Pasture pasture = ChoosePasture(candidates);
        if (pasture == null)
        {
            return;
        }
        if (returnOnDanger && GrazingUtility.DangerReason(Map, this, candidates, pasture) != null)
        {
            return;
        }
        if (HoldAutoStartForHostiles())
        {
            return;
        }
        StartSession(pasture, candidates);
    }

    private void UpdateSession()
    {
        Map map = Map;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            GrazeEntry e = entries[i];
            Pawn a = e.animal;
            if (a == null || a.Dead || !a.Spawned || a.Map != map || a.Faction != Faction.OfPlayer || a.Downed || a.GetLord() != null)
            {
                RemoveAt(i);
                continue;
            }
            if (a.MentalStateDef == MentalStateDefOf.Roaming)
            {
                if (S.routineMessages || e.phase != GrazePhase.ToPasture)
                {
                    Messages.Message("TTG_Msg_Wandered".Translate(a.LabelShort, PenLabel), a, MessageTypeDefOf.NegativeEvent);
                }
                RemoveAt(i);
                continue;
            }
            switch (e.phase)
            {
                case GrazePhase.ToPasture:
                    if (!a.roping.IsRoped && map.zoneManager.ZoneAt(a.Position) == activePasture)
                    {
                        SetPhase(e, returning ? GrazePhase.ToPen : GrazePhase.Grazing);
                    }
                    else if (returning || Now - sessionStartTick > LeadOutTimeoutTicks)
                    {
                        if (a.roping.IsRoped)
                        {
                            break;
                        }
                        if (InPen(a))
                        {
                            RemoveAt(i);
                        }
                        else
                        {
                            SetPhase(e, GrazePhase.ToPen);
                        }
                    }
                    break;
                case GrazePhase.Grazing:
                    if (returning)
                    {
                        SetPhase(e, GrazePhase.ToPen);
                    }
                    break;
                case GrazePhase.ToPen:
                    if (!a.roping.IsRoped && (InPen(a) || AnimalPenUtility.GetCurrentPenOf(a, allowUnenclosedPens: false) != null))
                    {
                        RemoveAt(i);
                    }
                    else if (Now - e.phaseTick > StuckReturnTimeoutTicks)
                    {
                        // Give up; vanilla roping takes over.
                        RemoveAt(i);
                    }
                    break;
            }
        }
        if (entries.Count == 0)
        {
            EndSession();
            return;
        }
        UpdateGuards();
        if (!returning)
        {
            string reason = CheckReturnReason();
            if (reason != null)
            {
                BeginReturn(reason);
            }
        }
        HandlePredators();
    }

    private string CheckReturnReason()
    {
        float hours = (Now - sessionStartTick) / 2500f;
        if (!GrazingUtility.IsValidPasture(activePasture, Map))
        {
            return "TTG_Reason_PastureGone".Translate();
        }
        if (returnOnDanger)
        {
            string danger = GrazingUtility.DangerReason(Map, this, entries.Select(e => e.animal), activePasture);
            if (danger != null)
            {
                return danger;
            }
        }
        if (useSchedule && !InSchedule())
        {
            return "TTG_Reason_Schedule".Translate();
        }
        if (!useSchedule && returnAtNight && GrazingUtility.IsDark(Map))
        {
            return "TTG_Reason_Night".Translate();
        }
        if (hours >= S.maxGrazingHours)
        {
            return "TTG_Reason_MaxTime".Translate();
        }
        if (hours < S.minGrazingHours || entries.Any(e => e.phase == GrazePhase.ToPasture))
        {
            return null;
        }
        List<Pawn> grazing = entries.Where(e => e.phase == GrazePhase.Grazing).Select(e => e.animal).ToList();
        if (grazing.Count == 0)
        {
            return null;
        }
        if (returnWhenFed && grazing.All(a => a.needs?.food == null || a.needs.food.CurLevelPercentage >= fedThreshold))
        {
            return "TTG_Reason_Fed".Translate();
        }
        if (activePasture.GrassNutrition < 0.5f)
        {
            return "TTG_Reason_NoGrass".Translate();
        }
        return null;
    }

    private void UpdateGuards()
    {
        IntVec3 center = GrazingUtility.HerdCenter(this);
        if (IsGuarding(shepherd, TTG_DefOf.TTG_WatchHerd, center))
        {
            shepherdSeenTick = Now;
        }
        if (IsGuarding(dog, TTG_DefOf.TTG_GuardHerd, center))
        {
            dogSeenTick = Now;
        }
    }

    private bool IsGuarding(Pawn guard, JobDef guardJob, IntVec3 herdCenter)
    {
        if (guard == null || !guard.Spawned || guard.Map != Map || guard.Downed || guard.Drafted || !herdCenter.IsValid)
        {
            return false;
        }
        Job job = guard.CurJob;
        if (job == null)
        {
            return false;
        }
        bool onDuty = (job.def == guardJob && job.targetB.Thing == parent) || job.def == JobDefOf.AttackMelee;
        return onDuty && guard.Position.InHorDistOf(herdCenter, 25f);
    }

    private void HandlePredators()
    {
        if (!entries.Any(e => e.phase != GrazePhase.ToPasture))
        {
            return;
        }
        int now = Now;
        foreach (int key in announcedPredators.Where(kv => now - kv.Value > 60000).Select(kv => kv.Key).ToList())
        {
            announcedPredators.Remove(key);
        }
        foreach (int key in scaredPredators.Where(kv => now > kv.Value).Select(kv => kv.Key).ToList())
        {
            scaredPredators.Remove(key);
        }
        foreach (Pawn predator in GrazingUtility.PredatorsHuntingHerd(Map, this).ToList())
        {
            if (announcedPredators.ContainsKey(predator.thingIDNumber))
            {
                continue;
            }
            announcedPredators[predator.thingIDNumber] = now;
            Pawn prey = predator.CurJob.targetA.Thing as Pawn;
            Messages.Message("TTG_Msg_PredatorHunting".Translate(predator.LabelShort, prey?.LabelShort ?? "?", PenLabel), predator, MessageTypeDefOf.ThreatSmall);
            if (S.guardsRepelPredators)
            {
                TryRepel(predator);
            }
        }
    }

    private void TryRepel(Pawn predator)
    {
        if (DogPresent && dog.Spawned && !dog.Downed && dog.Map == predator.Map && dog.CurJobDef != JobDefOf.AttackMelee)
        {
            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, predator);
            attack.expiryInterval = 2500;
            attack.checkOverrideOnExpire = true;
            dog.jobs.StartJob(attack, JobCondition.InterruptForced);
            Messages.Message("TTG_Msg_DogDefends".Translate(dog.LabelShort, predator.LabelShort), dog, MessageTypeDefOf.NeutralEvent);
        }
        if (ShepherdPresent && shepherd.Spawned && !shepherd.Downed && shepherd.Map == predator.Map)
        {
            float chance = Mathf.Clamp(0.25f + 0.035f * shepherd.skills.GetSkill(SkillDefOf.Animals).Level, 0.25f, 0.95f);
            if (Rand.Chance(chance))
            {
                ScareOff(predator, shepherd);
                Messages.Message("TTG_Msg_ShepherdScared".Translate(shepherd.LabelShort, predator.LabelShort), predator, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    private void ScareOff(Pawn predator, Pawn scarer)
    {
        scaredPredators[predator.thingIDNumber] = Now + ScaredTicks;
        IntVec3 dest = CellFinderLoose.GetFleeDest(predator, new List<Thing> { scarer }, 30f);
        if (dest.IsValid && dest != predator.Position)
        {
            Job flee = JobMaker.MakeJob(JobDefOf.Flee, dest, scarer);
            predator.jobs.StartJob(flee, JobCondition.InterruptForced);
        }
        else
        {
            predator.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }

    public bool IsScared(Pawn predator)
    {
        return scaredPredators.TryGetValue(predator.thingIDNumber, out int until) && Now < until;
    }

    // ================= Session control =================

    public void StartSession(Zone_Pasture pasture, List<Pawn> animals)
    {
        pasture.EnsureGrazingArea();
        activePasture = pasture;
        sessionStartTick = Now;
        lastStartTick = Now;
        returning = false;
        returnReason = null;
        entries.Clear();
        foreach (Pawn animal in animals)
        {
            GrazeEntry entry = new GrazeEntry { animal = animal, phase = GrazePhase.ToPasture, phaseTick = Now };
            entries.Add(entry);
            GrazingRegistry.Register(this, entry);
        }
        if (S.routineMessages)
        {
            Messages.Message("TTG_Msg_Started".Translate(PenLabel, pasture.label, animals.Count), parent, MessageTypeDefOf.SilentInput, historical: false);
        }
    }

    public void BeginReturn(string reason)
    {
        if (!SessionActive || returning)
        {
            return;
        }
        returning = true;
        returnReason = reason;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            GrazeEntry e = entries[i];
            if (e.phase == GrazePhase.Grazing)
            {
                SetPhase(e, GrazePhase.ToPen);
            }
            else if (e.phase == GrazePhase.ToPasture && !e.animal.roping.IsRoped && InPen(e.animal))
            {
                RemoveAt(i);
            }
        }
        if (S.routineMessages)
        {
            Messages.Message("TTG_Msg_Returning".Translate(PenLabel, reason), parent, MessageTypeDefOf.SilentInput, historical: false);
        }
        if (entries.Count == 0)
        {
            EndSession();
        }
    }

    private void EndSession()
    {
        GrazingRegistry.UnregisterAll(this);
        entries.Clear();
        Zone_Pasture pasture = activePasture;
        activePasture = null;
        if (pasture != null && parent.Map != null && !GrazingUtility.CompsOn(parent.Map).Any(c => c != this && c.ActivePasture == pasture))
        {
            pasture.ReleaseGrazingArea();
        }
        sessionStartTick = -1;
        returning = false;
        lastEndTick = Now;
    }

    public void Notify_ArrivedAtPasture(Pawn animal)
    {
        GrazeEntry e = entries.FirstOrDefault(x => x.animal == animal);
        if (e != null && e.phase == GrazePhase.ToPasture)
        {
            SetPhase(e, returning ? GrazePhase.ToPen : GrazePhase.Grazing);
        }
    }

    public void ManualStart()
    {
        if (GenHostility.AnyHostileActiveThreatToPlayer(Map))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("TTG_Hostiles_ConfirmManual".Translate(PenLabel), () => StartNow(silentReject: false), destructive: true));
            return;
        }
        StartNow(silentReject: false);
    }

    private void StartNow(bool silentReject)
    {
        if (SessionActive)
        {
            if (!silentReject)
            {
                Messages.Message("TTG_Msg_AlreadyGrazing".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
            }
            return;
        }
        List<Pawn> candidates = GetCandidates();
        if (candidates.Count == 0)
        {
            Messages.Message("TTG_Msg_NoCandidates".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        Zone_Pasture pasture = ChoosePasture(candidates);
        if (pasture == null)
        {
            Messages.Message("TTG_Msg_NoPasture".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }
        // The player has already accepted the risk for this enemy episode.
        hostileDialogShown = hostilesPresent || hostileDialogShown;
        StartSession(pasture, candidates);
    }

    public void ManualReturn()
    {
        BeginReturn("TTG_Reason_Manual".Translate());
    }

    private void SetPhase(GrazeEntry e, GrazePhase phase)
    {
        e.phase = phase;
        e.phaseTick = Now;
    }

    private void RemoveAt(int index)
    {
        GrazingRegistry.Unregister(entries[index].animal);
        entries.RemoveAt(index);
    }

    // ================= Queries =================

    public bool InPen(Pawn animal)
    {
        PenMarkerState state = Marker?.PenState;
        return state != null && state.Enclosed && state.ContainsConnectedRegion(animal.GetRegion());
    }

    public bool InSchedule()
    {
        return GrazingUtility.InHourWindow(GenLocalDate.HourOfDay(Map), hours);
    }

    public List<Pawn> AnimalsInPen()
    {
        List<Pawn> result = new List<Pawn>();
        foreach (Pawn animal in Map.mapPawns.SpawnedColonyAnimals)
        {
            if (AnimalPenUtility.NeedsToBeManagedByRope(animal) && InPen(animal))
            {
                result.Add(animal);
            }
        }
        return result;
    }

    public List<Pawn> GetCandidates()
    {
        List<Pawn> result = new List<Pawn>();
        foreach (Pawn animal in AnimalsInPen())
        {
            if (IsEligible(animal))
            {
                result.Add(animal);
            }
        }
        return result;
    }

    public bool IsEligible(Pawn animal)
    {
        if (!animal.RaceProps.Eats(FoodTypeFlags.Plant) || animal.needs?.food == null)
        {
            return false;
        }
        if (animal.Downed || animal.InMentalState || animal.roping.IsRoped || animal == dog || excludedAnimals.Contains(animal) || GrazingRegistry.Contains(animal))
        {
            return false;
        }
        DesignationManager designations = Map.designationManager;
        if (designations.DesignationOn(animal, DesignationDefOf.Slaughter) != null || designations.DesignationOn(animal, DesignationDefOf.ReleaseAnimalToWild) != null)
        {
            return false;
        }
        if (excludeSick && IsSickOrHurt(animal))
        {
            return false;
        }
        if (excludePregnant && animal.health.hediffSet.HasHediff(HediffDefOf.Pregnant))
        {
            return false;
        }
        if (excludeYoung && animal.ageTracker.CurLifeStageIndex < animal.RaceProps.lifeStageAges.Count - 1)
        {
            return false;
        }
        return true;
    }

    private static bool IsSickOrHurt(Pawn animal)
    {
        if (animal.health.HasHediffsNeedingTend() || animal.health.hediffSet.BleedRateTotal > 0f || animal.health.summaryHealth.SummaryHealthPercent < 0.85f)
        {
            return true;
        }
        // Malnutrition is why the herd goes out in the first place, never a reason to keep it in.
        foreach (Hediff hediff in animal.health.hediffSet.hediffs)
        {
            if (hediff.def.makesSickThought && hediff.def != HediffDefOf.Malnutrition && hediff.Visible)
            {
                return true;
            }
        }
        return false;
    }

    public float PenFoodDays()
    {
        CompAnimalPenMarker marker = Marker;
        PenMarkerState state = marker?.PenState;
        if (state == null || !state.Enclosed)
        {
            return float.PositiveInfinity;
        }
        PenFoodCalculator calc = marker.PenFoodCalculator;
        float net = calc.SumNutritionConsumptionPerDay - calc.NutritionPerDayToday;
        if (net <= 0.001f)
        {
            return float.PositiveInfinity;
        }
        float stock = GrazingUtility.GrassNutritionIn(state.ConnectedRegions) + calc.sumStockpiledNutritionAvailableNow;
        return stock / net;
    }

    public Zone_Pasture ChoosePasture(List<Pawn> herd)
    {
        Zone_Pasture best = null;
        float bestNutrition = 0f;
        Pawn sample = herd.FirstOrDefault();
        foreach (Zone_Pasture pasture in pastures)
        {
            if (!GrazingUtility.IsValidPasture(pasture, Map) || !PastureReachable(pasture, sample))
            {
                continue;
            }
            float nutrition = pasture.GrassNutrition;
            if (nutrition >= 1f && (best == null || nutrition > bestNutrition))
            {
                best = pasture;
                bestNutrition = nutrition;
            }
        }
        return best;
    }

    private bool PastureReachable(Zone_Pasture pasture, Pawn sampleAnimal)
    {
        TraverseParms parms = TraverseParms.For(TraverseMode.PassDoors);
        if (sampleAnimal != null)
        {
            parms = parms.WithFenceblockedOf(sampleAnimal);
        }
        int checks = Mathf.Min(pasture.cells.Count, 5);
        for (int i = 0; i < checks; i++)
        {
            IntVec3 cell = pasture.cells[i * pasture.cells.Count / checks];
            if (Map.reachability.CanReach(parent.Position, cell, PathEndMode.OnCell, parms))
            {
                return true;
            }
        }
        return false;
    }

    public IntVec3 FindPastureDestCell(Pawn roper, Pawn animal)
    {
        if (!GrazingUtility.IsValidPasture(activePasture, Map))
        {
            return IntVec3.Invalid;
        }
        Map map = Map;
        TraverseParms parms = TraverseParms.For(roper).WithFenceblockedOf(animal);
        IntVec3 penPos = parent.Position;
        List<IntVec3> best = activePasture.cells
            .Where(c => c.Standable(map) && !c.IsForbidden(roper))
            .OrderBy(c => c.DistanceToSquared(penPos))
            .Take(40)
            .Where(c => map.reachability.CanReach(roper.Position, c, PathEndMode.OnCell, parms))
            .Take(10)
            .ToList();
        return best.Count > 0 ? best.RandomElement() : IntVec3.Invalid;
    }

    public bool MayLeadOut(Pawn handler, bool forced)
    {
        if (forced || shepherd == null || shepherd == handler)
        {
            return true;
        }
        // The assigned shepherd gets the first two hours to come and pick the herd up.
        bool shepherdCanWork = shepherd.Spawned && shepherd.Map == Map && !shepherd.Downed && !shepherd.Drafted && !shepherd.WorkTypeIsDisabled(WorkTypeDefOf.Handling);
        return !shepherdCanWork || Now - sessionStartTick > 5000;
    }

    public string StatusText()
    {
        StringBuilder sb = new StringBuilder();
        if (!SessionActive)
        {
            sb.Append(enabled ? "TTG_Status_Waiting".Translate() : "TTG_Status_Disabled".Translate());
            return sb.ToString();
        }
        int toPasture = entries.Count(e => e.phase == GrazePhase.ToPasture);
        int grazing = entries.Count(e => e.phase == GrazePhase.Grazing);
        int toPen = entries.Count(e => e.phase == GrazePhase.ToPen);
        sb.Append(returning ? "TTG_Status_Returning".Translate(returnReason ?? "") : "TTG_Status_Grazing".Translate(activePasture?.label ?? "?"));
        sb.AppendLine();
        sb.Append("TTG_Status_Counts".Translate(toPasture, grazing, toPen));
        return sb.ToString();
    }

    // ================= ThingComp overrides =================

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref enabled, "enabled");
        Scribe_Collections.Look(ref pastures, "pastures", LookMode.Reference);
        Scribe_Values.Look(ref triggerHunger, "triggerHunger", true);
        Scribe_Values.Look(ref hungerThreshold, "hungerThreshold", 0.3f);
        Scribe_Values.Look(ref triggerPenFood, "triggerPenFood");
        Scribe_Values.Look(ref penFoodDays, "penFoodDays", 2f);
        Scribe_Values.Look(ref useSchedule, "useSchedule", true);
        Scribe_Values.Look(ref hours, "hours", new IntRange(7, 19));
        Scribe_Values.Look(ref returnWhenFed, "returnWhenFed", true);
        Scribe_Values.Look(ref fedThreshold, "fedThreshold", 0.9f);
        Scribe_Values.Look(ref returnAtNight, "returnAtNight", true);
        Scribe_Values.Look(ref returnOnDanger, "returnOnDanger", true);
        Scribe_Values.Look(ref excludeSick, "excludeSick", true);
        Scribe_Values.Look(ref excludePregnant, "excludePregnant");
        Scribe_Values.Look(ref excludeYoung, "excludeYoung");
        Scribe_Collections.Look(ref excludedAnimals, "excludedAnimals", LookMode.Reference);
        Scribe_References.Look(ref shepherd, "shepherd");
        Scribe_References.Look(ref dog, "dog");
        Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
        Scribe_References.Look(ref activePasture, "activePasture");
        Scribe_Values.Look(ref sessionStartTick, "sessionStartTick", -1);
        Scribe_Values.Look(ref returning, "returning");
        Scribe_Values.Look(ref returnReason, "returnReason");
        Scribe_Values.Look(ref lastStartTick, "lastStartTick", -99999);
        Scribe_Values.Look(ref lastEndTick, "lastEndTick", -99999);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            pastures ??= new List<Zone_Pasture>();
            pastures.RemoveAll(z => z == null);
            excludedAnimals ??= new List<Pawn>();
            excludedAnimals.RemoveAll(p => p == null);
            entries ??= new List<GrazeEntry>();
            entries.RemoveAll(e => e?.animal == null);
            if (SessionActive && (activePasture == null || entries.Count == 0))
            {
                entries.Clear();
                activePasture = null;
                sessionStartTick = -1;
                returning = false;
            }
            foreach (GrazeEntry entry in entries)
            {
                GrazingRegistry.Register(this, entry);
            }
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (SessionActive)
        {
            activePasture?.EnsureGrazingArea();
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        if (SessionActive)
        {
            EndSession();
        }
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        base.PostDrawExtraSelectionOverlays();
        foreach (Zone_Pasture pasture in pastures)
        {
            if (!GrazingUtility.IsValidPasture(pasture, Map))
            {
                continue;
            }
            Color color = pasture == activePasture ? new Color(1f, 0.9f, 0.3f) : new Color(0.55f, 0.85f, 0.35f);
            GenDraw.DrawFieldEdges(pasture.cells, color);
            GenDraw.DrawLineBetween(parent.TrueCenter(), pasture.Position.ToVector3Shifted(), SimpleColor.Green);
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }
        if (parent.Faction != Faction.OfPlayer)
        {
            yield break;
        }
        if (!SessionActive)
        {
            Command_Action start = new Command_Action
            {
                defaultLabel = "TTG_StartNow".Translate(),
                defaultDesc = "TTG_StartNowDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/TTG_StartGrazing"),
                action = ManualStart
            };
            if (pastures.Count == 0)
            {
                start.Disable("TTG_NoPastureLinked".Translate());
            }
            yield return start;
        }
        else if (!returning)
        {
            yield return new Command_Action
            {
                defaultLabel = "TTG_ReturnNow".Translate(),
                defaultDesc = "TTG_ReturnNowDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/TTG_ReturnHerd"),
                action = ManualReturn
            };
        }
    }

    public override string CompInspectStringExtra()
    {
        if (!enabled && !SessionActive)
        {
            return null;
        }
        return "TTG_InspectPrefix".Translate() + ": " + StatusText().Replace("\n", "; ").TrimEnd();
    }
}
