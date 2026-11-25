using System;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    /// <summary>
    /// v1126c PC3 capture shots (muted, part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly PC3_"): creation 屬性 with all
    /// seven rows, the derived-stat and attribute tooltips, the 先輩遺澤 right-click refund, the prologue dialogue styles
    /// (speaker / 旁白), the road-event narration, the portrait talk, the settlement map sites (nearby lists, numeric effects,
    /// the new NW sites), the HUD 任務 button and the tutorial. Each step logs what it checks.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private NewGameFlow pc3Flow;

        private NewGameFlow Pc3Flow() => pc3Flow ??= new NewGameFlow(ui, new GameState(data), _ => { });

        private (string filename, Action show)[] Pc3CaptureSteps() => new (string, Action)[]
        {
            ("PC3_01_attributes_all_rows.png", () =>
            {
                ui.CloseTitle();
                ui.CloseStoryBeat();
                ui.CloseModal(force: true);
                pc3Flow = null;
                Pc3Flow().CaptureCreationTalents("scholar_family", false);
                Debug.Log("[Capture] PC3_01 attribute rows: " + ui.CreationAttributeRowsVisible());
            }),
            ("PC3_02_derived_tip_attack.png", () =>
                Debug.Log("[Capture] PC3_02 derived tip 攻擊: " + ui.RevealDerivedTipForCapture("攻擊"))),
            ("PC3_03_derived_tip_fortune.png", () =>
                Debug.Log("[Capture] PC3_03 derived tip 奇遇: " + ui.RevealDerivedTipForCapture("奇遇"))),
            ("PC3_04_attribute_tip_luck.png", () =>
                Debug.Log("[Capture] PC3_04 幸運 row tip: " + ui.RevealAttributeRowTipForCapture(6))),
            ("PC3_05_legacy_shop_picked.png", () =>
            {
                Pc3Flow().CaptureLegacyShop();
                Debug.Log("[Capture] PC3_05 legacy shop: " + ui.LegacyShopSummary());
            }),
            ("PC3_06_legacy_shop_right_click_refund.png", () =>
                Debug.Log("[Capture] PC3_06 right-click refund: " + ui.RightClickFirstLegacyPick() + " → " + ui.LegacyShopSummary())),
            ("PC3_07_prologue_speaker.png", () =>
            {
                ui.CloseModal(force: true);
                pc3Flow = null;
                Pc3Flow().CapturePc3Beat("何苦蹚這渾水");
            }),
            ("PC3_08_prologue_narrator.png", () => Pc3Flow().CapturePc3Beat("藏經閣旁的廂房")),
            ("PC3_09_site_map_cishan.png", () =>
            {
                ui.CloseStoryBeat();
                pc3Flow = null;
                Pc3Flow().CapturePc3Site("cishan");
                LogPc3Sites();
            }),
            ("PC3_10_site_map_longzhong.png", () => Pc3Flow().CapturePc3Site("longzhong")),
            ("PC3_11_site_map_hetao.png", () => Pc3Flow().CapturePc3Site("hetao")),
            ("PC3_12_site_map_yanzhou.png", () => Pc3Flow().CapturePc3Site("yanzhou")),
            ("PC3_13_site_map_mogan_tip.png", () =>
            {
                Pc3Flow().CapturePc3Site("mogan");
                Debug.Log("[Capture] PC3_13 flag tip: " + ui.RevealSettlementTipForCapture(null));
            }),
            ("PC3_14_road_event_narration.png", () =>
            {
                ui.CloseModal(force: true);
                Pc3Courtyard();
                var road = RealmContent.Data.roadEvents?.FirstOrDefault(entry => entry.id == "road_wounded_swordsman");
                if (road != null) PlayRoadEvent(road, null, false);
            }),
            ("PC3_15_road_event_speaker.png", () => ui.Scene.AdvanceForCapture()),
            ("PC3_16_portrait_talk.png", () =>
            {
                if (ui.SceneOpen) ui.Scene.Close();
                Pc3Courtyard();
                if (state.Disciples.Count > 0) Speak(state.Disciples[0]);
            }),
            ("PC3_17_hud_quest_button_pinned.png", () =>
            {
                ui.CloseTalk(false);
                Pc3Courtyard();
                ui.ShowQuestPanel(state);
                Debug.Log("[Capture] PC3_17 bottom bar 任務 button + pinned 眼下要務 only");
            }),
            ("PC3_18_quest_book.png", () =>
            {
                ui.ToggleQuestBook();
                Debug.Log("[Capture] PC3_18 quest book open: " + ui.QuestBookOpen);
            }),
            ("PC3_19_tutorial_build_step.png", () =>
            {
                ui.CloseQuestBook();
                tutorial = new TutorialGuide(ui, state);
                ui.TutorialQuest = () => tutorial?.Current;
                tutorial.ChooseForCapture();
                tutorial.Record("camera_move");
                tutorial.Record("camera_zoom");
                tutorial.Record("click_building", "hall");
                tutorial.Record("talk_disciple");
                ui.CloseTalk(false);
                ui.CloseBuilding();
                Debug.Log("[Capture] PC3_19 build step recommends: " + tutorial.RecommendedBuilding + " | " +
                    TutorialAdvice.Recommend(state)?.reason);
            }),
            ("PC3_20_tutorial_build_menu_blueprint.png", () =>
            {
                BeginBuild(data.Actions.First(action => action.type == "build"));
                Debug.Log("[Capture] PC3_20 blueprint lit: " + ui.TargetActive("blueprint") + " highlight=" + tutorial?.Highlight);
            }),
            ("PC3_21_tutorial_ap_step.png", () =>
            {
                CancelBuild();
                tutorial.Record("build_any");
                Debug.Log("[Capture] PC3_21 AP step: " + TutorialAdvice.ApLine(state) + " | " + TutorialAdvice.ActionCosts(state));
            }),
            ("PC3_22_first_month_advisor_tip.png", () =>
            {
                state.Story.pcMonthsPlayed = 1;
                var tip = FirstMonthsTipLine();
                Debug.Log("[Capture] PC3_22 first-month tip: " + TutorialAdvice.FirstMonthsTip(state));
                if (tip.HasValue) ui.ShowTalk(new System.Collections.Generic.List<PortraitLine> { tip.Value }, null);
                ui.RevealTalkForCapture();
            }),
            ("PC3_23_xieyun_choice_cg.png", () => Pc3FriendChoice("男")), // male hero: the friend is 謝蘊
            ("PC3_24_xiejin_choice_cg.png", () => Pc3FriendChoice("女")),
        };

        /// <summary>PC3 #23: the 「我選哪條不要緊…」 beat of q_friend_1 with its CG and the friend's portrait.</summary>
        private void Pc3FriendChoice(string heroGender)
        {
                ui.CloseTalk(false);
                ui.CloseStoryBeat();
                tutorial = null;
                ui.HideObjective();
                state.Protagonist.gender = heroGender;
                var line = state.Questlines.FirstOrDefault(entry => entry.id == "q_friend");
                var beat = line?.beats?.FirstOrDefault(entry => entry.id == "q_friend_1");
                int at = beat == null ? -1 : Array.FindIndex(beat.scene, part => part.text?.Contains("我選哪條不要緊") == true);
                if (at < 0)
                {
                    Debug.LogWarning("[Capture] PC3 friend beat not found");
                    return;
                }
                string art = null;
                for (int prior = 0; prior <= at; prior++)
                    if (!string.IsNullOrEmpty(beat.scene[prior].image)) art = beat.scene[prior].image;
                var part0 = beat.scene[at];
                var focus = new QuestBeat
                {
                    id = beat.id, title = beat.title, playable = true, choices = beat.choices,
                    scene = new[] { new StoryBeat { speaker = part0.speaker, portrait = part0.portrait, image = art, text = part0.text } }
                };
                PlayQuest(line, focus);
                Debug.Log($"[Capture] PC3 friend beat hero={heroGender} friend={state.FriendId} art={art} portrait={part0.portrait}");
        }

        /// <summary>A fresh courtyard for the in-game PC3 shots (same defaults as skipping the prologue).</summary>
        private void Pc3Courtyard()
        {
            if (state != null && state.Story.locationId != null && courtyardReadyForPc3) return;
            var next = new GameState(data);
            var site = M2Data.Load<PrologueData>("prologue").locations?.FirstOrDefault();
            if (site != null)
            {
                next.Story.locationId = site.id;
                next.Story.mapNode = site.node;
                next.ApplyEffects(site.effects);
            }
            next.Protagonist.name = "柳行舟";
            next.Protagonist.gender = "男";
            next.Protagonist.title = "代掌門";
            next.Protagonist.portrait = next.Protagonist.lookId = "hero_m1";
            EnterCourtyard(next, true);
            controls.enabled = false;
            courtyardReadyForPc3 = true;
        }

        private bool courtyardReadyForPc3;

        /// <summary>Logs every site's numeric effects and its four nearest seats (PC3 #7 / #8 audit).</summary>
        private static void LogPc3Sites()
        {
            var map = WorldMapData.Load();
            foreach (var site in M2Data.Load<PrologueData>("prologue").locations ?? Array.Empty<StoryLocation>())
            {
                var near = SiteGeography.Nearby(map, site.node, 4)
                    .Select(entry => $"{entry.SeatNames}({entry.Faction.Relation},{entry.Days}日)");
                Debug.Log($"[Capture] PC3 site {site.name}: {string.Join("・", SiteEffects.Lines(site.effects))} | 附近 {string.Join("、", near)}");
            }
        }
    }

    internal sealed partial class NewGameFlow
    {
        /// <summary>v1126c PC3: plays the prologue beat whose text contains <paramref name="snippet"/> (friend / opening beats).</summary>
        internal void CapturePc3Beat(string snippet)
        {
            state.Protagonist.gender = "男";
            state.Protagonist.name = "柳行舟";
            state.Protagonist.portrait = state.Protagonist.lookId = "hero_m1";
            foreach (var beats in new[] { story.friendBeats, story.openingBeats, story.successionBeats, story.closingBeats })
            {
                int index = Array.FindIndex(beats ?? Array.Empty<StoryBeat>(), beat => beat.text?.Contains(snippet) == true);
                if (index < 0) continue;
                CaptureBeats(beats, index);
                return;
            }
            Debug.LogWarning("[Capture] PC3 beat not found: " + snippet);
        }

        /// <summary>v1126c PC3: the settlement map with the site <paramref name="id"/> focused.</summary>
        internal void CapturePc3Site(string id)
        {
            var options = story.locations ?? Array.Empty<StoryLocation>();
            var site = options.FirstOrDefault(option => option.id == id) ?? options.FirstOrDefault();
            ui.ShowSettlementMap(state, options, FormatStory(story.locationPrompt), _ => { }, () => { }, focus: site);
        }
    }
}
