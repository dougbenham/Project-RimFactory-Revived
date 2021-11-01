using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using static ProjectRimFactory.AutoMachineTool.Ops;
using ProjectRimFactory.Common;
using ProjectRimFactory.SAL3;



namespace ProjectRimFactory.AutoMachineTool
{

    public class PRF_SAL_Trarget
    {
        public enum SAL_TargetType
        {
            Invalid,
            ReserchBench,
            Drill,
            WorkTable
        }


        private abstract class Type
        {
            public Building TargetBuilding { get; set; }
            public Building_AutoMachineTool SAL { get; set; } 
            public abstract void Reserve();
            public abstract void Free();
            public abstract void WorkDone(out List<Thing> products);
            public abstract bool TryStartWork(out float workAmount);
            public abstract bool Ready();
            public abstract void CreateWorkingEffect(MapTickManager manager);
            public abstract void CleanupWorkingEffect(MapTickManager manager);
            public abstract void ExposeData();
            public abstract void Reset(WorkingState state);

            public Type(Building building, Building_AutoMachineTool sal)
            {
                TargetBuilding = building;
                SAL = sal;
            }

        }

        private abstract class Type_General : Type
        {
            override public void Reserve()
            {
                if (PRFGameComponent.PRF_StaticPawn == null) PRFGameComponent.GenStaticPawn();
                PRFGameComponent.PRF_StaticJob ??= new Job(PRFDefOf.PRFStaticJob);

                List<ReservationManager.Reservation> reservations;
                reservations = (List<ReservationManager.Reservation>)ReflectionUtility.sal_reservations.GetValue(TargetBuilding.Map.reservationManager);
                var res = new ReservationManager.Reservation(PRFGameComponent.PRF_StaticPawn, PRFGameComponent.PRF_StaticJob, 1, -1, TargetBuilding, null);

                if (!reservations.Where(r => r.Claimant == PRFGameComponent.PRF_StaticPawn && r.Job == PRFGameComponent.PRF_StaticJob && r.Target == TargetBuilding).Any()) reservations.Add(res);
                ReflectionUtility.sal_reservations.SetValue(TargetBuilding.Map.reservationManager, reservations);
            }
            override public void Free()
            {
                if (PRFGameComponent.PRF_StaticPawn == null) PRFGameComponent.GenStaticPawn();
                PRFGameComponent.PRF_StaticJob ??= new Job(PRFDefOf.PRFStaticJob);

                TargetBuilding.Map.reservationManager.Release(TargetBuilding, PRFGameComponent.PRF_StaticPawn, PRFGameComponent.PRF_StaticJob);
            }
            override public abstract void WorkDone(out List<Thing> products);

            public override void CreateWorkingEffect(MapTickManager manager)
            {
                
            }

            public override void CleanupWorkingEffect(MapTickManager manager)
            {
                SAL.WorkingEffect?.Cleanup();
                SAL.WorkingEffect = null;

                SAL.WorkingSound?.End();
                SAL.WorkingSound = null;
            }

            public override void ExposeData()
            {
                
            }

            public override void Reset(WorkingState state)
            {
                
            }

