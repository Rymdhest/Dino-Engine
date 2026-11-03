using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>
    /// Browses the ECS world: archetypes (filterable by component name) with entity counts, the entities of the
    /// selected archetype, and the live field values of every component on the selected entity.
    /// </summary>
    internal sealed class EntityInspectorController
    {
        private const float ArchetypeRefreshInterval = 1f;
        private const float ComponentRefreshInterval = 0.2f;
        private const int MaxListedEntities = 500;

        private readonly TextBox _filter;
        private readonly Checkbox _live;
        private readonly Label _summary;
        private readonly ScrollPanel _archetypeList;
        private readonly Dropdown _entityPicker;
        private readonly ScrollPanel _componentView;
        private readonly List<Entity> _pickerEntities = new();
        private readonly List<(Label label, int componentId, FieldInfo field)> _fieldLabels = new();

        private Archetype? _selectedArchetype;
        private Entity? _selectedEntity;
        private BitMask? _shownMask;
        private float _archetypeTimer = ArchetypeRefreshInterval;
        private float _componentTimer;

        public GuiWindow Window { get; }

        public EntityInspectorController()
        {
            GuiTheme theme = GuiContext.DefaultTheme;
            Window = new GuiWindow("Entities", 440f, 600f) { Anchor = Anchor.TopRight, X = 384f, Y = 56f };

            var toolbar = Window.Content.Add(DebugUi.Row());
            _filter = toolbar.Add(new TextBox("", "Filter by component name...") { Width = UIValue.Fill });
            _filter.TextChanged += _ => RefreshArchetypes();
            _live = toolbar.Add(new Checkbox("Live", true) { Tooltip = "Refresh archetypes every second and component values five times per second" });
            toolbar.Add(new Button("Refresh", RefreshArchetypes));

            _summary = Window.Content.Add(DebugUi.Muted(""));

            _archetypeList = Window.Content.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.FillWeight(1f), BackgroundColor = theme.InputBackground.WithAlpha(0.5f), CornerRadius = theme.CornerRadius, Padding = 2f });
            ((StackLayout)_archetypeList.Content.Layout!).Spacing = 1f;

            var entityRow = Window.Content.Add(DebugUi.Row());
            entityRow.Add(new Label("Entity") { Color = theme.TextMuted, FontSize = theme.SmallFontSize + 1f });
            _entityPicker = entityRow.Add(new Dropdown { Width = UIValue.Fill, Placeholder = "Select an archetype above", MaxVisibleItems = 14 });
            _entityPicker.SelectionChanged += index =>
            {
                _selectedEntity = index >= 0 && index < _pickerEntities.Count ? _pickerEntities[index] : null;
                RebuildComponents();
            };

            _componentView = Window.Content.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.FillWeight(1.4f) });

            Window.Updated += Update;
        }

        private void Update(float deltaTime)
        {
            _archetypeTimer += deltaTime;
            _componentTimer += deltaTime;
            if (_live.Checked && _archetypeTimer >= ArchetypeRefreshInterval) RefreshArchetypes();
            if (_live.Checked && _componentTimer >= ComponentRefreshInterval) RefreshComponentValues();
        }

        // ---------------------------------------------------------------- archetypes

        private static string ComponentName(int componentId)
        {
            string name = ComponentTypeRegistry.GetType(componentId).Name;
            return name.EndsWith("Component") && name.Length > "Component".Length ? name[..^"Component".Length] : name;
        }

        private void RefreshArchetypes()
        {
            _archetypeTimer = 0f;
            var world = Engine.Instance?.world;
            if (world == null) return;

            string filter = _filter.Text.Trim();
            var entries = new List<(Archetype archetype, string names)>();
            int listedEntities = 0;
            foreach (var archetype in world.Archetypes)
            {
                if (archetype.EntityCount == 0) continue;
                var names = archetype.Mask.GetSetBits().Select(ComponentName).OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (filter.Length > 0 && !names.Any(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase))) continue;
                entries.Add((archetype, string.Join(", ", names)));
                listedEntities += archetype.EntityCount;
            }
            entries.Sort((a, b) => b.archetype.EntityCount.CompareTo(a.archetype.EntityCount));
            _summary.Text = DebugUi.Inv($"{entries.Count} archetypes, {listedEntities:N0} entities shown ({world.Count:N0} total)");

            var content = _archetypeList.Content;
            while (content.Children.Count < entries.Count) content.Add(new ArchetypeRow(this));
            while (content.Children.Count > entries.Count) content.Remove(content.Children[^1]);
            for (int i = 0; i < entries.Count; i++)
            {
                var row = (ArchetypeRow)content.Children[i];
                row.Archetype = entries[i].archetype;
                row.Label.Text = DebugUi.Inv($"{entries[i].archetype.EntityCount,7:N0}  {entries[i].names}");
            }
        }

        internal bool IsSelected(Archetype? archetype) => archetype != null && archetype == _selectedArchetype;

        internal void SelectArchetype(Archetype archetype)
        {
            _selectedArchetype = archetype;
            _pickerEntities.Clear();
            int count = Math.Min(archetype.EntityCount, MaxListedEntities);
            for (int i = 0; i < count; i++) _pickerEntities.Add(archetype.entities[i]);
            var items = _pickerEntities.Select(e => $"Entity {e.Id}").ToList();
            if (archetype.EntityCount > MaxListedEntities) _entityPicker.Placeholder = $"First {MaxListedEntities} of {archetype.EntityCount:N0}";
            _entityPicker.SetItems(items, items.Count > 0 ? 0 : -1);
        }

        // ---------------------------------------------------------------- components

        private void RebuildComponents()
        {
            GuiTheme theme = GuiContext.DefaultTheme;
            _fieldLabels.Clear();
            _componentView.Content.ClearChildren();
            _shownMask = null;
            _componentTimer = 0f;

            var world = Engine.Instance?.world;
            if (world == null || _selectedEntity == null) return;
            Entity entity = _selectedEntity.Value;
            if (!world.TryGetEntityLocation(entity, out Archetype archetype, out int index))
            {
                _componentView.Content.Add(DebugUi.Muted($"Entity {entity.Id} no longer exists."));
                return;
            }
            _shownMask = archetype.Mask;

            foreach (int componentId in archetype.Mask.GetSetBits())
            {
                Type type = ComponentTypeRegistry.GetType(componentId);
                var foldout = _componentView.Content.Add(new Foldout(type.Name));
                ((StackLayout)foldout.Content.Layout!).Spacing = 1f;
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fields.Length == 0)
                {
                    foldout.Content.Add(DebugUi.Muted("(tag, no data)"));
                    continue;
                }
                foreach (var field in fields)
                {
                    var grid = foldout.Content.Add(DebugUi.KeyValueGrid(0.36f));
                    grid.Add(new Label(CleanFieldName(field.Name)) { Color = theme.TextMuted, FontSize = theme.SmallFontSize, Wrap = true, Width = UIValue.Fill, Tooltip = field.FieldType.Name });
                    var valueLabel = grid.Add(new Label("") { Font = theme.MonoFont, FontSize = theme.SmallFontSize, Wrap = true, Width = UIValue.Fill });
                    _fieldLabels.Add((valueLabel, componentId, field));
                }
            }
            RefreshComponentValues();
        }

        private void RefreshComponentValues()
        {
            _componentTimer = 0f;
            var world = Engine.Instance?.world;
            if (world == null || _selectedEntity == null) return;
            if (!world.TryGetEntityLocation(_selectedEntity.Value, out Archetype archetype, out int index))
            {
                if (_shownMask != null) RebuildComponents();
                return;
            }
            if (_shownMask == null || !_shownMask.Value.Equals(archetype.Mask))
            {
                RebuildComponents();
                return;
            }
            foreach (var (label, componentId, field) in _fieldLabels)
            {
                object? component = ((IList)archetype.ComponentArrays[componentId])[index];
                label.Text = FormatValue(component == null ? null : field.GetValue(component));
            }
        }

        private static string CleanFieldName(string name)
        {
            // Auto-property backing fields look like "<Value>k__BackingField".
            if (name.StartsWith('<'))
            {
                int end = name.IndexOf('>');
                if (end > 1) return name.Substring(1, end - 1);
            }
            return name;
        }

        private static string FormatValue(object? value)
        {
            string text = value switch
            {
                null => "null",
                float f => f.ToString("0.####", CultureInfo.InvariantCulture),
                double d => d.ToString("0.####", CultureInfo.InvariantCulture),
                Vector2 v => DebugUi.Inv($"({v.X:0.###}, {v.Y:0.###})"),
                Vector3 v => DebugUi.Inv($"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###})"),
                Vector4 v => DebugUi.Inv($"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###}, {v.W:0.###})"),
                Quaternion q => DebugUi.Inv($"({q.X:0.###}, {q.Y:0.###}, {q.Z:0.###}, {q.W:0.###})"),
                Matrix4 m => $"{FormatRow(m.Row0)}\n{FormatRow(m.Row1)}\n{FormatRow(m.Row2)}\n{FormatRow(m.Row3)}",
                string s => $"\"{s}\"",
                Array a => $"{a.GetType().GetElementType()?.Name}[{a.Length}]",
                ICollection c => $"{value.GetType().Name} (Count = {c.Count})",
                IFormattable formattable => formattable.ToString(null, DebugUi.Culture),
                _ => value.ToString() ?? "",
            };
            return text.Length > 400 ? text.Substring(0, 400) + "..." : text;
        }

        private static string FormatRow(Vector4 v) => DebugUi.Inv($"{v.X,8:0.###} {v.Y,8:0.###} {v.Z,8:0.###} {v.W,8:0.###}");

        private sealed class ArchetypeRow : GuiElement
        {
            private readonly EntityInspectorController _owner;

            public Archetype? Archetype { get; set; }
            public Label Label { get; }

            public ArchetypeRow(EntityInspectorController owner)
            {
                GuiTheme theme = GuiContext.DefaultTheme;
                _owner = owner;
                CapturesMouse = true;
                Cursor = MouseCursor.Hand;
                Width = UIValue.Fill;
                Padding = new Thickness(6f, 3f);
                CornerRadius = 3f;
                Label = Add(new Label("") { Width = UIValue.Fill, Wrap = true, Font = theme.MonoFont, FontSize = theme.SmallFontSize, HitTest = HitTestMode.None });
            }

            protected override void OnClick(GuiMouseEventArgs e)
            {
                if (e.Button == MouseButton.Left && Archetype != null) _owner.SelectArchetype(Archetype);
            }

            protected override void OnDraw(GuiDrawList drawList)
            {
                GuiTheme theme = Theme;
                if (_owner.IsSelected(Archetype)) drawList.AddRect(Bounds, theme.Selection, CornerRadius);
                else if (IsHovered) drawList.AddRect(Bounds, theme.Highlight, CornerRadius);
            }
        }
    }
}
