using REPOLib.Extensions;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using REPOLib;

namespace ItemBundles
{
    public static class BundleHelper
    {
        public static int GetItemBundleChance(Item item)
        {
            var output = ItemBundles.Instance.itemTypeBundleInfo[item.itemType].chanceInShop;
            if (ItemBundles.Instance.itemBundleInfo[item.itemAssetName].chanceInShop >= 0)
            {
                output = ItemBundles.Instance.itemBundleInfo[item.itemAssetName].chanceInShop;
            }

            return output;
        }

        public static int GetItemBundleMax(Item item)
        {
            var output = ItemBundles.Instance.itemTypeBundleInfo[item.itemType].maxInShop;
            if (ItemBundles.Instance.itemBundleInfo[item.itemAssetName].maxInShop >= 0)
            {
                output = ItemBundles.Instance.itemBundleInfo[item.itemAssetName].maxInShop;
            }

            return output;
        }

        public static int GetItemBundleMinItem(Item item)
        {
            var output = ItemBundles.Instance.config_minConsumablePerBundle.Value;
            if (ItemBundles.Instance.itemTypeBundleInfo[item.itemType].config_minPerBundle.Value >= 0)
            {
                output = ItemBundles.Instance.itemTypeBundleInfo[item.itemType].config_minPerBundle.Value;
            }
            if ( ItemBundles.Instance.itemBundleInfo[item.itemAssetName].config_minPerBundle.Value >= 0)
            {
                output = ItemBundles.Instance.itemBundleInfo[item.itemAssetName].config_minPerBundle.Value;
            }

            return output;
        }

        public static int GetItemBundleMinItem(string itemString, SemiFunc.itemType itemType)
        {
            var output = ItemBundles.Instance.config_minConsumablePerBundle.Value;
            if (ItemBundles.Instance.itemTypeBundleInfo[itemType].config_minPerBundle.Value >= 0)
            {
                output = ItemBundles.Instance.itemTypeBundleInfo[itemType].config_minPerBundle.Value;
            }
            if (ItemBundles.Instance.itemBundleInfo[itemString].config_minPerBundle.Value >= 0)
            {
                output = ItemBundles.Instance.itemBundleInfo[itemString].config_minPerBundle.Value;
            }

            return output;
        }

        public static string GetItemStringFromBundle( Item bundleItem )
        {
            string bundleItemString = bundleItem.itemAssetName;
            return GetItemStringFromBundle( bundleItemString );
        }

        public static string GetItemStringFromBundle(string bundleItemString)
        {
            string bundleString = " Bundle";
            var originalItemString = RemoveString(bundleItemString, bundleString);

            return originalItemString;
        }

        public static string RemoveString(string baseString, string removeString)
        {
            int index = baseString.IndexOf(removeString);
            var newString = (index < 0)
                ? baseString
                : baseString.Remove(index, removeString.Length);

            return newString;
        }
    }
}
