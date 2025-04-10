using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ItemBundles
{
    public class MoreUpgradesCompat_Bundle : MonoBehaviour
    {
        private ItemToggle itemToggle;
        public string upgradeItemName;

        private void Start()
        {
            itemToggle = GetComponent<ItemToggle>();
        }

        public void FixMaterial()
        {
            var testMat = MoreUpgradesCompat.GetBoxMat(upgradeItemName);
            if (testMat != null) 
            {
                var meshFilters = GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshF in meshFilters)
                {
                    var meshR = meshF.GetComponent<MeshRenderer>();
                    if (meshR != null)
                    {;
                        if (meshR.materials[0].name.Contains("upgrade"))
                        {
                            Material[] newMaterials = new Material[2] { testMat, meshR.materials[1] };
                            meshR.materials = newMaterials;
                        }
                    }
                }
            }
        }

        public void FixLight()
        {
            var light = gameObject.transform.Find("Light - Small Lamp").GetComponent<Light>();
            var light2 = MoreUpgradesCompat.GetLight(upgradeItemName);
            if  (light != null && light2 != null)
            {

                light.color = light2.color;
            }
        }


        public void Upgrade()
        {
            var players = SemiFunc.PlayerGetAll();

            foreach (var player in players)
            {
                var steamId = SemiFunc.PlayerGetSteamID(player);
                MoreUpgradesCompat.CallUpgrade(upgradeItemName, steamId, 1);
            }
        }
    }
}
