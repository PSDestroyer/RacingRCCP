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
        }
    }
}
