using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using static SemiFunc;
using static UnityEngine.Rendering.DebugUI;

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
            if ( itemName.Contains(bundleString) )
            {
                int index = itemName.IndexOf(bundleString);
                var orignalName = (index < 0)
                    ? itemName
                    : itemName.Remove(index, bundleString.Length);
                
                itemName = orignalName;
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
                if ( bundleItemName.Contains(bundleString) )
                {
                    var currentValue = __instance.value;

                    var playerCount = SemiFunc.PlayerGetAll().Count;
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
        private static void ShowingInfo_Postfix( ItemAttributes __instance )
        {
            var promptPre = __instance.promptName;
            if ( __instance.itemAssetName.Contains("Bundle") )
            {
                var newPrompt = promptPre + "\n [Bundle]";
                __instance.promptName = newPrompt;
            }
        }
    }
}