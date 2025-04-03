using Photon.Pun;
using Steamworks.Ugc;
using UnityEngine;
using static SemiFunc;

namespace ItemBundles
{
    public class ItemConsumableBundle : MonoBehaviour
    {
        private ItemToggle itemToggle;

        private PhotonView photonView;
        private PhysGrabObjectImpactDetector impactDetector;

        //What item should we spawn?
        public GameObject itemPrefab;
        private bool used;

        private void Start()
        {
            itemToggle = GetComponent<ItemToggle>();
            photonView = GetComponent<PhotonView>();
            impactDetector = GetComponent<PhysGrabObjectImpactDetector>();
        }

        private void Update()
        {
            if (SemiFunc.RunIsShop())
            {
                return;
            }

            if (!SemiFunc.IsMasterClientOrSingleplayer() || !itemToggle.toggleState || used )
            {
                return;
            }

            if ( !used && itemToggle.toggleState )
            {
                SpawnItems();

                StatsManager.instance.ItemRemove(this.GetComponent<ItemAttributes>().instanceName);

                impactDetector.DestroyObject(effects: false);
                used = true;
            }
        }

        public void SpawnItems()
        {
            //TODO: Add velocity to items
            //TODO: Adjust item spacing dyanmically
            var playerCount = SemiFunc.PlayerGetAll().Count;

            var item = GetComponent<ItemAttributes>().item;
            if (item.itemType == itemType.grenade || item.itemType == itemType.mine)
            {
                playerCount = Mathf.Max(playerCount, BundleHelper.GetItemBundleMinItem(BundleHelper.GetItemStringFromBundle(item), item.itemType));
            }

            var randomSpawnOffset = Random.insideUnitSphere * 0.2f;

            if (!SemiFunc.IsMultiplayer())
            {
                for (int i = 0; i < (playerCount + ItemBundles.Instance.config_debugFakePlayers.Value); i++)
                {
                    var obj = Object.Instantiate(itemPrefab, base.transform.position + randomSpawnOffset, Quaternion.identity);
                    StatsManager.instance.ItemPurchase(obj.GetComponent<ItemAttributes>().item.itemAssetName);
                }
            }
            else if (SemiFunc.IsMasterClient())
            {
                for (int j = 0; j < (playerCount + ItemBundles.Instance.config_debugFakePlayers.Value); j++)
                {
                    GameObject obj = PhotonNetwork.Instantiate("Items/" + itemPrefab.name, base.transform.position + randomSpawnOffset, Quaternion.identity, 0);
                    StatsManager.instance.ItemPurchase(obj.GetComponent<ItemAttributes>().item.itemAssetName);
                }
            }

            //particleScriptExplosion.Spawn(base.transform.position, 0.8f, 50, 100, 4f, onlyParticleEffect: false, disableSound: true);
            //soundExplosion.Play(base.transform.position);
            //soundExplosionGlobal.Play(base.transform.position);
        }
    }
}