            public Type_General(Building building, Building_AutoMachineTool sal) : base (building,sal)
            {

            }

        }
        private class Type_Drill : Type_General
        {
            //Based Upon Vanilla but capped at 1 to reduce unessesary calculations
            private readonly float[] miningyieldfactors = { 0.6f, 0.7f, 0.8f, 0.85f, 0.9f, 0.925f, 0.95f, 0.975f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

            private const float DeepDrill_WorkAmount = 1000f;

            public override void WorkDone(out List<Thing> products)
            {
                products = new List<Thing>();
                //From my understanding this WorkDone is added each pawn.tick
                //We dont want this with reflection so i will use a multiplier instead --> DeepDrill_WorkAmount

                CompDeepDrill compDeepDrill = TargetBuilding.TryGetComp<CompDeepDrill>();

                //Vanilla Mining Speed Calc may need an Update if Vanilla is Updated 
                float statValue = DeepDrill_WorkAmount * Mathf.Max(SAL.powerWorkSetting.GetSpeedFactor() * (SAL.GetSkillLevel(SkillDefOf.Mining) * 0.12f + 0.04f), 0.1f);

                ReflectionUtility.drill_portionProgress.SetValue(compDeepDrill, (float)ReflectionUtility.drill_portionProgress.GetValue(compDeepDrill) + statValue);
                ReflectionUtility.drill_portionYieldPct.SetValue(compDeepDrill, (float)ReflectionUtility.drill_portionYieldPct.GetValue(compDeepDrill) + statValue * miningyieldfactors[SAL.GetSkillLevel(SkillDefOf.Mining)] / 10000f);
                ReflectionUtility.drill_lastUsedTick.SetValue(compDeepDrill, Find.TickManager.TicksGame);
                if ((float)ReflectionUtility.drill_portionProgress.GetValue(compDeepDrill) > 10000f)
                {
                    ReflectionUtility.drill_TryProducePortion.Invoke(compDeepDrill, new object[] { ReflectionUtility.drill_portionYieldPct.GetValue(compDeepDrill), null });
                    ReflectionUtility.drill_portionProgress.SetValue(compDeepDrill, 0);
                    ReflectionUtility.drill_portionYieldPct.SetValue(compDeepDrill, 0);
                }
            }

            public override bool TryStartWork(out float workAmount)
            {
                CompDeepDrill compDeepDrill = TargetBuilding.TryGetComp<CompDeepDrill>();
                if (compDeepDrill.CanDrillNow())
                {
                    workAmount = DeepDrill_WorkAmount;
                    return true;
                }
                else
                {
                    workAmount = 0;
                    return false;
                }


            }

            public override bool Ready()
            {
                return TargetBuilding.TryGetComp<CompDeepDrill>().CanDrillNow() == false;
            }

            public Type_Drill(Building building, Building_AutoMachineTool sal) : base(building, sal)
            {

            }
        }

        private class Type_Reserch : Type_General
        {
            public override void WorkDone(out List<Thing> products)
            {
                products = new List<Thing>();
                if (Find.ResearchManager.currentProj != null)
                {

                    float statValue = Mathf.Max(SAL.powerWorkSetting.GetSpeedFactor() * (SAL.GetSkillLevel(SkillDefOf.Intellectual) * 0.115f + 0.08f), 0.1f);
                    statValue *= TargetBuilding.GetStatValue(StatDefOf.ResearchSpeedFactor);

                    statValue /= Find.ResearchManager.currentProj.CostFactor(Faction.OfPlayer.def.techLevel);
                    //Multiplier set to 100 instead of 1000 as the speedf factor is so powerfull (would be way too fast)
                    statValue *= 100;

                    Find.ResearchManager.ResearchPerformed(statValue, null);

                }
            }

            public override bool TryStartWork(out float workAmount)
            {
                if (Find.ResearchManager.currentProj != null)
                {
                    workAmount = 1000f;
                    return true;
                }
                else
                {
                    workAmount = 0;
                    return false;
                }
            }

            public override bool Ready()
            {
                return (Find.ResearchManager.currentProj == null || !Find.ResearchManager.currentProj.CanBeResearchedAt((Building_ResearchBench)TargetBuilding, false));
            }

            public Type_Reserch(Building building, Building_AutoMachineTool sal) : base(building, sal)
            {

            }
        }

        private class Type_WorkTable : Type
        {

            private Bill bill = null;
            private List<Thing> ingredients;
            private Thing dominant;
            private UnfinishedThing unfinished;

            private class ThingAmount
            {
                public ThingAmount(Thing thing, int count)
                {
                    this.thing = thing;
                    this.count = count;
                }

                public Thing thing;

                public int count;
            }
            public Building_WorkTable WorkTable { get; set; }

            public override void Free()
            {
                AllowBills();
            }

            public override void Reserve()
            {
                ForbidBills();
            }

