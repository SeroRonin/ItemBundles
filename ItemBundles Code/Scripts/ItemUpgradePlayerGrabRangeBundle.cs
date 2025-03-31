using UnityEngine;


namespace ItemBundles
{
    public class ItemUpgradePlayerGrabRangeBundle : MonoBehaviour
    {
        private ItemToggle itemToggle;

        private void Start()
        {
            itemToggle = GetComponent<ItemToggle>();
        }

        public void Upgrade()
        {
            var players = SemiFunc.PlayerGetAll();

            foreach (var player in players)
            {
                PunManager.instance.UpgradePlayerGrabRange(SemiFunc.PlayerGetSteamID(player));
            }
        }
    }
}
