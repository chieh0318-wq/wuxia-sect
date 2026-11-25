using System;

namespace Wuxia
{
    /// <summary>
    /// v1126c PF capture shots (muted, part of -wuxiaCapture; run alone with "-wuxiaCaptureOnly PF_"): the creation
    /// 屬性 step with the most talents the budget buys (書香門第, 技藝點 0/12, 屬性點 20/20) collapsed and expanded,
    /// the same with no talent, another background, and the 天賦 step's own drop-down with its 「×」 rows.
    /// Each shot logs the layout check (header one line, list and card clear of 繼續).
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] PfCaptureSteps() => new (string, Action)[]
        {
            ("PF_01_creation_talents_collapsed.png", () =>
            {
                ui.CloseTitle();
                ui.CloseStoryBeat();
                ui.CloseModal(force: true);
                CaptureFlow().CaptureCreationTalents("scholar_family", true);
                ui.LogCreationLayout("PF_01");
            }),
            ("PF_02_creation_talents_expanded.png", () =>
            {
                ui.SetCreationPicksOpen(true);
                ui.LogCreationLayout("PF_02");
            }),
            ("PF_03_creation_talents_zero.png", () =>
            {
                CaptureFlow().CaptureCreationTalents("scholar_family", false);
                ui.LogCreationLayout("PF_03");
            }),
            ("PF_04_other_background_collapsed.png", () =>
            {
                CaptureFlow().CaptureCreationTalents("fubing_orphan", true);
                ui.LogCreationLayout("PF_04");
            }),
            ("PF_05_other_background_expanded.png", () =>
            {
                ui.SetCreationPicksOpen(true);
                ui.LogCreationLayout("PF_05");
            }),
            ("PF_06_talent_step_expanded.png", () =>
            {
                CaptureFlow().CaptureTalentsStepFull();
                ui.SetCreationPicksOpen(true);
                ui.LogCreationLayout("PF_06");
            }),
            ("PF_07_talent_step_collapsed.png", () =>
            {
                ui.SetCreationPicksOpen(false);
                ui.LogCreationLayout("PF_07");
            }),
        };
    }
}
