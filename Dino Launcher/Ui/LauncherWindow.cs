using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dino_Launcher.Update;

namespace Dino_Launcher.Ui
{
    /// <summary>
    /// The launcher as a player sees it. There's one thing to do here: wait for the button to light, and press it. The window only watches:
    /// all the work is in <see cref="Updater"/>, on a worker thread, and every problem has a sentence that says what to do about it.
    /// </summary>
    internal sealed class LauncherWindow : Form
    {
        private const int RecheckMilliseconds = 10 * 60 * 1000;
        private const int ScaledWidth = 1000;
        private const int ScaledHeight = 720;
        private const int SmallestWidth = 760;
        private const int SmallestHeight = 520;
        private const int Gutter = 28;
        private const int WidestColumn = 1400;
        private const int FootHeight = 118;
        private const int ActionWidth = 190;
        private const int LowestBanner = 130;
        private const int TallestBanner = 240;
        private const float BannerShare = 0.27f;
        private const float BannerCrop = 0.15f;

        // The game's own colours, from its Ui class.
        internal static readonly Color Ink = Color.FromArgb(15, 12, 10);
        internal static readonly Color InkLight = Color.FromArgb(38, 31, 25);
        internal static readonly Color Parchment = Color.FromArgb(228, 214, 186);
        internal static readonly Color Faded = Color.FromArgb(164, 150, 126);
        internal static readonly Color Gold = Color.FromArgb(214, 172, 92);
        private static readonly Color Blood = Color.FromArgb(214, 84, 66);

        private readonly InstallPaths _paths;
        private readonly LaunchOptions _options;
        private readonly Updater _updater;
        private readonly CancellationTokenSource _closing = new CancellationTokenSource();
        private readonly EventWaitHandle _showRequests;
        private readonly System.Windows.Forms.Timer _recheck;
        private readonly float _scale;

        private readonly Image? _banner;
        private readonly NotesPanel _notes;
        private readonly Label _status;
        private readonly ProgressStrip _progress;
        private readonly Button _play;
        private readonly Button _tryAgain;
        private readonly Label _about;
        private readonly LinkLabel _logs;

        private Manifest? _build;
        private Process? _game;
        private bool _working;
        private long _shownSequence;
        private int _bannerHeight;
        private int _left;

        public int ExitCode { get; private set; }

        public LauncherWindow(InstallPaths paths, LaunchOptions options, InstanceLock instance)
        {
            _paths = paths;
            _options = options;

            var feed = new Uri(options.Feed ?? Feed.Url);
            var downloader = new Downloader();
            _updater = new Updater(paths, feed, Feed.PublicKey, downloader);
            var replacer = new LauncherUpdate(paths, feed, downloader, options.Forwarded(), instance.Release, () => instance.Take());
            _updater.ReplaceLauncher = replacer.Replace;

            using (Graphics graphics = CreateGraphics()) _scale = graphics.DpiX / 96f;

            Text = "Dino Defenders";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ink;
            ForeColor = Parchment;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = new Font("Segoe UI", 10f);

            _banner = LoadBanner();

            _notes = new NotesPanel(_scale);

            _status = new Label
            {
                ForeColor = Parchment,
                BackColor = Ink,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 10.5f),
            };

            _progress = new ProgressStrip(InkLight, Gold) { Visible = false };

            _play = new Button
            {
                Text = "PLAY",
                Font = new Font("Georgia", 18f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Gold,
                ForeColor = Ink,
                Enabled = false,
                UseVisualStyleBackColor = false,
            };
            _play.FlatAppearance.BorderSize = 0;
            _play.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 194, 118);
            _play.FlatAppearance.MouseDownBackColor = Color.FromArgb(182, 142, 70);
            _play.EnabledChanged += (_, __) => _play.BackColor = _play.Enabled ? Gold : InkLight;
            _play.Paint += PaintDisabledPlay;
            _play.Click += (_, __) => Play();

            _tryAgain = new Button
            {
                Text = "Try again",
                FlatStyle = FlatStyle.Flat,
                BackColor = InkLight,
                ForeColor = Parchment,
                Visible = false,
                UseVisualStyleBackColor = false,
            };
            _tryAgain.FlatAppearance.BorderColor = Faded;
            _tryAgain.Click += (_, __) => Check();

