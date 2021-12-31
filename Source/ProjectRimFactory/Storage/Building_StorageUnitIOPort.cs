﻿using ProjectRimFactory.Common;
using ProjectRimFactory.Common.HarmonyPatches;
using ProjectRimFactory.SAL3.UI;
using ProjectRimFactory.Storage.Editables;
using ProjectRimFactory.Storage.UI;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace ProjectRimFactory.Storage
{

    public interface IRenameBuilding
    {
        public string UniqueName { set;  get; }
        public Building Building { get; }
    }


    [StaticConstructorOnStartup]
    public abstract class Building_StorageUnitIOBase : Building_Storage, IForbidPawnInputItem , IRenameBuilding , INutrientPasteDispenserInput
    {
        public static readonly Texture2D CargoPlatformTex = ContentFinder<Texture2D>.Get("Storage/CargoPlatform");
        public static readonly Texture2D IOModeTex = ContentFinder<Texture2D>.Get("PRFUi/IoIcon");

        public StorageIOMode mode;
        public Building_MassStorageUnit boundStorageUnit;
        protected StorageSettings outputStoreSettings;
        private OutputSettings outputSettings;

        public virtual IntVec3 WorkPosition => this.Position;

        protected CompPowerTrader powerComp;


        public string UniqueName { get => uniqueName; set => uniqueName = value; }
        private string uniqueName;
        public Building Building => this;
        public override string LabelNoCount => uniqueName ?? base.LabelNoCount;
        public override string LabelCap => uniqueName ?? base.LabelCap;
        private static readonly Texture2D RenameTex = ContentFinder<Texture2D>.Get("UI/Buttons/Rename");

        private bool forbidOnPlacement = false;
        public virtual bool ForbidOnPlacement => forbidOnPlacement;


        public override Graphic Graphic => this.IOMode == StorageIOMode.Input ?
            base.Graphic.GetColoredVersion(base.Graphic.Shader, this.def.GetModExtension<DefModExtension_StorageUnitIOPortColor>().inColor, Color.white) :
            base.Graphic.GetColoredVersion(base.Graphic.Shader, this.def.GetModExtension<DefModExtension_StorageUnitIOPortColor>().outColor, Color.white);

        public virtual StorageIOMode IOMode
        {
            get
            {
                return mode;
            }
            set
            {
                if (mode == value) return;
                mode = value;
                Notify_NeedRefresh();
            }
        }

        public Building_MassStorageUnit BoundStorageUnit
        {
            get
            {
                return boundStorageUnit;
            }
            set
            {
                boundStorageUnit?.DeregisterPort(this);
                boundStorageUnit = value;
                value?.RegisterPort(this);
                Notify_NeedRefresh();
            }
        }

        protected OutputSettings OutputSettings
        {
            get
            {
                if (outputSettings == null)
                {
                    outputSettings = new OutputSettings("IOPort_Minimum_UseTooltip", "IOPort_Maximum_UseTooltip");
                }
                return outputSettings;
            }
            set
            {
                outputSettings = value;
            }
        }

        //
        public bool ForbidPawnInput
        {
            get
            {
                if (IOMode == StorageIOMode.Output && OutputSettings.useMax)
                {
                    //Only get currentItem if needed
                    Thing currentItem = WorkPosition.GetFirstItem(Map);
                    if (currentItem != null)
                    {
                        return OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit) <= 0;
                    }
                }
                return false;
            }
        }

        public Thing NPDI_Item => WorkPosition.GetFirstItem(this.Map);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mode, "mode");
            Scribe_References.Look(ref boundStorageUnit, "boundStorageUnit");
            Scribe_Deep.Look(ref outputStoreSettings, "outputStoreSettings", this);
            Scribe_Deep.Look(ref outputSettings, "outputSettings", "IOPort_Minimum_UseTooltip", "IOPort_Maximum_UseTooltip");
            Scribe_Values.Look(ref uniqueName, "uniqueName");
            Scribe_Values.Look(ref forbidOnPlacement, "forbidOnPlacement");
        }
        public override string GetInspectString()
        {
            if (OutputSettings.useMin && OutputSettings.useMax) return base.GetInspectString() + "\n" + "IOPort_Minimum".Translate(OutputSettings.min) + "\n" + "IOPort_Maximum".Translate(OutputSettings.max);
            else if (OutputSettings.useMin && !OutputSettings.useMax) return base.GetInspectString() + "\n" + "IOPort_Minimum".Translate(OutputSettings.min);
            else if (!OutputSettings.useMin && OutputSettings.useMax) return base.GetInspectString() + "\n" + "IOPort_Maximum".Translate(OutputSettings.max);
            else return base.GetInspectString();
        }


        public override void PostMake()
        {
            base.PostMake();
            powerComp = GetComp<CompPowerTrader>();
            outputStoreSettings = new StorageSettings(this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();

            if (boundStorageUnit?.Map != map) BoundStorageUnit = null;
        }

        protected override void ReceiveCompSignal(string signal)
        {
            base.ReceiveCompSignal(signal);
            if (signal == CompPowerTrader.PowerTurnedOnSignal)
            {
                Notify_NeedRefresh();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn();
            boundStorageUnit?.DeregisterPort(this);
        }

        public void Notify_NeedRefresh()
        {
            RefreshStoreSettings();
            switch (mode)
            {
                case StorageIOMode.Input:
                    RefreshInput();
                    break;
                case StorageIOMode.Output:
                    RefreshOutput();
                    break;
            }
        }

        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            if (mode == StorageIOMode.Input)
            {
                RefreshInput();
            }
        }

        public override void Notify_LostThing(Thing newItem)
        {
            base.Notify_LostThing(newItem);
            if (mode == StorageIOMode.Output)
            {
                RefreshOutput();
            }
        }


        public override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(10))
            {
                Notify_NeedRefresh();
            }
        }

        public void RefreshStoreSettings()
        {
            if (mode == StorageIOMode.Output)
            {
                settings = outputStoreSettings;
                if (boundStorageUnit != null && settings.Priority != boundStorageUnit.settings.Priority)
                {
                    //the setter of settings.Priority is expensive
                    settings.Priority = boundStorageUnit.settings.Priority;
                }
            }
            else if (boundStorageUnit != null)
            {
                settings = boundStorageUnit.settings;
            }
            else
            {
                settings = new StorageSettings(this);
            }
        }

        public virtual void RefreshInput()
        {
            if (powerComp.PowerOn)
            {
                Thing item = WorkPosition.GetFirstItem(Map);
                if (item != null && boundStorageUnit != null && boundStorageUnit.settings.AllowedToAccept(item) && boundStorageUnit.CanReceiveIO && boundStorageUnit.CanStoreMoreItems)
                {
                    foreach (IntVec3 cell in boundStorageUnit.AllSlotCells())
                    {
                        if (cell.GetFirstItem(Map) == null)
                        {
                            boundStorageUnit.RegisterNewItem(item);
                            break;
                        }
                    }
                }
            }
        }

        protected bool ItemsThatSatisfyMin(ref List<Thing> itemCandidates, Thing currentItem)
        {
            if (currentItem != null)
            {
                itemCandidates = itemCandidates.Where(t => currentItem.CanStackWith(t)).ToList();
                int minReqierd = OutputSettings.useMin ? outputSettings.min : 0;
                int count = currentItem.stackCount;
                int i = 0;
                while (i < itemCandidates.Count && count < minReqierd)
                {
                    count += itemCandidates[i].stackCount;
                    i++;
                }
                return OutputSettings.SatisfiesMin(count);
            }
            //I wonder if GroupBy is benifficial or not
            return itemCandidates.GroupBy(t => t.def)
                .FirstOrDefault(g => OutputSettings.SatisfiesMin(g.Sum(t => t.stackCount)))?.Any() ?? false;
        }


        protected virtual void RefreshOutput() //
        {
            if (powerComp.PowerOn)
            {
                Thing currentItem = WorkPosition.GetFirstItem(Map);
                bool storageSlotAvailable = currentItem == null || (settings.AllowedToAccept(currentItem) &&
                                                                    OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit));
                if (boundStorageUnit != null && boundStorageUnit.CanReceiveIO)
                {
                    if (storageSlotAvailable)
                    {
                        List<Thing> itemCandidates = new List<Thing>(from Thing t in boundStorageUnit.StoredItems where settings.AllowedToAccept(t) select t); // ToList very important - evaluates enumerable
                        if (ItemsThatSatisfyMin(ref itemCandidates, currentItem))
                        {
                            foreach (Thing item in itemCandidates)
                            {
                                if (currentItem != null)
                                {
                                    if (currentItem.CanStackWith(item))
                                    {
                                        int count = Math.Min(item.stackCount, OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit));
                                        if (count > 0)
                                        {
                                            currentItem.TryAbsorbStack(item.SplitOff(count), true);
                                        }
                                    }
                                }
                                else
                                {
                                    int count = OutputSettings.CountNeededToReachMax(0, item.stackCount);
                                    if (count > 0)
                                    {
                                        currentItem = GenSpawn.Spawn(item.SplitOff(count), WorkPosition, Map);
                                    }
                                }
                                if (currentItem != null && !OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit))
                                {
                                    break;
                                }
                            }
                        }
                    }
                    //Transfre a item back if it is either too few or disallowed
                    if (currentItem != null && (!settings.AllowedToAccept(currentItem) || !OutputSettings.SatisfiesMin(currentItem.stackCount)) && boundStorageUnit.settings.AllowedToAccept(currentItem))
                    {
                        currentItem.SetForbidden(false, false);
                        boundStorageUnit.RegisterNewItem(currentItem);
                    }
                    //Transfer the diffrence back if it is too much
                    if (currentItem != null && (!OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit) && boundStorageUnit.settings.AllowedToAccept(currentItem)))
                    {
                        int splitCount = -OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit);
                        if (splitCount > 0)
                        {
                            Thing returnThing = currentItem.SplitOff(splitCount);
                            returnThing.SetForbidden(false, false);
                            boundStorageUnit.RegisterNewItem(returnThing);
                        }
                    }
                    if (currentItem != null)
                    {
                        currentItem.SetForbidden(ForbidOnPlacement,false);
                    }
                }
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            yield return new Command_Action()
            {
                defaultLabel = "PRFBoundStorageBuilding".Translate() + ": " + (boundStorageUnit?.LabelCap ?? "NoneBrackets".Translate()),
                action = () =>
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>(
                        from Building_MassStorageUnit b in Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_MassStorageUnit>()
                        where b.def.GetModExtension<DefModExtension_CanUseStorageIOPorts>() != null
                        select new FloatMenuOption(b.LabelCap, () => SelectedPorts().ForEach(p => p.BoundStorageUnit = b))
                    );
                    if (list.Count == 0)
                    {
                        list.Add(new FloatMenuOption("NoneBrackets".Translate(), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                },
                icon = CargoPlatformTex
            };
            yield return new Command_Action
            {
                icon = RenameTex,
                action = () => Find.WindowStack.Add(new Dialog_RenameMassStorageUnit(this)),
                hotKey = KeyBindingDefOf.Misc1,
                defaultLabel = "PRFRenameMassStorageUnitLabel".Translate(),
                defaultDesc = "PRFRenameMassStorageUnitDesc".Translate()
            };
            if (IOMode == StorageIOMode.Output)
            {
                yield return new Command_Action()
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel"),
                    defaultLabel = "PRFIOOutputSettings".Translate(),
                    action = () => Find.WindowStack.Add(new Dialog_OutputMinMax(OutputSettings, () => SelectedPorts().Where(p => p.IOMode == StorageIOMode.Output).ToList().ForEach(p => this.OutputSettings.Copy(p.OutputSettings))))
                };
            }
            if (mode == StorageIOMode.Output)
            {
                yield return new Command_Toggle()
                {
                    isActive = () => this.forbidOnPlacement,
                    toggleAction = () => this.forbidOnPlacement = !this.forbidOnPlacement,
                    defaultLabel = "PRF_Toggle_ForbidOnPlacement".Translate(),
                    defaultDesc = "PRF_Toggle_ForbidOnPlacementDesc".Translate(),
                    icon = forbidOnPlacement ? RS.ForbidOn : RS.ForbidOff

                };
            }



        }

        private List<Building_StorageUnitIOBase> SelectedPorts()
        {
            var l = Find.Selector.SelectedObjects.OfType<Building_StorageUnitIOBase>().ToList();
            if (!l.Contains(this))
            {
                l.Add(this);
            }
            return l;
        }

        public virtual bool OutputItem(Thing thing)
        {
            if (boundStorageUnit?.CanReceiveIO ?? false)
            {
                return GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), WorkPosition, Map, ThingPlaceMode.Near,
                    null, pos =>
                    {
                        if (settings.AllowedToAccept(thing) && OutputSettings.SatisfiesMin(thing.stackCount))
                            if (pos == WorkPosition)
                                return true;
                        foreach (Thing t in Map.thingGrid.ThingsListAt(pos))
                        {
                            if (t is Building_StorageUnitIOPort) return false;
                        }

                        return true;
                    });
            }

            return false;
        }
    }




    [StaticConstructorOnStartup]
    public class Building_StorageUnitIOPort : Building_StorageUnitIOBase
    {

        public override Graphic Graphic => this.IOMode == StorageIOMode.Input ?
            base.Graphic.GetColoredVersion(base.Graphic.Shader, this.def.GetModExtension<DefModExtension_StorageUnitIOPortColor>().inColor, Color.white) :
            base.Graphic.GetColoredVersion(base.Graphic.Shader, this.def.GetModExtension<DefModExtension_StorageUnitIOPortColor>().outColor, Color.white);

        public override StorageIOMode IOMode
        {
            get
            {
                return mode;
            }
            set
            {
                if (mode == value) return;
                mode = value;
                Notify_NeedRefresh();
            }
        }

        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            if (mode == StorageIOMode.Input)
            {
                RefreshInput();
            }
        }

        public override void Notify_LostThing(Thing newItem)
        {
            base.Notify_LostThing(newItem);
            if (mode == StorageIOMode.Output)
            {
                RefreshOutput();
            }
        }

        public override void RefreshInput()
        {
            if (powerComp.PowerOn)
            {
                Thing item = Position.GetFirstItem(Map);
                if (item != null && boundStorageUnit != null && boundStorageUnit.settings.AllowedToAccept(item) && boundStorageUnit.CanReceiveIO && boundStorageUnit.CanStoreMoreItems)
                {
                    foreach (IntVec3 cell in boundStorageUnit.AllSlotCells())
                    {
                        if (cell.GetFirstItem(Map) == null)
                        {
                            boundStorageUnit.RegisterNewItem(item);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Modified version of Verse.Thing.TryAbsorbStack (based on 1.3.7964.22648)
        /// Might Cause unexpected things as 
        /// DS Has a patch for Thing.TryAbsorbStack
        /// Thing.SplitOff has a CommonSense Transpiler
        /// </summary>
        /// <param name="baseThing"></param>
        /// <param name="toBeAbsorbed"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private static bool AbsorbAmmount( ref Thing baseThing, ref Thing toBeAbsorbed,int count)
        {

            if (!baseThing.CanStackWith(toBeAbsorbed))
            {
                return false;
            }
            int num = count;


            if (baseThing.def.useHitPoints)
            {
                baseThing.HitPoints = Mathf.CeilToInt((float)(baseThing.HitPoints * baseThing.stackCount + toBeAbsorbed.HitPoints * num) / (float)(baseThing.stackCount + num));
            }


            baseThing.stackCount += num;
            toBeAbsorbed.stackCount -= num;
            if (baseThing.Map != null)
            {
                baseThing.DirtyMapMesh(baseThing.Map);
            }
            StealAIDebugDrawer.Notify_ThingChanged(baseThing);
            if (baseThing.Spawned)
            {
                baseThing.Map.listerMergeables.Notify_ThingStackChanged(baseThing);
            }
            if (toBeAbsorbed.stackCount <= 0)
            {
                toBeAbsorbed.Destroy();
                return true;
            }
            return false;


        } 

        protected override void RefreshOutput()
        {
            if (powerComp.PowerOn)
            {
                Thing currentItem = Position.GetFirstItem(Map);
                bool storageSlotAvailable = currentItem == null || (settings.AllowedToAccept(currentItem) &&
                                                                    OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit));
                if (boundStorageUnit != null && boundStorageUnit.CanReceiveIO)
                {
                    if (storageSlotAvailable)
                    {
                        List<Thing> itemCandidates = new List<Thing>(from Thing t in boundStorageUnit.StoredItems where settings.AllowedToAccept(t) select t); // ToList very important - evaluates enumerable
                        //ItemsThatSatisfyMin somtimes spikes to 0.1 but it is mostly an none issue
                        if (ItemsThatSatisfyMin(ref itemCandidates, currentItem))
                        {
                            foreach (Thing item in itemCandidates)
                            {
                                if (currentItem != null)
                                {
                                    if (currentItem.CanStackWith(item))
                                    {
                                        int count = Math.Min(item.stackCount, OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit));
                                        if (count > 0)
                                        {
                                            Thing Mything = item;
                                            //Merge Stacks - Gab count required to fulfill settings and merge them to the stuff on the IO Port
                                            //For SplitOff "MakeThing" is expensive
                                            //For TryAbsorbStack "Destroy" is expensive
                                            AbsorbAmmount(ref currentItem, ref Mything, count);
                                        }
                                    }
                                }
                                else
                                {
                                    int count = OutputSettings.CountNeededToReachMax(0, item.stackCount);
                                    if (count > 0)
                                    {
                                        //Nothing on the IO Port - grab thing from storage and place it on the port
                                        //For SplitOff "MakeThing" is expensive
                                        currentItem = GenSpawn.Spawn(item.SplitOff(count), Position, Map);
                                    }
                                }
                                if (currentItem != null && !OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit))
                                {
                                    break;
                                }
                            }
                        }
                    }
                    //Transfre a item back if it is either too few or disallowed
                    if (currentItem != null && (!settings.AllowedToAccept(currentItem) || !OutputSettings.SatisfiesMin(currentItem.stackCount)) && boundStorageUnit.settings.AllowedToAccept(currentItem))
                    {
                        currentItem.SetForbidden(false, false);
                        boundStorageUnit.RegisterNewItem(currentItem);
                    }
                    //Transfer the diffrence back if it is too much
                    if (currentItem != null && (!OutputSettings.SatisfiesMax(currentItem.stackCount, currentItem.def.stackLimit) && boundStorageUnit.settings.AllowedToAccept(currentItem)))
                    {
                        int splitCount = -OutputSettings.CountNeededToReachMax(currentItem.stackCount, currentItem.def.stackLimit);
                        if (splitCount > 0)
                        {
                            Thing returnThing = currentItem.SplitOff(splitCount);
                            returnThing.SetForbidden(false, false);
                            boundStorageUnit.RegisterNewItem(returnThing);
                        }
                    }
                    if (currentItem != null)
                    {
                        currentItem.SetForbidden(ForbidOnPlacement, false);
                    }
                }
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            yield return new Command_Action()
            {
                defaultLabel = "PRFIOMode".Translate() + ": " + (IOMode == StorageIOMode.Input ? "PRFIOInput".Translate() : "PRFIOOutput".Translate()),
                action = () =>
                {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                    {
                        new FloatMenuOption("PRFIOInput".Translate(), () => SelectedPorts().ForEach(p => p.IOMode = StorageIOMode.Input)),
                        new FloatMenuOption("PRFIOOutput".Translate(), () => SelectedPorts().ForEach(p => p.IOMode = StorageIOMode.Output))
                    }));
                },
                icon = IOModeTex
            };
        }

        private List<Building_StorageUnitIOPort> SelectedPorts()
        {
            var l = Find.Selector.SelectedObjects.OfType<Building_StorageUnitIOPort>().ToList();
            if (!l.Contains(this))
            {
                l.Add(this);
            }
            return l;
        }

        public override bool OutputItem(Thing thing)
        {
            if (boundStorageUnit?.CanReceiveIO ?? false)
            {
                return GenPlace.TryPlaceThing(thing.SplitOff(thing.stackCount), Position, Map, ThingPlaceMode.Near,
                    null, pos =>
                    {
                        if (settings.AllowedToAccept(thing) && OutputSettings.SatisfiesMin(thing.stackCount))
                            if (pos == Position)
                                return true;
                        foreach (Thing t in Map.thingGrid.ThingsListAt(pos))
                        {
                            if (t is Building_StorageUnitIOPort) return false;
                        }

                        return true;
                    });
            }

            return false;
        }
    }

    public class DefModExtension_StorageUnitIOPortColor : DefModExtension
    {
        public Color inColor;
        public Color outColor;
    }
}
