using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] Pg3CaptureSteps() => new (string, Action)[]
        {
            ("PG3_06_huayue_sword_hall.png", () =>
            {
                ui.CloseTitle();
                var beat = M2Data.Load<PrologueData>("prologue").disasters
                    .SelectMany(disaster => disaster.beats)
                    .Single(part => part.image == "cg_elder_hall");
                ui.ShowStoryBeat(beat.speaker, beat.text, beat.image, beat.portrait, true,
                    () => { }, () => { }, speakerOnRight: true);
            }),
            ("PG3_24_xie_yun_dialogue.png", () =>
            {
                EnsureCaptureCourtyard();
                state.Protagonist.gender = "男";
                state.Protagonist.lookId = state.Protagonist.portrait = "hero_m1";
                var line = state.Questlines.Single(entry => entry.id == "q_friend");
                var beat = line.beats.Single(entry => entry.id == "q_friend_1");
                PlayQuest(line, beat);
                int target = Array.FindIndex(beat.scene, part => part.text.StartsWith("我選哪條", StringComparison.Ordinal));
                if (target < 0) throw new InvalidOperationException("Missing Xie Yun dialogue beat.");
                for (int i = 0; i < target; i++)
                {
                    ui.RevealStoryForCapture();
                    if (!CaptureClick("Dialogue")) throw new InvalidOperationException("Cannot advance Xie Yun dialogue.");
                }
            }),
            ("PG3_24_xie_yun_card.png", () =>
            {
                ui.CloseStoryBeat();
                ShowCharacterDetail(StoryCharacters.Single(person => person.id == "friend_f"), ShowCharacterDirectory);
            }),
            ("PG3_25_wounded_swordsman.png", () =>
            {
                EnsureCaptureCourtyard();
                PlayRoadEvent(RealmContent.Data.roadEvents.Single(entry => entry.id == "road_wounded_swordsman"), "", false);
            }),
            ("PG3_25_wounded_swordsman_choices.png", () => CaptureClickThrough(1)),
        };
    }
}
