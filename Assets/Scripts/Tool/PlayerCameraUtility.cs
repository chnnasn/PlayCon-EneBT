using UnityEngine;
using Cinemachine;

namespace Tools
{
    [System.Serializable]
    public class PlayerCameraUtility
    {
        [field: SerializeField] public CinemachineVirtualCamera virtualCamera { get; private set; }
        [field: SerializeField] public float DefaultHorizontalWaitTime { get; private set; } = 0f;

        [field: SerializeField] public float DefaultHorizontalRecenteringTime { get; private set; } = 0.5f;
        [field: SerializeField] public float InitialHorizontalAxisValue { get; private set; } = 0f;
        [field: SerializeField] public float InitialVerticalAxisValue { get; private set; } = 0f;
        [field: SerializeField] public bool ResetAxisOnInit { get; private set; } = true;

        private CinemachinePOV cinemachinePOV;
        private CinemachineInputProvider inputProvider;

        public void EnableRecentering(float waitTime = -1f, float recenteringTime = -1f)
        {
            if (cinemachinePOV == null) return;
            cinemachinePOV.m_HorizontalRecentering.m_enabled = true;
            cinemachinePOV.m_HorizontalRecentering.m_WaitTime = waitTime < 0f ? DefaultHorizontalWaitTime : waitTime;
            cinemachinePOV.m_HorizontalRecentering.m_RecenteringTime = recenteringTime < 0f ? DefaultHorizontalRecenteringTime : recenteringTime;
        }
        public void DisableRecentering()
        {
            if (cinemachinePOV == null) return;
            cinemachinePOV.m_HorizontalRecentering.m_enabled = false;
            //Debug.Log("水平居中相机关闭！");
        }

        public void Init()
        {
            if (virtualCamera == null) return;
            cinemachinePOV = virtualCamera.GetCinemachineComponent<CinemachinePOV>();
            inputProvider = virtualCamera.GetComponent<CinemachineInputProvider>();
            if (!ResetAxisOnInit || cinemachinePOV == null)
            {
                return;
            }

            cinemachinePOV.m_HorizontalAxis.Value = InitialHorizontalAxisValue;
            cinemachinePOV.m_VerticalAxis.Value = InitialVerticalAxisValue;
        }

        public void SetInputEnabled(bool enabled)
        {
            if (inputProvider == null)
            {
                return;
            }

            inputProvider.enabled = enabled;
        }
    }
}
