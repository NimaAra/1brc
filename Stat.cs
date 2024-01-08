using System.Runtime.CompilerServices;
using System.Text;

namespace _1brc;

public record struct Stat
{
    private short _min, _max;
    private long _sum;
    private int _count;

    public Stat(string name, int hash, int value)
    {
        Name = name;
        NameHash = hash;
        Apply(value);
    }

    public readonly string Name;
    public readonly int NameHash;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply(int value)
    {
        _sum += value;
        _count++;

        _min = (short)GetMin(_min, value);
        _max = (short)GetMax(_max, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetMin(int a, int b)
        {
            int diff = a - b;
            return b + (diff & (diff >> 31));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetMax(int a, int b)
        {
            int dif = a - b;
            return a - (dif & (dif >> 31));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply(in Stat other)
    {
        _sum += other._sum;
        _count += other._count;

        if (other._min < _min)
        {
            _min = other._min;
        }

        if (other._max > _max)
        {
            _max = other._max;
        }
    }

    public readonly void Write(StringBuilder builder) =>
        builder.Append(Name).Append(" = ")
            .AppendFormat("{0:N1}", _min * 0.1f).Append('/')
            .AppendFormat("{0:N1}", ((float)_sum / _count) * 0.1f).Append('/')
            .AppendFormat("{0:N1}", _max * 0.1f);
}