            public override void WorkDone(out List<Thing> products)
            {
                products = GenRecipe2.MakeRecipeProducts(this.bill.recipe, SAL, this.ingredients, this.dominant, WorkTable, this.bill.precept).ToList();

                this.ingredients.ForEach(i => bill.recipe.Worker.ConsumeIngredient(i, bill.recipe, WorkTable.Map));
                Option(this.unfinished).ForEach(u => u.Destroy(DestroyMode.Vanish));
                this.bill.Notify_IterationCompleted(null, this.ingredients);

                this.bill = null;
                this.dominant = null;
                this.unfinished = null;
                this.ingredients = null;
                // Because we use custom GenRecipe2, we have to handle bonus items and product modifications directly:
                SAL.ModifyProductExt?.ProcessProducts(products, this as IBillGiver, SAL, this.bill.recipe); // this as IBillGiver is probably null
            }
            public override bool TryStartWork(out float workAmount)
            {
                var consumable = Consumable();

                List<ThingAmount> things;

                Bill nextbill = GetnextBill(consumable, out things);
                if (nextbill != null)
                {
                    this.bill = nextbill;

                    this.ingredients = things?.Where(t => t.count > 0).Select(t => t.thing.SplitOff(t.count))?.ToList() ?? new List<Thing>();

                    //Get dominant ingredient
                    this.dominant = this.DominantIngredient(this.ingredients);


                    if (this.bill.recipe.UsesUnfinishedThing)
                    {
                        ThingDef stuff = (!this.bill.recipe.unfinishedThingDef.MadeFromStuff) ? null : this.dominant.def;
                        this.unfinished = (UnfinishedThing)ThingMaker.MakeThing(this.bill.recipe.unfinishedThingDef, stuff);
                        this.unfinished.BoundBill = (Bill_ProductionWithUft)this.bill;
                        this.unfinished.ingredients = this.ingredients;
                        CompColorable compColorable = this.unfinished.TryGetComp<CompColorable>();
                        if (compColorable != null)
                        {
                            compColorable.SetColor(this.dominant.DrawColor);
                        }
                    }

                    ThingDef thingDef = null;
                    if (this.bill.recipe.UsesUnfinishedThing && this.bill.recipe.unfinishedThingDef.MadeFromStuff)
                    {
                        thingDef = this.bill.recipe.UsesUnfinishedThing ? this.dominant?.def : null;
                    }
                    workAmount = this.bill.recipe.WorkAmountTotal(thingDef);

                    return true;

                }
                else
                {
                    workAmount = 0;
                    return false;
                }
            }
            public override bool Ready()
            {
                return (!WorkTable.CurrentlyUsableForBills() || !WorkTable.billStack.AnyShouldDoNow);
            }

            public override void CreateWorkingEffect(MapTickManager manager)
            {
                

                SAL.WorkingEffect = this.bill.recipe.effectWorking?.Spawn();

                SAL.WorkingSound = this.bill.recipe.soundWorking?.TrySpawnSustainer(WorkTable);
                SAL.WorkingSound?.Maintain();

                manager.EachTickAction(EffectTick);
            }

            public override void CleanupWorkingEffect(MapTickManager manager)
            {
                SAL.WorkingEffect?.Cleanup();
                SAL.WorkingEffect = null;

                SAL.WorkingSound?.End();
                SAL.WorkingSound = null;

                manager.RemoveEachTickAction(EffectTick);
            }

            public override void ExposeData()
            {
                Scribe_Deep.Look<UnfinishedThing>(ref this.unfinished, "unfinished");

                Scribe_References.Look<Bill>(ref this.bill, "bill");
                Scribe_References.Look<Thing>(ref this.dominant, "dominant");
                Scribe_Collections.Look<Thing>(ref this.ingredients, "ingredients", LookMode.Deep);
            }

            public override void Reset(WorkingState state)
            {
                if (state == WorkingState.Working)
                {
                    if (this.unfinished == null)
                    {
                        this.ingredients.ForEach(t => GenPlace.TryPlaceThing(t, SAL.Position, SAL.Map, ThingPlaceMode.Near));
                    }
                    else
                    {
                        GenPlace.TryPlaceThing(this.unfinished, SAL.Position, SAL.Map, ThingPlaceMode.Near);
                        this.unfinished.Destroy(DestroyMode.Cancel);
                    }
                }

                this.bill = null;
                this.dominant = null;
                this.unfinished = null;
                this.ingredients = null;
            }

            protected bool EffectTick()
            {
                SAL.WorkingEffect?.EffectTick(new TargetInfo(SAL), new TargetInfo(WorkTable));

                return SAL.WorkingEffect == null;
            }

