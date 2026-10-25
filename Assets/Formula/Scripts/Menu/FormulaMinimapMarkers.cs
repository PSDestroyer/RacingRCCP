using UnityEngine;

namespace FormulaGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormulaMinimapMarkers : UnityEngine.UI.MaskableGraphic
    {
        private struct Marker { public Vector2 position, forward; }
        private Marker[] positions = new Marker[9];
        private int count;
        private FormulaRaceMinimap map;
        private GamePlayManager game;
        public Vector2 PlayerMapPosition { get; private set; }
        public int OpponentMarkerCount => Mathf.Max(0, count - 1);
        public void Bind(FormulaRaceMinimap circuit, GamePlayManager race)
        {
            map = circuit; game = race; raycastTarget = false; Refresh();
        }
        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            if (map == null || game == null || game.CarController == null) return;
            int capacity = (game.aiRacers == null ? 0 : game.aiRacers.Length) + 1;
            if (positions.Length < capacity) System.Array.Resize(ref positions, capacity);
            int previous = count; bool dirty = false; count = 0;
            if (game.aiRacers != null)
                foreach (var opponent in game.aiRacers)
                    if (opponent != null && !opponent.eliminated && opponent.racerTransform != null && opponent.racerTransform.gameObject.activeInHierarchy)
                        Store(opponent.racerTransform, false, ref dirty);
            Store(game.CarController.transform, true, ref dirty);
            PlayerMapPosition = positions[count - 1].position;
            if (dirty || count != previous) SetVerticesDirty();
        }
        private void Store(Transform target, bool player, ref bool dirty)
        {
            Vector2 position = map.ClampMarker(map.WorldToMap(target.position));
            Vector3 heading = target.forward;
            Vector2 forward = player ? new Vector2(heading.x, heading.z).normalized : Vector2.zero;
            if (player && forward.sqrMagnitude < .1f) forward = Vector2.up;
            if ((positions[count].position - position).sqrMagnitude > .0001f || (positions[count].forward - forward).sqrMagnitude > .0001f)
            {
                dirty = true; positions[count] = new Marker { position = position, forward = forward };
            }
            count++;
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            for (int i = 0; i < count - 1; i++)
            {
                FormulaRaceMinimap.Disc(mesh, positions[i].position, 5, Color.black * color);
                FormulaRaceMinimap.Disc(mesh, positions[i].position, 3.2f, Color.white * color);
            }
            if (count == 0) return;
            var player = positions[count - 1];
            FormulaRaceMinimap.Arrow(mesh, player.position, player.forward, 11, Color.black * color);
            FormulaRaceMinimap.Arrow(mesh, player.position, player.forward, 8.5f, FormulaUI.Accent * color);
        }
    }
}
