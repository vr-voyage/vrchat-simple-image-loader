
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace VRVoyage.SimpleScripts
{
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
        VRCImageDownloader downloader;
        TextureInfo info;

        Vector3 scale = Vector3.one;
        float panelScaleX = 1;

        bool loggingErrors = false;
        /* So 2023 ! */
        IVRCImageDownload lastError;

        void OnEnable()
        {
            downloader = new VRCImageDownloader();
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

            DownloadFromCurrentURL();

            if (loggingErrors)
            {
                /* Ugh... */
                CheckForErrors();
            }
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
            SendCustomEventDelayedSeconds(
                "DownloadFromNextURL",
                pictureRefreshTime,
                VRC.Udon.Common.Enums.EventTiming.Update);
        }

        void DownloadFromCurrentURL()
        {
            var url = CurrentURL();
            if (url == null)
            {
                ScheduleNextDownload();
                return;
            }

            downloader.DownloadImage(
                CurrentURL(),
                receivingMaterial,
                (IUdonEventReceiver)this,
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