            private List<Thing> Consumable()
            {
                return SAL.GetAllTargetCells()
                    .SelectMany(c => c.AllThingsInCellForUse(SAL.Map)) // Use GatherThingsUtility to also grab from belts
                    .Distinct<Thing>().ToList();
            }

            private Bill GetnextBill(List<Thing> consumable, out List<ThingAmount> ingredients)
            {
                ingredients = new List<ThingAmount>();
                //Return null as Workbench is not ready
                if (!WorkTable.CurrentlyUsableForBills()) return null;
                foreach (Bill bill in WorkTable.billStack)
                {
                    //Ready to start?
                    if (!bill.ShouldDoNow() || !bill.recipe.AvailableNow) continue;
                    //Sufficiant skills?
                    if (!bill.recipe.skillRequirements?.All(r => r.minLevel <= SAL.GetSkillLevel(r.skill)) ?? false) continue;

                    if (bill.recipe.ingredients.Count == 0)
                    {
                        ingredients = null;
                        return bill;
                    }
                    if (consumable == null) continue;
                    ingredients = Ingredients(bill, consumable);
                    if (ingredients.Count > 0) return bill;

                }
                ingredients = new List<ThingAmount>();
                return null;

            }

            /// <summary>
            /// I guess thet finds the correct ingridiants for the bill
            /// </summary>
            /// <param name="bill"></param>
            /// <param name="consumable"></param>
            /// <returns></returns>
            private List<ThingAmount> Ingredients(Bill bill, List<Thing> consumable)
            {
                var initial = consumable
                    //                .Where(c => bill.IsFixedOrAllowedIngredient(c))
                    .Select(x => new ThingAmount(x, x.stackCount))
                    .ToList();

                Func<List<ThingAmount>, List<ThingDefGroup>> grouping = (consumableAmounts) =>
                    consumableAmounts
                        .GroupBy(c => c.thing.def)
                        .Select(c => new { Def = c.Key, Count = c.Sum(t => t.count), Amounts = c.Select(t => t) })
                        .OrderByDescending(g => g.Def.IsStuff)
                        .ThenByDescending(g => g.Count * bill.recipe.IngredientValueGetter.ValuePerUnitOf(g.Def))
                        .Select(g => new ThingDefGroup() { def = g.Def, consumable = g.Amounts.ToList() })
                        .ToList();

                var grouped = grouping(initial);

                var ingredients = bill.recipe.ingredients.Select(i =>
                {
                    var result = new List<ThingAmount>();
                    float remain = i.GetBaseCount();

                    foreach (var things in grouped)
                    {
                        foreach (var amount in things.consumable)
                        {
                            var thing = amount.thing;
                            if (i.filter.Allows(thing) && (bill.ingredientFilter.Allows(thing) || i.IsFixedIngredient) && !SAL.Map.reservationManager.AllReservedThings().Contains(thing))
                            {
                                remain = remain - bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def) * amount.count;
                                int consumption = amount.count;
                                if (remain <= 0.0f)
                                {
                                    consumption -= Mathf.RoundToInt(-remain / bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def));
                                    remain = 0.0f;
                                }
                                result.Add(new ThingAmount(thing, consumption));
                            }
                            if (remain <= 0.0f)
                                break;
                        }
                        if (remain <= 0.0f)
                            break;

                        if ((things.def.IsStuff && bill.recipe.productHasIngredientStuff) || !bill.recipe.allowMixingIngredients)
                        {
                            // ミックスしたり、stuffの場合には、一つの要求素材に複数種類のものを混ぜられない.
                            // なので、この種類では満たせなかったので、残りを戻して、中途半端に入った利用予定を空にする.
                            remain = i.GetBaseCount();
                            result.Clear();
                        }
                    }

                    if (remain <= 0.0f)
                    {
                        // 残りがなく、必要分が全て割り当てられれば、割り当てた分を減らして、その状態でソートして割り当て分を返す.
                        result.ForEach(r =>
                        {
                            var list = grouped.Find(x => x.def == r.thing.def).consumable;
                            var c = list.Find(x => x.thing == r.thing);
                            list.Remove(c);
                            c.count = c.count - r.count;
                            list.Add(c);
                        });
                        grouped = grouping(grouped.SelectMany(x => x.consumable).ToList());
                        return result;
                    }
                    else
                    {
                        // 割り当てできなければ、空リスト.
                        return new List<ThingAmount>();
                    }
                }).ToList();

