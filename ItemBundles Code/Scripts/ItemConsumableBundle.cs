using Photon.Pun;
using Steamworks.Ugc;
using UnityEngine;

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

                impactDetector.DestroyObject(effects: false);
                used = true;
            }
        }

        public void SpawnItems()
        {
            //TODO: Add velocity to items
            //TODO: Adjust item spacing dyanmically
            var playerCount = SemiFunc.PlayerGetAll().Count;
            if (!SemiFunc.IsMultiplayer())
            {
                for (int i = 0; i < (playerCount + ItemBundles.Instance.config_debugFakePlayers.Value); i++)
                {
                    Vector3 vector = new Vector3(0f, 0.2f * (float)i, 0f);
                    Object.Instantiate(itemPrefab, base.transform.position + vector, Quaternion.identity);
                }
            }
            else if (SemiFunc.IsMasterClient())
            {
                for (int j = 0; j < (playerCount + ItemBundles.Instance.config_debugFakePlayers.Value); j++)
                {
                    Vector3 vector2 = new Vector3(0f, 0.2f * (float)j, 0f);
                    GameObject obj = PhotonNetwork.Instantiate("Items/" + itemPrefab.name, base.transform.position + vector2, Quaternion.identity, 0);
                }
            }
            //particleScriptExplosion.Spawn(base.transform.position, 0.8f, 50, 100, 4f, onlyParticleEffect: false, disableSound: true);
            //soundExplosion.Play(base.transform.position);
            //soundExplosionGlobal.Play(base.transform.position);
        }
    }
}
