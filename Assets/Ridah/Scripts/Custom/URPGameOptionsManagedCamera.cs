using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameOptionsUtility.URP
{
    [RequireComponent(typeof(UniversalAdditionalCameraData))]
    public class URPGameOptionsManagedCamera : MonoBehaviour
    {
        private void OnEnable()
        {
            URPCameraOption.AddCamera(GetComponent<UniversalAdditionalCameraData>());
        }

        private void OnDisable()
        {
            URPCameraOption.RemoveCamera(GetComponent<UniversalAdditionalCameraData>());
        }
    }
}