                if (ingredients.All(x => x.Count > 0))
                {
                    return ingredients.SelectMany(c => c).ToList();
                }
                else
                {
                    return new List<ThingAmount>();
                }
            }

            private struct ThingDefGroup
            {
                public ThingDef def;
                public List<ThingAmount> consumable;
            }


            private Thing DominantIngredient(List<Thing> ingredients)
            {
                if (ingredients.Count == 0)
                {
                    return null;
                }
                if (this.bill.recipe.productHasIngredientStuff)
                {
                    return ingredients[0];
                }
                if (this.bill.recipe.products.Any(x => x.thingDef.MadeFromStuff))
                {
                    return ingredients.Where(x => x.def.IsStuff).RandomElementByWeight((Thing x) => (float)x.stackCount);
                }
                return ingredients.RandomElementByWeight((Thing x) => (float)x.stackCount);
            }


            #region WorkTableReserve

            public interface IBill_PawnForbidded
            {
                Bill Original { get; set; }
            }

            /// <summary>
            /// Forbid bills to normal Pawns by converting them to a new bill type
            /// While saving the Original for restoration later
            /// </summary>
            private void ForbidBills()
            {
                if (WorkTable.BillStack.Bills.Any(b => !(b is IBill_PawnForbidded)))
                {
                    var tmp = WorkTable.BillStack.Bills.ToList();
                    WorkTable.BillStack.Clear();
                    WorkTable.BillStack.Bills.AddRange(tmp.SelectMany(b =>
                    {
                        var forbidded = b as IBill_PawnForbidded;
                        if (forbidded == null)
                        {
                            if (b is Bill_ProductionWithUft)
                            {
                                forbidded = ((Bill_ProductionWithUft)b).CopyTo((Bill_ProductionWithUftPawnForbidded)Activator.CreateInstance(typeof(Bill_ProductionWithUftPawnForbidded), b.recipe));
                                ((Bill_Production)b).repeatMode = BillRepeatModeDefOf.Forever;
                                forbidded.Original = b;
                            }
                            else if (b is Bill_Production)
                            {
                                forbidded = ((Bill_Production)b).CopyTo((Bill_ProductionPawnForbidded)Activator.CreateInstance(typeof(Bill_ProductionPawnForbidded), b.recipe));
                                ((Bill_Production)b).repeatMode = BillRepeatModeDefOf.Forever;
                                forbidded.Original = b;
                            }
                        }
                        return Option((Bill)forbidded);
                    }));
                }
            }
            /// <summary>
            /// Unforbid Bills by restoring them to the correct class Called after ForbidBills
            /// </summary>
            private void AllowBills()
            {
                if (WorkTable.BillStack.Bills.Any(b => b is IBill_PawnForbidded))
                {
                    var tmp = WorkTable.BillStack.Bills.ToList();
                    WorkTable.BillStack.Clear();
                    WorkTable.BillStack.Bills.AddRange(tmp.SelectMany(b =>
                    {
                        var forbidded = b as IBill_PawnForbidded;
                        Bill unforbbided = b;
                        if (forbidded != null)
                        {
                            if (b is Bill_ProductionWithUft)
                            {
                                unforbbided = ((Bill_ProductionWithUft)b).CopyTo((Bill_ProductionWithUft)Activator.CreateInstance(forbidded.Original?.GetType() ?? typeof(Bill_ProductionWithUft), b.recipe));
                            }
                            else if (b is Bill_Production)
                            {
                                unforbbided = ((Bill_Production)b).CopyTo((Bill_Production)Activator.CreateInstance(forbidded.Original?.GetType() ?? typeof(Bill_Production), b.recipe));
                            }
                        }
                        return Option(unforbbided);
                    }));
                }
            }


            /// <summary>
            /// Used to reseve the workbench
            /// </summary>
            public class Bill_ProductionPawnForbidded : Bill_Production, IBill_PawnForbidded
            {
                public Bill_ProductionPawnForbidded() : base()
                {
                }

