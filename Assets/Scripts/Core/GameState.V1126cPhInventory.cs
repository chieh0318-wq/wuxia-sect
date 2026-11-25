using System;
using System.Linq;

namespace Wuxia
{
    public sealed partial class GameState
    {
        internal const string StoredManualPrefix = "manual:";
        internal static readonly (string id, int hp, int qi, int injury)[] SectMedicines =
        {
            ("item_jiuzhuan", 120, 60, 99),
            ("item_jinchuang", 90, 0, 1),
        };

        internal string StoreInventoryGear(GearItem item)
        {
            Story.armoury = (Story.armoury ?? Array.Empty<GearItem>()).Append(item).ToArray();
            return $"【{item.grade}】{item.name}收入庫房。";
        }

        internal string StoreRealmManual(string artId)
        {
            var art = MartialArts.Find(artId);
            if (art == null) return "";
            LegacySystem.AddInventory(Story, StoredManualPrefix + artId, 1);
            return $"《{art.name}》秘笈 ×1 收入庫房。";
        }

        internal string AssignStoredManual(string memberId, string artId)
        {
            string key = StoredManualPrefix + artId;
            if (LegacySystem.GetInventory(Story, key) <= 0 || MartialArts.Find(artId) == null) return "";
            var member = Disciples.Find(entry => entry.id == memberId);
            if (memberId != HeroId && member == null) return "";
            if (memberId == HeroId ? KnowsArt(artId) : ArtsOf(member).Any(art => art.id == artId))
                return "此人已習得此藝，秘笈仍存庫房。";
            if (!GrantManual(memberId, artId)) return "此人已習得此藝，秘笈仍存庫房。";
            LegacySystem.AddInventory(Story, key, -1);
            return $"習得《{MartialArts.Find(artId).name}》";
        }
    }
}
