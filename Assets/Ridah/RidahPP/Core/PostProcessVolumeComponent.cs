using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ridah.URPPostProcessing
{
    public enum InjectionPoint
    {
        AfterOpaqueAndSky = 400,
        BeforePostProcess = 550,
        AfterPostProcess = 600,
    }

    public abstract class PostProcessVolumeComponent : VolumeComponent, IPostProcessComponent
    {
        protected PostProcessVolumeComponent()
        {
            string className = GetType().ToString();// Ridah.URPPostProcessing
            int dotIndex = className.LastIndexOf(".") + 1;
            displayName = className.Substring(dotIndex);
        }

        public virtual InjectionPoint InjectionPoint { get; } = InjectionPoint.BeforePostProcess;

        public virtual bool visibleInSceneView { get; } = true;

        public abstract bool IsActive();

        public abstract void Initialize();

        public abstract void Render(CommandBuffer cb, CameraData camera, RenderTargetIdentifier source, RenderTargetIdentifier dest);

        internal bool isInitialized = false;

        internal void SetupIfNeed()
        {
            if (isInitialized == true)
                return;

            Initialize();
            isInitialized = true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            isInitialized = false;
            CleanUp();
        }

        protected virtual void CleanUp()
        {

        }

        public bool IsTileCompatible() => false;
    }
}