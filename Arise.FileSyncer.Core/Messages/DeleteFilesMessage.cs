using System;
using System.Collections.Generic;
using System.IO;
using Arise.FileSyncer.Core.FileSync;
using Arise.FileSyncer.Core.Helpers;
using Arise.FileSyncer.Serializer;

namespace Arise.FileSyncer.Core.Messages
{
    internal sealed class DeleteFilesMessage : NetMessage
    {
        public Guid ProfileId { get; set; }
        public Guid Key { get; set; }
        public IList<string> Files { get; set; }

        public override NetMessageType MessageType => NetMessageType.DeleteFiles;

        public DeleteFilesMessage() { Files = Array.Empty<string>(); }

        public DeleteFilesMessage(Guid profileId, SyncProfile syncProfile, IList<string> files)
        {
            ProfileId = profileId;
            Key = syncProfile.Key;
            Files = files;
        }

        public override void Process(SyncerConnection con)
        {
            if (con.Owner.Profiles.GetProfile(ProfileId, out var profile))
            {
                if (profile.AllowDelete && profile.Key == Key)
                {
                    for (int i = 0; i < Files.Count; i++)
                    {
                        Utility.FileDelete(profile.RootDirectory, PathHelper.GetCorrect(Files[i], false));
                    }

                    if (Files.Count > 0)
                    {
                        profile.UpdateLastSyncDate(con.Owner.Profiles, ProfileId);
                    }
                }
            }
        }

        public override void Deserialize(Stream stream)
        {
            ProfileId = stream.ReadGuid();
            Key = stream.ReadGuid();
            Files = stream.ReadStringArray();
        }

        public override void Serialize(Stream stream)
        {
            stream.WriteAFS(ProfileId);
            stream.WriteAFS(Key);
            stream.WriteAFS(Files);
        }
    }
}
