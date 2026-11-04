using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Dino_Engine.Networking
{
    /// <summary>
    /// Marks a component as part of what clients see. That is all it takes: any entity with a <see cref="NetIdentityComponent"/>
    /// has its marked components sent to every client it is relevant to, as they change, and they appear on the client's copy
    /// as the same component type. Every field goes unless it says otherwise.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class ReplicatedAttribute : Attribute
    {
        /// <summary>Another replicated component this one makes redundant: while an entity has this, that one isn't sent.</summary>
        public Type? Replaces { get; set; }
    }

    /// <summary>Keeps a field of a replicated component on the machine it is on: an AI's plans, a client's own bookkeeping.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NotReplicatedAttribute : Attribute
    {
    }

    /// <summary>How finely a float or vector is sent. Coarser costs fewer bits, and changes too small to matter cost none.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NetStepAttribute : Attribute
    {
        public readonly float Step;

        public NetStepAttribute(float step)
        {
            Step = step;
        }
    }

    /// <summary>A number that jumps rather than glides: clients take the new value as it comes, rather than blending towards it.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NetSnapAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a struct as something that happens rather than something that is: a spark, a number floating up. Events are for
    /// show. They are resent until received or stale, and can still be missed, so nothing that matters may depend on one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NetEventAttribute : Attribute
    {
    }

    /// <summary>
    /// Everything that can cross the network, worked out once from the attributes on the game's types. Each field becomes one or
    /// more integer lanes in a row, which is what is compared, delta coded and stored. Both ends build this from their own code,
    /// and the <see cref="Hash"/> is compared when a player joins: two different builds are told so, rather than quietly
    /// reading each other's rows wrongly.
    /// </summary>
    public static class NetSchema
    {
        private static readonly object Lock = new();
        private static bool _built;
        private static ComponentCodec[] _components = Array.Empty<ComponentCodec>();
        private static EventCodec[] _events = Array.Empty<EventCodec>();
        private static readonly Dictionary<Type, int> ComponentIndex = new();
        private static readonly Dictionary<Type, int> EventIndex = new();

        /// <summary>Changes whenever anything sent over the network does.</summary>
        public static uint Hash { get; private set; }

        internal static ComponentCodec[] Components => _components;
        internal static EventCodec[] Events => _events;

        /// <summary>Scans the running program. ECS components must be registered first. Doing it again does nothing.</summary>
        public static void Build()
        {
            lock (Lock)
            {
                if (_built) return;

                Assembly engine = typeof(NetSchema).Assembly;
                string engineName = engine.GetName().Name!;
                var types = new List<Type>();
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic) continue;
                    if (assembly != engine && !assembly.GetReferencedAssemblies().Any(a => a.Name == engineName)) continue;
                    types.AddRange(assembly.GetTypes());
                }

                // Sorted by name: both ends must number these alike, whatever order their runtimes found them in.
                types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

                var components = new List<ComponentCodec>();
                var events = new List<EventCodec>();
                foreach (Type type in types)
                {
                    if (type.GetCustomAttribute<ReplicatedAttribute>() != null)
                    {
                        var codec = (ComponentCodec)Activator.CreateInstance(typeof(ComponentCodec<>).MakeGenericType(type))!;
                        codec.Index = components.Count;
                        ComponentIndex[type] = codec.Index;
                        components.Add(codec);
                    }
                    if (type.GetCustomAttribute<NetEventAttribute>() != null)
                    {
                        var codec = (EventCodec)Activator.CreateInstance(typeof(EventCodec<>).MakeGenericType(type))!;
                        codec.Index = events.Count;
                        EventIndex[type] = codec.Index;
                        events.Add(codec);
                    }
                }

                if (components.Count > 64) throw new InvalidOperationException("More than 64 replicated component types: a ghost's component mask no longer fits.");
                if (events.Count > 255) throw new InvalidOperationException("More than 255 event types.");

                foreach (ComponentCodec codec in components)
                {
                    Type? replaced = codec.Type.GetCustomAttribute<ReplicatedAttribute>()!.Replaces;
                    if (replaced == null) continue;
                    if (!ComponentIndex.TryGetValue(replaced, out int index)) throw new InvalidOperationException($"{codec.Type.Name} replaces {replaced.Name}, which isn't replicated.");
                    codec.Replaced = 1ul << index;
                }

                _components = components.ToArray();
                _events = events.ToArray();

                var signature = new StringBuilder();
                foreach (ComponentCodec codec in _components) signature.Append(codec.Signature).Append('\n');
                foreach (EventCodec codec in _events) signature.Append(codec.Signature).Append('\n');
                Hash = Fnv(signature.ToString());
                _built = true;
            }
        }

        public static int IndexOfComponent(Type type) => ComponentIndex[type];

        internal static int IndexOfEvent(Type type)
        {
            if (!EventIndex.TryGetValue(type, out int index)) throw new InvalidOperationException($"{type.Name} isn't marked [NetEvent].");
            return index;
        }

        // string.GetHashCode changes from run to run, which is no use for comparing two machines.
        public static uint Fnv(string text)
        {
            uint hash = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return hash;
        }
    }

    /// <summary>
    /// Strings in components are sent once and referred to by number from then on, which suits names and prompts. Anything that
    /// keeps changing belongs in a number instead: the table only grows.
    /// </summary>
    public sealed class NetStrings
    {
        public const int MostStrings = 65536;
        public const int LongestString = 256;

        private readonly List<string> _texts = new() { "" };
        private readonly Dictionary<string, int> _ids = new() { [""] = 0 };

        public int Count => _texts.Count;

        /// <summary>The highest number handed out since this was last set to 0: what a row just captured refers to.</summary>
        internal int Highest;

        public int Intern(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (!_ids.TryGetValue(text, out int id))
            {
                id = _texts.Count;
                _texts.Add(text);
                _ids.Add(text, id);
            }
            if (id > Highest) Highest = id;
            return id;
        }

        public string Get(int id) => id >= 0 && id < _texts.Count ? _texts[id] : "";

        /// <summary>Stores what the server calls <paramref name="id"/>. They arrive in order, each one the next.</summary>
        internal void Define(int id, string text)
        {
            if (id != _texts.Count || id >= MostStrings) return;
            _texts.Add(text);
        }
    }

    internal delegate TField FieldGetter<T, TField>(ref T target);
    internal delegate void FieldSetter<T, TField>(ref T target, TField value);

    /// <summary>One field of a component or event, as lanes in a row.</summary>
    internal abstract class NetField<T> where T : struct
    {
        public abstract int Lanes { get; }
        public abstract string Signature { get; }
        public abstract void Capture(ref T source, int[] row, int at, NetStrings strings);
        public abstract void Apply(ref T target, int[] row, int at, NetStrings strings);

        /// <summary>The value <paramref name="t"/> of the way from one row to the next. Unless it can glide, a field keeps the older value until the newer one's time has come.</summary>
        public virtual void Blend(ref T target, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings)
        {
            Apply(ref target, from, fromAt, strings);
        }

        /// <summary>Events skip rows, and carry their strings whole, as those are as likely to be different every time.</summary>
        public virtual void Write(ref T source, BitWriter writer, int[] scratch, NetStrings strings)
        {
            Capture(ref source, scratch, 0, strings);
            for (int i = 0; i < Lanes; i++) writer.WriteDelta(scratch[i]);
        }

        public virtual void Read(ref T target, BitReader reader, int[] scratch, NetStrings strings)
        {
            for (int i = 0; i < Lanes; i++) scratch[i] = reader.ReadDelta();
            Apply(ref target, scratch, 0, strings);
        }
    }

    internal sealed class IntegerField<T> : NetField<T> where T : struct
    {
        private readonly FieldGetter<T, int> _get;
        private readonly FieldSetter<T, int> _set;
        private readonly string _signature;

        public IntegerField(FieldInfo field)
        {
            _get = NetFields.Getter<T, int>(field);
            _set = NetFields.Setter<T, int>(field);
            _signature = $"{field.Name}:{field.FieldType.Name}";
        }

        public override int Lanes => 1;
        public override string Signature => _signature;
        public override void Capture(ref T source, int[] row, int at, NetStrings strings) => row[at] = _get(ref source);
        public override void Apply(ref T target, int[] row, int at, NetStrings strings) => _set(ref target, row[at]);
    }

    internal sealed class FloatField<T> : NetField<T> where T : struct
    {
        private readonly FieldGetter<T, float> _get;
        private readonly FieldSetter<T, float> _set;
        private readonly float _step;
        private readonly float _inverseStep;
        private readonly bool _snap;
        private readonly string _signature;

        public FloatField(FieldInfo field, float step, bool snap)
        {
            _get = NetFields.Getter<T, float>(field);
            _set = NetFields.Setter<T, float>(field);
            _step = step;
            _inverseStep = 1f / step;
            _snap = snap;
            _signature = $"{field.Name}:float:{NetFields.StepText(step)}:{snap}";
        }

        public override int Lanes => 1;
        public override string Signature => _signature;
        public override void Capture(ref T source, int[] row, int at, NetStrings strings) => row[at] = NetFields.Quantise(_get(ref source), _inverseStep);
        public override void Apply(ref T target, int[] row, int at, NetStrings strings) => _set(ref target, row[at] * _step);

        public override void Blend(ref T target, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings)
        {
            if (_snap)
            {
                Apply(ref target, from, fromAt, strings);
                return;
            }
            float a = from[fromAt] * _step;
            _set(ref target, a + (to[toAt] * _step - a) * t);
        }
    }

    internal sealed class Vector2Field<T> : NetField<T> where T : struct
    {
        private readonly FieldGetter<T, Vector2> _get;
        private readonly FieldSetter<T, Vector2> _set;
        private readonly float _step;
        private readonly float _inverseStep;
        private readonly bool _snap;
        private readonly string _signature;

        public Vector2Field(FieldInfo field, float step, bool snap)
        {
            _get = NetFields.Getter<T, Vector2>(field);
            _set = NetFields.Setter<T, Vector2>(field);
            _step = step;
            _inverseStep = 1f / step;
            _snap = snap;
            _signature = $"{field.Name}:vec2:{NetFields.StepText(step)}:{snap}";
        }

        public override int Lanes => 2;
        public override string Signature => _signature;

        public override void Capture(ref T source, int[] row, int at, NetStrings strings)
        {
            Vector2 value = _get(ref source);
            row[at] = NetFields.Quantise(value.X, _inverseStep);
            row[at + 1] = NetFields.Quantise(value.Y, _inverseStep);
        }

        public override void Apply(ref T target, int[] row, int at, NetStrings strings)
        {
            _set(ref target, new Vector2(row[at] * _step, row[at + 1] * _step));
        }

        public override void Blend(ref T target, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings)
        {
            Vector2 a = new Vector2(from[fromAt] * _step, from[fromAt + 1] * _step);
            Vector2 b = new Vector2(to[toAt] * _step, to[toAt + 1] * _step);
            _set(ref target, _snap || (b - a).LengthSquared > NetFields.JumpSquared ? a : a + (b - a) * t);
        }
    }

    internal sealed class Vector3Field<T> : NetField<T> where T : struct
    {
        private readonly FieldGetter<T, Vector3> _get;
        private readonly FieldSetter<T, Vector3> _set;
        private readonly float _step;
        private readonly float _inverseStep;
        private readonly bool _snap;
        private readonly string _signature;

        public Vector3Field(FieldInfo field, float step, bool snap)
        {
            _get = NetFields.Getter<T, Vector3>(field);
            _set = NetFields.Setter<T, Vector3>(field);
            _step = step;
            _inverseStep = 1f / step;
            _snap = snap;
            _signature = $"{field.Name}:vec3:{NetFields.StepText(step)}:{snap}";
        }

        public override int Lanes => 3;
        public override string Signature => _signature;

        public override void Capture(ref T source, int[] row, int at, NetStrings strings)
        {
            Vector3 value = _get(ref source);
            row[at] = NetFields.Quantise(value.X, _inverseStep);
            row[at + 1] = NetFields.Quantise(value.Y, _inverseStep);
            row[at + 2] = NetFields.Quantise(value.Z, _inverseStep);
        }

        public override void Apply(ref T target, int[] row, int at, NetStrings strings)
        {
            _set(ref target, new Vector3(row[at] * _step, row[at + 1] * _step, row[at + 2] * _step));
        }

        public override void Blend(ref T target, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings)
        {
            Vector3 a = new Vector3(from[fromAt] * _step, from[fromAt + 1] * _step, from[fromAt + 2] * _step);
            Vector3 b = new Vector3(to[toAt] * _step, to[toAt + 1] * _step, to[toAt + 2] * _step);

            // Someone who covers this much between two samples didn't walk there.
            _set(ref target, _snap || (b - a).LengthSquared > NetFields.JumpSquared ? a : a + (b - a) * t);
        }
    }

    /// <summary>Characters only ever turn about the vertical, so a rotation is a single angle: a tenth of the size of a quaternion.</summary>
    internal sealed class YawField<T> : NetField<T> where T : struct
    {
        private const int StepsPerTurn = 1024;

        private readonly FieldGetter<T, Quaternion> _get;
        private readonly FieldSetter<T, Quaternion> _set;
        private readonly string _signature;

        public YawField(FieldInfo field)
        {
            _get = NetFields.Getter<T, Quaternion>(field);
            _set = NetFields.Setter<T, Quaternion>(field);
            _signature = $"{field.Name}:yaw:{StepsPerTurn}";
        }

        public override int Lanes => 1;
        public override string Signature => _signature;

        public override void Capture(ref T source, int[] row, int at, NetStrings strings)
        {
            Quaternion value = _get(ref source);
            float yaw = 2f * MathF.Atan2(value.Y, value.W);
            row[at] = ((int)MathF.Round(yaw / MathF.Tau * StepsPerTurn) % StepsPerTurn + StepsPerTurn) % StepsPerTurn;
        }

        public override void Apply(ref T target, int[] row, int at, NetStrings strings)
        {
            _set(ref target, Quaternion.FromAxisAngle(Vector3.UnitY, row[at] * (MathF.Tau / StepsPerTurn)));
        }

        public override void Blend(ref T target, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings)
        {
            int difference = to[toAt] - from[fromAt];
            if (difference > StepsPerTurn / 2) difference -= StepsPerTurn;
            else if (difference < -StepsPerTurn / 2) difference += StepsPerTurn;
            _set(ref target, Quaternion.FromAxisAngle(Vector3.UnitY, (from[fromAt] + difference * t) * (MathF.Tau / StepsPerTurn)));
        }
    }

    internal sealed class StringField<T> : NetField<T> where T : struct
    {
        private readonly FieldGetter<T, string?> _get;
        private readonly FieldSetter<T, string?> _set;
        private readonly string _signature;

        public StringField(FieldInfo field)
        {
            _get = NetFields.Getter<T, string?>(field);
            _set = NetFields.Setter<T, string?>(field);
            _signature = $"{field.Name}:string";
        }

        public override int Lanes => 1;
        public override string Signature => _signature;
        public override void Capture(ref T source, int[] row, int at, NetStrings strings) => row[at] = strings.Intern(_get(ref source));
        public override void Apply(ref T target, int[] row, int at, NetStrings strings) => _set(ref target, strings.Get(row[at]));
        public override void Write(ref T source, BitWriter writer, int[] scratch, NetStrings strings) => writer.WriteString(_get(ref source) ?? "");
        public override void Read(ref T target, BitReader reader, int[] scratch, NetStrings strings) => _set(ref target, reader.ReadString(NetStrings.LongestString));
    }

    internal static class NetFields
    {
        public const float DefaultStep = 1f / 64f;
        public const float JumpSquared = 12f * 12f;

        // Invariant, or two players whose systems write decimals differently would be told they run different builds.
        public static string StepText(float step) => step.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        public static int Quantise(float value, float inverseStep)
        {
            double scaled = Math.Round((double)value * inverseStep);
            if (double.IsNaN(scaled)) return 0;
            if (scaled >= int.MaxValue) return int.MaxValue;
            if (scaled <= int.MinValue) return int.MinValue;
            return (int)scaled;
        }

        public static FieldGetter<T, TField> Getter<T, TField>(FieldInfo field)
        {
            ParameterExpression target = Expression.Parameter(typeof(T).MakeByRefType(), "target");
            Expression value = Expression.Field(target, field);
            if (field.FieldType != typeof(TField)) value = Convert(value, typeof(TField));
            return Expression.Lambda<FieldGetter<T, TField>>(value, target).Compile();
        }

        public static FieldSetter<T, TField> Setter<T, TField>(FieldInfo field)
        {
            ParameterExpression target = Expression.Parameter(typeof(T).MakeByRefType(), "target");
            ParameterExpression value = Expression.Parameter(typeof(TField), "value");
            Expression converted = field.FieldType != typeof(TField) ? Convert(value, field.FieldType) : value;
            return Expression.Lambda<FieldSetter<T, TField>>(Expression.Assign(Expression.Field(target, field), converted), target, value).Compile();
        }

        // Booleans don't convert to numbers, even for Expression.
        private static Expression Convert(Expression value, Type to)
        {
            if (value.Type == typeof(bool)) return Expression.Condition(value, Expression.Constant(1), Expression.Constant(0));
            if (to == typeof(bool)) return Expression.NotEqual(value, Expression.Constant(0));
            return Expression.Convert(value, to);
        }

        public static NetField<T>[] For<T>() where T : struct
        {
            var fields = new List<NetField<T>>();
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).OrderBy(f => f.MetadataToken))
            {
                if (field.GetCustomAttribute<NotReplicatedAttribute>() != null) continue;
                fields.Add(Create<T>(field));
            }
            return fields.ToArray();
        }

        private static NetField<T> Create<T>(FieldInfo field) where T : struct
        {
            float step = field.GetCustomAttribute<NetStepAttribute>()?.Step ?? DefaultStep;
            bool snap = field.GetCustomAttribute<NetSnapAttribute>() != null;
            Type type = field.FieldType;

            if (type == typeof(float)) return new FloatField<T>(field, step, snap);
            if (type == typeof(Vector2)) return new Vector2Field<T>(field, step, snap);
            if (type == typeof(Vector3)) return new Vector3Field<T>(field, step, snap);
            if (type == typeof(Quaternion)) return new YawField<T>(field);
            if (type == typeof(string)) return new StringField<T>(field);
            if (type == typeof(int) || type == typeof(uint) || type == typeof(byte) || type == typeof(bool) || type.IsEnum) return new IntegerField<T>(field);

            throw new InvalidOperationException(
                $"{typeof(T).Name}.{field.Name} is a {type.Name}, which can't be sent. Mark it [NotReplicated], or use a number, vector, string or enum.");
        }

        public static string Signature<T>(NetField<T>[] fields) where T : struct
        {
            return $"{typeof(T).FullName}{{{string.Join(",", fields.Select(f => f.Signature))}}}";
        }
    }

    /// <summary>Reads and writes one replicated component type on the ECS.</summary>
    internal abstract class ComponentCodec
    {
        public int Index;
        public ulong Replaced;
        public abstract Type Type { get; }
        public abstract int Lanes { get; }
        public abstract string Signature { get; }
        public abstract void Capture(Archetype archetype, int entity, int[] row, int at, NetStrings strings);
        public abstract bool Has(EntityView view);
        public abstract void Apply(EntityView view, int[] row, int at, NetStrings strings);
        public abstract void Blend(EntityView view, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings);
        public abstract IComponent Create(int[] row, int at, NetStrings strings);
    }

    internal sealed class ComponentCodec<T> : ComponentCodec where T : struct, IComponent
    {
        private readonly NetField<T>[] _fields = NetFields.For<T>();
        private readonly int _lanes;
        private readonly string _signature;

        public ComponentCodec()
        {
            foreach (NetField<T> field in _fields) _lanes += field.Lanes;
            _signature = NetFields.Signature(_fields);
        }

        public override Type Type => typeof(T);
        public override int Lanes => _lanes;
        public override string Signature => _signature;

        public override void Capture(Archetype archetype, int entity, int[] row, int at, NetStrings strings)
        {
            T component = archetype.GetComponentArray<T>()![entity];
            foreach (NetField<T> field in _fields)
            {
                field.Capture(ref component, row, at, strings);
                at += field.Lanes;
            }
        }

        public override bool Has(EntityView view) => view.Has<T>();

        public override void Apply(EntityView view, int[] row, int at, NetStrings strings)
        {
            T component = view.Get<T>();
            foreach (NetField<T> field in _fields)
            {
                field.Apply(ref component, row, at, strings);
                at += field.Lanes;
            }
            view.Set(component);
        }

        public override void Blend(EntityView view, int[] from, int fromAt, int[] to, int toAt, float t, NetStrings strings)
        {
            T component = view.Get<T>();
            foreach (NetField<T> field in _fields)
            {
                field.Blend(ref component, from, fromAt, to, toAt, t, strings);
                fromAt += field.Lanes;
                toAt += field.Lanes;
            }
            view.Set(component);
        }

        public override IComponent Create(int[] row, int at, NetStrings strings)
        {
            T component = default;
            foreach (NetField<T> field in _fields)
            {
                field.Apply(ref component, row, at, strings);
                at += field.Lanes;
            }
            return component;
        }
    }

    /// <summary>Reads and writes one event type.</summary>
    internal abstract class EventCodec
    {
        public int Index;
        public abstract string Signature { get; }
        public abstract void Dispatch(BitReader reader, uint tick, NetEventHandlers handlers);
    }

    internal sealed class EventCodec<T> : EventCodec where T : struct
    {
        private static readonly NetStrings NoStrings = new();

        private readonly NetField<T>[] _fields = NetFields.For<T>();
        private readonly int[] _scratch = new int[4];

        public override string Signature => NetFields.Signature(_fields);

        public void Write(ref T value, BitWriter writer)
        {
            int[] scratch = new int[4];
            foreach (NetField<T> field in _fields) field.Write(ref value, writer, scratch, NoStrings);
        }

        // Reading has to get through the event's bits whether anyone wants it or not.
        public override void Dispatch(BitReader reader, uint tick, NetEventHandlers handlers)
        {
            T value = default;
            foreach (NetField<T> field in _fields) field.Read(ref value, reader, _scratch, NoStrings);
            handlers.Queue(value, tick);
        }
    }
}
