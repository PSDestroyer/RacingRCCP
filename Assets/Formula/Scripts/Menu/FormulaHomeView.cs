using System;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaHomeView : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("continueCareer")]
        [SerializeField] private UnityEngine.UI.Button career;
        [SerializeField] private UnityEngine.UI.Button garage, quick, profile, settings;
        [SerializeField] private UnityEngine.UI.Button quit;
        public void Bind(Action careerAction, Action garageAction, Action quickAction, Action profileAction, Action settingsAction)
        {
            career.onClick.AddListener(() => careerAction()); garage.onClick.AddListener(() => garageAction());
            quick.onClick.AddListener(() => quickAction());
            profile.onClick.AddListener(() => profileAction()); settings.onClick.AddListener(() => settingsAction());
            if (quit != null)
            {
#if UNITY_SWITCH && !UNITY_EDITOR
                quit.gameObject.SetActive(false);
#else
                quit.onClick.AddListener(() => Application.Quit());
#endif
            }
            WireNavigation();
        }
        public void WireNavigation()
        {
            var quitAction = quit != null && quit.gameObject.activeSelf ? quit : null;
            Link(career, quitAction != null ? quitAction : settings, garage, null, null);
            Link(garage, career, quick, null, null);
            Link(quick, garage, profile, null, null);
            Link(profile, quick, quitAction != null ? quitAction : career, settings, settings);
            Link(settings, quick, quitAction != null ? quitAction : career, profile, profile);
            if (quitAction != null) Link(quitAction, settings, career, null, null);
        }
        private static void Link(UnityEngine.UI.Button button, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down, UnityEngine.UI.Selectable left, UnityEngine.UI.Selectable right)
        {
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
        }
    }
}
