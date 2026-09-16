using UnityEngine;
using UnityEngine.UI;

namespace Tuyoo.Game.Demo
{
    // A restrained mint-to-emerald fill, with a subtle vertical bevel.
    internal sealed class KillProgressGradient : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            Rect rect = graphic.rectTransform.rect;
            Color emerald = new Color(0.10f, 0.53f, 0.37f);
            Color mint = new Color(0.43f, 0.82f, 0.60f);
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                float x = Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x);
                float y = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
                Color tint = Color.Lerp(mint, emerald, x);
                tint = Color.Lerp(tint * new Color(0.86f, 0.86f, 0.86f, 1f), tint, y);
                tint.a = vertex.color.a / 255f;
                vertex.color = tint;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
