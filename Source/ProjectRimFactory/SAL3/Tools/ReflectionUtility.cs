﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;


namespace ProjectRimFactory.SAL3
{
    public static class ReflectionUtility
    {
        public static readonly FieldInfo mapIndexOrState = typeof(Thing).GetField("mapIndexOrState", BindingFlags.NonPublic | BindingFlags.Instance);
        //public static readonly FieldInfo cachedDisabledWorkTypes = typeof(Pawn_StoryTracker).GetField("cachedDisabledWorkTypes", BindingFlags.Instance | BindingFlags.NonPublic);
        public static readonly FieldInfo cachedDisabledWorkTypesPermanent = typeof(Pawn).GetField("cachedDisabledWorkTypesPermanent", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo cachedTotallyDisabled = typeof(SkillRecord).GetField("cachedTotallyDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo ingredientsOrdered = typeof(WorkGiver_DoBill).GetField("ingredientsOrdered", BindingFlags.NonPublic | BindingFlags.Static);
        // RimWorld.WorkGier_DoBill's static TryFindBestBillIngredientsInSet: expects a list of (valid) available ingredients for a bill, 
        //   fills a list of chosen ingredients for that bill if it returns true
        public static readonly MethodInfo TryFindBestBillIngredientsInSet = typeof(WorkGiver_DoBill).GetMethod("TryFindBestBillIngredientsInSet", BindingFlags.NonPublic | BindingFlags.Static);
        public static readonly MethodInfo MakeIngredientsListInProcessingOrder = typeof(WorkGiver_DoBill).GetMethod("MakeIngredientsListInProcessingOrder", BindingFlags.NonPublic | BindingFlags.Static);

        //For SAL Deep Drill Support
        public static readonly FieldInfo drill_portionProgress = typeof(CompDeepDrill).GetField("portionProgress", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo drill_portionYieldPct = typeof(CompDeepDrill).GetField("portionYieldPct", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo drill_lastUsedTick = typeof(CompDeepDrill).GetField("lastUsedTick", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly MethodInfo drill_TryProducePortion = typeof(CompDeepDrill).GetMethod("TryProducePortion", BindingFlags.NonPublic | BindingFlags.Instance);

        //BackCompatibility
        public static readonly FieldInfo BackCompatibility_conversionChain = typeof(BackCompatibility).GetField("conversionChain", BindingFlags.NonPublic | BindingFlags.Static);

        public static readonly FieldInfo ResearchManager_progress = typeof(ResearchManager).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic);
        
        public static readonly FieldInfo allRecipesCached = typeof(ThingDef).GetField("allRecipesCached", BindingFlags.Instance | BindingFlags.NonPublic);

        //reservations
        public static readonly FieldInfo sal_reservations = typeof(Verse.AI.ReservationManager).GetField("reservations", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo sal_reservations2 = typeof(Verse.AI.ReservationManager).GetField("reservations", BindingFlags.NonPublic | BindingFlags.Static);

        //Pod Launcher
        public static readonly FieldInfo LandInSpecificCellGetCell = typeof(RimWorld.Planet.TransportPodsArrivalAction_LandInSpecificCell).GetField("cell", BindingFlags.NonPublic | BindingFlags.Instance);



        public static readonly MethodInfo SRTS_TryLaunch = HarmonyLib.AccessTools.Method("SRTS.CompLaunchableSRTS:TryLaunch");
        public static readonly MethodInfo SOS2_TryLaunch = HarmonyLib.AccessTools.Method("RimWorld.CompShuttleLaunchable:TryLaunch");
        public static readonly MethodInfo SRTS_StartChoosingDestination = HarmonyLib.AccessTools.Method("SRTS.CompLaunchableSRTS:StartChoosingDestination");
        public static readonly MethodInfo SOS2_StartChoosingDestination = HarmonyLib.AccessTools.Method("RimWorld.CompShuttleLaunchable:StartChoosingDestination");



    }
}
