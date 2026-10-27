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
            Present(null);
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
