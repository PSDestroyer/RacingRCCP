using UnityEngine;

namespace FormulaGame
{
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public sealed class FormulaPlayerAvatar : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.RawImage fallback;
        private UnityEngine.UI.RawImage image;
        private bool fallbackEnabled;

        private void Awake()
        {
            image = GetComponent<UnityEngine.UI.RawImage>();
            fallbackEnabled = fallback != null && fallback.enabled;
        }
        private void OnEnable()
        {
            Texture texture = null;
#if UNITY_SWITCH && !UNITY_EDITOR
            texture = _Assets._PlatformSpeciffics.Switch.NintendoManager.GetProfileImage();
#endif
            Present(texture);
        }
        public void Present(Texture texture)
        {
            image.texture = texture;
            image.enabled = texture != null;
            if (fallback != null) fallback.enabled = texture == null && fallbackEnabled;
        }
        private void OnDisable()
        {
            image.texture = null; image.enabled = false;
            if (fallback != null) fallback.enabled = fallbackEnabled;
        }
    }
}
