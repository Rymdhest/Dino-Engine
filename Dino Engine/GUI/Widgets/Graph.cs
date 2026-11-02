using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Widgets
{
    public enum GraphStyle
    {
        Bars,
        Line,
    }

    /// <summary>
    /// Rolling history graph (frame times, memory, any per-frame value). Push one sample per frame.
    /// The vertical range adapts to the data unless <see cref="FixedMax"/> is set; reference lines mark budgets such as 16.6 ms.
    /// </summary>
    public class Graph : GuiElement
    {
        private float[] _samples;
        private int _head;
        private int _count;
        private float _displayMax = 1f;
        private readonly List<(float value, Colour colour)> _referenceLines = new();
        private readonly List<Vector2> _points = new();

        public GraphStyle Style { get; set; } = GraphStyle.Bars;
        public Colour LineColor { get; set; }
        /// <summary>Samples above this value are drawn in <see cref="WarningColor"/>. 0 disables.</summary>
        public float WarningThreshold { get; set; }
        public Colour WarningColor { get; set; }
        public float? FixedMax { get; set; }
        /// <summary>Smallest automatic range, so a flat signal does not fill the whole height.</summary>
        public float MinimumRange { get; set; } = 1f;
        public string Title { get; set; } = "";
        public string ValueFormat { get; set; } = "0.00";
        public string Unit { get; set; } = "";
        /// <summary>Culture used to format numbers; null uses the current culture.</summary>
        public IFormatProvider? FormatProvider { get; set; }

        public Graph(int capacity = 240)
        {
            GuiTheme theme = Theme;
            _samples = new float[Math.Max(2, capacity)];
            Width = UIValue.Fill;
            Height = 70f;
            BackgroundColor = theme.InputBackground;
            BorderColor = theme.Separator;
            BorderWidth = 1f;
            CornerRadius = theme.CornerRadius;
            LineColor = theme.Accent;
            WarningColor = theme.Warning;
            ClipChildren = true;
        }

        public int Capacity => _samples.Length;
        public int Count => _count;
        public float Latest => _count > 0 ? _samples[(_head - 1 + _samples.Length) % _samples.Length] : 0f;

        public void Push(float value)
        {
            if (!float.IsFinite(value)) value = 0f;
            _samples[_head] = value;
            _head = (_head + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>Gets a sample, 0 = oldest.</summary>
        public float GetSample(int index)
        {
            int start = (_head - _count + _samples.Length) % _samples.Length;
            return _samples[(start + index) % _samples.Length];
        }

        public void AddReferenceLine(float value, Colour colour) => _referenceLines.Add((value, colour));
        public void ClearReferenceLines() => _referenceLines.Clear();

        public float Average()
        {
            if (_count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _count; i++) sum += GetSample(i);
            return sum / _count;
        }

        public float Maximum()
        {
            float max = 0f;
            for (int i = 0; i < _count; i++) max = MathF.Max(max, GetSample(i));
            return max;
        }

        protected override void OnUpdate(float deltaTime)
        {
            // Smoothly adapt the range so the graph does not jump around on every spike.
            float target = FixedMax ?? MathF.Max(MinimumRange, Maximum() * 1.15f);
            float t = 1f - MathF.Exp(-6f * deltaTime);
            _displayMax = target > _displayMax ? target : _displayMax + (target - _displayMax) * t;
        }

        protected override void OnDraw(GuiDrawList drawList)
        {
            base.OnDraw(drawList);
            GuiTheme theme = Theme;
            GuiRect area = Bounds.Deflate(new Thickness(2f));
            if (area.Width <= 0f || area.Height <= 0f) return;
            float max = MathF.Max(0.0001f, FixedMax ?? _displayMax);

            float step = area.Width / _samples.Length;
            float firstX = area.Right - _count * step;
            if (Style == GraphStyle.Bars)
            {
                for (int i = 0; i < _count; i++)
                {
                    float value = GetSample(i);
                    float height = MathF.Min(1f, value / max) * area.Height;
                    if (height <= 0f) continue;
                    float x = firstX + i * step;
                    Colour colour = WarningThreshold > 0f && value > WarningThreshold ? WarningColor : LineColor;
                    drawList.AddRect(GuiRect.FromEdges(x, area.Bottom - height, x + MathF.Max(1f, step - 0.5f), area.Bottom), colour.WithAlpha(0.85f));
                }
            }
            else
            {
                _points.Clear();
                for (int i = 0; i < _count; i++)
                {
                    float value = GetSample(i);
                    _points.Add(new Vector2(firstX + (i + 0.5f) * step, area.Bottom - MathF.Min(1f, value / max) * area.Height));
                }
                drawList.AddPolyline(_points, LineColor, 1.5f);
            }

            foreach (var (value, colour) in _referenceLines)
            {
                if (value > max) continue;
                float y = area.Bottom - value / max * area.Height;
                drawList.AddLine(new Vector2(area.X, y), new Vector2(area.Right, y), colour, 1f);
            }

            if (Context != null)
            {
                string unit = Unit.Length > 0 ? " " + Unit : "";
                string latest = Latest.ToString(ValueFormat, FormatProvider);
                string title = Title.Length > 0 ? $"{Title}  {latest}{unit}" : $"{latest}{unit}";
                GuiRect textArea = area.Deflate(new Thickness(4f, 2f));
                GuiText.Draw(Context, drawList, title, theme.MonoFont, theme.SmallFontSize, textArea, theme.Text, Alignment.Start, Alignment.Start);
                GuiText.Draw(Context, drawList, $"max {max.ToString(ValueFormat, FormatProvider)}{unit}", theme.MonoFont, theme.SmallFontSize, textArea, theme.TextMuted, Alignment.End, Alignment.Start);
            }
        }
    }
}
