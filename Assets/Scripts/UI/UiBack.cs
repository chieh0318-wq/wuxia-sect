using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1125 #16: one shared "go back" stack for right-click and Esc. A panel that has a previous page calls
    /// <see cref="Push"/> with the action that returns there (or closes it at the outermost level) and an optional
    /// liveness check; the bootstrap calls <see cref="TryBack"/> once per frame when the back input fires.
    /// Battle UI (PB) pushes "cancel the selected move" while a move is armed.
    /// </summary>
    internal static class UiBack
    {
        private sealed class Entry
        {
            public object owner;
            public Action back;
            public Func<bool> alive;
        }

        private static readonly List<Entry> stack = new();
        private static int consumedFrame = -1;
        private static Vector3 rightPress;
        private static bool rightArmed;

        /// <summary>Registers a back step. Returns a token for <see cref="Pop"/>; re-pushing the same owner replaces it.</summary>
        internal static object Push(Action back, Func<bool> alive = null, object owner = null)
        {
            owner ??= new object();
            stack.RemoveAll(entry => ReferenceEquals(entry.owner, owner));
            stack.Add(new Entry { owner = owner, back = back, alive = alive });
            return owner;
        }

        /// <summary>Removes the topmost entry, or the entry registered with <paramref name="owner"/>.</summary>
        internal static void Pop(object owner = null)
        {
            if (owner != null)
            {
                stack.RemoveAll(entry => ReferenceEquals(entry.owner, owner));
                return;
            }
            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
        }

        internal static void Clear() => stack.Clear();

        internal static int Count
        {
            get
            {
                Prune();
                return stack.Count;
            }
        }

        /// <summary>
        /// True on the frame the player asks to go back: Esc, or a right click that was released without dragging
        /// (right-drag still pans the courtyard camera and the world map).
        /// </summary>
        internal static bool BackPressed
        {
            get
            {
                bool right = RightClick; // always sample so a press is never missed
                if (consumedFrame == Time.frameCount) return false;
                return right || Input.GetKeyDown(KeyCode.Escape);
            }
        }

        /// <summary>Right button released close to where it was pressed (≤ 12 px) — a click, not a drag.</summary>
        internal static bool RightClick
        {
            get
            {
                if (clickFrame == Time.frameCount) return clickResult;
                clickFrame = Time.frameCount;
                clickResult = false;
                if (Input.GetMouseButtonDown(1))
                {
                    rightPress = Input.mousePosition;
                    rightArmed = true;
                }
                if (Input.GetMouseButtonUp(1) && rightArmed)
                {
                    rightArmed = false;
                    clickResult = (Input.mousePosition - rightPress).sqrMagnitude <= 144;
                }
                return clickResult;
            }
        }

        private static int clickFrame = -1;
        private static bool clickResult;

        /// <summary>Marks this frame's back input as handled so no second listener reacts to it.</summary>
        internal static void Consume() => consumedFrame = Time.frameCount;

        /// <summary>Pops and runs the topmost live entry. Returns false when nothing was registered.</summary>
        internal static bool TryBack()
        {
            Prune();
            if (stack.Count == 0) return false;
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            Consume();
            try { top.back?.Invoke(); }
            catch (Exception error) { Debug.LogError("[UiBack] " + error); }
            return true;
        }

        private static void Prune()
        {
            for (int index = stack.Count - 1; index >= 0; index--)
            {
                var alive = stack[index].alive;
                bool ok;
                try { ok = alive == null || alive(); }
                catch { ok = false; }
                if (!ok) stack.RemoveAt(index);
            }
        }
    }
}
