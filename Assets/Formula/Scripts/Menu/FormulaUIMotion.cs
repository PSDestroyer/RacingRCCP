using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FormulaGame
{
    // Transient presentation only. No animation queues, gameplay state, materials
    // or renderers. Each target runs one unscaled coroutine only while moving.
    [DisallowMultipleComponent]
    public sealed class FormulaUIMotion : MonoBehaviour
    {
        private Vector3 restPosition, restScale;
        private Vector3 writtenPosition, writtenScale;
        private bool pose;
        private RectTransform rectangle;
        private Coroutine runner;
        private float focus, focusFrom, focusTo, focusAt;
        private float pulseAt = -100, pulseAmount, pulseDuration;
        private bool visibility, closing;
        private float revealAt, revealDuration, revealDelay, revealScale = 1, alphaFrom = 1;
        private Vector3 revealOffset;
        private CanvasGroup group;
        private bool originalInteractable, originalRaycasts;
        private Action hidden;
        private Graphic tinted;
        private Color tintFrom, tintTo;
        private float tintAt, tintDuration, tintDelay;
        private bool tinting;
        private Image filled;
        private float fillFrom, fillTo, fillAt, fillDuration;
        private bool filling;
        public static bool Allowed => Application.isPlaying && !FormulaProgression.State.reducedMotion;
        public bool IsAnimating => runner != null;
        public static bool IsClosing(GameObject target) => target != null && target.GetComponent<FormulaUIMotion>()?.closing == true;

        private static FormulaUIMotion Get(GameObject target)
        {
            var value = target.GetComponent<FormulaUIMotion>();
            if (value == null) value = target.AddComponent<FormulaUIMotion>();
            if (!value.pose)
            { value.pose = true; value.rectangle = target.transform as RectTransform; value.restPosition = value.writtenPosition = target.transform.localPosition; value.restScale = value.writtenScale = target.transform.localScale; }
            else
            {
                // Authoritative layout changes (safe area/outcome/category) are
                // a new resting pose. Our own in-flight offsets are never recaptured.
                if (target.transform.localPosition != value.writtenPosition) value.restPosition = target.transform.localPosition;
                if (target.transform.localScale != value.writtenScale) value.restScale = target.transform.localScale;
            }
            return value;
        }
        private void EnsureGroup()
        {
            if (group != null) return;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            originalInteractable = group.interactable; originalRaycasts = group.blocksRaycasts;
        }
        private void Run()
        {
            if (!isActiveAndEnabled) { Snap(); return; }
            if (runner == null) runner = StartCoroutine(Animate());
        }
        public static void Focus(GameObject target, float amount)
        {
            if (target == null || !target.activeInHierarchy) return;
            if (!Allowed) { var existing = target.GetComponent<FormulaUIMotion>(); if (existing != null) { existing.focusTo = 0; existing.Snap(); } return; }
            var m = Get(target); m.focusFrom = m.focus; m.focusTo = Allowed ? Mathf.Clamp01(amount) : 0; m.focusAt = Time.unscaledTime;
            m.Run();
        }
        public static void Pulse(GameObject target, float amount = .035f, float seconds = .18f)
        {
            if (target == null || !target.activeInHierarchy) return;
            if (!Allowed) { var existing = target.GetComponent<FormulaUIMotion>(); if (existing != null) existing.Snap(); return; }
            var m = Get(target); m.pulseAt = Time.unscaledTime; m.pulseAmount = amount; m.pulseDuration = Mathf.Max(.01f, seconds); m.Run();
        }
        public static void Enter(GameObject target, bool modal = false, float delay = 0)
        {
            if (target == null || !target.activeInHierarchy) return;
            var m = Get(target); m.EnsureGroup();
            m.hidden = null; m.closing = false; m.group.interactable = m.originalInteractable; m.group.blocksRaycasts = m.originalRaycasts;
            m.visibility = true; m.revealAt = Time.unscaledTime; m.revealDelay = delay; m.revealDuration = modal ? .16f : .20f;
            m.revealOffset = modal ? Vector3.zero : new Vector3(0, -12, 0); m.revealScale = modal ? .96f : 1; m.alphaFrom = 0;
            if (!Allowed) m.Snap(); else { m.group.alpha = 0; m.ApplyPose(0, 0); m.Run(); }
        }
        public static void Glide(GameObject target, Vector3 previousWorldPosition)
        {
            if (target == null || !target.activeInHierarchy || !Allowed) return;
            var m = Get(target); m.visibility = true; m.closing = false; m.hidden = null;
            m.revealAt = Time.unscaledTime; m.revealDelay = 0; m.revealDuration = .18f; m.revealScale = 1; m.alphaFrom = 1;
            m.revealOffset = target.transform.parent.InverseTransformPoint(previousWorldPosition) - m.restPosition;
            m.Run();
        }
        public static void Hide(GameObject target, Action after = null)
        {
            if (target == null || !target.activeInHierarchy || !Allowed)
            { if (target != null) target.SetActive(false); after?.Invoke(); return; }
            var m = Get(target); if (m.closing) return;
            m.EnsureGroup(); m.closing = true; m.hidden = after; m.visibility = true;
            m.group.interactable = false; m.group.blocksRaycasts = false;
            m.alphaFrom = m.group.alpha; m.revealAt = Time.unscaledTime; m.revealDelay = 0; m.revealDuration = .10f;
            m.revealOffset = Vector3.zero; m.revealScale = 1; m.Run();
        }
        public static void Tint(Graphic target, Color color, float seconds = .16f, float delay = 0)
        {
            if (target == null) return;
            if (!target.gameObject.activeInHierarchy || !Allowed)
            { var existing = target.GetComponent<FormulaUIMotion>(); if (existing != null) { existing.tintTo = color; existing.tinting = false; } target.color = color; return; }
            if (target.color == color && target.GetComponent<FormulaUIMotion>()?.tinting != true) return;
            var m = Get(target.gameObject);
            if (m.tinting && m.tinted == target && m.tintTo == color) return;
            m.tinted = target; m.tintFrom = target.color; m.tintTo = color;
            m.tintAt = Time.unscaledTime; m.tintDuration = Mathf.Max(.01f, seconds); m.tintDelay = delay; m.tinting = true; m.Run();
        }
        public static void Fill(Image target, float value, float seconds = .24f)
        {
            if (target == null) return;
            if (!target.gameObject.activeInHierarchy || !Allowed)
            { var existing = target.GetComponent<FormulaUIMotion>(); if (existing != null) { existing.fillTo = value; existing.filling = false; } target.fillAmount = value; return; }
            var m = Get(target.gameObject); if (m.filling && Mathf.Approximately(m.fillTo, value)) return;
            m.filled = target; m.fillFrom = target.fillAmount; m.fillTo = value; m.fillAt = Time.unscaledTime;
            m.fillDuration = Mathf.Max(.01f, seconds); m.filling = true; m.Run();
        }
        public static void Complete(Transform root)
        {
            if (root == null) return;
            foreach (var m in root.GetComponentsInChildren<FormulaUIMotion>(true)) m.Snap();
        }
        private static float Ease(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }
        private IEnumerator Animate()
        {
            // Defer once so Run can retain the handle even for an instant result.
            yield return null;
            while (true)
            {
                if (!Allowed) { runner = null; Snap(); yield break; }
                float now = Time.unscaledTime; bool busy = false;
                float ft = Mathf.Clamp01((now - focusAt) / .12f); focus = Mathf.Lerp(focusFrom, focusTo, Ease(ft)); busy |= ft < 1 && !Mathf.Approximately(focusFrom, focusTo);
                float pt = pulseDuration > 0 ? Mathf.Clamp01((now - pulseAt) / pulseDuration) : 1;
                float pulse = pt < 1 ? pulseAmount * Mathf.Sin(pt * Mathf.PI) : 0; busy |= pt < 1;
                float vt = visibility ? Mathf.Clamp01((now - revealAt - revealDelay) / revealDuration) : 1;
                ApplyPose(Ease(vt), pulse);
                if (visibility)
                {
                    if (group != null) group.alpha = closing ? Mathf.Lerp(alphaFrom, 0, Ease(vt)) : Mathf.Lerp(alphaFrom, 1, Ease(vt));
                    busy |= vt < 1;
                    if (vt >= 1) { visibility = false; if (closing) { runner = null; FinishHide(); yield break; } }
                }
                if (tinting && tinted != null)
                {
                    float t = Mathf.Clamp01((now - tintAt - tintDelay) / tintDuration); tinted.color = Color.Lerp(tintFrom, tintTo, Ease(t));
                    tinting = t < 1; busy |= tinting;
                }
                if (filling && filled != null)
                {
                    float t = Mathf.Clamp01((now - fillAt) / fillDuration); filled.fillAmount = Mathf.Lerp(fillFrom, fillTo, Ease(t));
                    filling = t < 1; busy |= filling;
                }
                if (!busy) { runner = null; yield break; }
                yield return null;
            }
        }
        private void ApplyPose(float reveal, float pulse)
        {
            if (!pose) return;
            float revealSize = visibility ? closing ? Mathf.Lerp(1, .975f, reveal) : Mathf.Lerp(revealScale, 1, reveal) : 1;
            float size = (1 + focus * .012f + pulse) * revealSize;
            Vector3 offset = visibility ? revealOffset * (1 - reveal) : Vector3.zero;
            if (rectangle != null)
            {
                // Keep visual compression centred without changing authored pivots
                // or the navigation/layout ownership of the RectTransform.
                Vector2 centre = (rectangle.pivot - Vector2.one * .5f) * rectangle.rect.size * (size - 1);
                offset += new Vector3(centre.x * restScale.x, centre.y * restScale.y, 0);
            }
            transform.localPosition = restPosition + offset;
            transform.localScale = restScale * size;
            writtenPosition = transform.localPosition; writtenScale = transform.localScale;
        }
        private void FinishHide()
        {
            var callback = hidden; hidden = null; closing = false;
            gameObject.SetActive(false); callback?.Invoke();
        }
        private void Snap()
        {
            if (runner != null) { StopCoroutine(runner); runner = null; }
            focus = Allowed ? focusTo : 0; pulseAt = -100; visibility = false;
            if (tinted != null && tinting) tinted.color = tintTo;
            if (filled != null && filling) filled.fillAmount = fillTo;
            tinting = filling = false;
            ApplyPose(1, 0);
            if (closing) { FinishHide(); return; }
            if (group != null) group.alpha = 1;
        }
        private void OnDisable()
        {
            if (runner != null) StopCoroutine(runner); runner = null;
            focus = focusFrom = focusTo = 0; pulseAt = -100; visibility = closing = false; hidden = null;
            if (tinted != null && tinting) tinted.color = tintTo;
            if (filled != null && filling) filled.fillAmount = fillTo;
            tinting = filling = false;
            if (pose) { transform.localPosition = writtenPosition = restPosition; transform.localScale = writtenScale = restScale; }
            if (group != null) { group.alpha = 1; group.interactable = originalInteractable; group.blocksRaycasts = originalRaycasts; }
        }
    }
}
