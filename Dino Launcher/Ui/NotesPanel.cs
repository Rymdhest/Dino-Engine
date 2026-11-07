using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Dino_Launcher.Update;

namespace Dino_Launcher.Ui
{
    /// <summary>
    /// What changed, however much of it there is. All the releases are one page to read down, with the one being read named at the top
    /// and a list of them beside it to jump around in, so a big patch costs a longer page and nothing else. A search cuts the page down to
    /// what's being looked for. All of it is drawn here rather than left to a text box, which can't indent a wrapped line under its own
    /// words or scroll a page of this length by anything but whole lines.
    /// </summary>
    internal sealed class NotesPanel : Control
    {
        private const int RailWidth = 170;
        private const int RailGap = 20;
        private const int SearchHeight = 32;
        private const int SearchInset = 10;
        private const int SearchGap = 8;
        private const int CueBanner = 0x1501;

        private readonly float _scale;
        private readonly TextBox _search;
        private readonly VersionRail _rail;
        private readonly NotesBody _body;
        private NoteSection[] _sections = new NoteSection[0];

        public NotesPanel(float scale)
        {
            _scale = scale;
            BackColor = LauncherWindow.Ink;
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, false);

            // Not a tab stop: Play is what the keyboard should land on, and a search is one click or Ctrl+F away.
            _search = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = LauncherWindow.InkLight,
                ForeColor = LauncherWindow.Parchment,
                Font = new Font("Segoe UI", 10f),
                TabStop = false,
            };
            _search.HandleCreated += (_, __) => SendMessage(_search.Handle, CueBanner, new IntPtr(1), "Search the notes");
            _search.TextChanged += (_, __) => ShowMatching();

            _rail = new VersionRail(scale);
            _body = new NotesBody(scale);
            _rail.Picked += _body.GoTo;
            _body.Reached += _rail.Mark;

