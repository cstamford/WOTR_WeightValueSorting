using HarmonyLib;
using Kingmaker.Items;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.Slots;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;

namespace WeightValueSorting
{
    public class WeightValueSorting
    {
        public static UnityModManager.ModEntry.ModLogger Logger;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;

            Harmony harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            return true;
        }
    }

    enum ExpandedSorterType
    {
        WeightValueUp = 11,
        WeightValueDown = 12
    }

    [HarmonyPatch(typeof(ItemsFilterPCView), nameof(ItemsFilterPCView.Initialize))]
    public static class ItemsFilterPCView_Initialize
    {
        [HarmonyPostfix]
        public static void Postfix(ItemsFilterPCView __instance)
        {
            string[] entries = new string[]
            {
                "Price / Weight (in ascending order)",
                "Price / Weight (in descending order)"
            };

            __instance.m_Sorter.AddOptions(entries.ToList());
        }
    }

    [HarmonyPatch(typeof(ItemsFilter), nameof(ItemsFilter.ItemSorter))]
    public static class ItemsFilter_ItemSorter
    {
        private static int CompareByWeightValue(ItemEntity a, ItemEntity b, ItemsFilter.FilterType filter)
        {
            float a_weight_value = a.Blueprint.Cost / a.Blueprint.Weight;
            float b_weight_value = b.Blueprint.Cost / b.Blueprint.Weight;
            return a_weight_value == b_weight_value ? ItemsFilter.CompareByTypeAndName(a, b, filter) : (a_weight_value > b_weight_value ? 1 : -1);
        }

        [HarmonyPrefix]
        public static bool Prefix(ItemsFilter.SorterType type, List<ItemEntity> items, ItemsFilter.FilterType filter, ref List<ItemEntity> __result)
        {
            ExpandedSorterType expanded_type = (ExpandedSorterType)type;

            if (expanded_type == ExpandedSorterType.WeightValueUp)
            {
                items.Sort((ItemEntity a, ItemEntity b) => CompareByWeightValue(a, b, filter));
                __result = items;
                return false;
            }
            else if (expanded_type == ExpandedSorterType.WeightValueDown)
            {
                items.Sort((ItemEntity a, ItemEntity b) => CompareByWeightValue(a, b, filter));
                items.Reverse();
                __result = items;
                return false;
            }

            return true;
        }
    }
}
