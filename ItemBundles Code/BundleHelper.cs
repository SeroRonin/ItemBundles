using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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

        internal static bool AddItem(this StatsManager statsManager, Item item)
        {
            if (!statsManager.itemDictionary.ContainsKey(item.itemAssetName))
            {
                //statsManager.itemDictionary.Add(item.itemAssetName, item);
            }

            foreach (Dictionary<string, int> dictionary in statsManager.AllDictionariesWithPrefix("item"))
            {
                dictionary[item.itemAssetName] = 0;
            }
            return true;
        }

    }
}
