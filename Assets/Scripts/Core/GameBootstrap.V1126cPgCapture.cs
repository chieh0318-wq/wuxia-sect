using System;
using System.Linq;
using System.Collections.Generic;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private (string filename, Action show)[] Pg3CaptureSteps() => Pg3MainArtSteps().Concat(Pg3PrologueArtSteps()).ToArray();

        private (string filename, Action show)[] Pg3MainArtSteps() => new (string, Action)[]
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
            ("PG3_24_xie_yun_dialogue.png", () => Pg3CaptureFriendVow("男", "hero_m1")),
            ("PG3_24_xie_yun_custom_hero.png", () => Pg3CaptureFriendVow("男", "hero_m6")),
            ("PG3_24_xie_jin_custom_hero.png", () => Pg3CaptureFriendVow("女", "hero_f2")),
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
        private void Pg3CaptureFriendVow(string gender, string heroLook)
        {
            EnsureCaptureCourtyard();
            state.Protagonist.gender = gender;
            state.Protagonist.lookId = state.Protagonist.portrait = heroLook;
            var line = state.Questlines.Single(entry => entry.id == "q_friend");
            var beat = line.beats.Single(entry => entry.id == "q_friend_1");
            PlayQuest(line, beat);
            int target = Array.FindIndex(beat.scene, part => part.text.StartsWith("我選哪條", StringComparison.Ordinal));
            if (target < 0) throw new InvalidOperationException("Missing adviser dialogue beat.");
            for (int i = 0; i < target; i++)
            {
                ui.RevealStoryForCapture();
                if (!CaptureClick("Dialogue")) throw new InvalidOperationException("Cannot advance adviser dialogue.");
            }
        }

        private IEnumerable<(string filename, Action show)> Pg3PrologueArtSteps()
        {
            var story = M2Data.Load<PrologueData>("prologue");
            var responseBeats = story.disasters.SelectMany(disaster => disaster.responses)
                .SelectMany(response => response.beats).ToArray();
            foreach (var (prefix, filename) in new[]
            {
                ("少林方丈聽完", "shaolin"),
                ("三更，柴房鎖斷", "betrayal_escape"),
                ("河南府貼出告示", "plague_recruitment"),
                ("駝鈴響起時", "plague_caravan"),
                ("這名聲是拿命換的", "plague_relief"),
            })
            {
                var beat = responseBeats.Single(part => part.text.StartsWith(prefix, StringComparison.Ordinal));
                yield return ($"PG3_25_prologue_{filename}.png", () =>
                {
                    ui.Scene.Close();
                    if (PortraitService.Story(beat.image) == null)
                        throw new InvalidOperationException($"Missing prologue CG: {beat.image}");
                    string portrait = PortraitService.StoryId(beat.portrait, "女");
                    string speaker = beat.speaker == "{friend}" ? "謝蘊" : beat.speaker;
                    ui.ShowStoryBeat(speaker, beat.text, beat.image, portrait, true, () => { }, () => { });
                });
            }
        }

    }
}