                public Bill_ProductionPawnForbidded(RecipeDef recipe) : base(recipe)
                {
                }

                public override bool PawnAllowedToStartAnew(Pawn p)
                {
                    return false;
                }

                public override void ExposeData()
                {
                    base.ExposeData();
                    Scribe_Deep.Look(ref this.original, "original");
                    if (Scribe.mode == LoadSaveMode.PostLoadInit)
                    {
                        this.original.billStack = this.billStack;
                    }
                }

                public Bill original;

                public Bill Original { get => this.original; set => this.original = value; }

                public override Bill Clone()
                {
                    var clone = (Bill_Production)this.original.Clone();
                    return this.CopyTo(clone);
                }

                public override void Notify_DoBillStarted(Pawn billDoer)
                {
                    base.Notify_DoBillStarted(billDoer);
                    Option(this.original).ForEach(o => o.Notify_DoBillStarted(billDoer));
                }

                public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
                {
                    base.Notify_IterationCompleted(billDoer, ingredients);
                    Option(this.original).ForEach(o => o.Notify_IterationCompleted(billDoer, ingredients));
                }

                public override void Notify_PawnDidWork(Pawn p)
                {
                    base.Notify_PawnDidWork(p);
                    Option(this.original).ForEach(o => o.Notify_PawnDidWork(p));
                }

                // proxy call. override other properties and methods.
            }
            /// <summary>
            /// Used to reseve the workbench
            /// </summary>
            public class Bill_ProductionWithUftPawnForbidded : Bill_ProductionWithUft, IBill_PawnForbidded
            {
                public Bill_ProductionWithUftPawnForbidded() : base()
                {
                }

                public Bill_ProductionWithUftPawnForbidded(RecipeDef recipe) : base(recipe)
                {
                }

                public override bool PawnAllowedToStartAnew(Pawn p)
                {
                    return false;
                }

                public override void ExposeData()
                {
                    base.ExposeData();
                    Scribe_Deep.Look(ref this.original, "original");
                    if (Scribe.mode == LoadSaveMode.PostLoadInit)
                    {
                        this.original.billStack = this.billStack;
                    }
                }

                public Bill original;

                public Bill Original { get => this.original; set => this.original = value; }

                public override Bill Clone()
                {
                    var clone = (Bill_ProductionWithUft)this.original.Clone();
                    return this.CopyTo(clone);
                }

                public override void Notify_DoBillStarted(Pawn billDoer)
                {
                    base.Notify_DoBillStarted(billDoer);
                    Option(this.original).ForEach(o => o.Notify_DoBillStarted(billDoer));
                }

                public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
                {
                    base.Notify_IterationCompleted(billDoer, ingredients);
                    Option(this.original).ForEach(o => o.Notify_IterationCompleted(billDoer, ingredients));
                }

                public override void Notify_PawnDidWork(Pawn p)
                {
                    base.Notify_PawnDidWork(p);
                    Option(this.original).ForEach(o => o.Notify_PawnDidWork(p));
                }

                // proxy call. override other properties and methods.
            }

            #endregion

            public Type_WorkTable(Building building, Building_AutoMachineTool sal) : base(building, sal)
            {
                WorkTable = (Building_WorkTable)building;
            }


        }

        public SAL_TargetType Target { get; set; } = SAL_TargetType.Invalid;
        private Type TargetType = null;

        //only tep as public
        public Building_WorkTable my_workTable = null;
        private Building drilltypeBuilding = null;
        private Building_ResearchBench researchBench = null;
        private Building_AutoMachineTool mySAL = null;


        private IntVec3 Position = new IntVec3();
        private Map Map;
        private Rot4 Rotation;


        public PRF_SAL_Trarget(Map map, IntVec3 cell, Rot4 rot, Building_AutoMachineTool sal)
        {
            Map = map;
            Position = cell;
            Rotation = rot;
            mySAL = sal;


            GetTarget();
        }


        public bool ValidTarget => Target != SAL_TargetType.Invalid;


