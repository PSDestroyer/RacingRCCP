using UnityEditor;
[InitializeOnLoad]
internal static class FormulaSwitch60TemporaryTrigger
{
    static FormulaSwitch60TemporaryTrigger()
    {
        EditorApplication.delayCall += () =>
        {
            const string key = "Formula.SwitchPresentation.60fps.20261030.1";
            if (SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, true);
            FormulaSwitchPresentationValidation.Run();
        };
    }
}
