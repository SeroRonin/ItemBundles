using BepInEx;
using MoreUpgrades.Classes;
using REPOLib;
using REPOLib.Modules;
using Steamworks.Ugc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ItemBundles
{
    public static class VanillaUpgradesCompat
    {
        public static string modName = "VanillaUpgrades";
        public static Assembly vanillaUpgradesAssembly;
        public static Type plugin_type;
        public static BaseUnityPlugin plugin_instance;
        public static List<object> plugin_upgradeItems;

        public static Type upgradesMananger_type;
        public static object upgradesMananger_instance;
        public static MethodInfo upgradesMananger_upgradeMethod;

        public static Type upgradeItem_type;


        public static BindingFlags bindingFlags;

        public static void InitCompat()
        {
            // Haven't found a good way to do this, rn BulletBot's classes are almost exclusively marked internal, making them hard to implement compatibility outside of implementing stuff it's own way
            // rn plan is to brute force adding to item count through internal methods
            var path = Path.GetDirectoryName(BepInEx.Bootstrap.Chainloader.PluginInfos[$"bulletbot.{modName.ToLower()}"].Location);
            vanillaUpgradesAssembly = Assembly.LoadFrom(path + $"\\{modName}.dll");
            plugin_type = vanillaUpgradesAssembly.GetType($"{modName}.Plugin");

            bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
            plugin_instance = (BaseUnityPlugin)(plugin_type).GetField("instance", bindingFlags).GetValue(null);


            upgradeItem_type = vanillaUpgradesAssembly.GetType($"{modName}.Classes.UpgradeItem");

            ItemBundlesLogger.LogWarning($"- {modName} instance: {plugin_instance}", true);
            GetUpgradeItemsList(out plugin_upgradeItems);
        }

        public static void GetUpgradeItemsList( out List<object> upgradeItems )
        {
            List<object> upgradeItemsTemp = (List<object>)(plugin_type).GetField("upgradeItems", bindingFlags).GetValue(plugin_instance);

            ItemBundlesLogger.LogWarning($"- {modName} upgradeItems test: {upgradeItemsTemp}", true);

            var listType = typeof(List<>);
            var constructedListType = listType.MakeGenericType(upgradeItem_type);

            var instance = Activator.CreateInstance(constructedListType);

            if (upgradeItemsTemp != null)
            {
                var test = (IList)instance;
                foreach (object item in upgradeItemsTemp)
                {
                    if ( (Type)item == upgradeItem_type)
                    {
                        var upgradeItem_assetName = (string)(upgradeItem_type).GetField("name", bindingFlags).GetValue(item);
                        ItemBundlesLogger.LogWarning($"- {modName} upgradeItems list item: {upgradeItem_assetName}", true);
                        test.Add(item);
                    }
                }
                upgradeItems = test as List<object>;
            }
            else
            {
                ItemBundlesLogger.LogError($"- {modName}s.Plugin.instance.upgradeItems was not found, generating empty list to prevent NRE!", true);
                upgradeItems = new List<object>();
            }
        }

        public static void CallUpgrade( string upgradeItemName, string steamId, int amount = 1 )
        {
            var parametersArray = new object[] { upgradeItemName, steamId, amount };

            //Update with latest references, exit early if we don't find one
            if (upgradesMananger_type == null) upgradesMananger_type = vanillaUpgradesAssembly.GetType($"{modName}.Classes.{modName}Manager");
            if (upgradesMananger_type == null) return;
            if (upgradesMananger_instance == null) upgradesMananger_instance = (object)(upgradesMananger_type).GetField("instance", bindingFlags).GetValue(null);
            if (upgradesMananger_instance == null) return;
            if (upgradesMananger_upgradeMethod == null) upgradesMananger_upgradeMethod = (upgradesMananger_type).GetMethod("Upgrade", bindingFlags);
            if (upgradesMananger_upgradeMethod == null) return;

            upgradesMananger_upgradeMethod.Invoke(upgradesMananger_instance, parametersArray);
        }
    }
}
