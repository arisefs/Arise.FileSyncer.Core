using System;

namespace Arise.FileSyncer.Core
{
    public class ConnectionEventArgs : EventArgs
    {
        public Guid Id { get; }

        public ConnectionEventArgs(Guid id)
        {
            Id = id;
        }
    }

    public class ConnectionVerifiedEventArgs : ConnectionEventArgs
    {
        public string Name { get; }

        public ConnectionVerifiedEventArgs(Guid id, string name) : base(id)
        {
            Name = name;
        }
    }

    public class ProfileEventArgs : EventArgs
    {
        public Guid Id { get; }
        public SyncProfile Profile { get; }

        public ProfileEventArgs(Guid id, SyncProfile profile)
        {
            Id = id;
            Profile = profile;
        }
    }

    public class ProfileErrorEventArgs : ProfileEventArgs
    {
        public SyncProfileError Error { get; }

        public ProfileErrorEventArgs(Guid id, SyncProfile profile, SyncProfileError error) : base(id, profile)
        {
            Error = error;
        }
    }

    public class ProfileReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Remote Device ID
        /// </summary>
        public Guid RemoteId { get; }

        /// <summary>
        /// Profile Share Data
        /// </summary>
        public SyncProfileShare ProfileShare { get; }

        internal ProfileReceivedEventArgs(Guid remoteId, SyncProfileShare profileShare)
        {
            RemoteId = remoteId;
            ProfileShare = profileShare;
        }
    }

    public class PairingRequestEventArgs : EventArgs
    {
        public string DisplayName { get; }
        public Guid RemoteDeviceId { get; }
        public Action<bool> ResultCallback { get; }

        public PairingRequestEventArgs(string displayName, Guid remoteDeviceId, Action<bool> resultCallback)
        {
            DisplayName = displayName;
            RemoteDeviceId = remoteDeviceId;
            ResultCallback = resultCallback;
        }
    }

    public class NewPairAddedEventArgs : EventArgs
    {
        public Guid RemoteDeviceId { get; }

        public NewPairAddedEventArgs(Guid remoteDeviceId)
        {
            RemoteDeviceId = remoteDeviceId;
        }
    }

    public class FileBuiltEventArgs : EventArgs
    {
        public Guid ProfileId { get; }
        public string RootPath { get; }
        public string RelativePath { get; }

        public FileBuiltEventArgs(Guid profileId, string rootPath, string relativePath)
        {
            ProfileId = profileId;
            RootPath = rootPath;
            RelativePath = relativePath;
        }
    }
}
