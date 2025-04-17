using HarmonyLib;
using Photon.Pun;
using Steamworks.Ugc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.VisualScripting;
using UnityEngine;
using static SemiFunc;
using static UnityEngine.SpookyHash;

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
        [HarmonyPostfix, HarmonyPatch(nameof(StatsManager.AddItemsUpgradesPurchased))]
        private static void AddItemsUpgradesPurchased_Postfix(StatsManager __instance, ref string itemName)
        {
            var bundleString = " Bundle";

            // Add base upgrades to make sure they scale properly
            if (itemName.Contains(bundleString))
            {
                var originalItemName = BundleHelper.GetItemStringFromBundle(itemName);

                Dictionary<string, int> dictionary = __instance.itemsUpgradesPurchased;
                int num = dictionary[originalItemName];
                dictionary[originalItemName] = num + 1;
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
        [HarmonyPrefix, HarmonyPatch(nameof(ItemAttributes.GetValue))]
        private static void GetValue_Prefix( ItemAttributes __instance)
        {
            if (ItemBundles.Instance.moreUpgradesLoaded )
            {
                //Temp patch, overrides price scaling so that it uses base costs
                //MoreUpgradesCompat.OverrideConfigs();
            }
        }
        /// <summary>
        /// Additional code that runs after GetValue to make bundles scale based off of player count. 
        ///     Original method does not return anything so we just have to brute-force recalculate the costs.
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

                    var playerCount = PlayerGetAll().Count;
                    playerCount += ItemBundles.Instance.config_debugFakePlayers.Value;

                    // Recalculate upgrade bundles to use base item instead of self, incase single upgrades have been bought
                    if (__instance.itemType == itemType.item_upgrade)
                    {
                        float num = UnityEngine.Random.Range(__instance.itemValueMin, __instance.itemValueMax) * ShopManager.instance.itemValueMultiplier;
                        if (num < 1000f)
                        {
                            num = 1000f;
                        }
                        if (num >= 1000f)
                        {
                            num = Mathf.Ceil(num / 1000f);
                        }
                        num += num * ShopManager.instance.upgradeValueIncrease * (float)StatsManager.instance.GetItemsUpgradesPurchased( BundleHelper.GetItemStringFromBundle(__instance.itemAssetName) );

                        __instance.value = (int)num;
                    }

                    // Adjust consumable bundle price by minimum value if higher than player count
                    if ( __instance.itemType == itemType.grenade || __instance.itemType == itemType.mine )
                    {
                        playerCount = Mathf.Max( playerCount, BundleHelper.GetItemBundleMinItem( BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType ) );
                    }

                    // If more than one player, apply player multiplier + percentage adjust
                    if (playerCount > 1)
                    {
                        var priceMult = BundleHelper.GetItemBundlePriceMult(BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType) / 100f;
                        var twoThirds = playerCount * priceMult;
                        currentValue = Mathf.RoundToInt(currentValue * twoThirds);

                        ItemBundlesLogger.LogWarning($"---- ITEM VALUE INCREASED: {currentValue}");
                        __instance.value = currentValue;

                        if (GameManager.Multiplayer())
                        {
                            __instance.photonView.RPC("GetValueRPC", RpcTarget.Others, __instance.value);
                        }
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
            .ThrowIfInvalid("ShowingInfo(): Couldn't find matching code");

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
            ItemBundlesLogger.LogInfo("--- ShowingInfo(): ADDING NEW INSTRUCTIONS", true);

            return codeMatcher.InstructionEnumeration();
        }

        /// <summary>
        /// Modifies item name displays with a little flare
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix, HarmonyPatch(nameof(ItemAttributes.ShowingInfo))]
        private static void ShowingInfo_Postfix(ItemAttributes __instance)
        {
            var promptPre = __instance.promptName;
            var bundleString = "Bundle";

            // If we have a bundle asset name, add a little tag underneath it :)
            if (__instance.itemAssetName.Contains(bundleString))
            {
                var numText = "";
                var playerCount = SemiFunc.PlayerGetAll().Count;
                if ( __instance.itemType == itemType.healthPack )
                {
                    var heal = __instance.GetComponent<ItemHealthPackBundle>().healAmount;
                    numText = (BundleHelper.PlayerGetAllAlive().Count * heal).ToString() + "hp";
                }
                else if (__instance.itemType == itemType.grenade || __instance.itemType == itemType.mine)
                {
                    numText = Mathf.Max(playerCount, BundleHelper.GetItemBundleMinItem(BundleHelper.GetItemStringFromBundle(__instance.item), __instance.itemType)).ToString();
                }
                else if ( __instance.itemType == itemType.item_upgrade )
                {
                    numText = playerCount.ToString();
                }

                promptPre = promptPre + $"\n[Bundle of {numText}]";
                __instance.promptName = promptPre;


                // REPLACED WITH ABOVE TRANSPILER
                // This method was tanking frames because InputManager.instance.InputDisplayReplaceTags("[interact]") loops through every input binding and is called every frame
                // The below change worked but I wanted a better solution so the transpiler version was made
                /*
                //If we're in a shop, remove interactable prompt
                // This is a fix for consumable items, upgrades and health packs have their own code already
                if (SemiFunc.RunIsShop())
                {
                    //var interactKeyText = InputManager.instance.InputDisplayReplaceTags("[interact]")
                    var inputManager = InputManager.instance;
                    var interactKey = inputManager.tagDictionary["[interact]"];
                    var interactKeyText = inputManager.InputDisplayGet(interactKey, MenuKeybind.KeyType.InputKey, MovementDirection.Up);
                
                    var interactString = " <color=#FFFFFF>[<u><b>" + interactKeyText + "/b></u>]</color>";
                    if (__instance.promptName.Contains(interactString))
                    {
                        __instance.promptName = BundleHelper.RemoveString(__instance.promptName, interactString);
                    }
                }*/
            }
        }
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

            ItemBundlesLogger.LogInfo($"------ Overriding Shop List", true);

            ItemBundles.Instance.itemDictionaryShop.Clear();
            foreach (KeyValuePair<string, Item> entry in StatsManager.instance.itemDictionary)
            {
                var keys = ItemBundles.Instance.itemDictionaryShopBlacklist.Keys.ToList();
                var values = ItemBundles.Instance.itemDictionaryShopBlacklist.Values.ToList();
                if (keys.Contains(entry.Key) || values.Contains(entry.Value))
                {
                    ItemBundlesLogger.LogInfo($"------ Blacklisting {entry.Key} or {entry.Value} from shop list", true);
                    continue;
                }

                ItemBundlesLogger.LogInfo($"------ Adding {entry.Key} or {entry.Value} to shop list", true);
                ItemBundles.Instance.itemDictionaryShop.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// IL Transpiler that replaces StatsManager.instance.itemDictionary with ItemBundles.Instance.itemDicitonaryShop
        ///     itemDictionaryShop is populated in the prefix above and simply copies the itemDicitonary, while omiting anything in itemDicitonaryShopBlacklist
        ///     this allows us to add items to the game without having them appear in the shop itself, something the base game nor REPOlib currently support
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

            // Expect IL_003F
            codeMatcher.MatchForward(false, (CodeMatch[])(object)new CodeMatch[3]
            {
                new CodeMatch((OpCode?)OpCodes.Ldsfld),
                new CodeMatch((OpCode?)OpCodes.Ldfld),
                new CodeMatch((OpCode?)OpCodes.Callvirt)
            })
            .ThrowIfInvalid("GetAllItemsFromStatsManager(): Couldn't find matching code");

            ItemBundlesLogger.LogInfo("--- GetAllItemsFromStatsManager(): ADDING NEW INSTRUCTIONS", true);

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

            ItemBundlesLogger.LogInfo($"------ Bundling Lists", true);
            if (!SemiFunc.IsMultiplayer() && ItemBundles.Instance.config_disableBundlesSP.Value) return;
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
                    ItemBundlesLogger.LogInfo($"-{num}- Found {item.itemAssetName} entry", true);
                    var itemTypeBundleInfo = ItemBundles.Instance.itemTypeBundleInfo[item.itemType];
                    var itemBundleInfo = ItemBundles.Instance.itemBundleInfo[item.itemAssetName];

                    float bundleFinalChance = BundleHelper.GetItemBundleChance(item);
                    bundleFinalChance /= 100f;

                    bool maxMet = BundleHelper.GetItemBundleMax(item) == 0;
                    if (maxMet)
                    {
                        ItemBundlesLogger.LogWarning($"-{num}- Already have max bundles for {item.itemAssetName}!", true);
                        continue;
                    }

                    var rand = UnityEngine.Random.Range(0f, 1f);
                    if (rand <= bundleFinalChance)
                    {
                        //REPLACE ITEM WITH BUNDLE
                        ItemBundlesLogger.LogWarning($"-{num}- Passed with {rand} {rand <= bundleFinalChance}, Replacing item {tempList[num]} with {itemBundleInfo.bundleItem}!", true);

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
                        ItemBundlesLogger.LogError($"-{num}- Failed with {rand} {rand <= bundleFinalChance}, keeping item {tempList[num]}!", true);
                    }
                }
            }

            tempList.Shuffle();
            itemList = tempList;
        }
    }
}