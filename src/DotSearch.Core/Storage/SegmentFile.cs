using System.Buffers.Binary;
using System.Text;
using DotSearch.Index;

namespace DotSearch.Storage;

internal static class SegmentFile
{
    private static readonly byte[] Magic = "DSSEG001"u8.ToArray();

    public static SegmentReader Write(string segmentsDirectory, SegmentData data)
    {
        Directory.CreateDirectory(segmentsDirectory);
        string path = GetPath(segmentsDirectory, data.Id);
        string tempPath = path + ".tmp";

        using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(Magic);
            WriteInt64(stream, data.Id);
            VarInt.Write(stream, data.Documents.Count);

            foreach (SegmentDocument document in data.Documents)
            {
                VarInt.Write(stream, document.LocalId);
                WriteString(stream, document.Id.Value);
                VarInt.Write(stream, document.Fields.Count);
                foreach (KeyValuePair<string, string> field in document.Fields)
                {
                    WriteString(stream, field.Key);
                    WriteString(stream, field.Value);
                }
            }

            VarInt.Write(stream, data.FieldLengths.Count);
            foreach (KeyValuePair<string, Dictionary<int, int>> field in data.FieldLengths.OrderBy(static x => x.Key, StringComparer.Ordinal))
            {
                WriteString(stream, field.Key);
                VarInt.Write(stream, field.Value.Count);
                foreach (KeyValuePair<int, int> length in field.Value.OrderBy(static x => x.Key))
                {
                    VarInt.Write(stream, length.Key);
                    VarInt.Write(stream, length.Value);
                }
            }

            VarInt.Write(stream, data.PostingLists.Count);
            foreach (SegmentPostingList postingList in data.PostingLists
                .OrderBy(static x => x.Field, StringComparer.Ordinal)
                .ThenBy(static x => x.Term, StringComparer.Ordinal))
            {
                WriteString(stream, postingList.Field);
                WriteString(stream, postingList.Term);
                VarInt.Write(stream, postingList.Postings.Count);

                int previousDocId = 0;
                foreach (KeyValuePair<int, int> posting in postingList.Postings.OrderBy(static x => x.Key))
                {
                    VarInt.Write(stream, posting.Key - previousDocId);
                    VarInt.Write(stream, posting.Value);
                    previousDocId = posting.Key;
                }
            }
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(tempPath, path);

        return Read(path);
    }

    public static SegmentReader Read(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> magic = stackalloc byte[Magic.Length];
        ReadExactly(stream, magic);
        if (!magic.SequenceEqual(Magic))
        {
            throw new FormatException($"Invalid segment file: {path}");
        }

        long id = ReadInt64(stream);
        SegmentData data = new(id);

        int documentCount = VarInt.Read(stream);
        for (int i = 0; i < documentCount; i++)
        {
            int localId = VarInt.Read(stream);
            SegmentDocument document = new(localId, new DocumentId(ReadString(stream)));
            int fieldCount = VarInt.Read(stream);
            for (int j = 0; j < fieldCount; j++)
            {
                document.Fields[ReadString(stream)] = ReadString(stream);
            }
            data.Documents.Add(document);
        }

        int fieldLengthCount = VarInt.Read(stream);
        for (int i = 0; i < fieldLengthCount; i++)
        {
            string field = ReadString(stream);
            int count = VarInt.Read(stream);
            Dictionary<int, int> lengths = new();
            for (int j = 0; j < count; j++)
            {
                lengths[VarInt.Read(stream)] = VarInt.Read(stream);
            }
            data.FieldLengths[field] = lengths;
        }

        int postingListCount = VarInt.Read(stream);
        for (int i = 0; i < postingListCount; i++)
        {
            string field = ReadString(stream);
            string term = ReadString(stream);
            int count = VarInt.Read(stream);
            Dictionary<int, int> postings = new();
            int docId = 0;
            for (int j = 0; j < count; j++)
            {
                docId += VarInt.Read(stream);
                postings[docId] = VarInt.Read(stream);
            }
            data.PostingLists.Add(new SegmentPostingList(field, term, postings));
        }

        FileInfo file = new(path);
        return new SegmentReader(data, path, file.Length);
    }

    public static string GetPath(string segmentsDirectory, long segmentId)
    {
        return Path.Combine(segmentsDirectory, $"{segmentId:0000000000}.seg");
    }

    private static void WriteString(Stream stream, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        VarInt.Write(stream, byteCount);
        if (byteCount <= 256)
        {
            Span<byte> buffer = stackalloc byte[256];
            int written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
            stream.Write(buffer[..written]);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes);
    }

    private static string ReadString(Stream stream)
    {
        int byteCount = VarInt.Read(stream);
        if (byteCount == 0)
        {
            return string.Empty;
        }

        byte[] bytes = new byte[byteCount];
        ReadExactly(stream, bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static long ReadInt64(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        ReadExactly(stream, buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            offset += read;
        }
    }
}
