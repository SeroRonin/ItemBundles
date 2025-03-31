using UnityEngine;

namespace ItemBundles
{
    public class ItemUpgradePlayerSprintSpeedBundle : MonoBehaviour
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
                PunManager.instance.UpgradePlayerSprintSpeed(SemiFunc.PlayerGetSteamID(player));
            }
        }
    }
}