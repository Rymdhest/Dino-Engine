using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>How something is written on a page, which is dark on light, and the only place in the game that is.</summary>
    internal static class Vellum
    {
        public static readonly Colour Sheet = GuiColors.Rgba(226, 209, 172);
        public static readonly Colour Edge = GuiColors.Rgba(150, 122, 84);
        public static readonly Colour Ink = GuiColors.Rgba(46, 33, 22);
        public static readonly Colour Pale = GuiColors.Rgba(112, 92, 66);
        public static readonly Colour Rubric = GuiColors.Rgba(136, 34, 18);
        public static readonly Colour Lit = GuiColors.Rgba(120, 80, 30, 46);

        public static void Write(GuiContext context, GuiDrawList draw, string text, float size, GuiRect rect, Colour colour, Alignment horizontal = Alignment.Start, Alignment vertical = Alignment.Center)
        {
            GuiText.Draw(context, draw, text, Ui.Face, size, rect, colour, horizontal, vertical);
        }
    }

    /// <summary>
    /// What's written on a page, for reading and for changing. Each part of it that was chosen can be clicked, which is how anything on a page is ever changed, and what's still missing from it reads as a
    /// fainter line that does the same. There's no form to fill in: the page is the form.
    /// </summary>
    internal sealed class PageView : GuiElement
    {
        private const float Size = 21f;
        private const float LineHeight = 31f;
        private const float Indent = 22f;

        private readonly BookPanel _panel;
        private readonly List<(GuiRect rect, PagePlace place)> _spots = new();

        public PageView(BookPanel panel)
        {
            _panel = panel;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.Block;
            Clicked += e =>
            {
                if (e.Button != MouseButton.Left) return;
                foreach ((GuiRect rect, PagePlace place) in _spots)
                {
                    if (rect.Contains(e.Position)) _panel.Pick(place);
                }
            };
        }

        // Words a line at a time, each of which can be clicked for whatever it's part of. Long parts break over the end of a line, as anything written does.
        private void Flow(GuiDrawList draw, string text, PagePlace? place, ref float x, ref float y, float left, float right, bool faint = false)
        {
            if (Context == null) return;
            Vector2 mouse = Context.MousePosition;
            bool open = place != null && place == _panel.Open;
            float space = GuiText.MeasureWidth(Context, " ", Ui.Face, Size);

            foreach (string word in text.Split(' '))
            {
                float width = GuiText.MeasureWidth(Context, word, Ui.Face, Size);
                if (x + width > right && x > left)
                {
                    x = left;
                    y += LineHeight;
                }

                var rect = new GuiRect(x, y, width, LineHeight);
                if (place is PagePlace at)
                {
                    GuiRect hit = new GuiRect(x - 2f, y, width + space + 2f, LineHeight);
                    _spots.Add((hit, at));
                    if (open) draw.AddRect(hit, Vellum.Lit, 2f);
                    else if (hit.Contains(mouse) && IsHovered) draw.AddRect(hit, Vellum.Lit.MultiplyAlpha(0.6f), 2f);
                    draw.AddLine(new Vector2(x, y + LineHeight - 5f), new Vector2(x + width, y + LineHeight - 5f), (faint ? Vellum.Pale : Vellum.Rubric).MultiplyAlpha(0.5f), 1f);
                }

                Colour colour = faint ? Vellum.Pale : place != null ? Vellum.Rubric : Vellum.Ink;
                Vellum.Write(Context, draw, word, Size, rect, colour);
                x += width + space;
            }
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            _spots.Clear();
            if (Context == null || _panel.Page is not Page page) return;

            int index = _panel.PageIndex;
            float left = Bounds.X;
            float right = Bounds.Right;
            float y = Bounds.Y;
            List<List<PagePart>> lines = BookText.Parts(page, index);
            int outcomes = page.Outcomes.Count;
            int before = lines.Count - outcomes;

            for (int i = 0; i < lines.Count; i++)
            {
                if (i == before)
                {
                    y += 8f;
                    float heading = left;
                    Flow(draw, "On Impact,", null, ref heading, ref y, left, right);
                    y += LineHeight;
                }

                float start = i >= before ? left + Indent : left;
                float x = start;
                foreach (PagePart part in lines[i]) Flow(draw, part.Text, part.Place, ref x, ref y, start, right);

                // What could still be said goes at the end of what it would be said about.
                if (i == 0 && page.FromWithin == null) Flow(draw, "+ how far off", new PagePlace(Blank.Within), ref x, ref y, start, right, faint: true);
                if (i == 1 && page.Kind == PageKind.Projectile && page.TowardsWithin == null) Flow(draw, "+ how far off", new PagePlace(Blank.Within, Towards: true), ref x, ref y, start, right, faint: true);
                y += LineHeight;

                if (i == before - 1 && page.Clauses.Count < Rulebook.MostClauses)
                {
                    float more = left;
                    Flow(draw, "+ what else", new PagePlace(Blank.Clause), ref more, ref y, left, right, faint: true);
                    y += LineHeight;
                }
            }

            if (outcomes < Rulebook.MostOutcomes)
            {
                float x = left + Indent;
                Flow(draw, "+ another Outcome", new PagePlace(Blank.Deed, outcomes), ref x, ref y, left + Indent, right, faint: true);
            }
        }
    }

    /// <summary>One thing that could go in the blank that's open, with what it does to the page, and a click writes it in.</summary>
    internal sealed class ChoiceRow : GuiElement
    {
        private const float WordSize = 18f;
        private const float TradeSize = 14.5f;

        private readonly string _words;
        private readonly string _trade;
        private readonly bool _chosen;

        public ChoiceRow(string words, string trade, bool chosen, Action pressed)
        {
            _words = words;
            _trade = trade;
            _chosen = chosen;
            Width = UIValue.Fill;
            Height = trade.Length > 0 ? 50f : 32f;
            HitTest = HitTestMode.Block;
            Clicked += e =>
            {
                if (e.Button == MouseButton.Left) pressed();
            };
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;
            if (IsHovered || _chosen) draw.AddRect(Bounds, Vellum.Lit.MultiplyAlpha(_chosen ? 1f : 0.6f), 2f);

            float x = Bounds.X + 8f;
            float width = Bounds.Width - 12f;
            Vellum.Write(Context, draw, _words, WordSize, new GuiRect(x, Bounds.Y + 3f, width, 25f), _chosen ? Vellum.Rubric : Vellum.Ink);
            if (_trade.Length > 0) Vellum.Write(Context, draw, _trade, TradeSize, new GuiRect(x, Bounds.Y + 27f, width, 19f), Vellum.Pale);
        }
    }

    /// <summary>What a book comes to, read off its card, on the page beside it while nothing else is being chosen.</summary>
    internal sealed class Summary : GuiElement
    {
        private const float Size = 16f;
        private const float RowHeight = 23f;

        private readonly BookPanel _panel;

        public Summary(BookPanel panel)
        {
            _panel = panel;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null || _panel.Reading is not BookProfile book) return;

            float y = Bounds.Y;
            foreach (CardLine line in Cards.Book(book).Lines)
            {
                if (y > Bounds.Bottom - RowHeight) break;
                switch (line.Look)
                {
                    case LineLook.Gap:
                        y += 9f;
                        break;
                    case LineLook.Row:
                    case LineLook.Cell:
                        var row = new GuiRect(Bounds.X, y, Bounds.Width, RowHeight);
                        Vellum.Write(Context, draw, line.Text, Size, row, Vellum.Pale);
                        Vellum.Write(Context, draw, line.Value, Size, row, Vellum.Ink, Alignment.End);
                        y += RowHeight;
                        break;
                    default:
                        foreach (string wrapped in Ui.Wrap(Context, line.Text, Size, Bounds.Width))
                        {
                            Vellum.Write(Context, draw, wrapped, Size, new GuiRect(Bounds.X, y, Bounds.Width, RowHeight), Vellum.Ink);
                            y += RowHeight;
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// An open book. The left page is what's written, and the right is whatever could go in the part of it that's been clicked, with what each does to the page, or otherwise what the whole book comes to. Everything's
    /// written at once, as the only thing there is to do with a book is change it. Down the side is every book the hero has, and New Book.
    /// </summary>
    internal sealed class BookPanel : GuiElement
    {
        private const float ShelfWidth = 210f;

        private static readonly Colour Leather = GuiColors.Rgba(44, 26, 18, 248);

        private readonly StackPanel _shelf;
        private readonly TextBox _name;
        private readonly Button _projectile;
        private readonly Button _area;
        private readonly Label _numbers;
        private readonly Label _book;
        private readonly Label _where;
        private readonly Button _next;
        private readonly Button _last;
        private readonly Button _turn;
        private readonly Button _tear;
        private readonly Label _heading;
        private readonly ScrollPanel _choices;
        private readonly Summary _summary;

        private ItemPlace _place;
        private Book _written = new();
        private int _shown = -1;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        /// <summary>The part of the page whose choices are showing, if any.</summary>
        public PagePlace? Open { get; private set; }

        public int PageIndex { get; private set; }

        public Page? Page => PageIndex < _written.Pages.Count ? _written.Pages[PageIndex] : null;

        /// <summary>What's being written comes to, in this hero's hands.</summary>
        public BookProfile? Reading => GameSession.Current == null ? null : Session.Sheet.Preview(Scribe.Read(_written, _name.Text));

        public BookPanel()
        {
            Anchor = Anchor.Center;
            Width = 1320f;
            Height = 800f;
            Padding = new Thickness(16f);
            Ui.Dress(this);
            BackgroundColor = Leather;
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, 14f) { Width = UIValue.Fill, Height = UIValue.Fill });

            var side = columns.Add(new StackPanel(Orientation.Vertical, 6f) { Width = ShelfWidth, Height = UIValue.Fill });
            side.Add(Ui.Line("Your books", 16f, Ui.Gold));
            var scroll = side.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            _shelf = scroll.Content.Add(new StackPanel(Orientation.Vertical, 4f) { Width = UIValue.Fill, Height = UIValue.Auto });
            Button fresh = side.Add(Ui.Action("New Book", NewBook));
            fresh.Height = 30f;
            side.Add(Ui.Paragraph("Drag a book onto the bar from your pack to use it.", 13f, Ui.Faded));

            var left = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill, Padding = new Thickness(26f, 20f) });
            left.BackgroundColor = Vellum.Sheet;
            left.BorderColor = Vellum.Edge;
            left.BorderWidth = 1f;

            _name = left.Add(new TextBox("", "Name this book") { Width = UIValue.Fill, Height = 32f, MaxLength = HeroState.LongestBookName });
            _name.BackgroundColor = GuiColors.Rgba(60, 44, 30, 235);
            _name.Submitted += _ => { _name.Unfocus(); Commit(); };

            var kinds = left.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = 28f });
            _projectile = kinds.Add(Ui.Action("Projectile", () => SetKind(PageKind.Projectile)));
            _projectile.Width = 130f;
            _projectile.Height = 28f;
            _area = kinds.Add(Ui.Action("Area", () => SetKind(PageKind.Area)));
            _area.Width = 130f;
            _area.Height = 28f;

            left.Add(new Spacer(height: 4f));
            left.Add(new PageView(this));

            _numbers = left.Add(new Label("") { Font = Ui.Face, FontSize = 16f, Color = Vellum.Ink });
            _book = left.Add(new Label("") { Font = Ui.Face, FontSize = 16f, Color = Vellum.Pale });

            var turning = left.Add(new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill, Height = 30f });
            turning.Stack!.CrossAlignment = Alignment.Center;
            _last = turning.Add(Ui.Action("<", () => Turn(-1)));
            _last.Width = 40f;
            _last.Height = 30f;
            _where = turning.Add(new Label("") { Font = Ui.Face, FontSize = 15f, Color = Vellum.Pale, Width = 90f });
            _next = turning.Add(Ui.Action(">", () => Turn(1)));
            _next.Width = 40f;
            _next.Height = 30f;
            turning.Add(new Spacer(width: UIValue.Fill));
            _turn = turning.Add(Ui.Action("Add a Page", AddPage));
            _turn.Width = 120f;
            _turn.Height = 30f;
            _tear = turning.Add(Ui.Action("Tear it out", TearOut));
            _tear.Width = 120f;
            _tear.Height = 30f;

            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill, Padding = new Thickness(26f, 20f) });
            right.BackgroundColor = Vellum.Sheet;
            right.BorderColor = Vellum.Edge;
            right.BorderWidth = 1f;

            _heading = right.Add(new Label("") { Font = Ui.Face, FontSize = 21f, Color = Vellum.Rubric });
            _choices = right.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            _choices.Content.Layout = new StackLayout(Orientation.Vertical, 3f);
            _summary = right.Add(new Summary(this));

            // Decided here rather than by each of them, as a hidden element isn't updated and could never show itself again.
            right.Updated += _ =>
            {
                _choices.Visible = Open != null;
                _summary.Visible = Open == null;
            };
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null) return;

            // The server has the last word on what's where, and something can be gone from under an open book.
            if (Hero.At(_place) is not { IsBook: true }) Close();
            if (!Visible || Reading is not BookProfile book || book.Pages.Length <= PageIndex) return;

            PageProfile page = book.Pages[PageIndex];
            string size = page.Page.Kind == PageKind.Projectile ? $"Projectile Speed {Say.Amount(page.Speed)} m a second" : page.Page.Shape == AreaShape.Circle ? $"Radius {Say.Metres(page.Length)}" : $"Reach {Say.Metres(page.Length)}";
            _numbers.Text = $"Cost {Say.Amount(page.Cost)}   Potency {Say.Share(page.Potency)}   {size}";
            _book.Text = $"The whole Book Costs {Say.Amount(book.Cost)} {(book.CostsHealth ? "Health" : "Mana")}";
            _where.Text = $"Page {PageIndex + 1} of {book.Pages.Length}";
            _last.Visible = PageIndex > 0;
            _next.Visible = PageIndex < book.Pages.Length - 1;
            _heading.Text = Open is PagePlace open ? BookText.NameOf(open.Blank) : "What it comes to";
        }

        private void Close()
        {
            Visible = false;
            Open = null;
        }

        /// <summary>Opens at whatever comes first: what's on the bar, or else what's in the pack.</summary>
        public void OpenAny()
        {
            foreach (ItemPlace place in Hero.BookPlaces())
            {
                Show(place);
                return;
            }

            _place = default;
            _written = new Book();
            Rebuild();
        }

        /// <summary>Opens a book, at its first page.</summary>
        public void Show(ItemPlace place)
        {
            if (Hero.At(place) is not { Book: Book book } item) return;
            _place = place;
            _written = book.Copy();
            _name.Text = item.Name;
            PageIndex = 0;
            Open = null;
            Rebuild();
        }

        private void Rebuild()
        {
            _shelf.ClearChildren();
            foreach (ItemPlace place in Hero.BookPlaces())
            {
                ItemPlace at = place;
                Item item = Hero.At(place)!;
                Button button = _shelf.Add(Ui.Action($"{(place == _place ? "> " : "")}{item.Name}", () =>
                {
                    Audio.Play(Interface.Open);
                    Show(at);
                }));
                button.Height = 30f;
            }
            Offer();
        }

        // Everything that could go in what's open, as a row each.
        private void Offer()
        {
            _choices.Content.ClearChildren();
            _choices.ScrollToTop();
            if (Open is not PagePlace open || Page is not Page page) return;

            string? chosen = Current(page, open);
            bool optional = open.Blank == Blank.Within || open.Blank == Blank.Clause;
            if (optional && chosen != null || open.Blank == Blank.Deed && open.Outcome < page.Outcomes.Count && page.Outcomes.Count > 1)
            {
                _choices.Content.Add(new ChoiceRow("Strike this out", "", false, () => Strike(open)));
            }

            foreach (Sentence sentence in Sentences.For(open.Blank))
            {
                if (!Fits(sentence, page, open, chosen)) continue;
                Sentence offered = sentence;
                _choices.Content.Add(new ChoiceRow(sentence.Words, string.Join(", ", BookText.Trade(sentence.Weights)), sentence.Id == chosen, () => Choose(offered)));
            }
        }

        private bool Fits(Sentence sentence, Page page, in PagePlace open, string? chosen)
        {
            if (sentence.Fits is PageKind kind && kind != page.Kind) return false;
            if (sentence.FollowsOnly && PageIndex == 0) return false;
            if (open.Blank != Blank.Clause) return true;

            if (sentence.Id != chosen && page.Clauses.Contains(sentence.Id)) return false;
            if (sentence.Rides && (PageIndex > 0 || Sentences.Find(page.From)!.Start != Start.You || Sentences.Find(page.Towards)!.Bearing == Bearing.Down)) return false;
            return true;
        }

        private static string? Current(Page page, in PagePlace open)
        {
            Outcome? outcome = open.Outcome >= 0 && open.Outcome < page.Outcomes.Count ? page.Outcomes[open.Outcome] : null;
            return open.Blank switch
            {
                Blank.From => page.From,
                Blank.Towards => page.Towards,
                Blank.Shape => page.Shape,
                Blank.Timing => page.Timing,
                Blank.Within => open.Towards ? page.TowardsWithin : page.FromWithin,
                Blank.Clause => open.Clause >= 0 ? page.Clauses[open.Clause] : null,
                Blank.Deed => outcome?.Deed,
                Blank.Element => outcome?.Element,
                Blank.Measure => outcome?.Measure,
                Blank.Ailment => outcome?.Ailment,
                Blank.Gift => outcome?.Gift,
                Blank.Kin => outcome?.Kin,
                Blank.Pool => outcome?.Pool,
                _ => outcome?.Whom,
            };
        }

        /// <summary>Opens a part of the page, or closes it if it's open already.</summary>
        public void Pick(PagePlace place)
        {
            Audio.Play(Interface.Click);
            Open = Open == place ? null : place;
            Offer();
        }

        private void Choose(Sentence sentence)
        {
            if (Open is not PagePlace open || Page is not Page page) return;

            string id = sentence.Id;
            Outcome? outcome = open.Outcome >= 0 && open.Outcome < page.Outcomes.Count ? page.Outcomes[open.Outcome] : null;
            switch (open.Blank)
            {
                case Blank.From: page.From = id; break;
                case Blank.Towards: page.Towards = id; break;
                case Blank.Shape: page.Shape = id; break;
                case Blank.Timing: page.Timing = id; break;
                case Blank.Within when open.Towards: page.TowardsWithin = id; break;
                case Blank.Within: page.FromWithin = id; break;
                case Blank.Clause when open.Clause >= 0: page.Clauses[open.Clause] = id; break;
                case Blank.Clause:
                    page.Clauses.Add(id);
                    Open = null;
                    break;
                case Blank.Deed when outcome == null:
                    page.Outcomes.Add(new Outcome { Deed = id });
                    Open = null;
                    break;
                case Blank.Deed: outcome.Deed = id; break;
                case Blank.Element when outcome != null: outcome.Element = id; break;
                case Blank.Measure when outcome != null: outcome.Measure = id; break;
                case Blank.Ailment when outcome != null: outcome.Ailment = id; break;
                case Blank.Gift when outcome != null: outcome.Gift = id; break;
                case Blank.Kin when outcome != null: outcome.Kin = id; break;
                case Blank.Pool when outcome != null: outcome.Pool = id; break;
                case Blank.Whom when outcome != null: outcome.Whom = id; break;
            }
            Audio.Play(Interface.Equip);
            Commit();
        }

        private void Strike(in PagePlace place)
        {
            if (Page is not Page page) return;
            switch (place.Blank)
            {
                case Blank.Within when place.Towards: page.TowardsWithin = null; break;
                case Blank.Within: page.FromWithin = null; break;
                case Blank.Clause when place.Clause >= 0: page.Clauses.RemoveAt(place.Clause); break;
                case Blank.Deed when place.Outcome < page.Outcomes.Count: page.Outcomes.RemoveAt(place.Outcome); break;
            }
            Open = null;
            Audio.Play(Interface.Click);
            Commit();
        }

        private void SetKind(PageKind kind)
        {
            if (Page is not Page page || page.Kind == kind) return;
            if (kind == PageKind.Area && PageIndex < _written.Pages.Count - 1)
            {
                Audio.Play(Interface.Refuse);
                Session.Note("An Area Page has to be the last. Tear out the Pages after it first.");
                return;
            }
            page.Kind = kind;
            Open = null;
            Audio.Play(Interface.Click);
            Commit();
        }

        private void Turn(int by)
        {
            PageIndex = Math.Clamp(PageIndex + by, 0, _written.Pages.Count - 1);
            Open = null;
            Audio.Play(Interface.Click);
            Offer();
        }

        private void AddPage()
        {
            if (_written.Pages.Count >= Rulebook.MostPages || _written.Pages[^1].Kind == PageKind.Area)
            {
                Audio.Play(Interface.Refuse);
                Session.Note(_written.Pages.Count >= Rulebook.MostPages ? "This Book is full." : "An Area Page has to be the last. Make this one a Projectile first.");
                return;
            }
            _written.Pages.Add(Scribe.NewPage(PageKind.Projectile));
            PageIndex = _written.Pages.Count - 1;
            Open = null;
            Audio.Play(Interface.Equip);
            Commit();
        }

        private void TearOut()
        {
            if (_written.Pages.Count <= 1)
            {
                Audio.Play(Interface.Refuse);
                return;
            }
            _written.Pages.RemoveAt(PageIndex);
            PageIndex = Math.Min(PageIndex, _written.Pages.Count - 1);
            Open = null;
            Audio.Play(Interface.Click);
            Commit();
        }

        private void NewBook()
        {
            if (!Hero.HasRoom())
            {
                Audio.Play(Interface.Refuse);
                Session.Note("Your pack is full.");
                return;
            }
            Audio.Play(Interface.Open);
            Session.Client.NewBook();
            Show(ItemPlace.Cell(Array.FindLastIndex(Hero.Inventory, item => item is { IsBook: true })));
        }

        // Done at once and asked of the server, which has the last word: see GameClient.AskGear. What comes back from being put right is what's shown.
        private void Commit()
        {
            Scribe.Fix(_written);
            Session.Client.Write(_place, _written, _name.Text);
            PageIndex = Math.Min(PageIndex, _written.Pages.Count - 1);
            Rebuild();
        }
    }

    /// <summary>A place on the bar. It's a place like any on the hero, but it's only there to be used while there's a pack open to bring something from.</summary>
    internal sealed class BarCell : ItemCell
    {
        public BarCell(HeroPanel panel, int slot, float size)
            : base(panel, new Spot(Shelf.Bar, slot), size)
        {
        }

        public override bool HitTestSelf(Vector2 point) => GameUi.Current.Hero.Visible && base.HitTestSelf(point);
    }

    /// <summary>
    /// The bar, with dodging and the rest beside it. Six places take a book each, and those are real places: what's dragged here lands the same way it would anywhere on the hero. It stops the mouse only while the pack is
    /// open, as otherwise the mouse is what's aimed with.
    /// </summary>
    internal sealed class ActionBar : GuiElement
    {
        private const float Size = 62f;
        private const float Gap = 8f;
        private const float Bottom = 26f;

        public static readonly string[] Keys = { "LMB", "RMB", "Q", "1", "2", "3" };

        private readonly BarCell[] _cells = new BarCell[HeroState.BarSlots];

        private readonly record struct Ability(string Key, string Name, float Share, bool Ready, Func<Card?> Card);

        public ActionBar(HeroPanel panel)
        {
            Anchor = Anchor.Bottom;
            Height = Size;
            Width = 800f;
            Margin = new Thickness(0f, 0f, 0f, Bottom);
            HitTest = HitTestMode.PassThrough;
            for (int slot = 0; slot < _cells.Length; slot++)
            {
                _cells[slot] = Add(new BarCell(panel, slot, Size) { X = slot * (Size + Gap) });
            }
        }

        private static bool Resting(GuiContext context, GuiRect rect)
        {
            if (GameUi.Current.AnyOpen || context.IsMouseDown(MouseButton.Left) || context.IsMouseDown(MouseButton.Right)) return false;
            return rect.Contains(context.MousePosition);
        }

        private List<Ability> Others(GameSession session)
        {
            var abilities = new List<Ability>(3);
            HeroSimState sim = session.Sim;
            abilities.Add(new("Space", "Dodge", sim.DodgeCooldown / MathF.Max(session.Sheet.DodgeCooldown, 0.1f), true, () => Cards.Dodge(session.Sheet)));
            abilities.Add(new("R", $"Flask {session.Hero.Flasks}", 0f, session.Hero.Flasks > 0, () => Cards.Flask(session.Hero, session.Sheet)));
            if (session.Hero.HasGuidingWind) abilities.Add(new("F", "Wind", session.GuideCooldown / Rulebook.GuideCooldown, true, Cards.Wind));
            return abilities;
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (Context == null || GameSession.Current is not GameSession session) return;

            HeroSimState sim = session.Sim;
            for (int slot = 0; slot < _cells.Length; slot++)
            {
                GuiRect cell = _cells[slot].Bounds;
                BookProfile? book = session.Client.Kit.Slots[slot];
                Item? item = session.Hero.Bar[slot];

                float wait = book != null && book.Cooldown > 0f ? Math.Clamp(sim.Cooldowns[slot] / book.Cooldown, 0f, 1f) : 0f;
                if (wait > 0f) draw.AddRect(new GuiRect(cell.X, cell.Y + cell.Height * (1f - wait), cell.Width, cell.Height * wait), GuiColors.Rgba(20, 16, 12, 170), 2f);

                bool affordable = book == null || (book.CostsHealth ? session.Health > book.Cost : sim.Mana >= book.Cost);
                if (!affordable) draw.AddRect(cell, GuiColors.Rgba(20, 24, 70, 150), 2f);

                if (item != null)
                {
                    float nameSize = item.Name.Length > 9 ? 10.5f : 12f;
                    Ui.Text(Context, draw, item.Name, nameSize, new GuiRect(cell.X + 2f, cell.Y + 2f, cell.Width - 4f, 16f), affordable ? Ui.Parchment : Ui.Faded, Alignment.Center);
                }
                Ui.Text(Context, draw, Keys[slot], 11.5f, new GuiRect(cell.X, cell.Bottom - 20f, cell.Width, 18f), Ui.Gold, Alignment.Center);

                if (book != null && Resting(Context, cell))
                {
                    BookProfile shown = book;
                    GameUi.Current.Tips.Show(() => Cards.Book(shown), cell, above: true);
                }
            }

            float x = Bounds.X + _cells.Length * (Size + Gap);
            float y = Bounds.Y;
            foreach (Ability ability in Others(session))
            {
                GuiRect slot = new GuiRect(x, y, Size, Size);
                draw.AddRect(slot, Ui.Ink, 3f, 1f, GuiColors.Rgba(120, 96, 58));

                float share = Math.Clamp(ability.Share, 0f, 1f);
                if (share > 0f) draw.AddRect(new GuiRect(x, y + Size * (1f - share), Size, Size * share), GuiColors.Rgba(150, 120, 70, 90), 3f);

                Colour colour = !ability.Ready ? Ui.Faded.MultiplyAlpha(0.6f) : share > 0f ? Ui.Faded : Ui.Parchment;
                Ui.Text(Context, draw, ability.Name, 12.5f, new GuiRect(x, y + 8f, Size, 22f), colour, Alignment.Center);
                Ui.Text(Context, draw, ability.Key, 11.5f, new GuiRect(x, y + Size - 22f, Size, 18f), Ui.Gold, Alignment.Center);
                if (Resting(Context, slot)) GameUi.Current.Tips.Show(ability.Card, slot, above: true);
                x += Size + Gap;
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current is not GameSession session) return;
            int others = session.Hero.HasGuidingWind ? 3 : 2;
            Width = (_cells.Length + others) * Size + (_cells.Length + others - 1) * Gap;
        }
    }
}
