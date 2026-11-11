using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.UI
{
    // Arc at the top of a square element, which the HUD rotates about the screen centre toward where a hit came
    // from: up is ahead of the camera, down is behind. Its colour is the element's USS color.
    public class HitMarker : VisualElement
    {
        const float HalfSpread = 24f;
        const float Thickness = 9f;

        public HitMarker()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hit-marker");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - Thickness;
            if (radius <= 0f) return;
            var painter = context.painter2D;
            painter.lineWidth = Thickness;
            painter.lineCap = LineCap.Round;
            painter.strokeColor = resolvedStyle.color;
            painter.BeginPath();
            painter.Arc(rect.center, radius, Angle.Degrees(-90f - HalfSpread), Angle.Degrees(-90f + HalfSpread));
            painter.Stroke();
        }
    }
}
