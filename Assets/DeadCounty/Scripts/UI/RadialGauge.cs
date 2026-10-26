using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.UI
{
    // Ring that fills clockwise from the top with Value (0 to 1), without numbers. Colours and thickness come
    // from USS custom properties: --gauge-track, --gauge-fill, --gauge-thickness.
    [UxmlElement]
    public partial class RadialGauge : VisualElement
    {
        // Smaller changes are not worth a repaint.
        const float RepaintStep = 0.002f;

        static readonly CustomStyleProperty<Color> TrackProperty = new("--gauge-track");
        static readonly CustomStyleProperty<Color> FillProperty = new("--gauge-fill");
        static readonly CustomStyleProperty<float> ThicknessProperty = new("--gauge-thickness");

        float value = 1f;
        Color track = new(0f, 0f, 0f, 0.5f);
        Color fill = Color.white;
        float thickness = 4f;

        public RadialGauge()
        {
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        [UxmlAttribute]
        public float Value
        {
            get => value;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Abs(clamped - this.value) < RepaintStep && clamped is > 0f and < 1f) return;
                this.value = clamped;
                MarkDirtyRepaint();
            }
        }

        void OnStyleResolved(CustomStyleResolvedEvent e)
        {
            if (e.customStyle.TryGetValue(TrackProperty, out var t)) track = t;
            if (e.customStyle.TryGetValue(FillProperty, out var f)) fill = f;
            if (e.customStyle.TryGetValue(ThicknessProperty, out var w)) thickness = w;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - thickness * 0.5f;
            if (radius <= 0f) return;
            var painter = context.painter2D;
            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Butt;
            painter.strokeColor = track;
            painter.BeginPath();
            painter.Arc(rect.center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Stroke();
            if (value <= 0f) return;
            painter.strokeColor = fill;
            painter.BeginPath();
            painter.Arc(rect.center, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * value));
            painter.Stroke();
        }
    }
}
