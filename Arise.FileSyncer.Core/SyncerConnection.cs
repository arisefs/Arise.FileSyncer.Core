using System;
using Arise.FileSyncer.Core.Components;
using Arise.FileSyncer.Core.FileSync;
using Arise.FileSyncer.Core.Messages;

namespace Arise.FileSyncer.Core
{
    public interface ISyncerConnection
    {
        /// <summary>
        /// Is the connection verified and allows syncronization.
        /// </summary>
        bool Verified { get; }

        /// <summary>
        /// Does the remote device supports file timestamp changes.
        /// </summary>
        bool SupportTimestamp { get; }

        /// <summary>
        /// Name of the remote device.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Current progress of the syncronization process.
        /// </summary>
        ProgressCounter Progress { get; }

        /// <summary>
        /// Returns true if the connection is currently syncing.
        /// </summary>
        bool IsSyncing();
    }

    internal sealed class SyncerConnection : ISyncerConnection, IDisposable
    {
        public SyncerPeer Owner { get; }
        public ProgressCounter Progress { get; }

        public bool Verified { get => _verified; set => _verified = value; }
        private volatile bool _verified = false;

        public bool Pairing { get => _pairing; set => _pairing = value; }
        private volatile bool _pairing = false;

        public bool SupportTimestamp { get => _supportTimestamp; set => _supportTimestamp = value; }
        private volatile bool _supportTimestamp = false;

        public string DisplayName { get => _displayName; set => _displayName = value; }
        private volatile string _displayName = "Unknown";

        private readonly Lazy<FileSender> fileSender;
        private readonly NetMessageHandler messageHandler;
        private readonly ProgressChecker progressChecker;
        private readonly ConnectionChecker connectionChecker;

        public SyncerConnection(SyncerPeer owner, INetConnection connection)
        {
            Owner = owner;
            Progress = new ProgressCounter();

            fileSender = new Lazy<FileSender>(() => new FileSender(this));
            messageHandler = new NetMessageHandler(connection, MessageReceived, Disconnect);
            progressChecker = new ProgressChecker(Progress, ProgressTimeout, Owner.Settings.ProgressTimeout);
            connectionChecker = new ConnectionChecker(messageHandler.Send, Owner.Settings.PingInterval);
        }

        public void Start()
        {
            VerificationDataMessage.Send(this);

            if (!disposedValue)
            {
                messageHandler.Start();
            }
        }

        public Guid GetRemoteDeviceId()
        {
            return messageHandler.Connection.Id;
        }

        public void Disconnect()
        {
            Owner.Connections.RemoveConnection(GetRemoteDeviceId());
        }

        public bool IsSyncing()
        {
            if (Progress.Indeterminate) return true;
            if (Progress.Current != Progress.Maximum) return true;
            if (fileSender.IsValueCreated && !fileSender.Value.IsSendQueueEmpty()) return true;

            return false;
        }

        public void Pair()
        {
            if (!Verified)
            {
                Log.Verbose("Sending pairing request");
                Pairing = true;
                Send(new PairingRequestMessage(Owner.Settings.DisplayName));
            }
        }

        public void Send(NetMessage message)
        {
            messageHandler.Send(message);
        }

        public void SendAndDisconnect(NetMessage message)
        {
            messageHandler.SendAndDisconnect(message);
        }

        internal void OnChunkRequest()
        {
            if (fileSender.IsValueCreated) fileSender.Value.ChunkRequest();
            else Log.Warning($"{DisplayName}: FileChunkRequest before fileSender Init");
        }

        internal void OnPairingRequestCallback(bool accepted)
        {
            if (accepted)
            {
                Guid rawKey = Guid.NewGuid();
                AddDeviceKey(GetRemoteDeviceId(), rawKey);
                Send(PairingResponseMessage.Accept(rawKey));
                VerificationDataMessage.Send(this);
            }
            else
            {
                Send(PairingResponseMessage.Refuse());
            }
        }

        internal void AddDeviceKey(Guid deviceId, Guid verificationKey)
        {
            Owner.DeviceKeys.Add(deviceId, verificationKey);
            Owner.OnNewPairAdded(new NewPairAddedEventArgs(deviceId));
        }

        internal void StartProfileSync(SyncProfileState remoteProfile)
        {
            SyncProfile localProfile;

            if (Owner.Profiles.GetProfile(remoteProfile.Id, out var profile))
            {
                localProfile = profile;
            }
            else
            {
                Log.Warning("Tried to sync non-existing profile: " + remoteProfile.Id);
                return;
            }

            if (!localProfile.AllowSend || localProfile.Key != remoteProfile.Key)
            {
                Log.Warning("Tried to sync invalid profile: " + remoteProfile.Id);
                return;
            }

            var localState = localProfile.GenerateState();
            if (localState == null)
            {
                Log.Warning($"Failed to get profile state: PID:{remoteProfile.Id}");
                return;
            }

            var remoteState = remoteProfile.State;
            if (remoteState == null)
            {
                Log.Warning($"Remote state is null: PID:{remoteProfile.Id}");
                return;
            }

            Log.Info("Processing Sync Profile: " + remoteProfile.Id);

            var delta = new DirectoryTreeDifference(localState, remoteState, SupportTimestamp);

            if (remoteProfile.AllowDelete)
            {
                // Delete
                Send(new DeleteFilesMessage(remoteProfile.Id, localProfile, delta.LocalMissingFiles));
                Send(new DeleteDirectoriesMessage(remoteProfile.Id, localProfile, delta.LocalMissingDirectories));
            }

            // Send
            Send(new CreateDirectoriesMessage(remoteProfile.Id, localProfile, delta.RemoteMissingDirectories));
            fileSender.Value.AddFiles(FileSendInfo.Create(remoteProfile.Id, localProfile, delta.RemoteMissingFiles));

            // Update last sync date
            localProfile.UpdateLastSyncDate(Owner.Profiles, remoteProfile.Id);
        }

        private void ProgressTimeout()
        {
            Log.Info($"{DisplayName}: Sync progress timeout. Disconnecting...");
            Disconnect();
        }

        private void MessageReceived(NetMessage message)
        {
            if (CheckMessage(message)) message.Process(this);
            else Log.Warning($"{DisplayName}: Message rejected!");
        }

        private bool CheckMessage(NetMessage message)
        {
            return message != null
                && (Verified
                    || message.MessageType == NetMessageType.VerificationData
                    || message.MessageType == NetMessageType.VerificationResponse
                    || message.MessageType == NetMessageType.IsAlive
                    || message.MessageType == NetMessageType.PairingRequest
                    || message.MessageType == NetMessageType.PairingResponse
                );
        }

        #region IDisposable Support
        private bool disposedValue = false;

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    messageHandler.Dispose();
                    progressChecker.Dispose();
                    connectionChecker.Dispose();

                    if (fileSender.IsValueCreated)
                    {
                        fileSender.Value.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
