using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using REPOLib.Extensions;
using REPOLib.Modules;
using Steamworks.Ugc;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace ItemBundles
{
    [BepInPlugin("SeroRonin.ItemBundles", "ItemBundles", "1.2.0")]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("nickklmao.repoconfig", BepInDependency.DependencyFlags.HardDependency)]
    //[BepInDependency("BULLETBOT-MoreUpgrades-1.4.5", BepInDependency.DependencyFlags.SoftDependency)]
    public class ItemBundles : BaseUnityPlugin
    {
        internal static ItemBundles Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger => Instance._logger;
        private ManualLogSource _logger => base.Logger;
        internal Harmony? Harmony { get; set; }

        public AssetBundle assetBundle;

        public ConfigEntry<int> config_chanceBundlesInShop;
        public ConfigEntry<int> config_maxBundlesInShop;
        public ConfigEntry<int> config_debugFakePlayers;

        public Dictionary<SemiFunc.itemType, BundleShopInfo> itemTypeBundleInfo = new Dictionary<SemiFunc.itemType, BundleShopInfo>();
        public Dictionary<string, BundleShopInfo> itemBundleInfo = new Dictionary<string, BundleShopInfo>();
        public class BundleShopInfo
        {
            public Item bundleItem;
            public int chanceInShop;
            public int maxInShop;
            public ConfigEntry<int> config_chanceInShop;
            public ConfigEntry<int> config_maxInShop;
        }

        private void Awake()
        {
            Instance = this;

            string pluginFolderPath = Path.GetDirectoryName(Info.Location);
            string assetBundleFilePath = Path.Combine(pluginFolderPath, "itembundles");
            assetBundle = AssetBundle.LoadFromFile(assetBundleFilePath);

            // Prevent the plugin from being deleted
            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            CreateConfigs();

            Patch();

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BULLETBOT-MoreUpgrades-1.4.5"))
            {
                //Modules.Config.CreateRiskofOptionsCompat();
            }

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
        }

        public void RegisterItemBundles()
        {
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Map Player Count Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Energy Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Extra Jump Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Grab Range Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Grab Strength Bundle");
            ////RegisterBundleItem(assetBundle, "Item Upgrade Player Grab Throw Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Health Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Sprint Speed Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Upgrade Player Tumble Launch Bundle");

            //RegisterBundleItemRepoLib(assetBundle, "Item Health Pack Small Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Health Pack Medium Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Health Pack Large Bundle");

            //RegisterBundleItemRepoLib(assetBundle, "Item Grenade Explosive Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Grenade Shockwave Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Grenade Stun Bundle");

            //RegisterBundleItemRepoLib(assetBundle, "Item Mine Explosive Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Mine Shockwave Bundle");
            //RegisterBundleItemRepoLib(assetBundle, "Item Mine Stun Bundle");

            RegisterBundleItemCustom(assetBundle, "Item Upgrade Map Player Count Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Energy Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Extra Jump Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Grab Range Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Grab Strength Bundle");
            //RegisterBundleItem(assetBundle, "Item Upgrade Player Grab Throw Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Health Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Sprint Speed Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Upgrade Player Tumble Launch Bundle");

            RegisterBundleItemCustom(assetBundle, "Item Health Pack Small Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Health Pack Medium Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Health Pack Large Bundle");

            RegisterBundleItemCustom(assetBundle, "Item Grenade Explosive Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Grenade Shockwave Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Grenade Stun Bundle");

            RegisterBundleItemCustom(assetBundle, "Item Mine Explosive Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Mine Shockwave Bundle");
            RegisterBundleItemCustom(assetBundle, "Item Mine Stun Bundle");
        }

        public void CreateConfigs()
        {
            //TODO: Re-add max total bundles once I figure out how to not make it stop on first list.
            config_chanceBundlesInShop = Config.Bind("General", "Bundle Chance", 20, new ConfigDescription("Percent chance that an item will be replaced with a bundle variant.\nDefault: 20", new AcceptableValueRange<int>(0, 100)));
            config_maxBundlesInShop = Config.Bind("General", "Maximum Bundles In Shop", -1, new ConfigDescription("Maximum number of bundles that can appear of ANY one type. Setting to -1 makes shop ignore this entry\nDefault: -1", new AcceptableValueRange<int>(-1, 10)));

            itemTypeBundleInfo[SemiFunc.itemType.mine] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles: Item Type", "Mines: Chance", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles: Item Type", "Mines: Chance", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 10)))
            };
            itemTypeBundleInfo[SemiFunc.itemType.grenade] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles: Item Type", "Grenades: Chance", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles: Item Type", "Grenades: Max", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 10)))
            };
            itemTypeBundleInfo[SemiFunc.itemType.healthPack] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles: Item Type", "Health Packs: Chance", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles: Item Type", "Health Packs: Max", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 10)))
            };
            itemTypeBundleInfo[SemiFunc.itemType.item_upgrade] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles: Item Type", "Upgrades: Chance", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles: Item Type", "Upgrades: Max", -1, new ConfigDescription("Overrides the General entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 10)))
            };

            config_debugFakePlayers = Config.Bind("Dev", "Number of Fake Players", 0, new ConfigDescription("Adds fake players to bundle player calculations", new AcceptableValueRange<int>(-1, 3), "HideFromREPOConfig"));
        }

        internal void RegisterBundleItemRepoLib( AssetBundle assetBundle, string itemString )
        {
            Item item = assetBundle.LoadAsset<Item>(itemString);
            if ( item == null )
            {
                Logger.LogError($"Item {itemString} not found!");
                return;
            }

            REPOLib.Modules.Items.RegisterItem(item);
        }

        internal void RegisterBundleItemCustom(AssetBundle assetBundle, string bundleItemString, string originalItemString = "")
        {
            Item item = assetBundle.LoadAsset<Item>(bundleItemString);
            if (item == null)
            {
                Logger.LogError($"--- Bundle Item {bundleItemString} not found!");
                return;
            }

            var bundleString = " Bundle";
            if ( !bundleItemString.Contains(bundleString) )
            {
                Logger.LogError($"--- Item {bundleItemString} is not a bundle! Add \" Bundle\" to item name (WITH THE SPACE)");
                return;
            }

            // use string manipulation to remove " Bundle" from item name to get original item name
            if (string.IsNullOrEmpty(originalItemString))
            {
                int index = bundleItemString.IndexOf(bundleString);
                originalItemString = (index < 0)
                    ? bundleItemString
                    : bundleItemString.Remove(index, bundleString.Length);
            }

            var originalItem = StatsManager.instance.itemDictionary[originalItemString];

            if ( !originalItem )
            {
                Logger.LogError($"--- Didn't find {originalItemString}! Make sure itemAssetName of bundle Item and bundle Prefab is {originalItemString + bundleString}");
                return;
            }

            if (itemBundleInfo.ContainsKey(originalItemString) )
            {
                Logger.LogWarning($"--- bundleStringPairs {originalItemString} already has an entry {itemBundleInfo[originalItemString]}, we are overriding something!");
            }

            Logger.LogInfo($"--- Adding bundleStringPairs {originalItemString}, {item.itemAssetName}");

            Utilities.FixAudioMixerGroups(item.prefab);
            StatsManager.instance.AddItem( item );

            itemBundleInfo[originalItemString] = new BundleShopInfo {
                bundleItem = item,
                config_maxInShop = Config.Bind("Bundles: Item", $"{originalItem.itemName}: Max", -1, new ConfigDescription("Overrides the Item Type entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 10))),
                config_chanceInShop = Config.Bind("Bundles: Item", $"{originalItem.itemName}: Chance", -1, new ConfigDescription("Overrides the Item Type entry if not set to -1. Default: -1", new AcceptableValueRange<int>(-1, 100)))
            };
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