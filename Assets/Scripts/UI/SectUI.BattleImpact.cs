using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        // Presentation runs on an unscaled clock. Holding the contact frame locally
        // leaves Unity's time scale, menu transitions and capture timing intact.
        private IEnumerator PlayImpact(RectTransform stage, BattleUnitView attacker, BattleUnitView target,
            BattleEvent entry, int speed, bool firstBigMove, System.Action<BattleEvent> capture)
        {
            bool heavy = entry.Skill?.telegraph == true || entry.Skill?.all == true;
            bool guarded = entry.Target.statuses.ContainsKey("guard");
            var (elementSheet, tint) = entry.Skill?.element switch
            {
                "金" => ("metal", new Color(1f, .89f, .49f)),
                "木" => ("wood", new Color(.61f, 1f, .64f)),
                "水" => ("water", new Color(.57f, .83f, 1f)),
                "火" => ("fire", new Color(1f, .63f, .49f)),
                "土" => ("ground_crack", new Color(.93f, .72f, .45f)),
                _ => ("impact_cross", Color.white)
            };
            string arc = entry.Skill?.id == "seal" ? "palm" : heavy ? "arc_heavy" : "arc_medium";
            var center = target.StandingPosition + Vector2.up * (stage.rect.height * target.Root.anchorMin.y + 116);
            var blade = Strip(arc, center, heavy ? 364 : 272, Color.white);
            var ink = Strip(heavy ? "shock_ring" : "ink_trail", center, heavy ? 380 : 245, Color.white);
            var impact = Strip(elementSheet, center, heavy ? 308 : 205, tint);
            var stagePosition = stage.anchoredPosition;
            var stageScale = stage.localScale;
            var targetColor = target.Art.color;
            float tempo = Mathf.Sqrt(speed);
            float hold = (heavy ? .085f : .060f) / tempo;
            float duration = (firstBigMove ? .54f : heavy ? .32f : .28f) / tempo;
            float direction = entry.Actor.isPlayer ? 1 : -1;
            AudioManager.Play("swing_fist");
            attacker.Root.anchoredPosition += Vector2.right * (30 * direction);
            yield return null;
            if (stage == null) yield break;
            AudioManager.Play(guarded ? "hit_guard" : "hit_fist");
            for (float time = 0; time < duration && stage != null; time += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01((time - hold) / (duration - hold));
                float shake = time < hold ? 0 : Mathf.Sin(progress * 36) * (1 - progress) * (heavy ? 16 : 8);
                stage.anchoredPosition = stagePosition + new Vector2(shake, shake * .27f);
                if (firstBigMove) stage.localScale = stageScale * (1 + .037f * (1 - progress));
                target.Art.color = Color.Lerp(tint, targetColor, progress);
                if (time >= hold) attacker.Root.anchoredPosition = attacker.StandingPosition;
                SetFrame(blade, progress, direction < 0);
                SetFrame(ink, progress, direction < 0);
                SetFrame(impact, progress, false);
                if (progress >= .36f && capture != null)
                {
                    capture(entry);
                    capture = null;
                }
                yield return null;
            }
            if (stage == null) yield break;
            stage.anchoredPosition = stagePosition;
            stage.localScale = stageScale;
            attacker.Root.anchoredPosition = attacker.StandingPosition;
            target.Art.color = targetColor;
            if (blade != null) Object.Destroy(blade.gameObject);
            if (ink != null) Object.Destroy(ink.gameObject);
            if (impact != null) Object.Destroy(impact.gameObject);

            RawImage Strip(string id, Vector2 position, float size, Color color)
            {
                var texture = Resources.Load<Texture2D>("Battle/SkillFX/" + id);
                if (texture == null) return null;
                var rect = Rect(id, stage);
                var bottom = new Vector2(.5f, 0);
                var radius = Vector2.one * (size / 2);
                Anchor(rect, bottom, bottom, position - radius, position + radius);
                var image = AddImage(rect, texture);
                image.raycastTarget = false;
                image.color = color;
                SetFrame(image, 0, false);
                return image;
            }
        }

        private IEnumerator PlayRecovery(BattleUnitView unit)
        {
            var texture = Resources.Load<Texture2D>("Battle/SkillFX/heal_lotus");
            if (texture == null) yield break;
            var rect = Rect("Recovery", unit.Root);
            var center = new Vector2(.5f, .45f);
            Anchor(rect, center, center, new(-112, -112), new(112, 112));
            var art = AddImage(rect, texture);
            art.raycastTarget = false;
            for (float elapsed = 0; elapsed < .34f && art != null; elapsed += Time.unscaledDeltaTime)
            {
                SetFrame(art, elapsed / .34f, false);
                yield return null;
            }
            if (art != null) Object.Destroy(art.gameObject);
        }

        private static void SetFrame(RawImage strip, float progress, bool flip)
        {
            if (strip == null) return;
            const int frames = 6;
            int frame = Mathf.Min(frames - 1, (int)(progress * frames));
            float width = 1f / frames;
            strip.uvRect = new Rect((frame + (flip ? 1 : 0)) * width, 0, flip ? -width : width, 1);
        }
    }
}

