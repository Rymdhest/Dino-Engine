using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Music;

namespace Dino_Engine.GUI.Debug
{
    /// <summary>
    /// The sound system from the inside: levels, voices and cost, the volume of each bus, and every sound there is, to be heard at a click. Sounds
    /// have to be judged by listening, and this is where to do it without playing the game until something happens.
    /// </summary>
    internal sealed class AudioWindowController
    {
        private const float RefreshInterval = 0.1f;

        private readonly Dictionary<LiveDefinition, SoundHandle> _playing = new();
        private readonly Label _status;
        private float _timer;

        public GuiWindow Window { get; }

        public AudioWindowController()
        {
            Window = new GuiWindow("Sound", 380f, 640f) { Anchor = Anchor.TopRight, X = 12f, Y = 170f };

            _status = Window.Content.Add(new Label("") { Font = GuiContext.DefaultTheme.MonoFont, FontSize = GuiContext.DefaultTheme.SmallFontSize, Wrap = true, Width = UIValue.Fill });

            var scroll = Window.Content.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });

            var mix = scroll.Content.Add(new Foldout("Mix"));
            mix.Content.Add(DebugUi.LabeledRow("Master", new Slider(0f, 1f) { Format = "0.00", FormatProvider = DebugUi.Culture }.Bind(() => Audio.MasterVolume, v => Audio.MasterVolume = v)));
            foreach (SoundBus bus in Enum.GetValues<SoundBus>())
            {
                mix.Content.Add(DebugUi.LabeledRow(bus.ToString(), new Slider(0f, 1.5f) { Format = "0.00", FormatProvider = DebugUi.Culture }.Bind(() => Audio.GetVolume(bus), v => Audio.SetVolume(bus, v))));
            }

            var music = scroll.Content.Add(new Foldout("Music"));
            music.Content.Add(new Checkbox("Set by hand").Bind(() => Music.Manual, on => Music.Manual = on));
            foreach (MusicMood mood in Enum.GetValues<MusicMood>())
            {
                music.Content.Add(new Button(mood.ToString(), () =>
                {
                    Music.Manual = true;
                    Music.Mood = mood;
                }) { Width = UIValue.Fill, Height = 20f });
            }
            music.Content.Add(DebugUi.LabeledRow("Intensity", new Slider(0f, 1f) { Format = "0.00", FormatProvider = DebugUi.Culture }.Bind(() => Music.Intensity, v => Music.Intensity = v)));
            music.Content.Add(new Button("Victory", () => Music.Play(MusicCue.Victory)) { Width = UIValue.Fill, Height = 20f });
            music.Content.Add(new Button("Defeat", () => Music.Play(MusicCue.Defeat)) { Width = UIValue.Fill, Height = 20f });

            var live = scroll.Content.Add(new Foldout("Going on"));
            foreach (LiveDefinition definition in SoundCatalog.AllLive) AddLive(live, definition);

            // Grouped by the first word of the id, which is what they're named by.
            foreach (IGrouping<string, SoundDefinition> group in SoundCatalog.All.GroupBy(d => d.Id.Split('.')[0]))
            {
                var section = scroll.Content.Add(new Foldout(group.Key));
                section.Expanded = false;
                foreach (SoundDefinition definition in group)
                {
                    section.Content.Add(new Button(definition.Id, () => Audio.Play(definition)) { Width = UIValue.Fill, Height = 20f });
                }
            }

            Window.Updated += Update;
        }

        private void AddLive(Foldout section, LiveDefinition definition)
        {
            var values = (float[])definition.Defaults.Clone();
            section.Content.Add(new Checkbox(definition.Id).Bind(() => _playing.ContainsKey(definition), on =>
            {
                if (on)
                {
                    SoundHandle handle = Audio.Start(definition);
                    for (int i = 0; i < values.Length; i++) handle.Set(i, values[i]);
                    _playing[definition] = handle;
                }
                else if (_playing.Remove(definition, out SoundHandle handle))
                {
                    handle.Stop(0.4f);
                }
            }));

            for (int p = 0; p < definition.Parameters.Length; p++)
            {
                int parameter = p;
                float most = MathF.Max(values[p] * 2.5f, 1f);
                section.Content.Add(DebugUi.LabeledRow(definition.Parameters[p], new Slider(0f, most) { Format = "0.00", FormatProvider = DebugUi.Culture }.Bind(() => values[parameter], v =>
                {
                    values[parameter] = v;
                    if (_playing.TryGetValue(definition, out SoundHandle handle)) handle.Set(parameter, v);
                })));
            }
        }

        private void Update(float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < RefreshInterval) return;
            _timer = 0f;

            var mixer = Audio.Mixer;
            if (mixer == null)
            {
                _status.Text = "Sound is off.";
                return;
            }

            float left = AudioFormat.GainToDecibels(mixer.PeakLeft);
            float right = AudioFormat.GainToDecibels(mixer.PeakRight);
            float reduction = AudioFormat.GainToDecibels(mixer.Reduction);
            var output = Audio.Output;
            _status.Text = DebugUi.Inv($"{mixer.ActiveVoices} voices, most {mixer.MostActiveVoices}, {mixer.Dropped} dropped\nPeak {left:0.0} / {right:0.0} dB, limiter {reduction:0.0} dB, window {mixer.WindowDecibels:0.0} dB\nMixing {mixer.Load * 100f:0.0}%, queue {(output?.Latency ?? 0f) * 1000f:0} ms, {output?.Underruns ?? 0} underruns, {Audio.TakesMade} takes");
        }
    }
}
