using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using REPOLib.Modules;
using Steamworks.Ugc;
using System.IO;
using UnityEngine;

namespace ItemBundles
{
    [BepInPlugin("SeroRonin.ItemBundles", "ItemBundles", "1.0")]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class ItemBundles : BaseUnityPlugin
    {
        internal static ItemBundles Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance._logger;
        private ManualLogSource _logger => base.Logger;
        internal Harmony? Harmony { get; set; }

        private void Awake()
        {
            Instance = this;

            // Prevent the plugin from being deleted
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            Patch();

            string pluginFolderPath = Path.GetDirectoryName(Info.Location);
            string assetBundleFilePath = Path.Combine(pluginFolderPath, "itembundles");

            AssetBundle assetBundle = AssetBundle.LoadFromFile(assetBundleFilePath);
            RegisterBundleItem(assetBundle, "Item Upgrade Map Player Count Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Energy Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Extra Jump Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Grab Range Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Grab Strength Bundle");
            //RegisterBundleItem(assetBundle, "Item Upgrade Player Grab Throw Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Health Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Sprint Speed Bundle");
            RegisterBundleItem(assetBundle, "Item Upgrade Player Tumble Launch Bundle");

            RegisterBundleItem(assetBundle, "Item Health Pack Small Bundle");
            RegisterBundleItem(assetBundle, "Item Health Pack Medium Bundle");
            RegisterBundleItem(assetBundle, "Item Health Pack Large Bundle");

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
        }

        internal void RegisterBundleItem( AssetBundle assetBundle, string itemString )
        {
            Item item = assetBundle.LoadAsset<Item>(itemString);
            if ( item == null )
            {
                Logger.LogError($"Item {itemString} not found!");
                return;
            }

            REPOLib.Modules.Items.RegisterItem(item);
        }

        internal void Patch()
        {
            Harmony ??= new Harmony(Info.Metadata.GUID);
            Harmony.PatchAll();
        }

        internal void Unpatch()
        {
            Harmony?.UnpatchSelf();
        }

        private void Update()
        {
            // Code that runs every frame goes here
        }
    }
}