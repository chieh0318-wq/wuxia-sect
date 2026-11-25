using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private void ShowSectInventory(int category = 0)
        {
            string[] categories = { "裝備", "丹藥", "秘笈", "雜物" };
            var buttons = categories.Select((label, index) =>
                (index == category ? "【" + label + "】" : label, (Action)(() => ShowSectInventory(index))))
                .Append(("關閉", (Action)(() => ui.CloseModal(force: true)))).ToArray();
            ui.ShowCreation("庫房", new[] { categories[category] }, buttons, null);
            if (category == 0)
            {
                var gear = (state.Story.armoury ?? Array.Empty<GearItem>()).Where(item => item != null).ToArray();
                if (gear.Length == 0) Empty();
                foreach (var item in gear)
                    ui.CreationChoice($"【{item.grade}】{item.name}", $"{item.slot}・{item.Stats}　由掌門換上", null, () =>
                    {
                        Equip(item);
                        Finish();
                    });
            }
            else if (category == 1)
            {
                foreach (var medicine in GameState.SectMedicines)
                {
                    int count = LegacySystem.GetInventory(state.Story, medicine.id);
                    ui.CreationChoice($"{EffectText.ItemName(medicine.id)} ×{count}",
                        $"上陣時氣血 +{medicine.hp}、內力 +{medicine.qi}；平日療傷最多 {medicine.injury}", null,
                        count > 0 && state.Protagonist.injury > 0 ? () =>
                        {
                            LegacySystem.AddInventory(state.Story, medicine.id, -1);
                            state.Protagonist.injury = Math.Max(0, state.Protagonist.injury - medicine.injury);
                            Finish();
                        } : null);
                }
            }
            else if (category == 2)
            {
                var manuals = (state.Story.inventory ?? Array.Empty<StoryValue>()).Where(entry => entry != null &&
                    entry.value > 0 && entry.id.StartsWith(GameState.StoredManualPrefix, StringComparison.Ordinal)).ToArray();
                if (manuals.Length == 0) Empty();
                foreach (var entry in manuals)
                {
                    string artId = entry.id.Substring(GameState.StoredManualPrefix.Length);
                    var art = MartialArts.Find(artId);
                    if (art == null) continue;
                    ui.CreationChoice($"《{art.name}》 ×{entry.value}", art.desc, null,
                        () => ChooseManualReader(artId, art.name));
                }
            }
            else ui.CreationChoice($"作坊材料 ×{state.Story.materials}", "用於營造與坊間工事。", null, null);

            void Empty() => ui.CreationChoice("庫中暫無此物", "", null, null);
            void Finish()
            {
                if (!capturing) state.Save();
                ui.Refresh(state);
                ShowSectInventory(category);
            }
        }

        private void ChooseManualReader(string artId, string name)
        {
            ui.ShowCreation($"傳授《{name}》", Array.Empty<string>(),
                new (string, Action)[] { ("返回庫房", () => ShowSectInventory(2)) }, null);
            Add(state.Protagonist.name, GameState.HeroId);
            foreach (var member in state.Disciples) Add(member.name, member.id);

            void Add(string label, string id) => ui.CreationChoice(label, "領用秘笈", null, () =>
            {
                string result = state.AssignStoredManual(id, artId);
                if (!capturing) state.Save();
                ShowSectInventory(2);
                if (!string.IsNullOrEmpty(result)) ui.Message(result);
            });
        }
    }
}
