using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126d-ag (C8): CJK kinsoku (避頭避尾) guard for legacy <see cref="Text"/>.
    /// Prevents closing punctuation (。，、；：！？）」』】》〉〕．·…—) from appearing at the start of a wrapped line,
    /// and prevents opening brackets (（「『【《〈〔) from dangling at the end of a wrapped line.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class CjkKinsoku : MonoBehaviour
    {
        private const string NoLineStart = "。，、；：！？）」』】》〉〕．·…—％%";
        private const string NoLineEnd = "（「『【《〈〔";

        private Text label;
        private TextGenerator generator;
        private string sourceText;
        private string appliedText;
        private float lastWidth = -1f;
        private int lastFontSize = -1;

        internal static void Attach(Text text)
        {
            if (text == null) return;
            if (text.GetComponent<CjkKinsoku>() == null)
                text.gameObject.AddComponent<CjkKinsoku>();
        }

        private void Awake() => label = GetComponent<Text>();

        private void LateUpdate()
        {
            if (label == null) label = GetComponent<Text>();
            if (label == null || label.horizontalOverflow != HorizontalWrapMode.Wrap) return;

            string current = label.text;
            if (string.IsNullOrEmpty(current) || current.Length < 4) return;
            // Skip rich-text markup to avoid inserting line breaks inside <color=...> tags.
            if (label.supportRichText && current.IndexOf('<') >= 0 && current.IndexOf('>') >= 0) return;

            var rect = label.rectTransform.rect;
            if (rect.width <= 1f || rect.height <= 1f) return;
            // v1126d: a one-line box has no room for a second line; a forced break would blank a best-fit label.
            if (rect.height < label.fontSize * 1.8f) return;
            // Best-fit labels wrap at the size actually drawn; wait until the canvas has generated it once.
            if (label.resizeTextForBestFit && label.cachedTextGenerator.fontSizeUsedForBestFit <= 0) return;

            bool textChanged = !string.Equals(current, appliedText, StringComparison.Ordinal);
            bool layoutChanged = Mathf.Abs(rect.width - lastWidth) > 0.5f || label.fontSize != lastFontSize;
            if (!textChanged && !layoutChanged) return;

            if (textChanged) sourceText = current;
            if (string.IsNullOrEmpty(sourceText)) return;

            lastWidth = rect.width;
            lastFontSize = label.fontSize;

            string formatted = Apply(sourceText, rect.size);
            appliedText = formatted;
            if (!string.Equals(label.text, formatted, StringComparison.Ordinal))
                label.text = formatted;
        }

        private string Apply(string raw, Vector2 extents)
        {
            generator ??= new TextGenerator();
            string work = raw;
            var settings = label.GetGenerationSettings(extents);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            if (settings.resizeTextForBestFit)
            {
                // fontSizeUsedForBestFit is in canvas pixels; settings.fontSize is in layout units.
                float scale = Mathf.Max(.0001f, settings.scaleFactor);
                settings.fontSize = Mathf.Max(1, Mathf.FloorToInt(label.cachedTextGenerator.fontSizeUsedForBestFit / scale));
                settings.resizeTextForBestFit = false;
            }
            for (int pass = 0; pass < 16; pass++)
            {
                generator.Populate(work, settings);
                IList<UILineInfo> lines = generator.lines;
                if (lines == null || lines.Count <= 1) break;

                int insertAt = -1;
                for (int i = 1; i < lines.Count; i++)
                {
                    int start = lines[i].startCharIdx;
                    int prevStart = lines[i - 1].startCharIdx;
                    if (start <= prevStart || start >= work.Length) continue;
                    if (work[start - 1] == '\n') continue;

                    char first = work[start];
                    char prev = work[start - 1];

                    if (NoLineStart.IndexOf(first) >= 0)
                    {
                        int cut = start - 1;
                        while (cut > prevStart && NoLineEnd.IndexOf(work[cut - 1]) >= 0) cut--;
                        if (cut > prevStart)
                        {
                            insertAt = cut;
                            break;
                        }
                    }
                    else if (NoLineEnd.IndexOf(prev) >= 0)
                    {
                        int cut = start - 1;
                        while (cut > prevStart && NoLineEnd.IndexOf(work[cut - 1]) >= 0) cut--;
                        if (cut > prevStart)
                        {
                            insertAt = cut;
                            break;
                        }
                    }
                }

                if (insertAt < 0) break;
                work = work.Insert(insertAt, "\n");
            }
            return work;
        }
    }
}
