using HarmonyLib;
using Kingmaker.Blueprints.Root;
using Kingmaker.Items;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.Slots;
using Kingmaker.UI.MVVM._VM.Slots;
using Owlcat.Runtime.UniRx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using UniRx;

namespace WeightValueSorting
{
    [Flags]
    public enum SorterCategories
    {
        NotSorted       = 0,
        TypeUp          = 1 << 0,
        TypeDown        = 1 << 1,
        PriceUp         = 1 << 2,
        PriceDown       = 1 << 3,
        NameUp          = 1 << 4,
        NameDown        = 1 << 5,
        DateUp          = 1 << 6,
        DateDown        = 1 << 7,
        WeightUp        = 1 << 8,
        WeightDown      = 1 << 9,
        WeightValueUp   = 1 << 10,
        WeightValueDown = 1 << 11
    }

    public enum ExpandedSorterType
    {
        WeightValueUp = 11,
        WeightValueDown = 12
    }

    public class Settings : UnityModManager.ModSettings
    {
        public SorterCategories EnabledCategories =
            SorterCategories.TypeUp | SorterCategories.TypeDown |
            SorterCategories.PriceUp | SorterCategories.PriceDown |
            SorterCategories.NameUp | SorterCategories.NameDown |
            SorterCategories.DateUp | SorterCategories.DateDown |
            SorterCategories.WeightUp | SorterCategories.WeightDown |
            SorterCategories.WeightValueUp | SorterCategories.WeightValueDown;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public class WeightValueSorting
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Settings Settings;
        public static bool Enabled;

        public static List<int> IdxToSorter = new List<int>();
        public static Dictionary<SorterCategories, Tuple<int, string>> CategoryMap;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            Settings = Settings.Load<Settings>(modEntry);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            Harmony harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            CategoryMap = new Dictionary<SorterCategories, Tuple<int, string>>
            {
                [SorterCategories.NotSorted] = new Tuple<int, string>((int)ItemsFilter.SorterType.NotSorted, null),
                [SorterCategories.TypeUp] = new Tuple<int, string>((int)ItemsFilter.SorterType.TypeUp, null),
                [SorterCategories.TypeDown] = new Tuple<int, string>((int)ItemsFilter.SorterType.TypeDown, null),
                [SorterCategories.PriceUp] = new Tuple<int, string>((int)ItemsFilter.SorterType.PriceUp, null),
                [SorterCategories.PriceDown] = new Tuple<int, string>((int)ItemsFilter.SorterType.PriceDown, null),
                [SorterCategories.NameUp] = new Tuple<int, string>((int)ItemsFilter.SorterType.NameUp, null),
                [SorterCategories.NameDown] = new Tuple<int, string>((int)ItemsFilter.SorterType.NameDown, null),
                [SorterCategories.DateUp] = new Tuple<int, string>((int)ItemsFilter.SorterType.DateUp, null),
                [SorterCategories.DateDown] = new Tuple<int, string>((int)ItemsFilter.SorterType.DateDown, null),
                [SorterCategories.WeightUp] = new Tuple<int, string>((int)ItemsFilter.SorterType.WeightUp, null),
                [SorterCategories.WeightDown] = new Tuple<int, string>((int)ItemsFilter.SorterType.WeightDown, null),
                [SorterCategories.WeightValueUp] = new Tuple<int, string>((int)ExpandedSorterType.WeightValueUp, "Price / Weight (in ascending order)"),
                [SorterCategories.WeightValueDown] = new Tuple<int, string>((int)ExpandedSorterType.WeightValueDown, "Price / Weight (in descending order)")
            };

            RefreshIdxToSorterArray();

            return true;
        }

        private static void RefreshIdxToSorterArray()
        {
            IdxToSorter.Clear();

            foreach (SorterCategories flag in Enum.GetValues(typeof(SorterCategories)))
            {
                if (Settings.EnabledCategories.HasFlag(flag))
                {
                    IdxToSorter.Add(CategoryMap[flag].Item1);
                }
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Show sorting categories:");
            GUILayout.EndHorizontal();

            SorterCategories new_options = SorterCategories.NotSorted;

            foreach (SorterCategories flag in Enum.GetValues(typeof(SorterCategories)))
            {
                if (flag == SorterCategories.NotSorted) continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(Settings.EnabledCategories.HasFlag(flag), $" {CategoryMap[flag].Item2 ?? flag.ToString()}"))
                {
                    new_options |= flag;
                }
                GUILayout.EndHorizontal();
            }

            if (Settings.EnabledCategories != new_options)
            {
                Settings.EnabledCategories = new_options;
                RefreshIdxToSorterArray();
            }

            GUILayout.Space(4);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }

    [HarmonyPatch(typeof(ItemsFilterPCView))]
    public static class ItemsFilterPCView_Initialize
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ItemsFilterPCView.BindViewImplementation))]
        public static bool BindViewImplementation(ItemsFilterPCView __instance)
        {
            __instance.Show();
            __instance.SubscribeToggles();
            __instance.AddDisposable(__instance.ViewModel.CurrentSorter.Subscribe(delegate (ItemsFilter.SorterType value)
            {
                __instance.m_Sorter.value = WeightValueSorting.IdxToSorter.FindIndex(i => i == (int)value);
            }));
            __instance.AddDisposable(__instance.m_Sorter.OnValueChangedAsObservable().Subscribe(delegate (int value)
            {
                __instance.ViewModel.SetCurrentSorter((ItemsFilter.SorterType)WeightValueSorting.IdxToSorter[value]);
            }));
            __instance.SetTooltips();


            // TODO: Transpiler? Reverse patch? I don't like this...
            // It would be nice if we could easily patch the delegates.

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ItemsFilterPCView.Initialize))]
        public static void Initialize(ItemsFilterPCView __instance)
        {
            __instance.m_Sorter.ClearOptions();

            List<string> options = new List<string>();

            foreach (SorterCategories flag in Enum.GetValues(typeof(SorterCategories)))
            {
                if (WeightValueSorting.Settings.EnabledCategories.HasFlag(flag))
                {
                    Tuple<int, string> cat = WeightValueSorting.CategoryMap[flag];
                    options.Add(cat.Item2 ?? LocalizedTexts.Instance.ItemsFilter.GetText((ItemsFilter.SorterType)cat.Item1));
                }
            }

            __instance.m_Sorter.AddOptions(options.ToList());
        }
    }

    [HarmonyPatch(typeof(ItemsFilter), nameof(ItemsFilter.ItemSorter))]
    public static class ItemsFilter_ItemSorter
    {
        private static int CompareByWeightValue(ItemEntity a, ItemEntity b, ItemsFilter.FilterType filter)
        {
            float a_weight_value = a.Blueprint.Weight <= 0.0f ? float.PositiveInfinity : a.Blueprint.Cost / a.Blueprint.Weight;
            float b_weight_value = b.Blueprint.Weight <= 0.0f ? float.PositiveInfinity : b.Blueprint.Cost / b.Blueprint.Weight;
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
