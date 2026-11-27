using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    /// <summary>
    /// v1126e CL2 capture shots for items 2–4 (muted, part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly V1126E2_",
    /// or "-wuxiaCaptureOnly V1126E" for all six items): #2 the settlement map at 太白山 and 河套渠田 (plates moved clear of
    /// the flags with leader lines, the nearby list filled); #3 the talent tip on a recruit card chip (click), the recruit
    /// table's 天賦 cell (hover) and a picked creation talent (hover); #4 a courtyard of twelve people spread out.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] V1126eCl2CaptureSteps() => new (string, Action)[]
        {
            ("V1126E2_01_sitemap_taibai_nearby.png", () =>
            {
                ui.CloseTitle();
                ui.CloseStoryBeat();
                ui.CloseModal(force: true);
                ui.CloseBattle();
                pc3Flow = null;
                Pc3Flow().CapturePc3Site("taibai");
                Debug.Log("[Capture] V1126E2_01 太白山 nearby rows: " + ui.SiteNearbyRowsForCapture());
            }),
            ("V1126E2_02_sitemap_hetao.png", () =>
            {
                Debug.Log("[Capture] V1126E2_02 plates after the taibai frame: " + ui.SitePlatesForCapture());
                Pc3Flow().CapturePc3Site("hetao");
                Debug.Log("[Capture] V1126E2_02 河套渠田 nearby rows: " + ui.SiteNearbyRowsForCapture());
            }),
            ("V1126E2_03_sitemap_yanzhou.png", () =>
            {
                Debug.Log("[Capture] V1126E2_03 plates after the hetao frame: " + ui.SitePlatesForCapture());
                Pc3Flow().CapturePc3Site("yanzhou");
                Debug.Log("[Capture] V1126E2_03 延州清涼山 nearby rows: " + ui.SiteNearbyRowsForCapture());
            }),
            ("V1126E2_04_creation_picked_talent_tip.png", () =>
            {
                Debug.Log("[Capture] V1126E2_04 plates after the yanzhou frame: " + ui.SitePlatesForCapture());
                ui.CloseModal(force: true);
                pc3Flow = null;
                Pc3Flow().CaptureCreationTalents("scholar_family", true);
                var header = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .FirstOrDefault(button => button.name == "已選收合" && button.isActiveAndEnabled);
                header?.onClick.Invoke();
                Debug.Log("[Capture] V1126E2_04 picks open=" + (header != null) + " tip: " + ui.RevealTalentTipForCapture());
            }),
            ("V1126E2_05_recruit_table_talent_tip.png", () =>
            {
                ui.CloseModal(force: true);
                V1126eCourtyard(null);
                CaptureRecruitment();
                var shown = candidates?.FirstOrDefault(candidate => candidate.talents?.Length > 0);
                if (shown != null) shown.talents = new[] { "broad_qi", "pure_forge", "weighted_strike" };
                ShowCandidates();
                Debug.Log("[Capture] V1126E2_05 table tip: " + ui.RevealTalentTipForCapture("氣海如淵"));
            }),
            ("V1126E2_06_recruit_card_talent_click.png", () =>
            {
                var shown = candidates?.FirstOrDefault(candidate => candidate.talents?.Contains("broad_qi") == true)
                    ?? candidates?.FirstOrDefault();
                if (shown != null) ReviewCandidate(shown);
                Debug.Log("[Capture] V1126E2_06 card click tip: " + ui.RevealTalentTipForCapture("氣海如淵", click: true));
            }),
            ("V1126E2_07_recruit_card_talent_hover.png", () =>
                Debug.Log("[Capture] V1126E2_07 card hover tip: " + ui.RevealTalentTipForCapture("擰腕沉肩"))),
            ("V1126E2_07b_dossier_talent_tip.png", () =>
            {
                ui.CloseCreation();
                ui.CloseModal(force: true);
                state.Protagonist.talents = new[] { "broad_qi", "pure_forge", "weighted_strike" };
                ShowCharacterDirectory(1, HeroCharacterId);
                Debug.Log("[Capture] V1126E2_07b dossier tip: " + ui.RevealTalentTipForCapture("氣海如淵", click: true));
            }),
            ("V1126E2_08_courtyard_twelve_spread.png", () =>
            {
                ui.CloseCreation();
                ui.CloseModal(force: true);
                V1126eCourtyard(null);
                CaptureRecruitment();
                ui.CloseCreation();
                var method = Recruitment.methods?.FirstOrDefault() ?? new RecruitMethod { id = "public", name = "公開招徒" };
                for (int guard = 0; state.Disciples.Count < 12 && guard < 20; guard++)
                {
                    var extra = RollCandidate(method);
                    extra.title ??= "入門弟子";
                    extra.trait ??= extra.personality;
                    state.EnsurePortrait(extra);
                    state.Disciples.Add(extra);
                    world.SpawnDisciple(extra);
                }
                var (count, gap, atWork) = world.SettleWalkersForCapture();
                ui.Refresh(state);
                Debug.Log($"[Capture] V1126E2_08 courtyard people={count} disciples={state.Disciples.Count} smallest ellipse gap={gap:0.00} (≥1 = apart) at their work building={atWork}");
            }),
            ("V1126E2_09_courtyard_after_walking.png", () =>
                Debug.Log($"[Capture] V1126E2_09 smallest ellipse gap while walking={DiscipleWalker.SmallestGap():0.00}")),
        };
    }

    public sealed partial class SectUI
    {
        /// <summary>v1126e capture: the nearby rows on the open settlement detail.</summary>
        internal string SiteNearbyRowsForCapture()
        {
            var rows = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .Where(text => text.name.StartsWith("附近_", StringComparison.Ordinal) && text.gameObject.activeInHierarchy)
                .Select(text => text.text).ToArray();
            return rows.Length == 0 ? "none" : $"{rows.Length}: " + string.Join("／", rows);
        }

        /// <summary>v1126e capture: what the leader-line guard did on the last drawn map.</summary>
        internal string SitePlatesForCapture()
        {
            var guard = UnityEngine.Object.FindObjectsByType<SiteLabelLeaders>(FindObjectsSortMode.None).FirstOrDefault();
            return guard == null ? "no map" : $"moved={guard.Moved} touching={guard.Overlaps}";
        }
    }
}
