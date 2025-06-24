using System;
using System.Collections.Generic;

public class IDManager<T> where T : unmanaged, IComparable<T>
{
    private Stack<T> _free = new();
    private T _next;
    private T _max;

    private Func<T, T> _increment;

    public IDManager()
    {
        _next = One();
        _max = MaxValue();

        if (typeof(T) == typeof(byte))
            _increment = (Func<T, T>)(object)(Func<byte, byte>)(v => (byte)(v + 1));
        else if (typeof(T) == typeof(ushort))
            _increment = (Func<T, T>)(object)(Func<ushort, ushort>)(v => (ushort)(v + 1));
        else if (typeof(T) == typeof(uint))
            _increment = (Func<T, T>)(object)(Func<uint, uint>)(v => v + 1);
        else if (typeof(T) == typeof(ulong))
            _increment = (Func<T, T>)(object)(Func<ulong, ulong>)(v => v + 1);
        else
            throw new NotSupportedException();
    }

    public T Allocate()
    {
        if (_free.Count > 0)
            return _free.Pop();

        if (_next.CompareTo(_max) > 0)
            throw new OverflowException();

        var result = _next;
        _next = _increment(_next);
        return result;
    }

    public void Release(T id)
    {
        _free.Push(id);
    }

    private static T One()
    {
        if (typeof(T) == typeof(byte)) return (T)(object)(byte)1;
        if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)1;
        if (typeof(T) == typeof(uint)) return (T)(object)(uint)1;
        if (typeof(T) == typeof(ulong)) return (T)(object)(ulong)1;
        throw new NotSupportedException();
    }

    private static T MaxValue()
    {
        if (typeof(T) == typeof(byte)) return (T)(object)byte.MaxValue;
        if (typeof(T) == typeof(ushort)) return (T)(object)ushort.MaxValue;
        if (typeof(T) == typeof(uint)) return (T)(object)uint.MaxValue;
        if (typeof(T) == typeof(ulong)) return (T)(object)ulong.MaxValue;
        throw new NotSupportedException();
    }
}