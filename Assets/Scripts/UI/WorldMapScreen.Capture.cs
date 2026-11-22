using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wuxia
{
    public sealed partial class WorldMapScreen
    {
        internal IEnumerator VerifyCaptureInteractions(GameState check)
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
            check.PendingReports.Clear();
            int expectedDays = Stamp(check)+trip.route.Days;
            int expectedMonths = (check.Day+trip.route.Days-1)/GameState.DaysPerMonth;
            FindCardButton("確認啟程")?.onClick.Invoke();
            float timeout = Time.realtimeSinceStartup + 2 + trip.route.Nodes.Count;
            while (traveling && Time.realtimeSinceStartup < timeout) yield return null;
            if (traveling || check.PartyNode != trip.node.id || Stamp(check) != expectedDays ||
                check.PendingReports.Count != expectedMonths || (expectedMonths == 0 && check.ActionPoints != expectedAp))
            {
                Debug.LogError("[Capture] Travel failed to reach the destination or update AP and date.");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("[Capture] Map interactions verified: scouting, route confirmation, AP, elapsed days and arrival.");
        }

        /// <summary>Opens the card of the outlaw stronghold nearest the party (for the 46 shot).</summary>
        internal bool CaptureOutlaw()
        {
            CloseIntel();
            var target = OutlawNodes().OrderBy(node => data.Route(host.PartyNode, node.id)?.Days ?? 999).FirstOrDefault();
            if (target == null) return false;
            CenterOn(target.id);
            ShowNode(target);
            return true;
        }

        private System.Collections.Generic.IEnumerable<MapNode> OutlawNodes() =>
            data.Nodes.Values.Where(node => data.Controllers(node).FirstOrDefault()?.Outlaw == true);

        /// <summary>M6 map checks: no empty node, spread layout, and 率眾攻討 spending AP on a throwaway state.</summary>
        internal void VerifyM6Map(GameState check)
        {
            var empty = data.Nodes.Values.Where(node => !data.Controllers(node).Any()).Select(node => node.id).ToArray();
            float closest = float.MaxValue;
            var nodes = data.Nodes.Values.ToArray();
            for (int i = 0; i < nodes.Length; i++)
                for (int j = i + 1; j < nodes.Length; j++)
                    closest = Mathf.Min(closest, Vector2.Distance(data.Bounds.Uv(nodes[i]), data.Bounds.Uv(nodes[j])));
            var outlaw = OutlawNodes().FirstOrDefault();
            string result = "";
            if (outlaw != null)
            {
                check.RefillAp();
                int before = check.ActionPoints;
                result = check.Assault(outlaw.id, data.Controllers(outlaw).First().id, 120, .8f);
                if (check.ActionPoints != before - 2)
                    Debug.LogError("[Capture] M6 assault did not spend 2 AP.");
            }
            if (empty.Length > 0 || closest < .075f)
                Debug.LogError($"[Capture] M6 map: empty nodes [{string.Join(",", empty)}], closest pair {closest:0.000}.");
            else
                Debug.Log($"[Capture] M6 map: {nodes.Length} nodes all controlled, closest pair {closest:0.000}, " +
                    $"outlaw holds {OutlawNodes().Count()}. Assault: {result}");
        }

        private static int Stamp(GameState state) => ((state.Year*12)+state.Month-1)*GameState.DaysPerMonth+state.Day;

        private Button FindCardButton(string label) =>
            card.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == label && button.interactable);
    }
}
