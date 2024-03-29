using System;
using System.IO;
using Arise.FileSyncer.Serializer;

namespace Arise.FileSyncer.Core.Messages
{
    internal sealed class FileDataMessage : FileMessageBase
    {
        public byte[] Chunk { get; set; } = Array.Empty<byte>();

        public override NetMessageType MessageType => NetMessageType.FileData;

        public override void Process(SyncerConnection con)
        {
            con.Owner.GetFileBuilder().MessageToQueue(this);
        }

        public override void Deserialize(Stream stream)
        {
            base.Deserialize(stream);

            Chunk = stream.ReadByteArray();
        }

        public override void Serialize(Stream stream)
        {
            base.Serialize(stream);

            stream.WriteAFS(Chunk);
        }
    }
}
