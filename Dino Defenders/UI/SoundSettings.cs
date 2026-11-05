using Dino_Engine.GUI;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using System.Text.Json;

namespace Dino_Defenders.UI
{
    /// <summary>How loud this player wants things. It belongs to them and not to a hero or a build, so it's kept beside the save, not in the game's own folder.</summary>
    internal sealed class SoundSettings
    {
        public float Master { get; set; } = 0.85f;
        public float Effects { get; set; } = 1f;
        public float World { get; set; } = 1f;
        public float Interface { get; set; } = 1f;
        public float Music { get; set; } = 0.65f;
        public bool Headphones { get; set; }

        private static SoundSettings? _current;

        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "sound.json");
            }
        }

        public static SoundSettings Load()
        {
            if (_current != null) return _current;
            try
            {
                string path = FilePath;
                if (File.Exists(path)) _current = JsonSerializer.Deserialize<SoundSettings>(File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                // Settings that won't read are worth a warning, not a game that won't start.
                Console.WriteLine($"Sound settings: {e.Message}");
            }
            return _current ??= new SoundSettings();
        }

        private void Store()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Sound settings: {e.Message}");
            }
        }

        public void Apply()
        {
            Audio.MasterVolume = Master;
            Audio.SetVolume(SoundBus.Foley, Effects);
            Audio.SetVolume(SoundBus.Combat, Effects);
            Audio.SetVolume(SoundBus.Voice, Effects);
            Audio.SetVolume(SoundBus.Ambience, World);
            Audio.SetVolume(SoundBus.Interface, Interface);
            Audio.SetVolume(SoundBus.Music, Music);
            if (Audio.Mixer != null) Audio.Mixer.Headphones = Headphones;
        }

        /// <summary>Sliders for all of it, for a menu.</summary>
        public static void AddControls(GuiElement parent)
        {
            SoundSettings settings = Load();
            parent.Add(Row("Sound", settings.Master, v => settings.Change(() => settings.Master = v)));
            parent.Add(Row("Effects", settings.Effects, v => settings.Change(() => settings.Effects = v)));
            parent.Add(Row("The land", settings.World, v => settings.Change(() => settings.World = v)));
            parent.Add(Row("Music", settings.Music, v => settings.Change(() => settings.Music = v)));
            parent.Add(new Checkbox("Wearing headphones", settings.Headphones) { Width = UIValue.Fill }.Bind(() => settings.Headphones, on => settings.Change(() => settings.Headphones = on)));
        }

        private void Change(Action change)
        {
            change();
            Apply();
            Store();
        }

        private static GuiElement Row(string caption, float value, Action<float> changed)
        {
            var row = new StackPanel(Orientation.Horizontal, 8f) { Width = UIValue.Fill };
            row.Stack!.CrossAlignment = Alignment.Center;
            row.Add(new Label(caption) { Width = 80f, Color = Ui.Faded, FontSize = 14f });
            var slider = new Slider(0f, 1f) { Width = UIValue.Fill, ShowValue = false, Value = value };
            slider.ValueChanged += changed;
            row.Add(slider);
            return row;
        }
    }
}
