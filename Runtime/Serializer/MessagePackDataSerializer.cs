// using MessagePack;
//
// public class MessagePackDataSerializer : IDataSerializer
// {
//     public string FileExtension => ".mpk";
//
//     private static readonly MessagePackSerializerOptions Options =
//         MessagePack.Resolvers.ContractlessStandardResolver.Options
//             .WithCompression(MessagePackCompression.Lz4BlockArray);
//
//     public byte[] Serialize<T>(T data)
//     {
//         return MessagePackSerializer.Serialize(data, Options);
//     }
//
//     public T Deserialize<T>(byte[] bytes)
//     {
//         if (bytes == null || bytes.Length == 0)
//             return default;
//
//         return MessagePackSerializer.Deserialize<T>(bytes, Options);
//     }
// }