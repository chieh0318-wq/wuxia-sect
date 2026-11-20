using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class WorldMapScreen
    {
        internal IEnumerator VerifyCaptureInteractions(LocalWorldMapHost check)
        {
            if (data.Nodes.Count == 0)
            {
                Debug.LogWarning("[Capture] Map interaction checks skipped because worldmap.json is absent.");
                yield break;
            }

            intel.Close();
            Open(check);
            var unknown = data.Factions.FirstOrDefault(f => !check.IsFactionKnown(f.id));
            if (unknown != null)
            {
                ShowFaction(unknown);
                int oldAp = check.ActionPoints;
                FindCardButton("派人打探　行動力 1")?.onClick.Invoke();
                if (!check.IsFactionKnown(unknown.id) || check.ActionPoints != oldAp-1)
                {
                    Debug.LogError("[Capture] Scouting did not reveal the faction and spend one AP.");
                    Application.Quit(1);
                    yield break;
                }
            }

            var trip = data.Nodes.Values.Select(node => (node, route:data.Route(check.PartyNode,node.id)))
                .Where(entry => entry.route != null && entry.route.Ap <= check.ActionPoints)
                .OrderBy(entry => entry.route.Days).FirstOrDefault();
            if (trip.route == null)
            {
                Debug.LogWarning("[Capture] No route was affordable from the capture start.");
                yield break;
            }

            ShowNode(trip.node);
            int expectedAp = check.ActionPoints-trip.route.Ap;
            int expectedDays = check.ElapsedDays+trip.route.Days;
            FindCardButton("確認啟程")?.onClick.Invoke();
            float timeout = Time.realtimeSinceStartup + 2 + trip.route.Nodes.Count;
            while (traveling && Time.realtimeSinceStartup < timeout) yield return null;
            if (traveling || check.PartyNode != trip.node.id || check.ActionPoints != expectedAp || check.ElapsedDays != expectedDays)
            {
                Debug.LogError("[Capture] Travel failed to reach the destination or update AP and date.");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("[Capture] Map interactions verified: scouting, route confirmation, AP, elapsed days and arrival.");
        }

        private Button FindCardButton(string label) =>
            card.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == label && button.interactable);
    }
}
