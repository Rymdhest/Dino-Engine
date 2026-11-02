using OpenTK.Mathematics;
using System.Buffers;

namespace Dino_Engine.GUI
{
    /// <summary>
    /// Strategy that measures and arranges an element's children. Children that are invisible or have
    /// <see cref="GuiElement.IgnoreLayout"/> set must be skipped (the element places those by anchor).
    /// </summary>
    public abstract class GuiLayout
    {
        /// <summary>Measures all laid out children and returns the content size they need.</summary>
        public abstract Vector2 Measure(GuiElement owner, Vector2 reference, Vector2 available);

        /// <summary>Arranges all laid out children inside the content rectangle.</summary>
        public abstract void Arrange(GuiElement owner, GuiRect content);

        protected static bool IsLaidOut(GuiElement child) => child.Visible && !child.IgnoreLayout;

        /// <summary>
        /// Copies the laid out children into a pooled array (layouts run every frame, so no allocations).
        /// Return it with <see cref="ReturnChildren"/>. Pooled arrays are safe with nested layouts, unlike a shared buffer.
        /// </summary>
        protected static GuiElement[] RentChildren(GuiElement owner, out int count)
        {
            var children = owner.Children;
            GuiElement[] array = ArrayPool<GuiElement>.Shared.Rent(Math.Max(1, children.Count));
            count = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (IsLaidOut(children[i])) array[count++] = children[i];
            }
            return array;
        }

