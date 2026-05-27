using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;
using static SemiFunc;

namespace ItemBundles
{
    [HarmonyPatch(typeof(StatsManager))]
    internal static class BundlePatch_StatsManager
    {
        [HarmonyPostfix, HarmonyPatch(nameof(StatsManager.Start))]
        public static void Start_Postfix(StatsManager __instance)
        {
            REPOLib.BundleLoader.OnAllBundlesLoaded += ItemBundles.Instance.InitializeItemBundles;
        }
    }

    [HarmonyPatch(typeof(ItemAttributes))]
    internal static class BundlePatch_ItemAttributes
    {
        /// <summary>
        /// Patch that prevents generated 'prefabs' from spamming NRE errors in the console
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix, HarmonyPatch(nameof(ItemAttributes.ShopInTruckLogic))]
        private static bool ShopInTruckLogic_Prefix(ItemAttributes __instance)
        {
            return !BundleHelper.IsObjectBundlePrefab(__instance.gameObject);
        }

        /// <summary>
        /// Patch that prevents generated 'prefabs' from spamming NRE errors in the console
        /// For Future/better patching: Caused by missed dict entry in StatsManager because it runs before the dicts get populated
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix, HarmonyPatch(nameof(ItemAttributes.GetValue))]
        private static bool GetValue_Prefix(ItemAttributes __instance)
        {
            return !BundleHelper.SceneIsPrefabStage();
        }

        /// <summary>
        /// Additional code that runs after GetValue to make bundles scale based off of player count. 
        ///     Original method does not return anything so we just have to brute-force recalculate the costs.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix, HarmonyPatch(nameof(ItemAttributes.GetValue))]
        [HarmonyPriority(Priority.Last)]
        private static void GetValue_Postfix(ItemAttributes __instance)
        {
            if (BundleHelper.SceneIsPrefabStage()) return;
            if (!GameManager.Multiplayer() || PhotonNetwork.IsMasterClient)
            {
                var bundleString = "Bundle";
                var bundleAssetName = __instance.itemAssetName;
                if (bundleAssetName.Contains(bundleString))
                {
                    var currentValue = __instance.value;

                    var playerCount = PlayerGetAll().Count;
                    playerCount += ItemBundles.Instance.config_debugFakePlayers.Value;

                    // We need to manually recalculate upgrade bundles so that they scale using the vanilla upgrades instead of themselves
                    if (__instance.itemType == itemType.item_upgrade)
                    {
                        var mult = ShopManager.instance.itemValueMultiplier;
                        var upgradeIncreaseMult = ShopManager.instance.upgradeValueIncrease;

                        // Base game multiplies prices by 4
                        // MoreUpgrades' base prices are multiplied by default
                        // Patch is need to correct this while still respecting MU configs
                        if (bundleAssetName.Contains("Modded") && MoreUpgradesCompat.enabled)
                        {
                            mult = MoreUpgradesCompat.GetOriginalValueMultiplier(bundleAssetName);
                            upgradeIncreaseMult = MoreUpgradesCompat.GetUpgradeValueIncrease(bundleAssetName);
                        }

                        /* Disabled until VanillaUpgrades is fixed for latest REPO
                        // Try to get config for VanillaUpgrades
                        else if (VanillaUpgradesCompat.enabled)
                        {
                            upgradeIncreaseMult = VanillaUpgradesCompat.GetUpgradeValueIncrease(upgradeIncreaseMult, bundleAssetName);
                        }
                        */

                        float num = UnityEngine.Random.Range(__instance.itemValueMin, __instance.itemValueMax) * mult;
                        num = Mathf.Max(num, 1000f);
                        num = Mathf.CeilToInt(num / 1000f);

                        //DebugLogger.LogInfo($"old base {__instance.value}, new base {num}, mult {mult}, upgradeIncreaseMult {upgradeIncreaseMult}, num items {(float)StatsManager.instance.GetItemsUpgradesPurchased(BundleHelper.GetItemStringFromBundle(__instance.itemAssetName))}", true);
                        num += num * upgradeIncreaseMult * (float)StatsManager.instance.GetItemsUpgradesPurchased(BundleHelper.GetItemStringFromBundle(__instance.itemAssetName));
                        currentValue = (int)num;
                    }

                    // Adjust consumable bundle price by minimum value if higher than player count
                    if (__instance.itemType == itemType.grenade || __instance.itemType == itemType.mine)
                    {
                        playerCount = Mathf.Max(playerCount, BundleHelper.GetItemBundleMinItem(BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType));
                    }

                    // If more than one player, apply player multiplier + percentage adjust
                    if (playerCount > 1)
                    {
                        var priceMult = BundleHelper.GetItemBundlePriceMult(BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType) / 100f;
                        var twoThirds = playerCount * priceMult;
                        var finalVal = Mathf.RoundToInt(currentValue * twoThirds);
                        currentValue = finalVal;
                    }

                    __instance.value = currentValue;
                    if (GameManager.Multiplayer())
                    {
                        __instance.photonView.RPC("GetValueRPC", RpcTarget.Others, __instance.value);
                    }
                }
            }
        }

        /// <summary>
        /// Adds an additional check to name display to prevent adding Interact text to bundles while in the shop
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyTranspiler, HarmonyPatch(nameof(ItemAttributes.ShowingInfo))]
        static IEnumerable<CodeInstruction> ShowingInfo_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);

            //
            // Expected Behavior: Add check to ShowingInfo() Line 13
            // bool flag = SemiFunc.RunIsShop() && (this.itemType == SemiFunc.itemType.item_upgrade || this.itemType == SemiFunc.itemType.healthPack);
            // to
            // bool flag = SemiFunc.RunIsShop() && (this.itemType == SemiFunc.itemType.item_upgrade || this.itemAssetName.Contains("Bundle") || this.itemType == SemiFunc.itemType.healthPack;
            //

            // Expect IL_0040
            codeMatcher.MatchForward(true, (CodeMatch[])(object)new CodeMatch[3]
            {
                new CodeMatch((OpCode?)OpCodes.Ldfld),
                new CodeMatch((OpCode?)OpCodes.Ldc_I4_3),
                new CodeMatch((OpCode?)OpCodes.Beq)
            })
            .ThrowIfInvalid("|---- ShowingInfo(): Couldn't find matching code");

            DebugLogger.LogInfo("|---- ShowingInfo(): ADDING NEW INSTRUCTIONS", true);

            // IL_004D label
            var exitOperand = codeMatcher.Operand;
            codeMatcher.Advance(1);

            codeMatcher.Insert((CodeInstruction[])(object)new CodeInstruction[5]
            {
                // store "this" to stack
			    new CodeInstruction(OpCodes.Ldarg_0),
                // consume stack obj 0, store current stack's itemAssetName field to stack
                new CodeInstruction(OpCodes.Ldfld, (object)AccessTools.Field(typeof(ItemAttributes), "itemAssetName")),
                // store "Bundle" to stack
                new CodeInstruction(OpCodes.Ldstr, "Bundle"),
                // see if stack obj 0 contains stack obj 1,                                                                v This helps specify which overload of method to use, we want the default so we only have one string param 
                new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(String), nameof(string.Contains), new System.Type[] { typeof(string) })),
                // move to IL_004D if above statement is true
			    new CodeInstruction(OpCodes.Brtrue, exitOperand)
            });

            return codeMatcher.InstructionEnumeration();
        }

        /// <summary>
        /// Modifies item name displays with a little flare
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix, HarmonyPatch(nameof(ItemAttributes.ShowingInfo))]
        private static void ShowingInfo_Postfix(ItemAttributes __instance)
        {
            if (__instance.itemAssetName.Contains("Bundle"))
            {
                var prompt = __instance.promptName;
                var numText = "";
                var playerCount = SemiFunc.PlayerGetAll().Count + ItemBundles.Instance.config_debugFakePlayers.Value;
                var shopPrompter = __instance.GetComponent<ItemBundleShopPrompter>();
                if (shopPrompter == null) return;
                shopPrompter.previousPrompt = prompt;

                switch (__instance.itemType)
                {
                    case itemType.healthPack:
                        var heal = __instance.GetComponent<ItemHealthPackBundle>().healAmount;
                        numText = ( (BundleHelper.PlayerGetAllAlive().Count + ItemBundles.Instance.config_debugFakePlayers.Value) * heal).ToString() + "hp";
                        break;
                    case itemType.grenade:
                    case itemType.mine:
                        numText = Mathf.Max(playerCount, BundleHelper.GetItemBundleMinItem(BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType)).ToString();
                        break;
                    case itemType.item_upgrade:
                        numText = playerCount.ToString();
                        break;
                    default: 
                        break;
                }

                /*
                if (__instance.itemType == itemType.healthPack)
                {
                    var heal = __instance.GetComponent<ItemHealthPackBundle>().healAmount;
                    numText = (BundleHelper.PlayerGetAllAlive().Count * heal).ToString() + "hp";
                }
                else if (__instance.itemType == itemType.grenade || __instance.itemType == itemType.mine)
                {
                    numText = Mathf.Max(playerCount, BundleHelper.GetItemBundleMinItem(BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType)).ToString();
                }
                else if (__instance.itemType == itemType.item_upgrade)
                {
                    numText = playerCount.ToString();
                }
                */

                bool grabbedLocal = __instance.physGrabObject.grabbedLocal;
                if ( shopPrompter.shopConfirm && grabbedLocal )
                {
                    var confirmTag = InputManager.instance.InputDisplayGet(InputManager.instance.tagDictionary["[interact]"], MenuKeybind.KeyType.InputKey, MovementDirection.Up);
                    prompt = shopPrompter.confirmUnbundlePrompt + $" <color=#FFFFFF>[<u><b>{confirmTag}</b></u>]</color>";
                }
                else
                {
                    prompt = prompt + $"\n[Bundle" + (String.IsNullOrEmpty(numText) ? numText : $" of {numText}") + "]";
                }
                __instance.promptName = prompt;
            }
        }
    }

    [HarmonyPatch(typeof(ShopManager))]
    [HarmonyPriority(Priority.Last)]
    internal static class BundlePatch_ShopManager
    {
        /// <summary>
        /// Refresh and populate ItemBundles.Instance.itemDictionaryShop, using a blacklist to prevent bundles from being directly spawned
        ///     so that we can replace items via another function without altering any item weights/chances
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPrefix, HarmonyPatch(nameof(ShopManager.GetAllItemsFromStatsManager))]
        static void GetAllItemsFromStatsManager_Prefix(ShopManager __instance)
        {
            if (SemiFunc.IsNotMasterClient())
            {
                return;
            }

            DebugLogger.LogInfo($"|---- GetAllItemsFromStatsManager_Prefix(): Overriding Shop List ----|", true);

            ItemBundles.Instance.itemDictionaryShop.Clear();
            
            if (StatsManager.instance.itemDictionary.Count == 0)
            {
                Traverse.Create(StatsManager.instance).Method("LoadItemsFromFolder").GetValue();
            }

            foreach (KeyValuePair<string, Item> entry in StatsManager.instance.itemDictionary)
            {
                var keys = ItemBundles.Instance.itemDictionaryShopBlacklist.Keys.ToList();
                var values = ItemBundles.Instance.itemDictionaryShopBlacklist.Values.ToList();
                if (keys.Contains(entry.Key) || values.Contains(entry.Value))
                {
                    DebugLogger.LogInfo($"|----- Blacklisting {entry.Key} or {entry.Value} from shop list", true);
                    continue;
                }

                DebugLogger.LogInfo($"|---- Adding {entry.Key} or {entry.Value} to shop list", true);
                ItemBundles.Instance.itemDictionaryShop.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// IL Transpiler that replaces StatsManager.instance.itemDictionary with ItemBundles.Instance.itemDicitonaryShop
        ///     itemDictionaryShop is populated in the prefix above and simply copies the itemDicitonary, while omiting anything in itemDicitonaryShopBlacklist
        ///     this allows us to add items to the game without having them appear in the shop itself, something the base game nor REPOlib currently support(? unknown if this is still true, too lazy to research because this works)
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyTranspiler, HarmonyPatch(nameof(ShopManager.GetAllItemsFromStatsManager))]
        static IEnumerable<CodeInstruction> GetAllItemsFromStatsManager_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);
            //
            // Expected Behavior: Replace dictionary at GetAllItemsFromStatsManager() Line 14
            // foreach (Item item in StatsManager.instance.itemDictionary.Values)
            // to
            // foreach (Item item in ItemBundles.instance.itemDictionaryShop.Values)
            //

            codeMatcher.MatchForward(false, (CodeMatch[])(object)new CodeMatch[3]
            {
                new CodeMatch((OpCode?)OpCodes.Ldsfld),
                new CodeMatch((OpCode?)OpCodes.Ldfld),
                new CodeMatch((OpCode?)OpCodes.Callvirt)
            })
            .ThrowIfInvalid("|---- GetAllItemsFromStatsManager(): Couldn't find matching code");

            DebugLogger.LogInfo("|---- GetAllItemsFromStatsManager(): ADDING NEW INSTRUCTIONS", true);

            // Replace Ldsfld with Call because we need to access a property instead of a field
            codeMatcher.Opcode = OpCodes.Call;
            codeMatcher.Operand = AccessTools.PropertyGetter(typeof(ItemBundles), "Instance");
            codeMatcher.Advance(1);
            codeMatcher.Operand = AccessTools.Field(typeof(ItemBundles), "itemDictionaryShop");

            return codeMatcher.InstructionEnumeration();
        }

        /// <summary>
        /// Check incoming list of items, replacing entries with bundled options
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="itemName"></param>
        [HarmonyPostfix, HarmonyPatch(nameof(ShopManager.GetAllItemsFromStatsManager))]
        private static void GetAllItemsFromStatsManager_Postfix(ShopManager __instance)
        {
            //TODO: Re-add max total bundles once I figure out how to not make it stop on first list.
            ItemBundles.globalSpawnBudget = ItemBundles.Instance.config_maxBundlesInShop.Value;
            foreach (KeyValuePair<itemType, ItemBundles.BundleShopInfo> bundleShopTypePairs in ItemBundles.Instance.itemTypeBundleInfos)
            {
                bundleShopTypePairs.Value.spawnBudget = bundleShopTypePairs.Value.config_maxInShop.Value;
            }
            foreach (KeyValuePair<string, ItemBundles.BundleShopInfo> bundleShopItemPairs in ItemBundles.Instance.itemBundleInfos)
            {
                bundleShopItemPairs.Value.spawnBudget = bundleShopItemPairs.Value.config_maxInShop.Value;
            }

            if (!SemiFunc.IsMultiplayer() && ItemBundles.Instance.config_disableBundlesSP.Value) return;
            BundleHelper.AttemptBundlesFromList(ref __instance.potentialItemUpgrades);
            BundleHelper.AttemptBundlesFromList(ref __instance.potentialItemConsumables);
            BundleHelper.AttemptBundlesFromList(ref __instance.potentialItems);
            BundleHelper.AttemptBundlesFromList(ref __instance.potentialItemHealthPacks);
            DebugLogger.LogInfo($"|---- GetAllItemsFromStatsManager_Postfix(): Bundling Lists ----|", true);
        }
    }

    [HarmonyPatch(typeof(UpgradeStand))]
    internal static class BundlePatch_UpdgradeStand
    {
        /// <summary>
        /// IL Transpiler that replaces StatsManager.instance.itemDictionary with ItemBundles.Instance.itemDicitonaryShop
        ///     itemDictionaryShop is populated in the prefix to ShopManager.GetAllItemsFromStatsManager and simply copies the itemDicitonary, while omiting anything in itemDicitonaryShopBlacklist
        ///     this allows us to add items to the game without having them appear in the shop itself, something the base game nor REPOlib currently support(? unknown if this is still true, too lazy to research because this works)
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyTranspiler, HarmonyPatch(nameof(UpgradeStand.GetWeightedUpgradeExcluding))]
        static IEnumerable<CodeInstruction> GetWeightedUpgradeExcluding_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);
            //
            // Expected Behavior: Replace dictionary at GetWeightedUpgradeExcluding() Line 7
            // foreach (Item item in StatsManager.instance.itemDictionary.Values)
            // to
            // foreach (Item item in ItemBundles.instance.itemDictionaryShop.Values)
            //

            codeMatcher.MatchForward(false, (CodeMatch[])(object)new CodeMatch[3]
            {
                new CodeMatch((OpCode?)OpCodes.Ldsfld),
                new CodeMatch((OpCode?)OpCodes.Ldfld),
                new CodeMatch((OpCode?)OpCodes.Callvirt)
            })
            .ThrowIfInvalid("|---- GetWeightedUpgradeExcluding(): Couldn't find matching code");

            DebugLogger.LogInfo("|---- GetWeightedUpgradeExcluding(): ADDING NEW INSTRUCTIONS", true);

            // Replace Ldsfld with Call because we need to access a property instead of a field
            codeMatcher.Opcode = OpCodes.Call;
            codeMatcher.Operand = AccessTools.PropertyGetter(typeof(ItemBundles), "Instance");
            codeMatcher.Advance(1);
            codeMatcher.Operand = AccessTools.Field(typeof(ItemBundles), "itemDictionaryShop");

            return codeMatcher.InstructionEnumeration();
        }

        /// <summary>
        /// IL Transpiler that rolls new upgrade choices against the configured bundle chance before spawning them
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyTranspiler, HarmonyPatch(nameof(UpgradeStand.SpawnNewUpgrades))]
        static IEnumerable<CodeInstruction> SpawnNewUpgrades_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions /*, ILGenerator generator*/);
            //
            // Expected Behavior: Add AttemptBundleItem to SpawnNewUpgrades at line 17
            // if (weightedUpgradeExcluding != null)
            // {
            //    string name = weightedUpgradeExcluding.name;
            // to
            // if (weightedUpgradeExcluding != null)
            // {
            //    weightedUpgradeExlcuding = BundleHelper.AttemptBundleItem( weightedUpgradeExlcuding );
            //    string name = weightedUpgradeExcluding.name;

            codeMatcher.MatchForward(false, (CodeMatch[])(object)new CodeMatch[3]
            {
                new CodeMatch((OpCode?)OpCodes.Ldloc_S),
                new CodeMatch((OpCode?)OpCodes.Callvirt),
                new CodeMatch((OpCode?)OpCodes.Stloc_S)
            })
            .ThrowIfInvalid("|---- SpawnNewUpgrades(): Couldn't find matching code");

            DebugLogger.LogInfo("|---- SpawnNewUpgrades(): ADDING NEW INSTRUCTIONS", true);

            // Replace Ldsfld with Call because we need to access a property instead of a field
            var tempOpCode = codeMatcher.Opcode;
            var tempOperand = codeMatcher.Operand;
            codeMatcher.InsertAndAdvance(new CodeInstruction(tempOpCode, tempOperand));
            codeMatcher.InsertAndAdvance( new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BundleHelper), nameof(BundleHelper.AttemptBundleItem))));
            codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_S, tempOperand));

            return codeMatcher.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(PhysGrabObject))]
    internal static class BundlePatch_PhysGrabObject
    {
        /// <summary>
        /// Patch that prevents generated 'prefabs' from causing NREs on startup
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix, HarmonyPatch(nameof(PhysGrabObject.OnEnable))]
        private static bool OnEnable_Prefix(PhysGrabObject __instance)
        {
            return !BundleHelper.SceneIsPrefabStage();
        }
    }

    [HarmonyPatch(typeof(RoomVolumeCheck))]
    internal static class BundlePatch_RoomVolumeCheck
    {
        /// <summary>
        /// Patch that prevents generated 'prefabs' from causing NREs on startup
        /// </summary>
        /// <param name="__instance"></param>
        /// <returns></returns>
        [HarmonyPrefix, HarmonyPatch(nameof(RoomVolumeCheck.CheckStart))]
        private static bool CheckStart_Prefix(RoomVolumeCheck __instance)
        {
            return !BundleHelper.SceneIsPrefabStage();
        }
    }
}