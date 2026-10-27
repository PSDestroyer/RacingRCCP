using TMPro;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaFontProfile : ScriptableObject
    {
        // Null preserves the authored face, avoiding duplicate Resources fonts in bundles.
        public TMP_FontAsset font;
    }
}
