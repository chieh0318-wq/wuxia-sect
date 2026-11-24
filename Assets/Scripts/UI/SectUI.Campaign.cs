using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class SectUI
    {
        internal void ShowCampaign(SectCampaign battle, string title, MonoBehaviour runner, GameState state,
            MapFaction faction, int apCost, Func<bool> begin, Action<bool> finish, Action back, bool capturing)
        {
            var board = Panel("SectCampaign", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layer = board.GetComponent<Image>();
            layer.color = new Color(.12f, .17f, .16f);
            Canvas.ForceUpdateCanvases();
            bool fighting = false;
            bool closing = false;
            int selected = 0;
            string message = "首位將領為主將。兵員上限依位階與統率，統率取悟性與魅力平均。";
            void Exit(bool won)
            {
                if (closing) return;
                closing = true;
                UiBack.Pop(board);
                UnityEngine.Object.Destroy(board.gameObject);
                if (fighting) finish(won);
                else back?.Invoke();
            }

            void Draw()
            {
                for (int i = board.childCount - 1; i >= 0; i--)
                {
                    var old = board.GetChild(i);
                    old.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(old.gameObject);
                }
                var stage = MapUi.Rect(board, "戰場", 0, 0, 1400, 740);
                stage.anchorMin = stage.anchorMax = new Vector2(.5f, .5f);
                stage.pivot = new Vector2(.5f, .5f);
                stage.anchoredPosition = Vector2.zero;
                stage.localScale = Vector3.one * Mathf.Min(board.rect.width * .95f / 1400, board.rect.height * .94f / 740);
                var backdrop = Resources.Load<Texture2D>("Campaign/Pass");
                if (backdrop != null)
                {
                    var image = stage.gameObject.AddComponent<RawImage>();
                    image.texture = backdrop;
                    image.color = new Color(.62f, .64f, .57f);
                    image.raycastTarget = false;
                }
                var header = MapUi.Kit(stage, "軍令", 15, 12, 1370, 85, "Lacquer");
                MapUi.Text(header, title, 20, 4, 980, 44, 32, MapUi.Gold);
                string status = battle.Surprise ? "敵軍突襲・無法選陣" : $"守備加成 {battle.Prepared}";
                MapUi.Text(header, $"第 {battle.Round} 回合　士氣 {battle.Morale:+0;-0;0}　{status}",
                    20, 48, 1000, 27, 19, Color.white);
                MapUi.Button(header, fighting ? "撤兵" : "返回", 1205, 21, 142, 43,
                    () => Exit(battle.Won), MapUi.Red);
                DrawArmy(battle.Allies, true);
                DrawArmy(battle.Enemies, false);
                var orders = MapUi.Docket(stage, "軍帳", 15, 525, 1370, 200);
                MapUi.Text(orders, message, 22, 15, 1315, 42, 20, MapUi.Ink);
                if (!fighting)
                {
                    var active = battle.Allies[selected];
                    MapUi.Text(orders, $"{active.Name}　兵 {active.Troops}／{active.Capacity}　{SectCampaign.TroopNames[(int)active.Kind]}",
                        22, 58, 460, 38, 21, MapUi.Ink);
                    MapUi.Button(orders, "更換兵種", 480, 58, 125, 41, () =>
                    {
                        active.Kind = (TroopKind)(((int)active.Kind + 1) % SectCampaign.TroopNames.Length);
                        if (active.Kind == TroopKind.Bow) active.Rear = true;
                        Draw();
                    });
                    MapUi.Button(orders, active.Rear ? "移到前排" : "移到後排", 620, 58, 125, 41,
                        () => { active.Rear = !active.Rear; Draw(); });
                    MapUi.Button(orders, "減兵", 760, 58, 95, 41, () => { active.Troops = Math.Max(0, active.Troops - 50); Draw(); });
                    MapUi.Button(orders, "加兵", 870, 58, 95, 41, () =>
                    {
                        active.Troops = Math.Min(active.Capacity, active.Troops + Math.Min(50, battle.Unassigned));
                        Draw();
                    }).interactable = battle.Unassigned > 0 && active.Troops < active.Capacity;
                    MapUi.Text(orders, $"待配兵員 {battle.Unassigned}", 985, 58, 315, 38, 19, MapUi.Ink);
                    if (!battle.Surprise)
                    {
                        for (int i = 0; i < SectCampaign.FormationNames.Length; i++)
                        {
                            var formation = (WarFormation)i;
                            MapUi.Button(orders, SectCampaign.FormationNames[i], 22 + i * 226, 126, 211, 48,
                                () => { battle.Formation = formation; Draw(); },
                                battle.Formation == formation ? MapUi.Red : (Color?)null);
                        }
                    }
                    MapUi.Button(orders, apCost > 0 ? $"開戰　行動力 {apCost}" : "開戰", 1108, 126, 228, 48, () =>
                    {
                        if (!begin()) { message = "出兵條件有變，請返回調整。"; Draw(); return; }
                        battle.Deploy();
                        fighting = true;
                        message = "出令後雙方按速度交鋒。強攻消耗內力，整軍恢復內力並加強防守。";
                        Draw();
                    }, MapUi.Red);
                    return;
                }
                if (battle.Finished)
                {
                    MapUi.Button(orders, "收兵", 1178, 110, 158, 48, () => Exit(battle.Won), MapUi.Red);
                    return;
                }
                MapUi.Button(orders, "強攻", 22, 110, 156, 48, () => Turn(false), MapUi.Red);
                MapUi.Button(orders, "整軍", 194, 110, 156, 48, () => Turn(true));
                if (battle.CanDuel)
                    MapUi.Button(orders, "主將對決 ›", 366, 110, 190, 48, Duel);
                void Turn(bool rally)
                {
                    message = battle.Advance(rally);
                    Draw();
                }

                void DrawArmy(WarUnit[] army, bool allies)
                {
                    float x = allies ? 23 : 713;
                    string name = allies ? "本門軍陣" : faction.Label;
                    MapUi.Text(stage, name, x, 103, 655, 35, 27, Color.white, TextAnchor.MiddleCenter);
                    foreach (bool rear in new[] { false, true })
                    {
                        float y = rear ? 335 : 151;
                        MapUi.Text(stage, rear ? "後排" : "前排", x, y, 95, 24, 18, MapUi.Gold);
                        var row = army.Where(unit => unit.Rear == rear).ToArray();
                        for (int i = 0; i < row.Length; i++)
                        {
                            var unit = row[i];
                            float width = Math.Min(150, 650f / Math.Min(5, row.Length));
                            var tile = MapUi.Panel(stage, "將旗", x + i % 5 * width, y + 30 + i / 5 * 77, width - 4, 74,
                                allies ? new Color(.15f, .27f, .22f, .97f) : new Color(.34f, .19f, .16f, .97f));
                            if (!string.IsNullOrEmpty(unit.Portrait))
                            {
                                var portrait = PortraitService.Resolve(unit.Portrait);
                                if (portrait != null) MapUi.Portrait(tile, portrait, 3, 2, 23, 25);
                            }
                            string callSign = unit.Name.Split('　').Last();
                            var unitLabel = MapUi.Text(tile, (unit.Commander ? "帥 " : "") + callSign,
                                26, 0, width - 34, 30, 17, Color.white, TextAnchor.MiddleCenter);
                            unitLabel.verticalOverflow = VerticalWrapMode.Truncate;
                            unitLabel.resizeTextMinSize = 14;
                            MapUi.Text(tile, $"兵 {unit.Troops}　{SectCampaign.TroopNames[(int)unit.Kind]}",
                                3, 50, width - 10, 23, 16, Color.white, TextAnchor.MiddleCenter);
                            int silhouettes = unit.Troops == 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(unit.Troops / 90f), 1, 12);
                            for (int j = 0; j < silhouettes; j++)
                            {
                                int offset = 5 + j * 8;
                                MapUi.Panel(tile, "矛", offset, 30, 2, 13, MapUi.Gold);
                                MapUi.Panel(tile, "盔", offset + 2, 32, 4, 4, MapUi.Ink);
                                MapUi.Panel(tile, "甲", offset + 1, 36, 6, 7, MapUi.Ink);
                            }
                            if (fighting)
                            {
                                float scale = (width - 14) / (unit.BaseHealth * 1.55f);
                                float maxWidth = Mathf.Min(width - 14, unit.MaxHealth * scale);
                                float hpWidth = maxWidth * unit.Health / Mathf.Max(1f, unit.MaxHealth);
                                MapUi.Panel(tile, "氣血底", 5, 46, maxWidth, 4, MapUi.Ink);
                                MapUi.Panel(tile, "氣血", 5, 46, hpWidth, 4, new Color(.65f, .26f, .21f));
                                if (unit.Shield > 0)
                                    MapUi.Panel(tile, "護盾", 5, 43, (width - 14) * Mathf.Clamp01(unit.Shield / (float)Math.Max(1, unit.Capacity / 5)), 3, MapUi.Gold);
                            }
                            if (allies && !fighting)
                            {
                                var button = tile.gameObject.AddComponent<Button>();
                                button.onClick.AddListener(() => { selected = Array.IndexOf(battle.Allies, unit); Draw(); });
                                if (Array.IndexOf(battle.Allies, unit) == selected) tile.GetComponent<Image>().color = MapUi.Red;
                            }
                        }
                    }
                }
            }

            void Duel()
            {
                board.gameObject.SetActive(false);
                StartTurnBattle(state, new BattleRequest
                {
                    ground = BattleGround.Fortification, expeditionParty = new[] { battle.Allies[0].Id },
                    enemyCount = 1, enemyLeadName = battle.Enemies[0].Name,
                    enemyLeadPortrait = battle.Enemies[0].Portrait, leadAsStandee = true, commanderDuel = true,
                    onComplete = won =>
                    {
                        battle.ResolveDuel(won);
                        state.RecordCommanderDuel(faction, battle.Allies[0].Id, won); // v1126b 決定 3: 天下第一
                        message = won ? "主將對決得勝，全軍士氣上揚。" : "主將對決失利，全軍士氣下降。";
                        board.gameObject.SetActive(true);
                        RegisterBack();
                        Draw();
                    }
                }, runner, captureRound: capturing ? _ => { } : null);
            }

            void RegisterBack() => UiBack.Push(() =>
            {
                if (!fighting || battle.Finished) Exit(battle.Won);
                else Exit(false);
            }, () => board != null && board.gameObject.activeSelf, board);
            RegisterBack();
            Draw();
        }
    }
}
