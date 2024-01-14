using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace _1brc;

internal static class Program
{
    const byte SepChar = (byte)';';
    const byte CarriageReturn = (byte)'\r';
    const byte LineFeedChar = (byte)'\n';

    static void Main(string[] args)
    {
        Stopwatch sw = Stopwatch.StartNew();

        FileInfo input = new(args.Length > 0 ? args[0] : @"C:\Scratch\Downloads\1brc\measurements.small.txt");

        GetChunks(input, (uint)Environment.ProcessorCount, out bool hasCarriageReturn, out (long start, long end)[] chunks);

        int carriageReturnOffset = hasCarriageReturn ? 1 : 0;

        using MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(input.FullName, FileMode.Open);

        IOrderedEnumerable<Stat> result = chunks
            .AsParallel()
            .Aggregate(
            () => new Dictionary<int, Stat>(10_000),
            (localDic, item) => ProcessChunk(mmf, item.start, item.end, carriageReturnOffset, localDic),
            (finalResult, localDic) =>
            {
                foreach (Stat item in localDic.Values)
                {
                    ref Stat stat = ref CollectionsMarshal.GetValueRefOrAddDefault(finalResult, item.NameHash, out bool exist);

                    if (exist)
                    {
                        stat.Apply(in item);
                    }
                    else
                    {
                        stat = item;
                    }
                }

                return finalResult;
            },
            finalResult => finalResult.Values.OrderBy(x => x.Name, StringComparer.Ordinal));

        StringBuilder builder = new();
        foreach (Stat item in result)
        {
            item.Write(builder);
            builder.AppendLine();
        }

        sw.Stop();
        
        builder.AppendLine().AppendFormat("Processed in: {0}", sw.Elapsed);
        Console.WriteLine(builder.ToString());
    }

    private static Dictionary<int, Stat> ProcessChunk(
        MemoryMappedFile input, long start, long end, int carriageReturnOffset, Dictionary<int, Stat> calculations)
    {
        const int InitialBufferSize = 1024 * 1024;
        byte[] buffer = new byte[InitialBufferSize];

        int bytesRead;
        int leftOver = 0;
        
        using MemoryMappedViewStream mms = input.CreateViewStream(start, end - start, MemoryMappedFileAccess.Read);

        while ((bytesRead = mms.Read(buffer, leftOver, buffer.Length - leftOver)) > 0)
        {
            ReadOnlySpan<byte> remainder = buffer.AsSpan(0, bytesRead + leftOver);
            while (true)
            {
                if (remainder.Length == 0) { break; }

                int lineFeedIdx = remainder.IndexOf(LineFeedChar);
                if (lineFeedIdx < 0)
                {
                    if (remainder.Length == buffer.Length)
                    {
                        buffer = new byte[buffer.Length * 2];
                    }
                    remainder.CopyTo(buffer);
                    break;
                }

                ReadOnlySpan<byte> linePart = remainder.Slice(0, lineFeedIdx - carriageReturnOffset);

                int sepIdx = linePart.IndexOf(SepChar);

                ReadOnlySpan<byte> namePart = linePart.Slice(0, sepIdx);
                ReadOnlySpan<byte> floatPart = linePart.Slice(sepIdx + 1);

                int nameHash = CalculateNameHash(namePart);
                int temperature = ParseTemperature(floatPart);

                ref Stat stat = ref CollectionsMarshal.GetValueRefOrAddDefault(calculations, nameHash, out bool exist);
                if (exist)
                {
                    stat.Apply(temperature);
                }
                else
                {
                    stat = new Stat(Encoding.UTF8.GetString(namePart), nameHash, temperature);
                }

                remainder = remainder.Slice(lineFeedIdx + 1);
            }
            leftOver = remainder.Length;
        }

        return calculations;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int CalculateNameHash(ReadOnlySpan<byte> input) =>
        input.Length switch
        {
            > 3 => (input.Length * 820243) ^ (int)MemoryMarshal.Read<uint>(input),
            > 1 => MemoryMarshal.Read<ushort>(input),
            > 0 => input[0],
            _ => 0
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ParseTemperature(ReadOnlySpan<byte> input)
    {
        const byte DashChar = (byte)'-';
        const byte DotChar = (byte)'.';
        const byte ZeroChar = (byte)'0';

        ref byte firstByte = ref Unsafe.AsRef(in input[0]);

        int tempLength = 0;
        bool isNeg = firstByte == DashChar;
        if (isNeg)
        {
            tempLength++;
            firstByte = ref Unsafe.Add(ref firstByte, tempLength);
        }

        int temp = 0;
        while (firstByte != DotChar)
        {
            tempLength++;
            temp = temp * 10 + (firstByte - ZeroChar);
            firstByte = ref Unsafe.Add(ref firstByte, 1);
        }

        byte dec = input[tempLength + 1];
        temp = temp * 10 + (dec - ZeroChar);

        if (isNeg)
        {
            temp = -temp;
        }

        return temp;
    }

    private static void GetChunks(FileInfo input, uint count, out bool hasCarriageReturn, out (long start, long end)[] chunks)
    {
        const int BufferSize = 1024 * 1024;

        hasCarriageReturn = false;

        using FileStream fs = input.OpenRead();
        long chunkSize = input.Length / count;

        byte[] buffer = new byte[BufferSize];

        if (chunkSize <= BufferSize)
        {
            int bytesRead = fs.Read(buffer, 0, buffer.Length);
            ReadOnlySpan<byte> bufferSpan = buffer.AsSpan(0, bytesRead);
            int indexOfLineDelimiter = bufferSpan.IndexOf(LineFeedChar);
            if (indexOfLineDelimiter > 0 && bufferSpan[indexOfLineDelimiter - 1] == CarriageReturn)
            {
                hasCarriageReturn = true;
            }
            chunks = [(0, input.Length)];
            return;
        }

        long startingPos = 0;

        chunks = new (long start, long end)[(int)count];
        for (int i = 0; i < count; i++)
        {
            if (i == count - 1)
            {
                chunks[i] = (startingPos, input.Length);
                break;
            }

            long newEndPos = startingPos + chunkSize - buffer.Length;
            fs.Position = newEndPos;
            _ = fs.Read(buffer, 0, buffer.Length);
            int indexOfLineDelimiter = Array.LastIndexOf(buffer, LineFeedChar);

            if (i == 1 && indexOfLineDelimiter > 0 && buffer[indexOfLineDelimiter - 1] == CarriageReturn)
            {
                hasCarriageReturn = true;
            }

            long finalEndPos = newEndPos + indexOfLineDelimiter;
            chunks[i] = (startingPos, finalEndPos);
            startingPos = finalEndPos + 1;
        }
    }
}