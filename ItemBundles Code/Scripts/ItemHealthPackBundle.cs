using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

namespace ItemBundles
{
    public class ItemHealthPackBundle : MonoBehaviour
    {
        public int healAmount;
        private int healingBank;

        private ItemToggle itemToggle;

        private ItemEquippable itemEquippable;

        private ItemAttributes itemAttributes;

        private PhotonView photonView;

        private PhysGrabObject physGrabObject;
        private List<PlayerAvatar> playersToHeal;

        [Space]
        public ParticleSystem[] particles;

        public ParticleSystem[] rejectParticles;

        [Space]
        public PropLight propLight;

        public AnimationCurve lightIntensityCurve;

        private float lightIntensityLerp;

        public MeshRenderer mesh;

        private Material material;

        private Color materialEmissionOriginal;

        private int materialPropertyEmission = Shader.PropertyToID("_EmissionColor");

        [Space]
        public Sound soundUse;

        public Sound soundReject;

        private bool used;

        private void Start()
        {
            itemToggle = GetComponent<ItemToggle>();
            itemEquippable = GetComponent<ItemEquippable>();
            itemAttributes = GetComponent<ItemAttributes>();
            photonView = GetComponent<PhotonView>();
            physGrabObject = GetComponent<PhysGrabObject>();
            playersToHeal = new List<PlayerAvatar>();
            material = mesh.material;
            materialEmissionOriginal = material.GetColor(materialPropertyEmission);
        }

        private void Update()
        {
            if (SemiFunc.RunIsShop())
            {
                return;
            }
            LightLogic();
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !itemToggle.toggleState || used)
            {
                return;
            }

            playersToHeal.Clear();
            foreach ( var player in SemiFunc.PlayerGetAll() )
            {
                // Only factor living players with missing health into later healing calculations
                if (player.playerHealth.health < player.playerHealth.maxHealth && player.playerHealth.health > 0 )
                {
                    healingBank += healAmount;
                    playersToHeal.Add( player );
                }
            }

            healingBank += healAmount * ItemBundles.Instance.config_debugFakePlayers.Value;

            if ( playersToHeal.Count <= 0 )
            {
                if (SemiFunc.IsMultiplayer())
                {
                    photonView.RPC("RejectRPC", RpcTarget.All);
                }
                else
                {
                    RejectRPC();
                }

                itemToggle.ToggleItem(toggle: false);
                physGrabObject.rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
                physGrabObject.rb.AddTorque(-physGrabObject.transform.right * 0.05f, ForceMode.Impulse);
                return;
            }

            Dictionary<PlayerAvatar, int> playerHealthToHeal = new Dictionary<PlayerAvatar, int>();
            foreach (PlayerAvatar player in playersToHeal)
            {
                playerHealthToHeal[player] = 0;
            }

            //Calculate heal for each person, if excess, spread between others
            while ( healingBank > 0 && playersToHeal.Count > 0 )
            {
                List<PlayerAvatar> playersToHealTemp = new List<PlayerAvatar>(playersToHeal);
                foreach (PlayerAvatar player in playersToHeal)
                {
                    //Calculate missing health, factor in stored healing values
                    var missing = player.playerHealth.maxHealth - (player.playerHealth.health + playerHealthToHeal[player] );

                    //See if we have less healing stored than healing of item
                    var targetHealAmount = Mathf.Min(healingBank, healAmount);

                    //Player is missing less health than total healing expected
                    //They are going to heal to full, remove them from list
                    var finalHealAmount = Mathf.Min(missing, targetHealAmount);
                    if (missing <= targetHealAmount)
                    {
                        //don't want to modify list during loop, modify a temp copy
                        playersToHealTemp.Remove(player);
                    }

                    //Remove expected healing from bank and store new value
                    healingBank = Mathf.Max(healingBank - finalHealAmount, 0);
                    playerHealthToHeal[player] += finalHealAmount;
                }

                playersToHeal = playersToHealTemp;
            }

            //Apply final healing
            foreach (KeyValuePair<PlayerAvatar, int> healEntry in playerHealthToHeal)
            {
                ItemBundlesLogger.LogInfo($"{healEntry.Key.playerName} missing {healEntry.Key.playerHealth.maxHealth - healEntry.Key.playerHealth.health} health, healing for {healAmount} base + {healEntry.Value - healAmount} excess!", true);
                healEntry.Key.playerHealth.HealOther(healEntry.Value, effect: true);
            }
            _ = StatsManager.instance.itemsPurchased[itemAttributes.item.itemAssetName];
            StatsManager.instance.ItemRemove(itemAttributes.instanceName);

            physGrabObject.impactDetector.destroyDisable = false;
            physGrabObject.impactDetector.indestructibleBreakEffects = true;

            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("UsedRPC", RpcTarget.All);
            }
            else
            {
                UsedRPC();
            }
        }

        private void LightLogic()
        {
            if (used && lightIntensityLerp < 1f)
            {
                lightIntensityLerp += 1f * Time.deltaTime;
                propLight.lightComponent.intensity = lightIntensityCurve.Evaluate(lightIntensityLerp);
                propLight.originalIntensity = propLight.lightComponent.intensity;
                material.SetColor(materialPropertyEmission, Color.Lerp(Color.black, materialEmissionOriginal, lightIntensityCurve.Evaluate(lightIntensityLerp)));
            }
        }

        [PunRPC]
        private void UsedRPC()
        {
            GameDirector.instance.CameraImpact.ShakeDistance(5f, 1f, 6f, base.transform.position, 0.2f);
            itemToggle.ToggleDisable(_disable: true);
            itemAttributes.DisableUI(_disable: true);
            Object.Destroy(itemEquippable);
            ParticleSystem[] array = particles;
            for (int i = 0; i < array.Length; i++)
            {
                array[i].Play();
            }
            soundUse.Play(base.transform.position);
            used = true;
        }

        [PunRPC]
        private void RejectRPC()
        {
            PlayerAvatar playerAvatar = SemiFunc.PlayerAvatarGetFromPhotonID(itemToggle.playerTogglePhotonID);
            if (playerAvatar.isLocal)
            {
                playerAvatar.physGrabber.ReleaseObjectRPC(physGrabEnded: false, 1f);
            }
            ParticleSystem[] array = rejectParticles;
            for (int i = 0; i < array.Length; i++)
            {
                array[i].Play();
            }
            GameDirector.instance.CameraImpact.ShakeDistance(5f, 1f, 6f, base.transform.position, 0.2f);
            soundReject.Play(base.transform.position);
        }

        public void OnDestroy()
        {
            ParticleSystem[] array = particles;
            foreach (ParticleSystem particleSystem in array)
            {
                if ((bool)particleSystem && particleSystem.isPlaying)
                {
                    particleSystem.transform.SetParent(null);
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.stopAction = ParticleSystemStopAction.Destroy;
                }
            }
            array = rejectParticles;
            foreach (ParticleSystem particleSystem2 in array)
            {
                if ((bool)particleSystem2 && particleSystem2.isPlaying)
                {
                    particleSystem2.transform.SetParent(null);
                    ParticleSystem.MainModule main2 = particleSystem2.main;
                    main2.stopAction = ParticleSystemStopAction.Destroy;
                }
            }
        }
    }
}
