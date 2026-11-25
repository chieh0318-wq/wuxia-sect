using UnityEngine;
using UnityEngine.EventSystems;

namespace Wuxia
{
    public sealed partial class GameBootstrap
    {
        private void PreviewCourtyardCells()
        {
            int[] cells = null;
            if (buildChoice != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) &&
                Physics.Raycast(view.ScreenPointToRay(Input.mousePosition), out var hit) &&
                world.TryGetSlot(hit.collider, out int index) && state.CanPlaceInCells(index, buildChoice))
                cells = state.FootprintCells(index, buildChoice);
            world.PreviewCells(cells);
        }
    }
}
