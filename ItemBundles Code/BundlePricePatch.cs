using HarmonyLib;
using Photon.Pun;
using Steamworks.Ugc;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SemiFunc;

namespace ItemBundles
{
    [HarmonyPatch(typeof(StatsManager))]
    internal static class BundlePricePatch_StatsManager
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
                int index = itemName.IndexOf(bundleString);
                var orignalName = (index < 0)
                    ? itemName
                    : itemName.Remove(index, bundleString.Length);

                itemName = orignalName;
            }
        }

        [HarmonyPostfix, HarmonyPatch(nameof(StatsManager.Start))]
        public static void Start_Postfix(StatsManager __instance)
        {
            ItemBundles.Instance.RegisterItemBundles();
        }

        [HarmonyPostfix, HarmonyPatch(nameof(StatsManager.RunStartStats))]
        public static void RunStartStats_Postfix(StatsManager __instance)
        {

            foreach (ItemBundles.BundleShopInfo itemBundleInfo in ItemBundles.Instance.itemBundleInfo.Values.ToList() )
            {
                var item = itemBundleInfo.bundleItem;
                if (!string.IsNullOrEmpty(item.itemAssetName))
                {
                    if (!__instance.itemDictionary.ContainsKey(item.itemAssetName))
                    {
                        //__instance.itemDictionary.Add(item.itemAssetName, item);
                    }
                    foreach (Dictionary<string, int> item2 in __instance.AllDictionariesWithPrefix("item"))
                    {
                        item2.Add(item.itemAssetName, 0);
                    }
                }
                else
                {
                    Debug.LogWarning("Item with empty or null itemName found and will be skipped.");
                }
            }
        }
    }

    [HarmonyPatch(typeof(ItemAttributes))]
    internal static class BundlePricePatch_ItemAttributes
    {
        /// <summary>
        /// Additional code that runs after GetValue to make bundles scale based off of the upgrades regardless of bundled or not. 
        /// Original method does not return anything so we just have to brute-force recalculate the costs
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
                    if (playerCount > 1)
                    {
                        var twoThirds = (playerCount + ItemBundles.Instance.config_debugFakePlayers.Value) * 0.6666f;
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
                        int index = __instance.promptName.IndexOf(interactString);
                        var orignalName = (index < 0)
                            ? __instance.promptName
                            : __instance.promptName.Remove(index, interactString.Length);

                        __instance.promptName = orignalName;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(DefaultPool))]
    internal static class BundlePricePatch_DefaultPool
    {
        [HarmonyPrefix, HarmonyPatch(nameof(DefaultPool.Instantiate))]
        public static bool Instantiate_Prefix(DefaultPool __instance, ref GameObject __result, string prefabId, Vector3 position, Quaternion rotation)
        {
            if (prefabId.Contains("Bundle"))
            {
                GameObject value = null;
                if (!__instance.ResourceCache.TryGetValue(prefabId, out value))
                {
                    //ItemBundles.Logger.LogInfo($"--- Trying Instantiate Pre-regex: {prefabId}");

                    var itemPathString = "Items/";
                    var originalPrefabName = prefabId;
                    if ( prefabId.Contains(itemPathString) )
                    {
                        int index = prefabId.IndexOf(itemPathString);
                        originalPrefabName = (index < 0)
                            ? prefabId
                            : prefabId.Remove(index, itemPathString.Length);
                    }

                    //ItemBundles.Logger.LogInfo($"--- Trying Instantiate Post-regex: {originalPrefabName}");

                    value = ItemBundles.Instance.assetBundle.LoadAsset<GameObject>(originalPrefabName);
                    if (value == null)
                    {
                        Debug.LogError("failed to load \"" + originalPrefabName + "\"");
                        return false;
                    }
                    else
                    {
                        //ItemBundles.Logger.LogInfo($"--- Item properly loaded: {originalPrefabName}");

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
        }
    }

    [HarmonyPatch(typeof(ShopManager))]
    internal static class BundlePricePatch_ShopManager
    {
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

            ItemBundles.Logger.LogInfo($"------ Bundling Lists");
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
                    ItemBundles.Logger.LogInfo($"-{num}- Found {item.itemAssetName} entry");
                    var itemTypeBundleInfo = ItemBundles.Instance.itemTypeBundleInfo[item.itemType];
                    var itemBundleInfo = ItemBundles.Instance.itemBundleInfo[item.itemAssetName];

                    float bundleFinalChance = BundleHelper.GetItemBundleChance(item);
                    bundleFinalChance /= 100f;

                    bool maxMet = BundleHelper.GetItemBundleMax(item) == 0;
                    if (maxMet)
                    {
                        ItemBundles.Logger.LogWarning($"-{num}- Already have max bundles for {item.itemAssetName}!");
                        continue;
                    }

                    var rand = Random.Range(0f, 1f);
                    if (rand <= bundleFinalChance)
                    {
                        //REPLACE ITEM WITH BUNDLE
                        ItemBundles.Logger.LogWarning($"-{num}- Passed with {rand} {rand <= bundleFinalChance}, Replacing item {tempList[num]} with {itemBundleInfo.bundleItem}!");

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
                        ItemBundles.Logger.LogError($"-{num}- Failed with {rand} {rand <= bundleFinalChance}, keeping item {tempList[num]}!");
                    }
                }
            }

            tempList.Shuffle();
            itemList = tempList;
        }
    }
}