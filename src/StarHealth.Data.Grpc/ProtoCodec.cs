using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace StarHealth.Data.Grpc;

/// <summary>
/// Minimal protobuf codec that works WITHOUT compiled .proto files.
/// Field numbers/names are discovered at runtime via gRPC server
/// reflection, so this keeps working across dish firmware updates that
/// renumber fields. Only field *names* (stable) are depended upon.
/// </summary>
internal static class ProtoCodec
{
    public static byte[] EmptyRequest(int fieldNumber)
    {
        using var ms = new MemoryStream();
        var cos = new CodedOutputStream(ms);
        cos.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        cos.WriteLength(0);
        cos.Flush();
        return ms.ToArray();
    }

    public sealed class Msg : Dictionary<string, object?> { }

    public static Msg Parse(byte[] bytes, DescriptorProto schema)
    {
        var byNumber = schema.Field.ToDictionary(f => f.Number);
        var msg = new Msg();
        var cis = new CodedInputStream(bytes);

        uint tag;
        while ((tag = cis.ReadTag()) != 0)
        {
            int number = WireFormat.GetTagFieldNumber(tag);
            if (!byNumber.TryGetValue(number, out var field))
            {
                cis.SkipLastField();
                continue;
            }
            object? value = ReadValue(cis, tag, field);
            if (field.IsRepeated())
            {
                if (!msg.TryGetValue(field.Name, out var existing) || existing is not List<object?> list)
                {
                    list = new List<object?>();
                    msg[field.Name] = list;
                }
                if (value is List<object?> packed)
                    list.AddRange(packed);
                else
                    list.Add(value);
            }
            else
            {
                msg[field.Name] = value;
            }
        }
        return msg;
    }

    private static object? ReadValue(CodedInputStream cis, uint tag, FieldDescriptorProto field)
    {
        // Packed repeated numerics arrive as one length-delimited blob.
        if (field.IsRepeated() && IsPackable(field.Type) &&
            WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
        {
            byte[] blob = cis.ReadBytes().ToByteArray();
            var inner = new CodedInputStream(blob);
            var list = new List<object?>();
            // Packed values have no tags; read scalars until exhausted.
            while (!inner.IsAtEnd) list.Add(ReadScalar(inner, field.Type));
            return list;
        }
        return ReadScalar(cis, field.Type);
    }

    private static object? ReadScalar(CodedInputStream cis, FieldDescriptorProto.Types.Type type)
    {
        switch (type)
        {
            case FieldDescriptorProto.Types.Type.Double: return cis.ReadDouble();
            case FieldDescriptorProto.Types.Type.Float: return (double)cis.ReadFloat();
            case FieldDescriptorProto.Types.Type.Int32:
            case FieldDescriptorProto.Types.Type.Sfixed32:
            case FieldDescriptorProto.Types.Type.Sint32: return (long)cis.ReadInt32();
            case FieldDescriptorProto.Types.Type.Int64:
            case FieldDescriptorProto.Types.Type.Sfixed64:
            case FieldDescriptorProto.Types.Type.Sint64: return cis.ReadInt64();
            case FieldDescriptorProto.Types.Type.Uint32:
            case FieldDescriptorProto.Types.Type.Fixed32: return (long)cis.ReadUInt32();
            case FieldDescriptorProto.Types.Type.Uint64:
            case FieldDescriptorProto.Types.Type.Fixed64: return (long)cis.ReadUInt64();
            case FieldDescriptorProto.Types.Type.Bool: return cis.ReadBool();
            case FieldDescriptorProto.Types.Type.String: return cis.ReadString();
            case FieldDescriptorProto.Types.Type.Enum: return (long)cis.ReadEnum();
            case FieldDescriptorProto.Types.Type.Bytes: return cis.ReadBytes().ToByteArray();
            case FieldDescriptorProto.Types.Type.Message:
            case FieldDescriptorProto.Types.Type.Group:
                // Nested schema unknown here; return raw bytes and let the
                // caller re-parse with the nested descriptor.
                return cis.ReadBytes().ToByteArray();
            default: cis.SkipLastField(); return null;
        }
    }

    private static bool IsPackable(FieldDescriptorProto.Types.Type t) => t switch
    {
        FieldDescriptorProto.Types.Type.Double => true,
        FieldDescriptorProto.Types.Type.Float => true,
        FieldDescriptorProto.Types.Type.Int32 => true,
        FieldDescriptorProto.Types.Type.Int64 => true,
        FieldDescriptorProto.Types.Type.Uint32 => true,
        FieldDescriptorProto.Types.Type.Uint64 => true,
        FieldDescriptorProto.Types.Type.Sint32 => true,
        FieldDescriptorProto.Types.Type.Sint64 => true,
        FieldDescriptorProto.Types.Type.Fixed32 => true,
        FieldDescriptorProto.Types.Type.Fixed64 => true,
        FieldDescriptorProto.Types.Type.Sfixed32 => true,
        FieldDescriptorProto.Types.Type.Sfixed64 => true,
        FieldDescriptorProto.Types.Type.Bool => true,
        FieldDescriptorProto.Types.Type.Enum => true,
        _ => false,
    };

    internal static bool IsRepeated(this FieldDescriptorProto f)
        => f.Label == FieldDescriptorProto.Types.Label.Repeated;
}

internal static class MsgExt
{
    public static double GetDouble(this ProtoCodec.Msg m, string name, double def = 0)
        => m.TryGetValue(name, out var v) ? Convert.ToDouble(v) : def;

    public static long GetLong(this ProtoCodec.Msg m, string name, long def = 0)
        => m.TryGetValue(name, out var v) ? Convert.ToInt64(v) : def;

    public static string GetString(this ProtoCodec.Msg m, string name, string def = "")
        => m.TryGetValue(name, out var v) && v is string s ? s : def;

    public static bool? TryGetBool(this ProtoCodec.Msg m, string name)
        => m.TryGetValue(name, out var v) && v is bool b ? b : null;

    public static List<double> GetDoubles(this ProtoCodec.Msg m, string name)
    {
        var out_ = new List<double>();
        if (m.TryGetValue(name, out var v) && v is List<object?> list)
            foreach (var i in list)
                try { out_.Add(Convert.ToDouble(i)); } catch { }
        return out_;
    }
}


