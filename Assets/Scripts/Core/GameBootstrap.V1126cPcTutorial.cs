using System.Linq;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        /// <summary>
        /// v1126c PC3 #15: during the first three months the 軍師 adds one more line after the month report — next
        /// month's AP and its sources, the food and silver balance, and the building worth raising next (all live).
        /// </summary>
        private PortraitLine? FirstMonthsTipLine()
        {
            string tip = TutorialAdvice.FirstMonthsTip(state);
            if (string.IsNullOrEmpty(tip)) return null;
            var friend = StoryCharacters.FirstOrDefault(person => person.id == state.FriendId);
            if (friend == null || AbsenceReason(friend.id) != null)
                return new PortraitLine("旁白", "", null, null, tip);
            return new PortraitLine(friend.name, "軍師・開山提點", friend.portrait ?? friend.id, friend.gender, tip, right: true);
        }
    }
}
