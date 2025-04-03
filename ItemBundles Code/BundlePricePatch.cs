﻿using HarmonyLib;
using Photon.Pun;
using Steamworks.Ugc;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static SemiFunc;

namespace ItemBundles
{
    [HarmonyPatch(typeof(StatsManager))]
    internal static class BundlePatch_StatsManager
    {
        /// <summary>
        /// Modify strings before they are passed to original method so that bundles are counted as
        ///     the original upgrades for the sake of cost scaling
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="itemName"></param>
        [HarmonyPrefix, HarmonyPatch(nameof(StatsManager.AddItemsUpgradesPurchased))]
        private static void AddItemsUpgradesPurchased_Prefix(StatsManager __instance, ref string itemName)
        {
            var bundleString = " Bundle";
            if (itemName.Contains(bundleString))
            {
                itemName = BundleHelper.GetItemStringFromBundle( itemName );
            }
        }

        [HarmonyPostfix, HarmonyPatch(nameof(StatsManager.Start))]
        public static void Start_Postfix(StatsManager __instance)
        {
            ItemBundles.Instance.InitializeItemBundles();
        }
    }

    [HarmonyPatch(typeof(ItemAttributes))]
    internal static class BundlePatch_ItemAttributes
    {
        /// <summary>
        /// Additional code that runs after GetValue to make bundles scale based off of player count. 
        /// Original method does not return anything so we just have to brute-force recalculate the costs.
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix, HarmonyPatch(nameof(ItemAttributes.GetValue))]
        private static void GetValue_Postfix(ItemAttributes __instance)
        {
            if (!GameManager.Multiplayer() || PhotonNetwork.IsMasterClient)
            {
                // We have a bundle, multiply price by a portion of player count
                var bundleString = "Bundle";
                var bundleItemName = __instance.itemAssetName;
                if (bundleItemName.Contains(bundleString))
                {
                    var currentValue = __instance.value;

                    var playerCount = SemiFunc.PlayerGetAll().Count;
                    playerCount += ItemBundles.Instance.config_debugFakePlayers.Value;
                    if ( __instance.itemType == itemType.grenade || __instance.itemType == itemType.mine )
                    {
                        playerCount = Mathf.Max( playerCount, BundleHelper.GetItemBundleMinItem( BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType ) );
                    }
                    if (playerCount > 1)
                    {
                        var twoThirds = playerCount * 0.6666f;
                        currentValue = Mathf.RoundToInt(currentValue * twoThirds);

                        __instance.value = currentValue;

                        if (GameManager.Multiplayer())
                        {
                            __instance.photonView.RPC("GetValueRPC", RpcTarget.Others, __instance.value);
                        }
                    }
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(nameof(ItemAttributes.ShowingInfo))]
        private static void ShowingInfo_Postfix(ItemAttributes __instance)
        {
            var promptPre = __instance.promptName;
            var bundleString = "Bundle";

            // If we have a bundle asset name, add a little tag underneath it :)
            if (__instance.itemAssetName.Contains(bundleString))
            {
                var newPrompt = promptPre + "\n[Bundle]";
                __instance.promptName = newPrompt;

                // If we're in a shop, remove interactable prompt
                // This is a fix for consumable items, upgrades and health packs have their own code already
                if (SemiFunc.RunIsShop())
                {
                    var itemTag = InputManager.instance.InputDisplayReplaceTags("[interact]");
                    var interactString = " <color=#FFFFFF>[" + itemTag + "]</color>";
                    if (__instance.promptName.Contains(interactString))
                    {
                        __instance.promptName = BundleHelper.RemoveString(__instance.promptName, interactString);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(DefaultPool))]
    internal static class BundlePatch_DefaultPool
    {
        /*
        // OLD CODE, was used to override spawning behaviour of items when a bundle was involved before the creation of the blacklist
        [HarmonyPrefix, HarmonyPatch(nameof(DefaultPool.Instantiate))]
        public static bool Instantiate_Prefix(DefaultPool __instance, ref GameObject __result, string prefabId, Vector3 position, Quaternion rotation)
        {
            if (prefabId.Contains("Bundle"))
            {
                GameObject value = null;
                if (!__instance.ResourceCache.TryGetValue(prefabId, out value))
                {
                    var itemPathString = "Items/";
                    var originalPrefabName = prefabId;
                    if ( prefabId.Contains(itemPathString) )
                    {
                        BundleHelper.RemoveString(prefabId, itemPathString);
                    }

                    value = ItemBundles.Instance.assetBundle.LoadAsset<GameObject>(originalPrefabName);
                    if (value == null)
                    {
                        CustomLogger.LogError("failed to load \"" + originalPrefabName + "\"");
                        return false;
                    }
                    else
                    {
                        if ( !__instance.ResourceCache.ContainsKey(originalPrefabName) )
                        {
                            __instance.ResourceCache.Add(originalPrefabName, value);
                        }
                    }
                }

                bool activeSelf = value.activeSelf;
                if (activeSelf)
                {
                    value.SetActive(value: false);
                }
                __result = Object.Instantiate(value, position, rotation);
                if (activeSelf)
                {
                    value.SetActive(value: true);
                }
                return false;
            }
            return true;
        } */
    }

    [HarmonyPatch(typeof(ShopManager))]
    [HarmonyPriority(Priority.Last)]
    internal static class BundlePatch_ShopManager
    {
        [HarmonyPrefix, HarmonyPatch(nameof(ShopManager.GetAllItemsFromStatsManager))]
        static void GetAllItemsFromStatsManager_Prefix(ShopManager __instance)
        {
            if (SemiFunc.IsNotMasterClient())
            {
                return;
            }

            CustomLogger.LogInfo($"------ Overriding Shop List", true);

            ItemBundles.Instance.itemDictionaryShop.Clear();
            foreach (KeyValuePair<string, Item> entry in StatsManager.instance.itemDictionary)
            {
                var keys = ItemBundles.Instance.itemDictionaryShopBlacklist.Keys.ToList();
                var values = ItemBundles.Instance.itemDictionaryShopBlacklist.Values.ToList();
                if (keys.Contains(entry.Key) || values.Contains(entry.Value))
                {
                    CustomLogger.LogInfo($"------ Blacklisting {entry.Key} or {entry.Value} from shop list", true);
                    continue;
                }

                CustomLogger.LogInfo($"------ Adding {entry.Key} or {entry.Value} to shop list", true);
                ItemBundles.Instance.itemDictionaryShop.Add(entry.Key, entry.Value);
            }

            // OLD OVERWRITE CODE
            // Replaced with below transpiler
            /*
            __instance.potentialItems.Clear();
            __instance.potentialItemConsumables.Clear();
            __instance.potentialItemUpgrades.Clear();
            __instance.potentialItemHealthPacks.Clear();
            __instance.potentialSecretItems.Clear();
            foreach (Item value in ItemBundles.Instance.itemDictionaryShop.Values)
            {
                int num = SemiFunc.StatGetItemsPurchased(value.itemAssetName);
                float num2 = value.value.valueMax / 1000f * __instance.itemValueMultiplier;
                if (value.itemType == SemiFunc.itemType.item_upgrade)
                {
                    num2 -= num2 * 0.05f * (float)(GameDirector.instance.PlayerList.Count - 1);
                    int itemsUpgradesPurchased = StatsManager.instance.GetItemsUpgradesPurchased(value.itemAssetName);
                    num2 += num2 * __instance.upgradeValueIncrease * (float)itemsUpgradesPurchased;
                    num2 = Mathf.Ceil(num2);
                }
                if (value.itemType == SemiFunc.itemType.healthPack)
                {
                    num2 += num2 * __instance.healthPackValueIncrease * (float)RunManager.instance.levelsCompleted;
                    num2 = Mathf.Ceil(num2);
                }
                if (value.itemType == SemiFunc.itemType.power_crystal)
                {
                    num2 += num2 * __instance.crystalValueIncrease * (float)RunManager.instance.levelsCompleted;
                    num2 = Mathf.Ceil(num2);
                }
                float num3 = Mathf.Clamp(num2, 1f, num2);
                bool flag = value.itemType == SemiFunc.itemType.power_crystal;
                bool flag2 = value.itemType == SemiFunc.itemType.item_upgrade;
                bool flag3 = value.itemType == SemiFunc.itemType.healthPack;
                int maxAmountInShop = value.maxAmountInShop;
                if (num >= maxAmountInShop || (value.maxPurchase && StatsManager.instance.GetItemsUpgradesPurchasedTotal(value.itemAssetName) >= value.maxPurchaseAmount) || (!(num3 <= (float)__instance.totalCurrency) && Random.Range(0, 100) >= 25))
                {
                    continue;
                }
                for (int i = 0; i < maxAmountInShop - num; i++)
                {
                    if (flag2)
                    {
                        __instance.potentialItemUpgrades.Add(value);
                        continue;
                    }
                    if (flag3)
                    {
                        __instance.potentialItemHealthPacks.Add(value);
                        continue;
                    }
                    if (flag)
                    {
                        __instance.potentialItemConsumables.Add(value);
                        continue;
                    }
                    if (value.itemSecretShopType == SemiFunc.itemSecretShopType.none)
                    {
                        __instance.potentialItems.Add(value);
                        continue;
                    }
                    if (!__instance.potentialSecretItems.ContainsKey(value.itemSecretShopType))
                    {
                        __instance.potentialSecretItems.Add(value.itemSecretShopType, new List<Item>());
                    }
                    __instance.potentialSecretItems[value.itemSecretShopType].Add(value);
                }
            }
            __instance.potentialItems.Shuffle();
            __instance.potentialItemConsumables.Shuffle();
            __instance.potentialItemUpgrades.Shuffle();
            __instance.potentialItemHealthPacks.Shuffle();
            foreach (List<Item> value2 in __instance.potentialSecretItems.Values)
            {
                value2.Shuffle();
            }

            // Replace/skip original function
            return false;
            */
        }

        [HarmonyTranspiler, HarmonyPatch(nameof(ShopManager.GetAllItemsFromStatsManager))]
        static IEnumerable<CodeInstruction> GetAllItemsFromStatsManager_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int insertIndex = -1;
            for (int i = 0; i < code.Count - 1; i++) // -1 since we will be checking i + 1
            {
                if (code[i].opcode == OpCodes.Ldsfld && code[i + 1].opcode == OpCodes.Ldfld)
                {
                    insertIndex = i;
                    break;
                }
            }

            object replace1 = code[insertIndex].operand;
            object replace2 = code[insertIndex + 1].operand;

            object itemBundlesInstanceField = (object)(typeof(ItemBundles).GetProperty(nameof(ItemBundles.Instance), BindingFlags.NonPublic | BindingFlags.Static).GetGetMethod(true));
            object itemDictionaryShopField = (object)(typeof(ItemBundles).GetField(nameof(ItemBundles.itemDictionaryShop)));

            CustomLogger.LogInfo($"------" +
                $"\n--- Replacing {replace1} of type {replace1.GetType()} with {itemBundlesInstanceField} of type {itemBundlesInstanceField.GetType()}" +
                $"\n--- Replacing {replace2} of type {replace2.GetType()} with {itemDictionaryShopField} of type {itemDictionaryShopField.GetType()}", true);

            if (itemBundlesInstanceField == null || itemDictionaryShopField == null)
            {
                CustomLogger.LogError($"------NULL OPERAND REPLACEMENT!!!\n--- itemBundlesInstanceField: {itemBundlesInstanceField}\n--- itemDictionaryShopField: {itemDictionaryShopField}");
            }
            else if (insertIndex != -1)
            {
                code[insertIndex].opcode = OpCodes.Call;
                code[insertIndex].operand = itemBundlesInstanceField;
                code[insertIndex + 1].operand = itemDictionaryShopField;
            }



            return code;
        }

        /// <summary>
        /// Check incoming list of items
        /// Replace entries with bundled options if rolled
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="itemName"></param>
        [HarmonyPostfix, HarmonyPatch(nameof(ShopManager.GetAllItemsFromStatsManager))]
        private static void GetAllItemsFromStatsManager_Postfix(ShopManager __instance)
        {
            //TODO: Re-add max total bundles once I figure out how to not make it stop on first list.
            foreach (KeyValuePair<itemType, ItemBundles.BundleShopInfo> bundleShopTypePairs in ItemBundles.Instance.itemTypeBundleInfo)
            {
                bundleShopTypePairs.Value.chanceInShop = bundleShopTypePairs.Value.config_chanceInShop.Value == -1 ? ItemBundles.Instance.config_chanceBundlesInShop.Value : bundleShopTypePairs.Value.config_chanceInShop.Value;
                bundleShopTypePairs.Value.maxInShop = bundleShopTypePairs.Value.config_maxInShop.Value == -1 ? ItemBundles.Instance.config_maxBundlesInShop.Value : bundleShopTypePairs.Value.config_maxInShop.Value;
            }
            foreach (KeyValuePair<string, ItemBundles.BundleShopInfo> bundleShopItemPairs in ItemBundles.Instance.itemBundleInfo)
            {
                bundleShopItemPairs.Value.chanceInShop = bundleShopItemPairs.Value.config_chanceInShop.Value;
                bundleShopItemPairs.Value.maxInShop = bundleShopItemPairs.Value.config_maxInShop.Value;
            }

            // Bundles have no use in single player
            if (!SemiFunc.IsMultiplayer()) return;

            CustomLogger.LogInfo($"------ Bundling Lists", true);
            AttemptBundlesFromList(ref __instance.potentialItems);
            AttemptBundlesFromList(ref __instance.potentialItemConsumables);
            AttemptBundlesFromList(ref __instance.potentialItemUpgrades);
            AttemptBundlesFromList(ref __instance.potentialItemHealthPacks);
        }

        private static void AttemptBundlesFromList( ref List<Item> itemList)
        {
            var tempList = new List<Item>(itemList);
            for (int num = tempList.Count - 1; num >= 0; num--)
            {
                var item = tempList[num];

                // Cant replace with a bundle if we don't have an entry at all
                //TODO Add minimum number
                if (ItemBundles.Instance.itemBundleInfo.ContainsKey(item.itemAssetName))
                {
                    CustomLogger.LogInfo($"-{num}- Found {item.itemAssetName} entry", true);
                    var itemTypeBundleInfo = ItemBundles.Instance.itemTypeBundleInfo[item.itemType];
                    var itemBundleInfo = ItemBundles.Instance.itemBundleInfo[item.itemAssetName];

                    float bundleFinalChance = BundleHelper.GetItemBundleChance(item);
                    bundleFinalChance /= 100f;

                    bool maxMet = BundleHelper.GetItemBundleMax(item) == 0;
                    if (maxMet)
                    {
                        CustomLogger.LogWarning($"-{num}- Already have max bundles for {item.itemAssetName}!", true);
                        continue;
                    }

                    var rand = Random.Range(0f, 1f);
                    if (rand <= bundleFinalChance)
                    {
                        //REPLACE ITEM WITH BUNDLE
                        CustomLogger.LogWarning($"-{num}- Passed with {rand} {rand <= bundleFinalChance}, Replacing item {tempList[num]} with {itemBundleInfo.bundleItem}!", true);

                        tempList[num] = itemBundleInfo.bundleItem;

                        if (itemTypeBundleInfo.maxInShop > 0)
                        {
                            itemTypeBundleInfo.maxInShop--;
                        }

                        if (itemBundleInfo.maxInShop > 0)
                        {
                            itemBundleInfo.maxInShop--;
                        }
                    }
                    else
                    {
                        CustomLogger.LogError($"-{num}- Failed with {rand} {rand <= bundleFinalChance}, keeping item {tempList[num]}!", true);
                    }
                }
            }

            tempList.Shuffle();
            itemList = tempList;
        }
    }
}