            Controls.AddRange(new Control[] { _search, _rail, _body });
        }

        public bool Typing => _search.Focused;

        public void Fill(ReleaseNote[]? notes)
        {
            _sections = PatchNotes.Read(notes);
            ShowMatching();
        }

        public void EndSearch() => _search.Clear();

        private void ShowMatching()
        {
            NoteSection[] shown = PatchNotes.Matching(_sections, _search.Text);
            _rail.Fill(shown);
            _body.Fill(shown, searching: _search.TextLength > 0);
        }

        /// <summary>What the mouse wheel does anywhere in the window that has no scrolling of its own.</summary>
        public void Wheel(int delta) => _body.Wheel(delta);

        /// <summary>True for the keys that belong to the notes, which work wherever the focus is.</summary>
        public bool Press(Keys key)
        {
            switch (key)
            {
                case Keys.Control | Keys.F:
                    _search.Focus();
                    _search.SelectAll();
                    return true;
                case Keys.Up:
                    _body.Lines(-1);
                    return true;
                case Keys.Down:
                    _body.Lines(1);
                    return true;
                case Keys.PageUp:
                    _body.Pages(-1);
                    return true;
                case Keys.PageDown:
                    _body.Pages(1);
                    return true;
                case Keys.Home when !Typing:
                    _body.GoToStart();
                    return true;
                case Keys.End when !Typing:
                    _body.GoToEnd();
                    return true;
                default:
                    return false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Wheel(e.Delta);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int rail = Scaled(RailWidth);
            int box = Scaled(SearchHeight);
            int gap = Scaled(SearchGap);
            int inset = Scaled(SearchInset);

            _search.SetBounds(inset, (box - _search.Height) / 2, rail - inset * 2, _search.Height);
            _rail.Bounds = new Rectangle(0, box + gap, rail, Math.Max(0, Height - box - gap));
            _body.Bounds = new Rectangle(rail + Scaled(RailGap), 0, Math.Max(0, Width - rail - Scaled(RailGap)), Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var field = new SolidBrush(LauncherWindow.InkLight);
            e.Graphics.FillRectangle(field, 0, 0, Scaled(RailWidth), Scaled(SearchHeight));
        }

        private int Scaled(int value) => (int)Math.Round(value * _scale);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);
    }

    /// <summary>
    /// Something tall to move down, with a scroll bar wide enough to find and grab. It glides to where it's told rather than stepping, as
    /// a page that jumps a screen at a time leaves the eye nothing to follow.
    /// </summary>
    internal abstract class ScrollingView : Control
    {
        private const int BarWidth = 10;
        private const int BarGap = 8;
        private const int ShortestThumb = 40;
        private const float GlideSeconds = 0.05f;
        private const float PageShare = 0.9f;

        private readonly float _scale;
        private readonly Timer _glide = new Timer { Interval = 15 };
        private readonly Stopwatch _clock = new Stopwatch();
        private float _offset;
        private float _target;
        private int _content;
        private bool _dragging;
        private bool _overThumb;
        private int _grip;

        protected ScrollingView(float scale)
        {
            _scale = scale;
            BackColor = LauncherWindow.Ink;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            _glide.Tick += (_, __) => Glide();
        }

        protected int Scaled(int value) => (int)Math.Round(value * _scale);

        protected int Offset => (int)Math.Round(_offset);

        protected int Furthest => Math.Max(0, _content - Height);

        /// <summary>What's kept clear of everything else down the right-hand side.</summary>
        protected int BarSpace => Scaled(BarWidth + BarGap);

        protected int Content
        {
            get => _content;
            set
            {
                _content = value;
                Settle();
            }
        }

        /// <summary>How far a line moves it, which is what the mouse wheel goes by.</summary>
        protected abstract int LineHeight { get; }

        protected abstract void PaintContent(Graphics g);

        protected virtual void Scrolled()
        {
        }

        public void Lines(int count) => GlideTo(_target + count * LineHeight);

        public void Pages(int count) => GlideTo(_target + count * Height * PageShare);

        public void GoToStart() => GlideTo(0);

        public void GoToEnd() => GlideTo(Furthest);

        public void Wheel(int delta)
        {
            // Negative means Windows is set to scroll a screen at a time.
            int lines = SystemInformation.MouseWheelScrollLines;
            float notch = lines < 0 ? Height * PageShare : lines * LineHeight;
            GlideTo(_target - delta / 120f * notch);
        }

        protected void GlideTo(float place)
        {
            _target = Clamped(place);
            if (_glide.Enabled) return;
            _clock.Restart();
            _glide.Start();
        }

        protected void JumpTo(float place)
        {
            _glide.Stop();
            _offset = _target = Clamped(place);
            Scrolled();
            Invalidate();
        }

        private void Glide()
        {
            float seconds = (float)_clock.Elapsed.TotalSeconds;
            _clock.Restart();
            _offset += (_target - _offset) * (1f - (float)Math.Exp(-seconds / GlideSeconds));
            if (Math.Abs(_target - _offset) < 0.5f)
            {
                _offset = _target;
                _glide.Stop();
            }
            Scrolled();
            Invalidate();
        }

        private float Clamped(float place) => Math.Max(0f, Math.Min(Furthest, place));

        private void Settle()
        {
            _target = Clamped(_target);
            _offset = Clamped(_offset);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Settle();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            PaintContent(e.Graphics);
            if (Furthest == 0) return;

            Rectangle thumb = Thumb();
            using var track = new SolidBrush(Color.FromArgb(24, 20, 16));
            using var grip = new SolidBrush(_dragging ? LauncherWindow.Gold : _overThumb ? LauncherWindow.Parchment : LauncherWindow.Faded);
            e.Graphics.FillRectangle(track, thumb.X, 0, thumb.Width, Height);
            e.Graphics.FillRectangle(grip, thumb);
        }

        private Rectangle Thumb()
        {
            int length = (int)Math.Max(Scaled(ShortestThumb), (long)Height * Height / Math.Max(1, _content));
            int top = Furthest == 0 ? 0 : (int)((long)(Height - length) * Offset / Furthest);
            return new Rectangle(Width - Scaled(BarWidth), top, Scaled(BarWidth), length);
        }

        protected bool OverBar(Point place) => Furthest > 0 && place.X >= Width - BarSpace;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !OverBar(e.Location)) return;

            Rectangle thumb = Thumb();
            if (e.Y < thumb.Top) Pages(-1);
            else if (e.Y >= thumb.Bottom) Pages(1);
            else
            {
                _dragging = true;
                _grip = e.Y - thumb.Top;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Rectangle thumb = Thumb();
                JumpTo((float)(e.Y - _grip) / Math.Max(1, Height - thumb.Height) * Furthest);
                return;
            }

            bool over = OverBar(e.Location) && e.Y >= Thumb().Top && e.Y < Thumb().Bottom;
            if (over == _overThumb) return;
            _overThumb = over;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _overThumb = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Furthest == 0) return; // Whatever's behind this may have somewhere to go.
            Wheel(e.Delta);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _glide.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Every release there are notes for, with the one being read marked. Clicking one goes there.</summary>
    internal sealed class VersionRail : ScrollingView
    {
        private const int RowHeight = 34;
        private const int Inset = 10;
        private const int MarkWidth = 3;

        private readonly Font _version = new Font("Segoe UI", 10.5f);
        private readonly Font _count = new Font("Segoe UI", 9f);
        private NoteSection[] _sections = new NoteSection[0];
        private int _marked;
        private int _hovered = -1;

        public VersionRail(float scale) : base(scale)
        {
        }

        public event Action<int>? Picked;

        protected override int LineHeight => Scaled(RowHeight);

        public void Fill(NoteSection[] sections)
        {
            _sections = sections;
            _marked = 0;
            _hovered = -1;
            Content = sections.Length * LineHeight;
            JumpTo(0);
        }

        public void Mark(int section)
        {
            if (section == _marked) return;
            _marked = section;
            Invalidate();

            int top = section * LineHeight;
            if (top < Offset) GlideTo(top);
            else if (top + LineHeight > Offset + Height) GlideTo(top + LineHeight - Height);
        }

        protected override void PaintContent(Graphics g)
        {
            int right = Furthest > 0 ? Width - BarSpace : Width;
            using var picked = new SolidBrush(LauncherWindow.InkLight);
            using var hovered = new SolidBrush(Color.FromArgb(26, 21, 17));
            using var mark = new SolidBrush(LauncherWindow.Gold);

            for (int i = Offset / LineHeight; i < _sections.Length && i * LineHeight < Offset + Height; i++)
            {
                var row = new Rectangle(0, i * LineHeight - Offset, right, LineHeight);
                if (i == _marked)
                {
                    g.FillRectangle(picked, row);
                    g.FillRectangle(mark, row.X, row.Y, Scaled(MarkWidth), row.Height);
                }
                else if (i == _hovered)
                {
                    g.FillRectangle(hovered, row);
                }

                var text = Rectangle.FromLTRB(row.Left + Scaled(Inset), row.Top, row.Right - Scaled(Inset), row.Bottom);
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
                TextRenderer.DrawText(g, _sections[i].Version, _version, text, i == _marked ? LauncherWindow.Gold : LauncherWindow.Parchment, flags);
                TextRenderer.DrawText(g, _sections[i].Changes.Length.ToString(), _count, text, LauncherWindow.Faded, flags | TextFormatFlags.Right);
            }
        }

        private int RowAt(Point place)
        {
            int row = (place.Y + Offset) / LineHeight;
            return OverBar(place) || row >= _sections.Length ? -1 : row;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = RowAt(e.Location);
            Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
            if (row == _hovered) return;
            _hovered = row;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int row = RowAt(e.Location);
            if (e.Button == MouseButtons.Left && row >= 0) Picked?.Invoke(row);
        }
    }

    /// <summary>
    /// The page itself. A line that wraps keeps to the right of its bullet, and the heading of the release being read stays at the top
    /// until the next one pushes it off, so nobody halfway down a long list has to wonder which patch it belongs to.
    /// </summary>
    internal sealed class NotesBody : ScrollingView
    {
        private const TextFormatFlags Wrapped = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private const TextFormatFlags OneLine = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private const int HeadingHeight = 40;
        private const int HeadingRule = 6;
        private const int SectionGap = 16;
        private const int LineGap = 6;
        private const int BulletColumn = 18;
        private const int BulletSize = 5;

        private sealed class Row
        {
            public string Text = "";
            public int Width;
            public int Top;
            public int Height;
        }

        private readonly Font _heading = new Font("Segoe UI", 13f, FontStyle.Bold); // Georgia's figures rise and fall, which a version number can't afford.
        private readonly Font _line = new Font("Segoe UI", 10.5f);
        private readonly Font _count = new Font("Segoe UI", 9f);
        private readonly int _lineHeight;
        private readonly List<Row> _rows = new List<Row>();
        private NoteSection[] _sections = new NoteSection[0];
        private int[] _tops = new int[0];
        private bool _searching;
        private int _reached;

        public NotesBody(float scale) : base(scale)
        {
            _lineHeight = TextRenderer.MeasureText("Ag", _line, Size.Empty, OneLine).Height;
        }

        /// <summary>Says which release is at the top of the page.</summary>
        public event Action<int>? Reached;

        protected override int LineHeight => _lineHeight + Scaled(LineGap);

        private int TextWidth => Math.Max(Scaled(BulletColumn), Width - BarSpace - Scaled(BulletColumn));

        public void Fill(NoteSection[] sections, bool searching)
        {
            _sections = sections;
            _searching = searching;
            _tops = new int[sections.Length];

            _rows.Clear();
            foreach (NoteSection section in sections)
            {
                foreach (string change in section.Changes)
                {
                    _rows.Add(new Row { Text = change, Width = TextRenderer.MeasureText(change, _line, Size.Empty, OneLine).Width });
                }
            }

            LayOut();
            _reached = 0;
            JumpTo(0);
        }

        public void GoTo(int section) => GlideTo(_tops[section]);

        /// <summary>Most lines fit, so a window being dragged wider only asks how tall the few that don't are.</summary>
        private void LayOut()
        {
            int width = TextWidth;
            int y = 0;
            int row = 0;
            for (int s = 0; s < _sections.Length; s++)
            {
                _tops[s] = y;
                y += Scaled(HeadingHeight);
                for (int i = 0; i < _sections[s].Changes.Length; i++, row++)
                {
                    Row line = _rows[row];
                    line.Top = y;
                    line.Height = line.Width <= width ? _lineHeight : TextRenderer.MeasureText(line.Text, _line, new Size(width, int.MaxValue), Wrapped).Height;
                    y += line.Height + Scaled(LineGap);
                }
                y += Scaled(SectionGap);
            }

            // Enough below the last release for it to reach the top of the page like the others, or the list beside it could never mark it.
            Content = _tops.Length > 0 ? Math.Max(y, _tops[_tops.Length - 1] + Height) : 0;
        }

        protected override void OnResize(EventArgs e)
        {
            // What's being read stays where it is on the page, however the lines above it wrap now.
            int anchor = FirstRowAt(Offset);
            int into = anchor < _rows.Count ? Offset - _rows[anchor].Top : 0;

            base.OnResize(e);
            if (_sections.Length == 0) return;
            LayOut();
            if (anchor < _rows.Count) JumpTo(_rows[anchor].Top + into);
        }

        private int FirstRowAt(int y)
        {
            int low = 0;
            int high = _rows.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (_rows[middle].Top + _rows[middle].Height <= y) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private int SectionAt(int y)
        {
            int found = Array.BinarySearch(_tops, y);
            return Math.Max(0, found >= 0 ? found : ~found - 1);
        }

        protected override void Scrolled()
        {
            if (_tops.Length == 0) return;
            int section = SectionAt(Offset);
            if (section == _reached) return;
            _reached = section;
            Reached?.Invoke(section);
        }

        protected override void PaintContent(Graphics g)
        {
            if (_sections.Length == 0)
            {
                if (_searching) TextRenderer.DrawText(g, "No changes match that.", _line, new Point(0, Scaled(HeadingRule)), LauncherWindow.Faded, OneLine);
                return;
            }

            int top = Offset;
            int bottom = top + Height;
            int bullet = Scaled(BulletSize);
            int column = Scaled(BulletColumn);
            int width = TextWidth;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var dot = new SolidBrush(LauncherWindow.Gold);
            for (int i = FirstRowAt(top); i < _rows.Count && _rows[i].Top < bottom; i++)
            {
                Row line = _rows[i];
                int y = line.Top - top;
                g.FillEllipse(dot, Scaled(3), y + (_lineHeight - bullet) / 2, bullet, bullet);
                TextRenderer.DrawText(g, line.Text, _line, new Rectangle(column, y, width, line.Height), LauncherWindow.Parchment, Wrapped);
            }
            g.SmoothingMode = SmoothingMode.None;

            int reading = SectionAt(top);
            for (int s = reading + 1; s < _sections.Length && _tops[s] < bottom; s++) PaintHeading(g, s, _tops[s] - top);

            int height = Scaled(HeadingHeight);
            int pushed = reading + 1 < _tops.Length ? _tops[reading + 1] - top - height : 0;
            PaintHeading(g, reading, Math.Min(0, pushed));
        }

        private void PaintHeading(Graphics g, int section, int y)
        {
            int height = Scaled(HeadingHeight);
            int right = Width - BarSpace;
            int rule = y + height - Scaled(HeadingRule);
            int count = _sections[section].Changes.Length;

            using var ground = new SolidBrush(BackColor);
            using var line = new Pen(LauncherWindow.InkLight);
            g.FillRectangle(ground, 0, y, right, height);
            g.DrawLine(line, 0, rule, right, rule);

            string version = _sections[section].Version;
            int across = TextRenderer.MeasureText(version, _heading, Size.Empty, OneLine).Width;
            var text = Rectangle.FromLTRB(0, y, right, rule);
            TextRenderer.DrawText(g, version, _heading, text, LauncherWindow.Gold, OneLine | TextFormatFlags.VerticalCenter);

            text.X += across + Scaled(12);
            text.Width -= across + Scaled(12);
            TextRenderer.DrawText(g, count == 1 ? "1 change" : $"{count} changes", _count, text, LauncherWindow.Faded, OneLine | TextFormatFlags.VerticalCenter);
        }
    }
}
