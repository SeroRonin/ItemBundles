using UnityEngine;

namespace ItemBundles
{
    public class ItemUpgradeMapPlayerCountBundle : MonoBehaviour
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
                PunManager.instance.UpgradeMapPlayerCount(SemiFunc.PlayerGetSteamID(player));
            }
        }
    }
}
