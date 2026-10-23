using System;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaQuickView : MonoBehaviour
    {
        [SerializeField] private FormulaEventCard[] events;
        public void Bind(Action<string, string> inspect) { foreach (var card in events) card.Bind(inspect); }
        public void Refresh() { for (int i = 0; i < events.Length; i++) events[i].Refresh(i); }
    }
}
