using BepInEx;
using BepInEx.Configuration;
using MoreUpgrades.Classes;
using REPOLib;
using REPOLib.Modules;
using Steamworks.Ugc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using static ItemBundles.ItemBundles;

namespace ItemBundles
{
    public static class MoreUpgradesCompat
    {
        public static string modName = "MoreUpgrades";
        public static Assembly moreUpgradesAssembly;
        public static Type plugin_type;
        public static BaseUnityPlugin plugin_instance;

        public static Type upgradesMananger_type;
        public static object upgradesMananger_instance;
        public static MethodInfo upgradesMananger_upgradeMethod;


        public static BindingFlags bindingFlags;
        public static void InitCompat()
        {
            // Haven't found a good way to do this, MoreUpgrades is not exposed to additions outside of implementing stuff it's own way
            // rn plan is to brute force adding to item count through internal methods
            var path = Path.GetDirectoryName(BepInEx.Bootstrap.Chainloader.PluginInfos[$"bulletbot.{modName.ToLower()}"].Location);
            moreUpgradesAssembly = Assembly.LoadFrom(path + $"\\{modName}.dll");
            plugin_type = moreUpgradesAssembly.GetType($"{modName}.Plugin");

            bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
            plugin_instance = (BaseUnityPlugin)(plugin_type).GetField("instance", bindingFlags).GetValue(null);

            List<UpgradeItem> plugin_upgradeItems;
            GetUpgradeItemsList(out plugin_upgradeItems);

            foreach (UpgradeItem item in plugin_upgradeItems)
            {
                var testText = item.fullName + " Bundle";
                var newItem = ItemBundles.Instance.assetBundle.LoadAsset<Item>(item.fullName + " Bundle");
                if ( newItem )
                {
                    REPOLib.Modules.Items.RegisterItem( newItem );

                    ItemBundlesLogger.LogInfo($"-- {modName}Compat found bundle item: {newItem}", true);
                    newItem.prefab.GetComponent<MoreUpgradesCompat_Bundle>().upgradeItemName = item.name;
                    newItem.prefab.GetComponent<MoreUpgradesCompat_Bundle>().FixMaterial();
                    newItem.prefab.GetComponent<MoreUpgradesCompat_Bundle>().FixLight();
                }
                else
                {
                    ItemBundlesLogger.LogError($"-- {modName} did not find bundle item", true);
                }
            }
        }

        public static void GetUpgradeItemsList(out List<UpgradeItem> upgradeItems)
        {
            List<UpgradeItem> upgradeItemsTemp = (List<UpgradeItem>)(plugin_type).GetField("upgradeItems", bindingFlags).GetValue(plugin_instance);

            if (upgradeItemsTemp != null)
            {
                /*foreach (UpgradeItem item in upgradeItemsTemp)
                {
                    ItemBundlesLogger.LogInfo($"- {modName}Compat upgradeItems list item: {item.fullName}", true);
                }*/
                upgradeItems = upgradeItemsTemp;
            }
            else
            {
                ItemBundlesLogger.LogError($"- {modName}Compat: {modName}.Plugin.instance.upgradeItems was not found, generating empty list to prevent NRE!", true);
                upgradeItems = new List<UpgradeItem>();
            }
        }

        public static void CallUpgrade(string upgradeItemName, string steamId, int amount = 1)
        {
            var parametersArray = new object[] { upgradeItemName, steamId, amount };

            //Update with latest references, exit early if we don't find one
            if (upgradesMananger_type == null) upgradesMananger_type = moreUpgradesAssembly.GetType($"{modName}.Classes.MoreUpgradesManager");
            if (upgradesMananger_type == null) return;
            if (upgradesMananger_instance == null) upgradesMananger_instance = upgradesMananger_type.GetField("instance", bindingFlags).GetValue(null);
            if (upgradesMananger_instance == null) return;
            if (upgradesMananger_upgradeMethod == null) upgradesMananger_upgradeMethod = upgradesMananger_type.GetMethod("Upgrade", bindingFlags);
            if (upgradesMananger_upgradeMethod == null) return;

            upgradesMananger_upgradeMethod.Invoke(upgradesMananger_instance, parametersArray);
        }

        public static Material? GetBoxMat(string upgradeItemName)
        {
            AssetBundle test = (AssetBundle)(plugin_type).GetField("assetBundle", bindingFlags).GetValue(plugin_instance);
            Item obj = test.LoadAsset<Item>(upgradeItemName);
            if ( obj == null )
            {
                ItemBundlesLogger.LogError($"- GetBoxMat {upgradeItemName} failed", true);
                return null;
            }

            var mat = obj.prefab.GetComponentInChildren<MeshRenderer>().materials[0];
            var meshFilters = obj.prefab.GetComponentsInChildren<MeshFilter>();
            foreach ( MeshFilter meshF in meshFilters)
            {
                var meshR = meshF.GetComponent<MeshRenderer>();
                if (meshR != null)
                {
                    if ( meshR.materials[0].name.Contains("upgrade") )
                    {
                        mat = meshR.materials[0];
                    }
                }
            }

            return mat;
        }

        public static Light? GetLight(string upgradeItemName)
        {
            AssetBundle test = (AssetBundle)(plugin_type).GetField("assetBundle", bindingFlags).GetValue(plugin_instance);
            Item obj = test.LoadAsset<Item>(upgradeItemName);
            if (obj == null)
            {
                ItemBundlesLogger.LogError($"- GetLight {upgradeItemName} failed", true);
                return null;
            }

            var lightObj = obj.prefab.transform.Find("Light - Small Lamp");
            if (lightObj == null)
            {
                return null;
            }
            else
            {
                var light = lightObj.GetComponent<Light>();
                return light;
            }
        }

        public static void RegisterBundleItem_MoreUpgrades( AssetBundle assetBundle, string bundleItemString )
        {
            ItemBundles.Instance.RegisterBundleItemCustom(assetBundle, bundleItemString, "MoreUpgrades ");
            UpdateItemInfo(bundleItemString);
        }

        /// <summary>
        /// This updates the bundle's price to match that of the original upgrade
        /// </summary>
        /// <param name="bundleItemString"></param>
        public static void UpdateItemInfo( string bundleItemString )
        {
            var item = StatsManager.instance.itemDictionary[bundleItemString];
            var newString = BundleHelper.RemoveString(bundleItemString, " Bundle");

            var item2 = StatsManager.instance.itemDictionary[newString];
            item.value = ScriptableObject.CreateInstance<Value>();

            //Hacky workaround atm
            //For some reason, MoreUpgrades ignores ShopMananger.instance.itemValueMultiplier which is equal to 4 and instead directly sets the costs
            //This just reverses that to make it in line with the game's original values
            //TODO: Make this and other stuff respect MoreUpgrades Configs
            item.value.valueMin = item2.value.valueMin / 4;
            item.value.valueMax = item2.value.valueMax / 4;
        }
    }
}
