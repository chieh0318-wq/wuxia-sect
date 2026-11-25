using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wuxia
{
    /// <summary>
    /// v1126e CL1 capture shots (muted, part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly V1126E_"):
    /// #1 the derived panel with 外功／內功／外防／內防 and their formula tips (creation card and hero page), a battle inner
    /// move against 內防 and an outer move against 外防; #5 the guide waiting on AP, the 「尚餘 X 點行動力」 confirmation and
    /// the 月底結算 step at 0 AP; #6 王佐之才 named in the AP breakdown (基礎 4 + 1 = 5), then stacked with a 總管 to 6／6 after the month. Each step logs what it checks.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] V1126eCaptureSteps() => new (string, Action)[]
        {
            ("V1126E_01_creation_derived_outer_def_tip.png", () =>
            {
                ui.CloseTitle();
                ui.CloseStoryBeat();
                ui.CloseModal(force: true);
                pc3Flow = null;
                Pc3Flow().CaptureCreationTalents("scholar_family", false);
                Debug.Log("[Capture] V1126E_01 外防 tip: " + ui.RevealDerivedTipForCapture("外防"));
            }),
            ("V1126E_02_creation_derived_inner_def_tip.png", () =>
                Debug.Log("[Capture] V1126E_02 內防 tip: " + ui.RevealDerivedTipForCapture("內防"))),
            ("V1126E_03_creation_derived_inner_power_tip.png", () =>
                Debug.Log("[Capture] V1126E_03 內功 tip: " + ui.RevealDerivedTipForCapture("內功"))),
            ("V1126E_04_hero_page_outer_def_tip.png", () =>
            {
                ui.CloseModal(force: true);
                pc3Flow = null;
                V1126eCourtyard(null);
                ShowProtagonist();
                var battle = state.BattleFigures(GameState.HeroId);
                var stats = state.HeroStats;
                Debug.Log($"[Capture] V1126E_04 hero sheet 外功 {stats.Attack} 內功 {stats.QiPower} 外防 {stats.OuterDefense} 內防 {stats.InnerDefense}" +
                    $" | battle 外功 {battle?.attack} 內功 {battle?.internalAttack} 外防 {battle?.outerDefense} 內防 {battle?.innerDefense}" +
                    " | tip: " + ui.RevealModalDerivedTipForCapture("外防"));
            }),
            ("V1126E_05_battle_inner_move_vs_inner_def.png", () =>
            {
                ui.CloseModal(force: true);
                state.Protagonist.skills = new[] { "zifu_xuanyuan", "qingping" };
                state.Protagonist.major = "neigong";
                v1126eInspect = null;
                ui.StartTurnBattle(state, new BattleRequest { expeditionParty = new[] { GameState.HeroId } }, this,
                    _ => { }, action => v1126eInspect = action, captureCommand: _ => "attack", captureSeed: 1126);
                v1126eInspect?.Invoke("skill");
                Debug.Log("[Capture] V1126E_05 inner move inspected: " + (v1126eInspect != null));
            }),
            ("V1126E_06_battle_outer_move_vs_outer_def.png", () =>
            {
                v1126eInspect?.Invoke("ultimate"); // the second known move: 青萍劍訣 (outer)
                Debug.Log("[Capture] V1126E_06 outer move inspected");
            }),
            ("V1126E_07_tutorial_waits_for_ap.png", () =>
            {
                ui.CloseBattle();
                V1126eCourtyard(null);
                tutorial = new TutorialGuide(ui, state);
                ui.TutorialQuest = () => tutorial?.Current;
                tutorial.ChooseForCapture();
                foreach (var (trigger, target) in new[] { ("camera_move", ""), ("camera_zoom", ""), ("click_building", "hall"),
                             ("talk_disciple", ""), ("build_any", ""), ("inspect_ap", ""), ("recruit", ""), ("open_roster", "") })
                    tutorial.Record(trigger, target);
                ui.CloseTalk(false);
                ui.CloseBuilding();
                ui.CloseModal(force: true);
                ui.Refresh(state);
                Debug.Log($"[Capture] V1126E_07 AP {state.ActionPoints}/{state.ApMax} affordable={state.HasAffordableAction} " +
                    $"step={tutorial.Current?.title} highlight={tutorial.Highlight}");
            }),
            ("V1126E_08_end_month_confirm.png", () =>
            {
                RequestEndMonth();
                Debug.Log($"[Capture] V1126E_08 confirm open={ui.ModalOpen} AP {state.ActionPoints}");
            }),
            ("V1126E_09_tutorial_month_step_at_zero_ap.png", () =>
            {
                ui.CloseModal(force: true);
                state.TrySpendAp(state.ActionPoints, "capture-v1126e");
                ui.Refresh(state);
                tutorial.RefreshForCapture();
                Debug.Log($"[Capture] V1126E_09 AP {state.ActionPoints}/{state.ApMax} step={tutorial.Current?.title} highlight={tutorial.Highlight}");
            }),
            ("V1126E_10_wangzuo_ap_six.png", () =>
            {
                tutorial = null;
                ui.HideObjective();
                V1126eCourtyard("wangzuo");
                ui.Refresh(state);
                ui.RevealApSourcesForCapture();
                Debug.Log($"[Capture] V1126E_10 王佐之才 AP {state.ActionPoints}/{state.ApMax} | {state.ApDescription.Replace("\n", "、")}");
            }),
            ("V1126E_11_wangzuo_plus_steward_after_month.png", () =>
            {
                // 王佐之才 stacks with the other sources: name a 總管 (+1) and settle the month → 6／6.
                var steward = state.Disciples.FirstOrDefault(d => d != null && d.age >= 20 && d.startInSect);
                if (steward != null && state.Story.delegateId != steward.id) state.ToggleDelegate(steward);
                var report = state.EndMonth();
                ui.Refresh(state);
                ui.RevealApSourcesForCapture();
                Debug.Log($"[Capture] V1126E_11 after month AP {state.ActionPoints}/{state.ApMax} | {report.Lines.LastOrDefault(l => l.StartsWith("行動力"))}");
            }),
        };

        private Action<string> v1126eInspect;

        /// <summary>A fresh courtyard (prologue defaults), optionally with one creation talent applied as StartStory does.</summary>
        private void V1126eCourtyard(string talent)
        {
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
            next.Protagonist.attrs = new[] { 12, 12, 13, 14, 11, 10, 10 };
            var option = string.IsNullOrEmpty(talent) ? null : (M2Data.Load<TalentCatalog>("talents").talents ?? Array.Empty<CreationOption>())
                .FirstOrDefault(entry => entry?.id == talent);
            if (option != null)
            {
                next.Protagonist.talents = new[] { option.id };
                next.ApplyEffects(option.effects);
            }
            next.RefillAp();
            EnterCourtyard(next, false);
            controls.enabled = false;
            courtyardReadyForPc3 = false;
        }
    }

    public sealed partial class SectUI
    {
        /// <summary>v1126e capture: hovers a derived cell (「衍生_外防」…) on the open character page.</summary>
        internal bool RevealModalDerivedTipForCapture(string key)
        {
            var cell = modal == null ? null : modal.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == "衍生_" + key && rect.GetComponent<EventTrigger>() != null);
            var enter = cell?.GetComponent<EventTrigger>().triggers.FirstOrDefault(entry => entry.eventID == EventTriggerType.PointerEnter);
            enter?.callback.Invoke(new PointerEventData(EventSystem.current));
            return enter != null;
        }

        /// <summary>v1126e capture: opens the 行動力 breakdown as a hover would.</summary>
        internal void RevealApSourcesForCapture() => apHud?.OnPointerEnter(new PointerEventData(EventSystem.current));

    }
}
