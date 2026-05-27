using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using REPOLib.Modules;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ItemBundles
{
    [BepInPlugin("SeroRonin.ItemBundles", "ItemBundles", "1.5.1")]
    [BepInDependency(REPOLib.MyPluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("nickklmao.repoconfig", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("bulletbot.moreupgrades", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("bulletbot.vanillaupgrades", BepInDependency.DependencyFlags.SoftDependency)]
    public class ItemBundles : BaseUnityPlugin
    {
        public static ItemBundles Instance { get; private set; } = null!;

        internal Harmony? Harmony { get; set; }

        public AssetBundle assetBundle;

        public Dictionary<string, Item> itemDictionaryShop = new Dictionary<string, Item>();
        public Dictionary<string, Item> itemDictionaryShopBlacklist = new Dictionary<string, Item>();
        public Dictionary<SemiFunc.itemType, BundleShopInfo> itemTypeBundleInfos = new Dictionary<SemiFunc.itemType, BundleShopInfo>();
        public Dictionary<string, BundleShopInfo> itemBundleInfos = new Dictionary<string, BundleShopInfo>();
        public Dictionary<Item, GameObject> generatedBundles = new Dictionary<Item, GameObject>();

        public ConfigEntry<bool> config_disableBundlesSP { get; private set; }
        public ConfigEntry<int> config_chanceBundlesInShop { get; private set; }
        public ConfigEntry<int> config_maxBundlesInShop { get; private set; }
        public ConfigEntry<int> config_minPerBundle { get; private set; }
        public ConfigEntry<float> config_priceMultiplier { get; private set; }
        public ConfigEntry<int> config_debugFakePlayers { get; private set; }
        public ConfigEntry<bool> config_debugLogging { get; private set; }

        //public List<Item> allUpgradesVanilla = new List<Item>();
        //public List<Item> allUpgradesREPOLib = new List<Item>();
        //public List<Item> allUpgrades = new List<Item>();
        public Dictionary<Item, string> upgradeParentModDict = new Dictionary<Item, string>();
        public List<Mesh> upgradeBundleMeshes = new List<Mesh>();

        public bool mainMenuReached { get; set; }

        public GameObject templateUpgradeBundlePrefab { get; set; }

        static public int globalSpawnBudget;

        public class BundleShopInfo
        {
            public Item bundleItem;
            public int chanceInShop
            {
                get
                {
                    return config_chanceInShop.Value;
                }
            }
            /// <summary>
            /// Used to track how many are in current shop list
            /// Resets every shop cycle
            /// </summary>
            public int spawnBudget;
            public ConfigEntry<int> config_chanceInShop;
            public ConfigEntry<int> config_maxInShop;
            public ConfigEntry<int> config_minPerBundle;
            public ConfigEntry<float> config_priceMultiplier;
        }

        private void Awake()
        {
            if (Instance) return;

            Instance = this;
            DebugLogger.Init(base.Logger);
            
            string pluginFolderPath = Path.GetDirectoryName(Info.Location);
            string assetBundleFilePath = Path.Combine(pluginFolderPath, "itembundles");
            assetBundle = AssetBundle.LoadFromFile(assetBundleFilePath);

            this.gameObject.transform.parent = null;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            CreateConfigs();

            Patch();

            // Create singleton GO to store each generated bundle 'prefab'
            GameObject manager = new GameObject("Bundle Manager");
            manager.AddComponent<BundleManager>();
            manager.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(manager);

            // Disabled until further testing can be done
            // MoreUpgrades and VanillaUpgrades have been overhauled since REPOLib 3.0
            /*
            if ( MoreUpgradesCompat.enabled )
            {
                MoreUpgradesCompat.InitCompat();
                DebugLogger.LogInfo($"MoreUpgradesCompat has loaded!", true);
            }
            if (VanillaUpgradesCompat.enabled)
            {
                VanillaUpgradesCompat.InitCompat();
                DebugLogger.LogInfo($"VanillaUpgradesCompat has loaded!", true);
            }
            */

            REPOLib.BundleLoader.OnAllBundlesLoaded += RegisterItemBundles;

            DebugLogger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
        }


        public void RegisterItemBundles()
        {
            if (!assetBundle)
            {
                DebugLogger.LogError($"Assetbundle \"itembundles\" not found! Please make sure that it exists in the same folder as the mod DLL");
                DebugLogger.LogError($"ItemBundles has run into a fatal error! The mod will not work correctly and may cause issues elsewhere!");
                return;
            }

            RegisterBundleItemRepoLib("Item Health Pack Small Bundle");
            RegisterBundleItemRepoLib("Item Health Pack Medium Bundle");
            RegisterBundleItemRepoLib("Item Health Pack Large Bundle");

            RegisterBundleItemRepoLib("Item Grenade Explosive Bundle");
            RegisterBundleItemRepoLib("Item Grenade Shockwave Bundle");
            RegisterBundleItemRepoLib("Item Grenade Stun Bundle");

            RegisterBundleItemRepoLib("Item Mine Explosive Bundle");
            RegisterBundleItemRepoLib("Item Mine Shockwave Bundle");
            RegisterBundleItemRepoLib("Item Mine Stun Bundle");

            // Start dynamic upgrade bundle prefab generation here
            var templateBundleItem = assetBundle.LoadAsset<Item>("Item Upgrade Bundle Template");
            templateUpgradeBundlePrefab = assetBundle.LoadAsset<GameObject>("Item Upgrade Bundle Template");

            if (!templateBundleItem || !templateUpgradeBundlePrefab)
            {
                DebugLogger.LogError($"Template bundle item or prefab was null! Unable to generate upgrade bundles");
                DebugLogger.LogError($"ItemBundles has run into a fatal error! The mod will not work correctly and may cause issues elsewhere!");
                return;
            }

            List <Item> allUpgradesVanilla = Resources.LoadAll<Item>("items/items").Where(item => item.itemType == SemiFunc.itemType.item_upgrade).ToList();
            foreach (var item in allUpgradesVanilla)
            {
                upgradeParentModDict.Add(item, "Vanilla");
            }

            foreach (REPOLib.Modules.PlayerUpgrade upgradeEntry in REPOLib.Modules.Upgrades.PlayerUpgrades)
            {
                var upgradeItem = upgradeEntry.Item;
                if (upgradeItem == null || upgradeItem?.prefab.Prefab == null) continue;

                upgradeParentModDict.Add(upgradeItem, "REPOLib");
            }
            
            if (StatsManager.instance.itemDictionary.Count == 0)
            {
                Traverse.Create(StatsManager.instance).Method("LoadItemsFromFolder").GetValue();
            }

            foreach (var itemKV in StatsManager.instance.itemDictionary.Where(itemKV => itemKV.Value.itemType == SemiFunc.itemType.item_upgrade).ToList())
            {
                Item item = itemKV.Value;

                if (upgradeParentModDict.ContainsKey(item))
                {
                    continue;
                }

                upgradeParentModDict.Add(item, "Modded");
            }

            // Disabled until further testing can be done
            // MoreUpgrades and VanillaUpgrades have been overhauled since REPOLib 3.0
            /*
            if ( MoreUpgradesCompat.enabled )
            {
                allUpgrades.AddRange(MoreUpgradesCompat.allUpgrades);
            }
            */
            /*
            foreach (Item upgradeItem in allUpgradesVanilla)
            {
                DebugLogger.LogInfo($"allUpgradesVanilla: {upgradeItem.itemName}", true);
            }

            foreach (Item upgradeItem in allUpgradesREPOLib)
            {
                DebugLogger.LogInfo($"allUpgradesREPOLib: {upgradeItem.itemName}", true);
            }*/

            foreach (var itemKV in upgradeParentModDict)
            {
                DebugLogger.LogInfo($"|- {itemKV.Value} Upgrade found and registered for bundles: {itemKV.Key.itemName}", true);
            }

            upgradeBundleMeshes = assetBundle.LoadAllAssets<Mesh>().Where(mesh => mesh.name.ToLower().Contains("mesh_bundle_upgrade")).ToList();
            foreach (Item upgradeItem in upgradeParentModDict.Keys)
            {
                // TODO: add support for pre-existing Upgrade Bundles, for custom behaviour or visuals
                GenerateUpgradeBundle(upgradeItem);
            }
        }

        public void GenerateUpgradeBundle( Item baseItem )
        {
            DebugLogger.LogInfo($"|-- Generating Upgrade Bundle: {baseItem.name}", true);

            var newBundleItem = ScriptableObject.CreateInstance<Item>();
            var newBundlePrefab = GameObject.Instantiate(templateUpgradeBundlePrefab, new Vector3(0, 100, 0), Quaternion.identity);

            newBundlePrefab.name = baseItem.name + " Bundle";
            newBundleItem.name = baseItem.name + " Bundle";
            newBundleItem.itemName = baseItem.itemName + "s";
            newBundleItem.itemType = BundleHelper.ValidateItemType(baseItem.itemType);
            newBundleItem.emojiIcon = baseItem.emojiIcon;
            newBundleItem.itemVolume = baseItem.itemVolume;
            newBundleItem.itemSecretShopType = baseItem.itemSecretShopType;
            newBundleItem.colorPreset = baseItem.colorPreset;
            newBundleItem.value = baseItem.value;
            newBundleItem.maxAmount = baseItem.maxAmount;
            newBundleItem.maxAmountInShop = baseItem.maxAmountInShop;
            newBundleItem.maxPurchase = baseItem.maxPurchase;
            newBundleItem.maxPurchaseAmount = baseItem.maxPurchaseAmount;
            newBundleItem.spawnRotationOffset = baseItem.spawnRotationOffset;
            newBundleItem.physicalItem = baseItem.physicalItem;

            var newPrefabRefName = baseItem.prefab.prefabName + " Bundle";
            var newBundlePrefabRef = new PrefabRef
            {
                prefabName = newPrefabRefName,
                resourcePath = "Items/" + newPrefabRefName
            };

            newBundleItem.prefab = newBundlePrefabRef;

            var itemComp = newBundlePrefab.GetComponent<ItemAttributes>();
            itemComp.item = newBundleItem;

            var bundleComp = newBundlePrefab.GetComponent<ItemUpgradeBundleGenerated>();
            bundleComp.originalItem = baseItem;

            if (baseItem.name.ToLower().Contains("gold") && GoldItemsCompat.enabled)
            {
                GoldItemsCompat.TryAddGoldMarker(baseItem.prefab.Prefab, ref newBundlePrefab);
            }

            var randMeshIndex = Random.RandomRangeInt(0, upgradeBundleMeshes.Count);
            bundleComp.SetBoxMesh(upgradeBundleMeshes[randMeshIndex]);

            REPOLib.Modules.Items.RegisterItem(itemComp);
            generatedBundles[newBundleItem] = newBundlePrefab;
        }

        public void InitializeItemBundles()
        {
            if (!assetBundle)
            {
                DebugLogger.LogError($"Assetbundle \"itembundles\" not found! Please make sure that it exists in the same folder as the mod DLL");
                DebugLogger.LogError($"ItemBundles has run into a fatal error! The mod will not work correctly and may cause issues elsewhere!");
                return;
            }

            InitializeBundle("Item Health Pack Small Bundle", "Health Packs");
            InitializeBundle("Item Health Pack Medium Bundle", "Health Packs");
            InitializeBundle("Item Health Pack Large Bundle", "Health Packs");

            InitializeBundle("Item Grenade Explosive Bundle", "Weapons");
            InitializeBundle("Item Grenade Shockwave Bundle", "Weapons");
            InitializeBundle("Item Grenade Stun Bundle", "Weapons");

            InitializeBundle("Item Mine Explosive Bundle", "Weapons");
            InitializeBundle("Item Mine Shockwave Bundle", "Weapons");
            InitializeBundle("Item Mine Stun Bundle", "Weapons");

            foreach ( Item item in generatedBundles.Keys )
            {
                // Originally meant to prepend mod name to config section for organization
                // AFAIK not currently possible to determine exact origin mod for REPO items
                var bundleComp = item.prefab.Prefab.GetComponent<ItemUpgradeBundleGenerated>();
                if (bundleComp == null) continue;
                if (bundleComp.originalItem == null) continue;

                string? configSection;
                if (!upgradeParentModDict.TryGetValue(bundleComp.originalItem, out configSection))
                {
                    DebugLogger.LogError($"InitializeItemsBundles(): Base item was not found in upgradeParentModDict! Something is broken, skipping bundle initialization");
                    continue;
                }

                InitializeBundle(item, $"Upgrades ({configSection})");
            }
        }

        public void CreateConfigs()
        {
            //TODO: Re-add max total bundles once I figure out how to not make it stop on first list.
            config_disableBundlesSP = Config.Bind("General", "Disable Bundles in Singleplayer", true, new ConfigDescription("Whether bundles are disabled when doing a singleplayer run"));
            config_chanceBundlesInShop = Config.Bind("General", "Bundle Chance", 20, new ConfigDescription("Percent chance that an item will be replaced with a bundle variant", new AcceptableValueRange<int>(0, 100)));
            config_maxBundlesInShop = Config.Bind("General", "Maximum Bundles In Shop", -1, new ConfigDescription("Maximum number of bundles that can appear of ANY one type. Setting to -1 makes shop ignore this entry", new AcceptableValueRange<int>(-1, 100)));
            config_minPerBundle = Config.Bind("General", "Mininum consumables per bundle", 0, new ConfigDescription("Minimum amount of items in valid bundles. Price still scales. Default: 0", new AcceptableValueRange<int>(0, 100)));
            config_priceMultiplier = Config.Bind("General", "Bundle Price Multiplier", 66.66f, new ConfigDescription("Multiplier of total item costs that bundles have", new AcceptableValueRange<float>(0f, 200f)));

            string overrideDesc = "Has priority over General entry. Ignored if set below 0";
            itemTypeBundleInfos[SemiFunc.itemType.mine] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles by Type", "Mines: Chance", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles by Type", "Mines: Chance", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_minPerBundle = Config.Bind("Bundles by Type", "Mines: Mininum per bundle", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_priceMultiplier = Config.Bind("Bundles by Type", "Mines: Price Multiplier", -1f, new ConfigDescription(overrideDesc, new AcceptableValueRange<float>(-1f, 200f)))
            };
            itemTypeBundleInfos[SemiFunc.itemType.grenade] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles by Type", "Grenades: Chance", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles by Type", "Grenades: Max", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_minPerBundle = Config.Bind("Bundles by Type", "Grenades: Mininum per bundle", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_priceMultiplier = Config.Bind("Bundles by Type", "Grenades: Price Multiplier", -1f, new ConfigDescription(overrideDesc, new AcceptableValueRange<float>(-1f, 200f)))
            };
            itemTypeBundleInfos[SemiFunc.itemType.healthPack] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles by Type", "Health Packs: Chance", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles by Type", "Health Packs: Max", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_priceMultiplier = Config.Bind("Bundles by Type", "Health Packs: Price Multiplier", -1f, new ConfigDescription(overrideDesc, new AcceptableValueRange<float>(-1f, 200f)))
            };
            itemTypeBundleInfos[SemiFunc.itemType.item_upgrade] = new BundleShopInfo
            {
                config_chanceInShop = Config.Bind("Bundles by Type", "Upgrades: Chance", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind("Bundles by Type", "Upgrades: Max", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_minPerBundle = Config.Bind("Bundles by Type", "Upgrades: Mininum per bundle", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_priceMultiplier = Config.Bind("Bundles by Type", "Upgrades: Price Multiplier", -1f, new ConfigDescription(overrideDesc, new AcceptableValueRange<float>(-1f, 200f)))
            };

            config_debugLogging = Config.Bind("Developer", "Debug Logging", false, new ConfigDescription("Enables debug logging", tags: "HideFromREPOConfig"));
            config_debugFakePlayers = Config.Bind("Developer", "Number of Fake Players", 0, new ConfigDescription("Adds fake players to bundle player calculations", new AcceptableValueRange<int>(0, 10), "HideFromREPOConfig"));
        }

        internal void RegisterBundleItemRepoLib(string itemString)
        {
            GameObject item = assetBundle.LoadAsset<GameObject>(itemString);
            ItemAttributes itemAttributes = item.GetComponent<ItemAttributes>();
            if ( !itemAttributes )
            {
                DebugLogger.LogError($"RegisterBundleItemRepoLib() Failed: ItemAttributes for {itemString} was null!");
                return;
            }
            REPOLib.Modules.Items.RegisterItem(itemAttributes);
        }

        internal void InitializeBundle( string bundleItemString, string configSection = "")
        {
            Item bundleItem = assetBundle.LoadAsset<Item>(bundleItemString);
            if (!bundleItem)
            {
                DebugLogger.LogError($"InitializeBundle() Failed: Bundle Item \"{bundleItemString}\" not found!");
                return;
            }

            InitializeBundle(bundleItem, configSection);
        }

        internal void InitializeBundle( Item item, string configSection = "")
        {
            Item bundleItem = item;
            if (!bundleItem)
            {
                DebugLogger.LogError($"InitializeBundle() Failed: Bundle Item was null!");
                return;
            }

            var bundleString = " Bundle";
            if (!item.name.Contains(bundleString))
            {
                DebugLogger.LogError($"InitializeBundle() Failed: Item {item.name} is not a bundle! Add \" Bundle\" to item name (WITH THE SPACE)");
                return;
            }

            var originalItemString = BundleHelper.GetItemStringFromBundle(bundleItem);
            var originalItem = StatsManager.instance.itemDictionary[originalItemString];

            if (!originalItem)
            {
                DebugLogger.LogError($"InitializeBundle() Failed: Didn't find {originalItemString}! Make sure itemAssetName of bundle Item and bundle Prefab is {originalItemString + bundleString}");
                return;
            }

            if (itemBundleInfos.ContainsKey(originalItemString))
            {
                DebugLogger.LogWarning($"InitializeBundle() Warning: bundleStringPairs {originalItemString} already has an entry {itemBundleInfos[originalItemString]}, we are overriding something!");
            }

            itemDictionaryShopBlacklist.Add(bundleItem.prefab.resourcePath, bundleItem);

            // FAILSAFE: Update bundle upgrade prices to match original upgrades
            if (bundleItem.itemType == SemiFunc.itemType.item_upgrade)
            {
                bundleItem.value = ScriptableObject.CreateInstance<Value>();
                bundleItem.value.valueMin = originalItem.value.valueMin;
                bundleItem.value.valueMax = originalItem.value.valueMax;
            }

            string overrideDesc = "Has Priority over Item Type entry. Ignored if set below 0";
            var bundleInfo = new BundleShopInfo
            {
                bundleItem = bundleItem,
                config_chanceInShop = Config.Bind($"{configSection}", $"{originalItem.itemName}: Chance", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 100))),
                config_maxInShop = Config.Bind($"{configSection}", $"{originalItem.itemName}: Max", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10))),
                config_priceMultiplier = Config.Bind($"{configSection}", $"{originalItem.itemName}: Price Multiplier", -1f, new ConfigDescription(overrideDesc, new AcceptableValueRange<float>(-1f, 200f)))
            };

            if (bundleItem.itemType == SemiFunc.itemType.grenade || bundleItem.itemType == SemiFunc.itemType.mine || bundleItem.itemType == SemiFunc.itemType.item_upgrade)
            {
                bundleInfo.config_minPerBundle = Config.Bind($"{configSection}", $"{originalItem.itemName}: Mininum per bundle", -1, new ConfigDescription(overrideDesc, new AcceptableValueRange<int>(-1, 10)));
            }

            itemBundleInfos[originalItemString] = bundleInfo;
            DebugLogger.LogInfo($"InitializeBundle(): Created BundleShopInfo [{originalItemString} | {bundleItem.prefab.prefabName}]", true);

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
            // Hacky code to bypass missing reference of RunManager.instance.levelSplashScreen or SemiFunc.SplashScreenLevel()
            // Necessary to initialize dynamically generated bundles without errors
            if ( mainMenuReached || !RunManager.instance ) return;

            mainMenuReached = SemiFunc.IsMainMenu();
        }
    }
}