        private void adjustIO()
        {
            if (mySAL == null) return;
            if (Target == SAL_TargetType.ReserchBench || Target == SAL_TargetType.Drill)
            {
                this.mySAL.compOutputAdjustable.Visible = false;
                this.mySAL.powerWorkSetting.RangeSettingHide = true;
            }
            else if (Target == SAL_TargetType.WorkTable)
            {
                this.mySAL.compOutputAdjustable.Visible = true;
                this.mySAL.powerWorkSetting.RangeSettingHide = false;
            }
        }

        public void GetTarget()
        {
            //Whats the purpose of this?
            bool spawned = false;

            List<Thing> buildingsAtPos = (this.Position + this.Rotation.FacingCell).GetThingList(Map)
                .Where(t => t.def.category == ThingCategory.Building).Where(t => t.InteractionCell == this.Position).ToList();

            Building_WorkTable new_my_workTable = (Building_WorkTable)buildingsAtPos
                .Where(t => t is Building_WorkTable).FirstOrDefault();

            Building new_drilltypeBuilding = (Building)buildingsAtPos
                .Where(t => t is Building && t.TryGetComp<CompDeepDrill>() != null).FirstOrDefault();

            Building_ResearchBench new_researchBench = (Building_ResearchBench)buildingsAtPos
                .Where(t => t is Building_ResearchBench).FirstOrDefault();

            Type new_TargetType = null;
            if (new_my_workTable != null)
            {
                Target = SAL_TargetType.WorkTable;
                new_TargetType = new Type_WorkTable(new_my_workTable, mySAL);
            }else if (new_drilltypeBuilding != null)
            {
                Target = SAL_TargetType.Drill;
                new_TargetType = new Type_Drill(new_drilltypeBuilding, mySAL);
            }
            else if (new_researchBench != null)
            {
                Target = SAL_TargetType.ReserchBench;
                new_TargetType = new Type_Reserch(new_researchBench, mySAL);
            }
            else
            {
                Target = SAL_TargetType.Invalid;
                new_TargetType = null;
            }

            if (spawned && 
                (      (my_workTable != null && new_my_workTable == null) 
                    || (researchBench != null && new_researchBench == null) 
                    || (drilltypeBuilding != null && new_drilltypeBuilding == null)))
            {
                TargetType.Free();
            }

            my_workTable = new_my_workTable;
            drilltypeBuilding = new_drilltypeBuilding;
            researchBench = new_researchBench;

            TargetType = new_TargetType;

            if (spawned && ValidTarget) TargetType.Reserve();


            adjustIO();
        }

        /// <summary>
        /// Return True if the Traget is Ready for work
        /// </summary>
        /// <returns></returns>
        public bool TrargetReady()
        {
            //no target --> not ready
            if (!ValidTarget) return false;

            return !TargetType.Ready();
        }

        public void ReserveTraget()
        {
            TargetType.Reserve();
        }
        public void FreeTarget()
        {
            TargetType.Free();
        }

        /*
        public Effecter GetEffecter()
        {
            if (my_workTable != null)
            {
                return this.bill.recipe.effectWorking.Spawn();
            }
            return null;
        } 
        public Sustainer GetSustainer()
        {
            if (my_workTable != null)
            {
                return this.bill.recipe.soundWorking?.TrySpawnSustainer(my_workTable);
            }
            return null;
        }
        */

        public void SignalWorkDone(out List<Thing> products)
        {
            TargetType.WorkDone(out products);
        }
        public bool TryStartWork( out float workAmount)
        {
            return TargetType.TryStartWork(out workAmount);
        }

