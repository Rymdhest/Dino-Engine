using Dino_Engine.Core;
using Dino_Engine.Util;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.Client
{
    /// <summary>Everything a player can press. Saved by name, so one can go anywhere in this list.</summary>
    public enum GameAction
    {
        Slot0,
        Slot1,
        Slot2,
        Slot3,
        Slot4,
        Slot5,
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Dodge,
        Flask,
        Interact,
        Guide,
        Hero,
        Talents,
        Books,
        Map,
        Chronicle,
        Glossary,
        Menu,
        Details,
        Compare,
        ForceAttack,
        Roads,
    }

    /// <summary>One key or one mouse button.</summary>
    public readonly record struct Bound(Keys Key, MouseButton? Button = null)
    {
        public static Bound Mouse(MouseButton button) => new(Keys.Unknown, button);
    }

    /// <summary>
    /// What's pressed for what. Nothing in the game reads a key but through here, and nothing shows one but by asking here, so what a player changes changes everywhere at once. A key can only mean one thing, so giving
    /// an action a key that's taken swaps the two, and nothing is ever left with no key at all. The left and right of Ctrl, Alt and Shift are one key. Two things aren't anyone's to take: Escape always goes back, as
    /// someone has to be able to get out, and F3 is the developer's. See MENU.md.
    /// </summary>
    public static class Bindings
    {
        private const string File = "keys.json";

        /// <summary>What the Input page shows, in order.</summary>
        public static readonly (string Group, (GameAction Action, string Name)[] Rows)[] Page =
        {
            ("Action Bar", new[] { (GameAction.Slot0, "Slot 1"), (GameAction.Slot1, "Slot 2"), (GameAction.Slot2, "Slot 3"), (GameAction.Slot3, "Slot 4"), (GameAction.Slot4, "Slot 5"), (GameAction.Slot5, "Slot 6") }),
            ("Movement", new[] { (GameAction.MoveUp, "Move up"), (GameAction.MoveDown, "Move down"), (GameAction.MoveLeft, "Move left"), (GameAction.MoveRight, "Move right"), (GameAction.Dodge, "Dodge") }),
            ("Actions", new[] { (GameAction.Interact, "Interact"), (GameAction.Flask, "Drink flask"), (GameAction.Guide, "Guiding wind"), (GameAction.Roads, "Call the waygates"), (GameAction.ForceAttack, "Attack without picking up") }),
            ("Menus", new[] { (GameAction.Menu, "Menu"), (GameAction.Hero, "Hero"), (GameAction.Talents, "Talents"), (GameAction.Books, "Books"), (GameAction.Map, "Map"), (GameAction.Chronicle, "Chronicle"), (GameAction.Glossary, "Glossary") }),
            ("Items and cards", new[] { (GameAction.Details, "Hold card, quick move"), (GameAction.Compare, "Compare") }),
        };

        private static readonly Dictionary<GameAction, Bound> Defaults = new()
        {
            [GameAction.Slot0] = Bound.Mouse(MouseButton.Left),
            [GameAction.Slot1] = Bound.Mouse(MouseButton.Right),
            [GameAction.Slot2] = new(Keys.Q),
            [GameAction.Slot3] = new(Keys.D1),
            [GameAction.Slot4] = new(Keys.D2),
            [GameAction.Slot5] = new(Keys.D3),
            [GameAction.MoveUp] = new(Keys.W),
            [GameAction.MoveDown] = new(Keys.S),
            [GameAction.MoveLeft] = new(Keys.A),
            [GameAction.MoveRight] = new(Keys.D),
            [GameAction.Dodge] = new(Keys.Space),
            [GameAction.Flask] = new(Keys.R),
            [GameAction.Interact] = new(Keys.E),
            [GameAction.Guide] = new(Keys.F),
            [GameAction.Hero] = new(Keys.C),
            [GameAction.Talents] = new(Keys.T),
            [GameAction.Books] = new(Keys.K),
            [GameAction.Map] = new(Keys.M),
            [GameAction.Chronicle] = new(Keys.J),
            [GameAction.Glossary] = new(Keys.G),
            [GameAction.Menu] = new(Keys.Escape),
            [GameAction.Details] = new(Keys.LeftControl),
            [GameAction.Compare] = new(Keys.LeftAlt),
            [GameAction.ForceAttack] = new(Keys.LeftShift),
            [GameAction.Roads] = new(Keys.B),
        };

        private static readonly Dictionary<GameAction, Bound> Current = new(Defaults);

        public static void Load()
        {
            Current.Clear();
            foreach ((GameAction action, Bound bound) in Defaults) Current[action] = bound;

            Dictionary<string, string>? saved = SettingsStore.Read<Dictionary<string, string>>(File);
            if (saved == null) return;
            foreach ((string name, string text) in saved)
            {
                if (Enum.TryParse(name, out GameAction action) && Parse(text) is Bound bound && (CanBind(bound) || bound == Defaults[action])) Current[action] = bound;
            }
            Mend();
        }

        // A file edited by hand, or from before something was added, can say two things about one key: whoever came first keeps it, and the other has to find another.
        private static void Mend()
        {
            var taken = new HashSet<Bound>();
            foreach (GameAction action in Enum.GetValues<GameAction>())
            {
                if (taken.Add(Current[action])) continue;
                Current[action] = Free(action);
                taken.Add(Current[action]);
            }
        }

        // Where something goes when what it had has gone: back to where it started if that's free, or else to the first key nobody has.
        private static Bound Free(GameAction action)
        {
            Bound first = Defaults[action];
            if (CanBind(first) && !Current.Any(pair => pair.Key != action && pair.Value == first)) return first;
            return new Bound(Enum.GetValues<Keys>().First(key => CanBind(key) && Current.Values.All(there => there.Key != key)));
        }

        private static void Store() => SettingsStore.Write(File, Current.ToDictionary(pair => pair.Key.ToString(), pair => Write(pair.Value)));

        private static string Write(Bound bound) => bound.Button is MouseButton button ? $"Mouse{(int)button}" : bound.Key.ToString();

        private static Bound? Parse(string text)
        {
            if (text.StartsWith("Mouse") && int.TryParse(text.AsSpan(5), out int button)) return button is >= (int)MouseButton.Button1 and <= (int)MouseButton.Button8 ? Bound.Mouse((MouseButton)button) : null;
            return Enum.TryParse(text, out Keys key) && Enum.IsDefined(key) ? Plain(new Bound(key)) : null;
        }

        public static Bound Of(GameAction action) => Current[action];

        /// <summary>Gives an action a key. If something else had it, it takes what this one had, or another if that's a key nobody can have. Says which that was, if any.</summary>
        public static GameAction? Set(GameAction action, Bound bound)
        {
            bound = Plain(bound);
            Bound old = Current[action];
            if (old == bound) return null;

            GameAction? swapped = null;
            foreach ((GameAction other, Bound there) in Current)
            {
                if (other == action || there != bound) continue;
                swapped = other;
                break;
            }
            Current[action] = bound;
            if (swapped is GameAction taken) Current[taken] = CanBind(old) ? old : Free(taken);
            Store();
            return swapped;
        }

        public static void Reset()
        {
            foreach ((GameAction action, Bound bound) in Defaults) Current[action] = bound;
            Store();
        }

        private static Bound Plain(Bound bound) => bound.Key switch
        {
            Keys.RightControl => new(Keys.LeftControl),
            Keys.RightAlt => new(Keys.LeftAlt),
            Keys.RightShift => new(Keys.LeftShift),
            _ => bound,
        };

        /// <summary>Whether something can be given to an action at all.</summary>
        public static bool CanBind(Keys key) => key is not (Keys.Unknown or Keys.Escape or Keys.F3);

        public static bool CanBind(Bound bound) => bound.Button != null || CanBind(bound.Key);

        private static bool Held(Keys key)
        {
            var window = Engine.WindowHandler;
            return window.IsKeyDown(key) || key switch
            {
                Keys.LeftControl => window.IsKeyDown(Keys.RightControl),
                Keys.LeftAlt => window.IsKeyDown(Keys.RightAlt),
                Keys.LeftShift => window.IsKeyDown(Keys.RightShift),
                _ => false,
            };
        }

        /// <summary>Whether it's being held.</summary>
        public static bool Down(GameAction action)
        {
            Bound bound = Current[action];
            return bound.Button is MouseButton button ? Engine.WindowHandler.IsMouseButtonDown(button) : Held(bound.Key);
        }

        /// <summary>Whether it went down this frame, for something that isn't a click on the interface.</summary>
        public static bool Asked(GameAction action) => Pressed(action) && !(OnMouse(action) && Engine.Gui.WantsMouse);

        /// <summary>Whether it went down this frame.</summary>
        public static bool Pressed(GameAction action)
        {
            Bound bound = Current[action];
            return bound.Button is MouseButton button ? Engine.WindowHandler.IsMouseButtonPressed(button) : Engine.WindowHandler.IsKeyPressed(bound.Key);
        }

        /// <summary>Whether it's on the mouse, as that goes to the interface first.</summary>
        public static bool OnMouse(GameAction action) => Current[action].Button != null;

        /// <summary>What a place on the bar is pressed with.</summary>
        public static GameAction Slot(int slot) => GameAction.Slot0 + slot;

        /// <summary>How it's written, wherever a key is shown.</summary>
        public static string Label(GameAction action) => Label(Current[action]);

        public static string Label(Bound bound)
        {
            if (bound.Button is MouseButton button) return button switch
            {
                MouseButton.Left => "LMB",
                MouseButton.Right => "RMB",
                MouseButton.Middle => "MMB",
                _ => $"Mouse {(int)button + 1}",
            };

            Keys key = bound.Key;
            if (key is >= Keys.D0 and <= Keys.D9) return ((int)key - (int)Keys.D0).ToString();
            if (key is >= Keys.KeyPad0 and <= Keys.KeyPad9) return $"Pad {(int)key - (int)Keys.KeyPad0}";
            return key switch
            {
                Keys.LeftControl or Keys.RightControl => "Ctrl",
                Keys.LeftAlt or Keys.RightAlt => "Alt",
                Keys.LeftShift or Keys.RightShift => "Shift",
                Keys.Escape => "Esc",
                Keys.Enter => "Enter",
                Keys.Backspace => "Back",
                Keys.CapsLock => "Caps",
                Keys.PageUp => "Pg Up",
                Keys.PageDown => "Pg Dn",
                Keys.Comma => ",",
                Keys.Period => ".",
                Keys.Slash => "/",
                Keys.Backslash => "\\",
                Keys.Semicolon => ";",
                Keys.Apostrophe => "'",
                Keys.LeftBracket => "[",
                Keys.RightBracket => "]",
                Keys.Minus => "-",
                Keys.Equal => "=",
                Keys.GraveAccent => "`",
                _ => key.ToString(),
            };
        }
    }

    /// <summary>
    /// Where a player's settings live: beside their saves and not in the game's own folder, as they belong to them and not to a build. A scripted run has no business reading or changing anyone's, so it gets the defaults,
    /// unless it's been given a folder of its own.
    /// </summary>
    public static class SettingsStore
    {
        private static string? _folder;
        private static bool _frozen;

        public static void Use(string? folder, bool scripted)
        {
            _folder = folder;
            _frozen = scripted && folder == null;
        }

        private static string PathOf(string file)
        {
            string folder = _folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DinoDefenders");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, file);
        }

        /// <summary>What's kept in a file, or nothing. Settings that won't read are worth a warning, not a game that won't start.</summary>
        public static T? Read<T>(string file) where T : class
        {
            if (_frozen) return null;
            try
            {
                string path = PathOf(file);
                return System.IO.File.Exists(path) ? System.Text.Json.JsonSerializer.Deserialize<T>(System.IO.File.ReadAllText(path), Options) : null;
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                Console.WriteLine($"{file}: {e.Message}");
                return null;
            }
        }

        public static void Write<T>(string file, T settings)
        {
            if (_frozen) return;
            try
            {
                System.IO.File.WriteAllText(PathOf(file), System.Text.Json.JsonSerializer.Serialize(settings, Options));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"{file}: {e.Message}");
            }
        }

        private static readonly System.Text.Json.JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
    }
}