            _about = new Label
            {
                ForeColor = Faded,
                BackColor = Ink,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"Launcher {Program.LauncherVersion}",
            };

            _logs = new LinkLabel
            {
                Text = "Open logs",
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Ink,
                LinkColor = Faded,
                ActiveLinkColor = Parchment,
                VisitedLinkColor = Faded,
                LinkBehavior = LinkBehavior.HoverUnderline,
            };
            _logs.LinkClicked += (_, __) => OpenLogs();

            Controls.AddRange(new Control[] { _notes, _status, _progress, _play, _tryAgain, _about, _logs });
            AcceptButton = _play;
            SetUpSize();

            _recheck = new System.Windows.Forms.Timer { Interval = RecheckMilliseconds };
            _recheck.Tick += (_, __) => { if (Visible && _game == null) Check(); };
            _recheck.Start();

            _showRequests = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceLock.ShowEventName(paths));
            var listener = new Thread(ListenForShowRequests) { IsBackground = true, Name = "Show requests" };
            listener.Start();
        }

        private int Scaled(int value) => (int)Math.Round(value * _scale);

        /// <summary>The size it was left at, or a size with room to read, but never more than the screen it's opening on.</summary>
        private void SetUpSize()
        {
            WindowMemory? remembered = WindowMemory.Read(_paths.WindowSize);
            Size wanted = remembered != null ? new Size(Scaled(remembered.Width), Scaled(remembered.Height)) : new Size(Scaled(ScaledWidth), Scaled(ScaledHeight));
            Size smallest = new Size(Scaled(SmallestWidth), Scaled(SmallestHeight));

            Size frame = SizeFromClientSize(Size.Empty);
            Rectangle screen = Screen.FromPoint(MousePosition).WorkingArea;
            Size room = new Size(screen.Width - frame.Width, screen.Height - frame.Height);

            smallest = new Size(Math.Min(smallest.Width, room.Width), Math.Min(smallest.Height, room.Height));
            MinimumSize = smallest + frame;
            ClientSize = new Size(Math.Max(smallest.Width, Math.Min(wanted.Width, room.Width)), Math.Max(smallest.Height, Math.Min(wanted.Height, room.Height)));
            if (remembered?.Maximized == true) WindowState = FormWindowState.Maximized;
            Arrange();
        }

        private void RememberSize()
        {
            Size size = (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds).Size - SizeFromClientSize(Size.Empty);
            new WindowMemory { Width = (int)Math.Round(size.Width / _scale), Height = (int)Math.Round(size.Height / _scale), Maximized = WindowState == FormWindowState.Maximized }.Write(_paths.WindowSize);
        }

        /// <summary>The foot stays as it is, the banner gives up what it can, and everything else goes to the notes.</summary>
        private void Arrange()
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (height == 0) return; // Minimized.

            // On a very wide screen everything stays together in the middle, or Play and the scroll bar end up a long way from what's being read.
            int column = Math.Min(width - Scaled(Gutter) * 2, Scaled(WidestColumn));
            int foot = height - Scaled(FootHeight);
            _left = (width - column) / 2;
            int action = _left + column - Scaled(ActionWidth);
            int status = action - Scaled(24) - _left;
            _bannerHeight = Math.Max(Scaled(LowestBanner), Math.Min(Scaled(TallestBanner), (int)(height * BannerShare)));

            int notes = _bannerHeight + Scaled(8);
            _notes.Bounds = new Rectangle(_left, notes, column, Math.Max(0, foot - Scaled(14) - notes));
            _status.Bounds = new Rectangle(_left, foot, status, Scaled(44));
            _progress.Bounds = new Rectangle(_left, foot + Scaled(50), status, Scaled(8));
            _play.Bounds = new Rectangle(action, foot + Scaled(2), Scaled(ActionWidth), Scaled(60));
            _tryAgain.Bounds = new Rectangle(action, foot + Scaled(66), Scaled(ActionWidth), Scaled(28));
            _about.Bounds = new Rectangle(_left, foot + Scaled(76), status - Scaled(130), Scaled(24));
            _logs.Bounds = new Rectangle(_left + status - Scaled(118), foot + Scaled(76), Scaled(118), Scaled(24));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_notes != null) Arrange(); // This also comes while the window is being built.
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _notes.Wheel(e.Delta);
        }

        protected override bool ProcessCmdKey(ref Message message, Keys key)
        {
            if (_notes.Typing && key == Keys.Enter) return true; // It would press Play, on someone only meaning to finish a search.
            if (_notes.Typing && key == Keys.Escape)
            {
                _notes.EndSearch();
                if (_play.Enabled) _play.Focus();
                return true;
            }
            return _notes.Press(key) || base.ProcessCmdKey(ref message, key);
        }

        private static Image? LoadBanner()
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("banner.jpg");
            return stream == null ? null : new Bitmap(Image.FromStream(stream));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            try
            {
                DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)); // A dark title bar, where Windows has one.
            }
            catch (DllNotFoundException)
            {
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_options.Handoff != null) Program.Prove(_options.Handoff);

            // Someone coming back sees what they have straight away, rather than an empty page until the network answers.
            Manifest? installed = _updater.Install.ReadInstalled(Feed.PublicKey);
            if (installed != null) ShowBuild(installed);
            Check();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RememberSize();
            _closing.Cancel();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            var area = new Rectangle(0, 0, ClientSize.Width, _bannerHeight);

            if (_banner != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float zoom = Math.Max((float)area.Width / _banner.Width, (float)area.Height / _banner.Height);
                float width = area.Width / zoom;
                float height = area.Height / zoom;
                // A wide window sees a strip of the picture. Its top is what's worth seeing, as the fade covers the bottom.
                var source = new RectangleF((_banner.Width - width) / 2f, (_banner.Height - height) * BannerCrop, width, height);
                g.DrawImage(_banner, area, source, GraphicsUnit.Pixel);
            }

            // The picture sinks into the page, and the name stands on the dark.
            var fade = new Rectangle(0, area.Height / 3, area.Width, area.Height - area.Height / 3 + 1);
            var gradient = Rectangle.Inflate(fade, 0, 1); // A brush tiles, and its exact size leaves a dark line where the last row wraps to the first.
            using (var brush = new LinearGradientBrush(gradient, Color.FromArgb(0, Ink), Ink, LinearGradientMode.Vertical)) g.FillRectangle(brush, fade);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var title = new Font("Georgia", 32f, FontStyle.Bold);
            using var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            using var ink = new SolidBrush(Parchment);
            var place = new PointF(_left - Scaled(4), area.Height - Scaled(66));
            g.DrawString("Dino Defenders", title, shadow, place.X + Scaled(2), place.Y + Scaled(2));
            g.DrawString("Dino Defenders", title, ink, place);
        }

        // A flat button draws disabled text in its own pale grey, which disappears on this background.
        private void PaintDisabledPlay(object? sender, PaintEventArgs e)
        {
            if (_play.Enabled) return;
            e.Graphics.Clear(InkLight);
            TextRenderer.DrawText(e.Graphics, _play.Text, _play.Font, _play.ClientRectangle, Faded, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void ListenForShowRequests()
        {
            var both = new[] { _showRequests, _closing.Token.WaitHandle };
            while (WaitHandle.WaitAny(both) == 0)
            {
                Post(() =>
                {
                    Show();
                    if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                    Activate();
                });
            }
        }

        /// <summary>Runs something on the window's thread, unless the window has gone.</summary>
        private void Post(Action action)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(action);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ObjectDisposedException)
            {
            }
        }

        private void Check()
        {
            if (_working) return;
            _working = true;
            _play.Enabled = false;
            _tryAgain.Visible = false;
            ShowStatus("Checking for updates", Parchment);

            CancellationToken cancel = _closing.Token;
            Task.Run(() =>
            {
                UpdateResult result;
                try
                {
                    // On this thread: a launcher this one just replaced can take a moment to finish closing, and the window shouldn't wait.
                    LauncherUpdate.CleanUp(_paths);
                    result = _updater.Run(progress => Post(() => ShowProgress(progress)), cancel);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Log.Write($"The update check failed: {e}");
                    result = new UpdateResult { Problem = "Something went wrong while checking for updates." };
                }
                Post(() => Finished(result));
            });
        }

        private void ShowProgress(Progress progress)
        {
            _progress.Visible = progress.Total > 0;
            _progress.Fraction = progress.Total > 0 ? (float)progress.Done / progress.Total : 0f;

            string text = progress.Text;
            if (progress.Total > 0) text += $"   {progress.Done / 1048576.0:0.0} of {progress.Total / 1048576.0:0.0} MB";
            ShowStatus(text, Parchment);
        }

        private void ShowStatus(string text, Color colour)
        {
            _status.Text = text;
            _status.ForeColor = colour;
        }

        private void Finished(UpdateResult result)
        {
            _working = false;
            _progress.Visible = false;

            if (result.HandedOver)
            {
                Close();
                return;
            }

            _build = result.Build;
            if (_build != null) ShowBuild(_build);

            if (_build == null)
            {
                ShowStatus(result.Problem ?? "Something went wrong.", Blood);
                _tryAgain.Visible = true;
                if (_options.QuitWhenReady)
                {
                    ExitCode = 3;
                    Close();
                }
                return;
            }

            if (result.Problem != null)
            {
                ShowStatus($"{result.Problem} You can still play {_build.Version}.", Gold);
                _tryAgain.Visible = true;
            }
            else
            {
                ShowStatus("Ready to play", Parchment);
            }

            if (GameProcess.IsRunning(_paths, _build))
            {
                ShowStatus("The game is running.", Parchment);
                WatchRunningGame();
                return;
            }

            _play.Enabled = true;
            if (!_notes.Typing) _play.Focus();

            if (_options.Play) Play();
            else if (_options.QuitWhenReady) Close();
        }

        private void ShowBuild(Manifest build)
        {
            _about.Text = $"Version {build.Version}   Launcher {Program.LauncherVersion}";
            if (build.Sequence == _shownSequence) return;
            _shownSequence = build.Sequence;

            _notes.Fill(build.Notes);
        }

        private void Play()
        {
            if (_build == null) return;
            try
            {
                _game = GameProcess.Start(_paths, _build);
                _game.EnableRaisingEvents = true;
                _game.SynchronizingObject = this;
                _game.Exited += (_, __) => GameExited();
                Log.Write($"Started {_build.Version}.");
                _play.Enabled = false;
                ShowStatus("The game is running.", Parchment);
                Hide();
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException || e is IOException)
            {
                // Most often a virus scanner taking the exe away. Checking again puts it back.
                Log.Write($"The game wouldn't start: {e.Message}");
                _game = null;
                ShowStatus("The game wouldn't start. Is an antivirus blocking it? Open the logs for more.", Blood);
                _tryAgain.Visible = true;
            }
        }

        private void GameExited()
        {
            int code = _game?.ExitCode ?? 0;
            _game?.Dispose();
            _game = null;
            Log.Write($"The game closed with code {code}.");

            if (code == 0)
            {
                Close();
                return;
            }

            Show();
            Activate();
            ExitCode = 4;
            ShowStatus($"The game stopped unexpectedly (code 0x{code:X8}). If you report this, send game.log from the logs folder.", Blood);
            _play.Enabled = true;
            if (_options.QuitWhenReady) Close();
        }

        // Started by an earlier launcher, or by hand. There's no process object to wait on, so this looks now and then.
        private void WatchRunningGame()
        {
            var watch = new System.Windows.Forms.Timer { Interval = 2000 };
            watch.Tick += (_, __) =>
            {
                if (_build != null && GameProcess.IsRunning(_paths, _build)) return;
                watch.Dispose();
                Check();
            };
            watch.Start();
        }

        private void OpenLogs()
        {
            Directory.CreateDirectory(_paths.Logs);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_paths.Logs}\"") { UseShellExecute = true })?.Dispose();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }

    /// <summary>A plain bar. The Windows one can't be told what colour to be.</summary>
    internal sealed class ProgressStrip : Control
    {
        private readonly Color _fill;
        private float _fraction;

        public ProgressStrip(Color back, Color fill)
        {
            BackColor = back;
            _fill = fill;
            DoubleBuffered = true;
        }

        public float Fraction
        {
            get => _fraction;
            set
            {
                _fraction = Math.Max(0f, Math.Min(1f, value));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new SolidBrush(_fill);
            e.Graphics.FillRectangle(brush, 0, 0, Width * _fraction, Height);
        }
    }
}
