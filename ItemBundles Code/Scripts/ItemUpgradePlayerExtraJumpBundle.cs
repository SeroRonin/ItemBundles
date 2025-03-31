using UnityEngine;

namespace ItemBundles
{
    public class ItemUpgradePlayerExtraJumpBundle : MonoBehaviour
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
                PunManager.instance.UpgradePlayerExtraJump(SemiFunc.PlayerGetSteamID(player));
            }
        }
    }
}