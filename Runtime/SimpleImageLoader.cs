
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace VRVoyage.SimpleScripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SimpleImageLoader : UdonSharpBehaviour
    {
        
        public VRCUrl[] urls;
        public Material receivingMaterial;
        public Transform receivingPanel;
        public bool rescalePanel = true;
        public TMPro.TextMeshPro errorOutput;
        public Texture2D errorTexture;

        const float minimumWaitTime = 5; /* seconds */
        public float pictureRefreshTime = 6; /* seconds */

        int urlIndex = 0;

        [UdonSynced]
        [HideInInspector]
        public int syncedUrlIndex = -1;
        public bool synchronise = false;

        VRCImageDownloader downloader;
        IUdonEventReceiver imageDownloadHandler;
        TextureInfo info;

        Vector3 scale = Vector3.one;
        float panelScaleX = 1;

        bool loggingErrors = false;
        /* So 2023 ! */
        IVRCImageDownload lastError;

        VRCPlayerApi localPlayer;
        GameObject thisGameObject;

        void OnEnable()
        {

            localPlayer = Networking.LocalPlayer;

            if (localPlayer == null)
            {
                Debug.LogError($"[{name}] [{this.GetType().Name}] VRChat is broken. Disabling.");
                enabled = false;
                return;
            }

            thisGameObject = gameObject;

            if (thisGameObject == null)
            {
                Debug.LogError($"[{name}] [{this.GetType().Name}] Unity is broken. Disabling.");
                enabled = false;
                return;
            }

            if (urls == null || urls.Length == 0)
            {
                Debug.LogError($"[{name}] [{this.GetType().Name}] No URLS set. Disabling.");
                enabled = false;
                return;
            }

            if (receivingMaterial == null)
            {

                Debug.LogError($"[{name}] [{this.GetType().Name}] Material not set. Disabling.");
                enabled = false;
                return;
            }

            if (errorTexture == null)
            {

                Debug.LogError($"[{name}] [{this.GetType().Name}] Forgot to set the error texture ? Disabling.");
                enabled = false;
                return;
            }

            if (rescalePanel & (receivingPanel == null))
            {

                Debug.LogError($"[{name}] [{this.GetType().Name}] You asked to rescale the display panel, but forgot to set it. Disabling.");
                enabled = false;
                return;
            }

            if (pictureRefreshTime < minimumWaitTime)
            {
                Debug.LogWarning($"[{name}] [{this.GetType().Name}] Wait time too low. Reset to {minimumWaitTime}");
                pictureRefreshTime = minimumWaitTime;
                return;
            }

            downloader = new VRCImageDownloader();
            loggingErrors = (errorOutput != null);

            info = new TextureInfo();
            /* Don't repeat the bottom on the top, the left on the right, ... */
            info.WrapModeU = TextureWrapMode.Clamp;
            info.WrapModeV = TextureWrapMode.Clamp;
            if (rescalePanel)
            {
                scale = receivingPanel.localScale;
                panelScaleX = scale.x;
            }

            /* We only keep the component Enabled if it's Synchronised.
             * Synchro doesn't work on 'Disabled' objects
             */
            enabled = synchronise;

            imageDownloadHandler = (IUdonEventReceiver)this;

            if (ShouldHandleDownload())
            {
                Synchronise();
                DownloadFromCurrentURL();
            }
            
            if (loggingErrors)
            {
                /* Ugh... */
                CheckForErrors();
            }

            
        }

        bool ShouldHandleDownload()
        {
            return (!synchronise || (Networking.GetOwner(thisGameObject) == localPlayer));
        }

        void Synchronise()
        {
            if (!synchronise) return;

            GameObject thisObject = gameObject;
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (Networking.GetOwner(thisObject) == localPlayer)
            {
                syncedUrlIndex = urlIndex;
                RequestSerialization();
            }
        }

        public override void OnPostSerialization(SerializationResult result)
        {
            syncedUrlIndex = -1;
        }

        public override void OnDeserialization()
        {
            Debug.Log($"<color=yellow>DESERIALIZE syncedUrlIndex = {syncedUrlIndex} !</color>");
            if (syncedUrlIndex == -1) return;

            urlIndex = syncedUrlIndex;
            downloader.DownloadImage(
                CurrentURL(),
                receivingMaterial,
                imageDownloadHandler,
                info);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            /* So...
             * When a player quit and hands you the ownership of the object,
             * OnOwnershipTransferred isn't called.
             * Because... VRChat !
             */
            if ((synchronise) & (localPlayer.isMaster))
            {
                if (Networking.GetOwner(thisGameObject) != localPlayer)
                {
                    Networking.SetOwner(localPlayer, thisGameObject);
                }
                else
                {
                    OnOwnershipTransferred(localPlayer);
                }
            }
        }

        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {

            return true;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if ((!synchronise) | (player != localPlayer)) return;
            DownloadFromNextURL();
        }

        #region Entrypoints
        /* Alright, so, right now, OnImageLoadError is called from a thread,
         * that cannot access Unity UI.
         *
         * So in order to show any error, I need to POLL on an error object
         * and use it if it is set !
         * 
         * I'm basically polling on the equivalent of 'errno' in 2023 because
         * some people thought it would be a good idea to call errors handlers
         * from a thread !
         */
        public void CheckForErrors()
        {
            if (lastError != null)
            {
                ShowError();
            }

            lastError = null;
            SendCustomEventDelayedSeconds("CheckForErrors", 2);
        }

        public void DownloadFromNextURL()
        {
            urlIndex += 1;
            Synchronise();
            DownloadFromCurrentURL();
        }



        #endregion

        #region Handle Downloads
        VRCUrl CurrentURL()
        {
            int nUrls = urls.Length;
            /* Handle +1 / -1 gracefully and avoid OOB errors */
            /* nUrls == 0 is handled in onEnable */
            urlIndex = urlIndex < nUrls ? urlIndex : 0;
            urlIndex = urlIndex >= 0 ? urlIndex : nUrls - 1;
            return urls[urlIndex];
        }

        void ScheduleNextDownload()
        {
            if (ShouldHandleDownload())
            {
                SendCustomEventDelayedSeconds(
                    "DownloadFromNextURL",
                    pictureRefreshTime,
                    VRC.Udon.Common.Enums.EventTiming.Update);
            }

        }

        void DownloadFromCurrentURL()
        {
            if (!ShouldHandleDownload()) return;
            var url = CurrentURL();
            if (url == null)
            {
                ScheduleNextDownload();
                return;
            }

            downloader.DownloadImage(
                CurrentURL(),
                receivingMaterial,
                imageDownloadHandler,
                info);
        }
        #endregion

        #region Success / Fail
        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            if (result.State == VRCImageDownloadState.Complete)
            {
                var image = result.Result;
                RescalePanelWidth(((float)image.width / (float)image.height));
                EnableErrorPanel(false);
                ScheduleNextDownload();
            }
        }

        public override void OnImageLoadError(IVRCImageDownload result)
        {
            if (result.State == VRCImageDownloadState.Error)
            {
                lastError = result;
                ScheduleNextDownload();
            }
        }

        void RescalePanelWidth(float ratio)
        {
            if (!rescalePanel) return;
            scale.x = panelScaleX * ratio;
            receivingPanel.localScale = scale;
        }

        void EnableErrorPanel(bool state)
        {
            if (!loggingErrors) return;
            errorOutput.gameObject.SetActive(state);
        }

        void ShowError()
        {
            if (loggingErrors == false) return;
            if (lastError == null) return;
            Debug.Log($"<color=orange>Show Error : I am the master : {localPlayer.isMaster}");

            errorOutput.gameObject.SetActive(true);
            errorOutput.text = $"Error {lastError.Error}\n{lastError.ErrorMessage}";

            var material = lastError.Material;
            /* Not really trusting VRC objects ... */
            if (material != null)
            {
                material.SetTexture("_MainTex", errorTexture);
            }

            RescalePanelWidth(1);
        }
        #endregion


    }

}
