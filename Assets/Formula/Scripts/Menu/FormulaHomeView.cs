using System;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaHomeView : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("continueCareer")]
        [SerializeField] private UnityEngine.UI.Button career;
        [SerializeField] private UnityEngine.UI.Button garage, quick, profile, settings;
        public void Bind(Action careerAction, Action garageAction, Action quickAction, Action profileAction, Action settingsAction)
        {
            career.onClick.AddListener(() => careerAction()); garage.onClick.AddListener(() => garageAction());
            quick.onClick.AddListener(() => quickAction());
            profile.onClick.AddListener(() => profileAction()); settings.onClick.AddListener(() => settingsAction());
            WireNavigation();
        }
        public void WireNavigation()
        {
            Link(career, settings, garage, null, null);
            Link(garage, career, quick, null, null);
            Link(quick, garage, profile, null, null);
            Link(profile, quick, career, settings, settings);
            Link(settings, quick, career, profile, profile);
        }
        private static void Link(UnityEngine.UI.Button button, UnityEngine.UI.Selectable up, UnityEngine.UI.Selectable down, UnityEngine.UI.Selectable left, UnityEngine.UI.Selectable right)
        {
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
        }
    }
}
