
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
        public TMPro.TextMeshPro statusOutput;
        public Texture2D errorTexture;

        const float minimumWaitTime = 5; /* seconds */
        public float pictureRefreshTime = 6; /* seconds */

        int urlIndex = 0;

        [UdonSynced]
        [HideInInspector]
        public int syncedUrlIndex = -1;
        public bool synchronise = false;

        VRCImageDownloader[] downloaders;
        int currentDownloader = 0;
        IUdonEventReceiver imageDownloadHandler;
        /* We need to ensure that the current download is finished
         * before triggering the next one, else it will be queued
         * and the display order will start to be messed up.
         */
        IVRCImageDownload currentDownload;

        TextureInfo info;

        Vector3 scale = Vector3.one;
        float panelScaleX = 1;

        bool loggingErrors = false;
        /* So 2023 ! */
        IVRCImageDownload lastError;
        bool errorCheckLoopOn = false;

        VRCPlayerApi localPlayer;
        GameObject thisGameObject;

        [HideInInspector]
        [UdonSynced]
        public bool slideshowOn = false;

        public bool startOnEnabled = true;

        public GameObject playButton;
        public GameObject stopButton;


        #region UI

        void ButtonShown(GameObject button, bool state)
        {
            if (button != null)
            {
                button.SetActive(state);
            }
        }

        void ShowText(TMPro.TextMeshPro textOutput, string message)
        {
            if ((textOutput != null) | (message != null)) return;

            textOutput.text = message;
        }

        void RefreshButtons()
        {
            ButtonShown(playButton, (slideshowOn));
            ButtonShown(stopButton, (!slideshowOn));
        }
        #endregion

        bool IOwnThisObject()
        {
            return localPlayer == Networking.GetOwner(thisGameObject);
        }

        void Awake()
        {
            if (rescalePanel & (receivingPanel != null))
            {
                scale = receivingPanel.localScale;
                panelScaleX = scale.x;
            }
        }

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

            downloaders = new VRCImageDownloader[2];
            loggingErrors = (errorOutput != null);
            imageDownloadHandler = (IUdonEventReceiver)this;
            info = new TextureInfo();
            /* Don't repeat the bottom on the top, the left on the right, ... */
            info.WrapModeU = TextureWrapMode.Clamp;
            info.WrapModeV = TextureWrapMode.Clamp;
            ButtonShown(playButton, ShouldHandleDownload());
            ButtonShown(stopButton, false);

            if (startOnEnabled)
            {
                SlideshowStart();
            }
            
        }

        bool ShouldHandleDownload()
        {
            return (!synchronise || IOwnThisObject());
        }

        #region VRChat Sync
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (!ShouldHandleDownload()) return;

            RefreshButtons();
            if (slideshowOn)
            {
                DownloadFromNextURL();
            }
        }

        void Synchronise()
        {
            if (!synchronise) return;

            if (IOwnThisObject())
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
            if (syncedUrlIndex < 0) return;
 
            urlIndex = syncedUrlIndex;
            DownloadCurrent();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            /* So...
             * When a player quits and hands you the ownership of the object,
             * OnOwnershipTransferred isn't called.
             * Because... VRChat !
             */
            if ((synchronise) & (localPlayer.isMaster))
            {
                if (!IOwnThisObject())
                {
                    Networking.SetOwner(localPlayer, thisGameObject);
                }
                else
                {
                    OnOwnershipTransferred(localPlayer);
                }
            }
        }


        #endregion

        void DisposeAllDownloaders()
        {
            int nDownloaders = downloaders.Length;
            for (int i = 0; i < nDownloaders; i++)
            {
                var downloader = downloaders[i];
                if (downloader == null) continue;
                downloader.Dispose();
                downloaders[i] = null;
            }
        }

        VRCImageDownloader GetNextDownloader()
        {
            int nextIndex = (currentDownloader + 1) & 1;
            /* Get rid of the old one if available */
            var staleDownloader = downloaders[nextIndex];
            if (staleDownloader != null) staleDownloader.Dispose();

            var newDownloader = new VRCImageDownloader();
            downloaders[nextIndex] = newDownloader;
            currentDownloader = nextIndex;

            return newDownloader;
        }

        void DownloadCurrent()
        {
            var downloader = GetNextDownloader();
            
            var currentUrl = CurrentURL();
            currentDownload = downloader.DownloadImage(
                currentUrl,
                receivingMaterial,
                imageDownloadHandler,
                info);
            ShowText(statusOutput, "Downloading from " + currentUrl.ToString());
            //Debug.Log("<color=cyan>currentDownload set !</color>");
        }





        #region Entrypoints

        public void DownloadFromNextURL()
        {
            if (currentDownload.State == VRCImageDownloadState.Pending)
            {
                SendCustomEventDelayedSeconds(nameof(DownloadFromNextURL), 1);
                return;
            }
            urlIndex += 1;
            if (!slideshowOn) return;
            Synchronise();
            DownloadFromCurrentURL();
        }

        public void SlideshowStart()
        {
            if (!ShouldHandleDownload()) return;
            if (slideshowOn) return;

            ButtonShown(playButton, false);
            slideshowOn = true;

            if (ShouldHandleDownload())
            {
                Synchronise();
                DownloadFromCurrentURL();
            }

            ButtonShown(stopButton, true);
        }

        public void SlideshowStop()
        {
            //Debug.Log("<color=yellow>Stopping</color>");
            if (!slideshowOn) return;
            slideshowOn = false;
            Synchronise();

            RefreshButtons();            
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
            if (slideshowOn & ShouldHandleDownload())
            {
                SendCustomEventDelayedSeconds(
                    nameof(DownloadFromNextURL),
                    pictureRefreshTime,
                    VRC.Udon.Common.Enums.EventTiming.Update);
            }
        }

        void DownloadFromCurrentURL()
        {
            if (!ShouldHandleDownload()) return;

            /* If the current URL is null, it means that
             * the user forgot to setup the component
             * correctly.
             * This can happen. Let's just skip to the next
             * Download.
             */
            if (CurrentURL() == null)
            {
                ScheduleNextDownload();
                return;
            }

            DownloadCurrent();
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
            ShowError();
            ScheduleNextDownload();
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

        void OnDisable()
        {
            SlideshowStop();
            RescalePanelWidth(1);
            DisposeAllDownloaders();
        }
    }

}
