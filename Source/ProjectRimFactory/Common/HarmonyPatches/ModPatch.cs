using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace ProjectRimFactory.Common.HarmonyPatches
{
    [HarmonyPatch(typeof(ModContentPack), "get_Patches")]
    class Patch_ModContentPack_Pathes
    {
        static void Postfix(ModContentPack __instance, ref IEnumerable<PatchOperation> __result)
        {
            if(__instance.PackageId.ToLower() == LoadedModManager.GetMod<ProjectRimFactory_ModComponent>().Content.PackageId.ToLower())
            {
                var setting = LoadedModManager.GetMod<ProjectRimFactory_ModComponent>().Settings;
                var patches = setting.Patches;
                int count = 0;

                foreach (PatchOperation patch in patches)
                {
                    count++;
                    patch.sourceFile = "PRF_SettingsPatch_" + count + "_";
                }

                __result = __result.Concat(patches);
            }
        }
    }



    [HarmonyPatch(typeof(ModContentPack), "AddDef")]
    class Patch_ModContentPack_AddDef
    {
        static bool Prefix(ModContentPack __instance, Def def)
        {
            //List of items to exclude from PRF Lite
            string[] ExcludeList_Primary = { "PRF_DeepQuarry_mkI", "PRF_DeepQuarry_mkII" , "PRF_DeepQuarry_mkIII" , "PRF_BillTypeMiner_I" ,
                "PRF_AutoCrafterSimple", "PRF_AutoCrafter", "PRF_TheArtMachine", "PRF_TheArtMaster", "PRF_OverclockedAutoAssembler", "PRF_OverclockedAutoAssemblerII", "PRF_SALAutoCooker", "PRF_SALAutoMinerI", "PRF_GodlyCrafter" ,
            "PRF_StoneWorks","PRF_Recycler","PRF_MrTsArtMachine","PRF_MetalRefinery","PRF_PartAssembler","PRF_DroneCultivator_I","PRF_DroneCultivator_II","PRF_DroneCultivator_II_sun","PRF_DroneCultivator_III","PRF_OldTypeCultivator_I","PRF_OldTypeCultivator_Sun","PRF_OldTypeCultivator_Xl","PRF_SelfCookerI","PRF_SelfCookerII","PRF_SelfCookerIII","PRF_MeatGrinder","PRF_FermentingBarrel","PRF_Slaughterhouse","PRF_AssemblerGroup","PRF_RecipeDatabase","PRF_Shearer","PRF_Milker","PRF_GenericAnimalHarvester","PRF_GenericAnimalHarvester_II",
            "PRF_Sprinkler_I","PRF_Sprinkler_II","PRF_MineShaft","PRF_MiniHelper","PRF_MiniDroneColumn","PRF_TypeOneAssembler_I","PRF_TypeTwoAssembler_I","PRF_TypeTwoAssembler_II","PRF_TypeTwoAssembler_III","PRF_FurnaceI","PRF_FurnaceII","PRF_FurnaceIII","PRF_SelfPrepper",
            "PRF_Factory_Supplier", "PRF_ResearchTerminal" , "TableRoboticMachining", "PRF_4k_Battery","PRF_16k_Battery","PRF_64k_Battery","PRF_256k_Battery"};
            string[] ExcludeList_Signs = { "PRF_FloorLampArrow", "PRF_RedFloorLampArrow", "PRF_GreenFloorLampArrow", "PRF_FloorLampX", "PRF_FloorInput", "PRF_FloorOutput", "PRF_IconClothes", "PRF_IconSkull", "PRF_IconToxic", "PRF_IconPower", "PRF_IconGears", "PRF_IconGun", "PRF_IconGasmask", "PRF_IconFire", "PRF_IconCold", "PRF_IconDanger", "PRF_IconExit", "PRF_IconPrison", "PRF_IconResearch", "PRF_IconHospital", "PRF_IconBarbedWire" };

            string[] ExcludeList_Research = { "PRF_AutomaticFarmingI", "PRF_AutomaticFarmingII", "PRF_AutomaticFarmingIII", "PRF_BasicDrones", "PRF_ImprovedDrones", "PRF_AdvancedDrones", "PRF_AutonomousMining", "PRF_AutonomousMiningII", "PRF_AutonomousMiningIII", "PRF_SALResearchI", "PRF_SALResearchII", "PRF_SALResearchIII", "PRF_SALResearchIV", "PRF_SALResearchV", "PRF_SALResearchVII", "PRF_SALResearchVI", "PRF_SALResearchVIII", "PRF_SALGodlyCrafting", "PRF_EnhancedBatteries", "PRF_LargeBatteries", "PRF_VeryLargeBatteries", "PRF_UniversalAutocrafting", "PRF_SelfCorrectingAssemblers", "PRF_SelfCorrectingAssemblersII", "PRF_MetalRefining", "PRF_AnimalStations", "PRF_AnimalStationsII", "PRF_AnimalStationsIII" ,
            "PRF_SelfCooking","PRF_SelfCookingII","PRF_MachineLearning","PRF_MagneticTape","PRF_CoreTierO","PRF_CoreTierI","PRF_CoreTierII","PRF_CoreTierIII","PRF_CoreTierIV"};

            if (ExcludeList_Primary.Contains(def?.defName ?? "-1")) return false;

            if (ExcludeList_Signs.Contains(def?.defName ?? "-1")) return false;

            if (ExcludeList_Research.Contains(def?.defName ?? "-1")) return false;


            //ToDo
            //Resolve Red errors
            //Remove research
            //Somhow handle Components


            return true;
        }
    }

}
