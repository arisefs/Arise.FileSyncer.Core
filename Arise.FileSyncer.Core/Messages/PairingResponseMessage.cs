using System;
using System.IO;
using Arise.FileSyncer.Core.Components;
using Arise.FileSyncer.Serializer;

namespace Arise.FileSyncer.Core.Messages
{
    internal class PairingResponseMessage : NetMessage
    {
        public bool Accepted { get; set; }
        public Guid RawKey { get; set; }
        public DateTime RemoteGenTime { get; set; }

        public override NetMessageType MessageType => NetMessageType.PairingResponse;

        public PairingResponseMessage() { }

        public static PairingResponseMessage Accept(Guid rawKey, DateTime remoteGenTime)
        {
            return new PairingResponseMessage()
            {
                Accepted = true,
                RawKey = rawKey,
                RemoteGenTime = remoteGenTime,
            };
        }

        public static PairingResponseMessage Refuse()
        {
            return new PairingResponseMessage() { Accepted = false };
        }

        public override void Process(SyncerConnection con)
        {
            Lazy<PairingSupport> pairing = con.GetPairingSupport();

            if (Accepted &&
                pairing.IsValueCreated &&
                pairing.Value.Accept &&
                pairing.Value.GenTime < RemoteGenTime)
            {
                con.AddDeviceKey(con.GetRemoteDeviceId(), RawKey);
                pairing.Value.Accept = false;

                //Start verification
                VerificationDataMessage.Send(con);
            }
        }

        public override void Deserialize(Stream stream)
        {
            Accepted = stream.ReadBoolean();
            if (Accepted)
            {
                RawKey = stream.ReadGuid();
                RemoteGenTime = stream.ReadDateTime();
            }
        }

        public override void Serialize(Stream stream)
        {
            stream.WriteAFS(Accepted);
            if (Accepted)
            {
                stream.WriteAFS(RawKey);
                stream.WriteAFS(RemoteGenTime);
            }
        }
    }
}