        public void CleanupWorkingEffect(MapTickManager manager)
        {
            TargetType.CleanupWorkingEffect(manager);
        }
        public void CreateWorkingEffect(MapTickManager manager)
        {
            TargetType.CreateWorkingEffect(manager);
        }
        public void ExposeData()
        {
            TargetType.ExposeData();
        }
        public void Reset(WorkingState state)
        {
            TargetType.Reset(state);
        }

    }



    public class Building_AutoMachineTool : Building_BaseRange<Building_AutoMachineTool>, IRecipeProductWorker
    {
  
        public Building_AutoMachineTool()
        {
            this.forcePlace = false;
            this.targetEnumrationCount = 0;
        }
        
        private bool forbidItem = false;

        public Effecter WorkingEffect { get; set; } = null;
        public Sustainer WorkingSound { get; set; } = null;

        private PRF_SAL_Trarget salTarget;

        private Building_WorkTable my_workTable = null;


        ModExtension_Skills extension_Skills;

        public ModExtension_ModifyProduct ModifyProductExt => this.def.GetModExtension<ModExtension_ModifyProduct>();

        public int GetSkillLevel(SkillDef def)
        {
            return extension_Skills?.GetExtendedSkillLevel(def,typeof(Building_AutoMachineTool)) ?? this.SkillLevel ?? 0;
        }

        protected override int? SkillLevel { get { return this.def.GetModExtension<ModExtension_Tier>()?.skillLevel; } }

        public override bool Glowable => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.forbidItem, "forbidItem");

            //TODO Add Save Game Compatibility
            salTarget.ExposeData();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            salTarget = new PRF_SAL_Trarget(map, Position, Rotation,this);
            my_workTable = null;
            extension_Skills = def.GetModExtension<ModExtension_Skills>();

        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            salTarget.FreeTarget();

            base.DeSpawn();
        }

        public override void PostMapInit()
        {
            base.PostMapInit();
            this.WorkTableSetting();
        }

        protected override void Reset()
        {
            salTarget.Reset(this.State);
            base.Reset();
        }

        protected override void CleanupWorkingEffect()
        {
            base.CleanupWorkingEffect();
            salTarget.CleanupWorkingEffect(MapManager);
        }

        protected override void CreateWorkingEffect()
        {
            if (salTarget.Target == PRF_SAL_Trarget.SAL_TargetType.WorkTable) base.CreateWorkingEffect();
            salTarget.CreateWorkingEffect(MapManager);
        }

        protected override TargetInfo ProgressBarTarget()
        {
            return my_workTable;
        }

        /// <summary>
        /// TODO Check that one again
        /// </summary>
        private void WorkTableSetting()
        {
            salTarget.GetTarget();
            my_workTable = salTarget.my_workTable;
        }

        protected override void Ready()
        {
            this.WorkTableSetting();
            base.Ready();
        }

        private IntVec3 FacingCell()
        {
            return this.Position + this.Rotation.FacingCell;
        }

        private Building_WorkTable GetmyTragetWorktable()
        {
            return (Building_WorkTable)this.FacingCell().GetThingList(Map)
                .Where(t => t.def.category == ThingCategory.Building)
                .Where(t => t is Building_WorkTable)
                .Where(t => t.InteractionCell == this.Position).FirstOrDefault();

        }


        /// <summary>
        /// Try to start a new Bill to work on
        /// </summary>
        /// <param name="target"></param>
        /// <param name="workAmount"></param>
        /// <returns></returns>
        protected override bool TryStartWorking(out Building_AutoMachineTool target, out float workAmount)
        {
            target = this;
            workAmount = 0;
            //Return if not ready
            if (!salTarget.TrargetReady()) return false;

 
            float val = 0;
            bool status = salTarget.TryStartWork(out val);
            workAmount = val;
            return status;
        }

        protected override bool FinishWorking(Building_AutoMachineTool working, out List<Thing> products)
        {
            salTarget.SignalWorkDone(out products);
            return true;
        }

        public List<IntVec3> OutputZone()
        {
            return this.OutputCell().SlotGroupCells(Map);
        }
        
        public override IntVec3 OutputCell()
        {
            return compOutputAdjustable.CurrentCell;
        }


        public override IEnumerable<InspectTabBase> GetInspectTabs()
        {
            return base.GetInspectTabs();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }
        }

        public override string GetInspectString()
        {
            String msg = base.GetInspectString();
            return msg;
        }

        public Room GetRoom(RegionType type)
        {
            return RegionAndRoomQuery.GetRoom(this, type);
        }

        protected override bool WorkInterruption(Building_AutoMachineTool working)
        {
            //Interupt if worktable chenged or is null
            if (salTarget.ValidTarget == false || (my_workTable != null &&  GetmyTragetWorktable() == null || GetmyTragetWorktable() != my_workTable)) return true;
            //Interrupt if worktable is not ready for work
            //if (my_workTable != null) return !my_workTable.CurrentlyUsableForBills();

            return !salTarget.TrargetReady();


        }
    }

}