        protected static void ReturnChildren(GuiElement[] array, int count)
        {
            Array.Clear(array, 0, count);
            ArrayPool<GuiElement>.Shared.Return(array);
        }
    }

    /// <summary>
    /// Places children one after another vertically or horizontally.
    /// Main axis: Pixels/Relative/Auto children take their size, Fill children share the remaining space by weight.
    /// Cross axis: Fill stretches, other sizes are aligned with <see cref="CrossAlignment"/>.
    /// </summary>
    public class StackLayout : GuiLayout
    {
        public Orientation Orientation { get; set; }
        public float Spacing { get; set; }
        /// <summary>Alignment of children across the stack direction (when not stretched with Fill).</summary>
        public Alignment CrossAlignment { get; set; } = Alignment.Start;
        /// <summary>Alignment of the whole group along the stack direction when there is spare space and no Fill child.</summary>
        public Alignment MainAlignment { get; set; } = Alignment.Start;

        public StackLayout(Orientation orientation = Orientation.Vertical, float spacing = 0f)
        {
            Orientation = orientation;
            Spacing = spacing;
        }

        private bool Vertical => Orientation == Orientation.Vertical;

        public override Vector2 Measure(GuiElement owner, Vector2 reference, Vector2 available)
        {
            float main = 0f;
            float cross = 0f;
            int count = 0;
            var children = owner.Children;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!IsLaidOut(child)) continue;
                Thickness m = child.Margin;
                // The main axis is unconstrained while measuring; Fill children report their content size.
                Vector2 childAvailable = Vertical
                    ? new Vector2(MathF.Max(0f, available.X - m.Horizontal), float.PositiveInfinity)
                    : new Vector2(float.PositiveInfinity, MathF.Max(0f, available.Y - m.Vertical));
                child.Measure(reference, childAvailable);
                Vector2 size = child.DesiredSize;
                if (Vertical)
                {
                    main += size.Y + m.Vertical;
                    cross = MathF.Max(cross, size.X + m.Horizontal);
                }
                else
                {
                    main += size.X + m.Horizontal;
                    cross = MathF.Max(cross, size.Y + m.Vertical);
                }
                count++;
            }
            if (count > 1) main += Spacing * (count - 1);
            return Vertical ? new Vector2(cross, main) : new Vector2(main, cross);
        }

        public override void Arrange(GuiElement owner, GuiRect content)
        {
            GuiElement[] children = RentChildren(owner, out int count);
            if (count == 0)
            {
                ReturnChildren(children, count);
                return;
            }
            float[] mainSizes = ArrayPool<float>.Shared.Rent(count);
            float[] crossSizes = ArrayPool<float>.Shared.Rent(count);
            try
            {
                ArrangeChildren(children, count, mainSizes, crossSizes, content);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(mainSizes);
                ArrayPool<float>.Shared.Return(crossSizes);
                ReturnChildren(children, count);
            }
        }

        private void ArrangeChildren(GuiElement[] children, int count, float[] mainSizes, float[] crossSizes, GuiRect content)
        {
            float contentMain = Vertical ? content.Height : content.Width;
            float contentCross = Vertical ? content.Width : content.Height;
            float fixedTotal = Spacing * (count - 1);
            float fillWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                var child = children[i];
                Thickness m = child.Margin;
                float marginMain = Vertical ? m.Vertical : m.Horizontal;
                float marginCross = Vertical ? m.Horizontal : m.Vertical;
                UIValue mainValue = Vertical ? child.Height : child.Width;

                // Cross size first (stretch with Fill), then main size (which may depend on aspect ratio).
                Vector2 available = Vertical
                    ? new Vector2(MathF.Max(0f, contentCross - marginCross), float.PositiveInfinity)
                    : new Vector2(float.PositiveInfinity, MathF.Max(0f, contentCross - marginCross));
                Vector2 size = child.ComputeArrangeSize(content.Size, available);
                crossSizes[i] = Vertical ? size.X : size.Y;

                if (mainValue.Mode == SizeMode.Fill)
                {
                    fillWeight += mainValue.Value;
                    mainSizes[i] = float.NaN;
                }
                else
                {
                    mainSizes[i] = Vertical ? size.Y : size.X;
                    fixedTotal += mainSizes[i];
                }
                fixedTotal += marginMain;
            }

            float remaining = MathF.Max(0f, contentMain - fixedTotal);
            float used = fixedTotal;
            for (int i = 0; i < count; i++)
            {
                if (!float.IsNaN(mainSizes[i])) continue;
                var child = children[i];
                float weight = (Vertical ? child.Height : child.Width).Value;
                float share = fillWeight > 0f ? remaining * weight / fillWeight : 0f;
                mainSizes[i] = Vertical ? child.ClampHeight(share) : child.ClampWidth(share);
                used += mainSizes[i];
            }

            float cursor = Vertical ? content.Y : content.X;
            if (fillWeight <= 0f && used < contentMain)
            {
                if (MainAlignment == Alignment.Center) cursor += (contentMain - used) * 0.5f;
                else if (MainAlignment == Alignment.End) cursor += contentMain - used;
            }

            for (int i = 0; i < count; i++)
            {
                var child = children[i];
                Thickness m = child.Margin;
                float crossStart = Vertical ? content.X + m.Left : content.Y + m.Top;
                float crossSpace = contentCross - (Vertical ? m.Horizontal : m.Vertical);
                float crossValue = crossSizes[i];
                UIValue crossMode = Vertical ? child.Width : child.Height;
                if (crossMode.Mode != SizeMode.Fill)
                {
                    if (CrossAlignment == Alignment.Center) crossStart += (crossSpace - crossValue) * 0.5f;
                    else if (CrossAlignment == Alignment.End) crossStart += crossSpace - crossValue;
                }

                if (Vertical)
                {
                    cursor += m.Top;
                    child.Arrange(new GuiRect(crossStart, cursor, crossValue, mainSizes[i]));
                    cursor += mainSizes[i] + m.Bottom + Spacing;
                }
                else
                {
                    cursor += m.Left;
                    child.Arrange(new GuiRect(cursor, crossStart, mainSizes[i], crossValue));
                    cursor += mainSizes[i] + m.Right + Spacing;
                }
            }
        }
    }

    /// <summary>
    /// Places children in rows of <see cref="Columns"/> cells. Column widths come from <see cref="ColumnWeights"/>
    /// (equal by default) and each row is as tall as its tallest child (or <see cref="RowHeight"/> if set).
    /// Children with Fill size stretch to their cell, others are left aligned and vertically centred.
    /// </summary>
    public class GridLayout : GuiLayout
    {
        private int _columns;
        public int Columns { get => _columns; set => _columns = Math.Max(1, value); }
        public float[]? ColumnWeights { get; set; }
        public float ColumnSpacing { get; set; }
        public float RowSpacing { get; set; }
        /// <summary>Fixed row height; 0 sizes rows to their content.</summary>
        public float RowHeight { get; set; }

        public GridLayout(int columns, float columnSpacing = 0f, float rowSpacing = 0f)
        {
            Columns = columns;
            ColumnSpacing = columnSpacing;
            RowSpacing = rowSpacing;
        }

        private float ColumnFraction(int column)
        {
            float total = 0f;
            float weight = 1f;
            for (int c = 0; c < _columns; c++)
            {
                float w = ColumnWeights != null && c < ColumnWeights.Length ? MathF.Max(0f, ColumnWeights[c]) : 1f;
                total += w;
                if (c == column) weight = w;
            }
            return total > 0f ? weight / total : 1f / _columns;
        }

        public override Vector2 Measure(GuiElement owner, Vector2 reference, Vector2 available)
        {
            float spacingTotal = ColumnSpacing * (_columns - 1);
            bool finiteWidth = float.IsFinite(available.X);
            float[] columnMax = ArrayPool<float>.Shared.Rent(_columns);
            Array.Clear(columnMax, 0, _columns);
            float height = 0f;
            float rowHeight = 0f;
            int rows = 0;
            int column = 0;

            var children = owner.Children;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (!IsLaidOut(child)) continue;
                Thickness m = child.Margin;
                float cellWidth = finiteWidth ? MathF.Max(0f, (available.X - spacingTotal) * ColumnFraction(column)) : float.PositiveInfinity;
                float cellHeight = RowHeight > 0f ? RowHeight : float.PositiveInfinity;
                child.Measure(new Vector2(cellWidth, cellHeight), new Vector2(MathF.Max(0f, cellWidth - m.Horizontal), MathF.Max(0f, cellHeight - m.Vertical)));
                columnMax[column] = MathF.Max(columnMax[column], child.DesiredSize.X + m.Horizontal);
                rowHeight = MathF.Max(rowHeight, child.DesiredSize.Y + m.Vertical);

                if (++column == _columns)
                {
                    height += RowHeight > 0f ? RowHeight : rowHeight;
                    rows++;
                    column = 0;
                    rowHeight = 0f;
                }
            }
            if (column > 0)
            {
                height += RowHeight > 0f ? RowHeight : rowHeight;
                rows++;
            }
            if (rows > 1) height += RowSpacing * (rows - 1);

            float width;
            if (finiteWidth) width = available.X;
            else
            {
                // Unconstrained: widest cell per column, scaled so the weights still hold.
                width = 0f;
                for (int c = 0; c < _columns; c++)
                {
                    float fraction = ColumnFraction(c);
                    width = MathF.Max(width, fraction > 0f ? columnMax[c] / fraction : 0f);
                }
                width += spacingTotal;
            }
            ArrayPool<float>.Shared.Return(columnMax);
            return new Vector2(width, height);
        }

        public override void Arrange(GuiElement owner, GuiRect content)
        {
            GuiElement[] children = RentChildren(owner, out int count);
            float[] columnX = ArrayPool<float>.Shared.Rent(_columns);
            float[] columnWidth = ArrayPool<float>.Shared.Rent(_columns);
            try
            {
                float usable = MathF.Max(0f, content.Width - ColumnSpacing * (_columns - 1));
                float x = content.X;
                for (int c = 0; c < _columns; c++)
                {
                    columnX[c] = x;
                    columnWidth[c] = usable * ColumnFraction(c);
                    x += columnWidth[c] + ColumnSpacing;
                }

                float y = content.Y;
                for (int rowStart = 0; rowStart < count; rowStart += _columns)
                {
                    int rowEnd = Math.Min(count, rowStart + _columns);
                    float rowHeight = RowHeight;
                    if (rowHeight <= 0f)
                    {
                        for (int i = rowStart; i < rowEnd; i++) rowHeight = MathF.Max(rowHeight, children[i].DesiredSize.Y + children[i].Margin.Vertical);
                    }

                    for (int i = rowStart; i < rowEnd; i++)
                    {
                        var child = children[i];
                        int c = i - rowStart;
                        Thickness m = child.Margin;
                        Vector2 cell = new Vector2(columnWidth[c], rowHeight);
                        Vector2 size = child.ComputeArrangeSize(cell, new Vector2(MathF.Max(0f, cell.X - m.Horizontal), MathF.Max(0f, cell.Y - m.Vertical)));
                        float childX = columnX[c] + m.Left;
                        float childY = y + m.Top + (child.Height.Mode == SizeMode.Fill ? 0f : (rowHeight - m.Vertical - size.Y) * 0.5f);
                        child.Arrange(new GuiRect(childX, childY, size.X, size.Y));
                    }
                    y += rowHeight + RowSpacing;
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(columnX);
                ArrayPool<float>.Shared.Return(columnWidth);
                ReturnChildren(children, count);
            }
        }
    }
}
