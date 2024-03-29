using System;
using System.Threading;
using System.Threading.Tasks;
using Arise.FileSyncer.Core.FileSync;
using Arise.FileSyncer.Core.Messages;
using Arise.FileSyncer.Core.Peer;

namespace Arise.FileSyncer.Core
{
    /// <summary>
    /// Core of the syncer. Stores connections, profiles and handles communication.
    /// </summary>
    public sealed class SyncerPeer : IDisposable
    {
        /// <summary>
        /// Called when received a pairing request from a remote device.
        /// Containes callback to accept/refuse the request.
        /// </summary>
        public event EventHandler<PairingRequestEventArgs>? PairingRequest;

        /// <summary>
        /// Called when a new pair has been successfully created.
        /// </summary>
        public event EventHandler<NewPairAddedEventArgs>? NewPairAdded;

        /// <summary>
        /// [Async] Called when the file builder completed a file.
        /// </summary>
        public event EventHandler<FileBuiltEventArgs>? FileBuilt;

        /// <summary>
        /// Allow sending and receiving pairing requests.
        /// </summary>
        public bool AllowPairing
        {
            get => Interlocked.Read(ref allowPairing) == 1;
            set => Interlocked.Exchange(ref allowPairing, Convert.ToInt64(value));
        }

        /// <summary>
        /// Manager of the peer's connections
        /// </summary>
        public ConnectionManager Connections { get; }
        /// <summary>
        /// Manager of paired devices keys
        /// </summary>
        public DeviceKeyManager DeviceKeys { get; }
        /// <summary>
        /// Manager of saved profiles
        /// </summary>
        public ProfileManager Profiles { get; }

        /// <summary>
        /// The peer settings class.
        /// </summary>
        public SyncerPeerSettings Settings { get; }

        private readonly Lazy<FileBuilder> fileBuilder;
        private long allowPairing = 0;

        /// <summary>
        /// Creates a new peer with the specified settings.
        /// </summary>
        public SyncerPeer(SyncerPeerSettings settings, DeviceKeyManager deviceKeyManager, ProfileManager profileManager)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AllowPairing = false;

            Connections = new ConnectionManager();
            DeviceKeys = deviceKeyManager ?? new DeviceKeyManager();
            Profiles = profileManager ?? new ProfileManager();

            fileBuilder = new Lazy<FileBuilder>(() => new FileBuilder(this));
        }

        /// <summary>
        /// Adds a new connection.
        /// </summary>
        public bool AddConnection(INetConnection connection)
        {
            return Connections.AddConnection(this, connection);
        }

        /// <summary>
        /// Returns if any of the underlying systems (including connections) currently executing important logic.
        /// </summary>
        public bool IsSyncing()
        {
            if (fileBuilder.IsValueCreated)
            {
                if (!fileBuilder.Value.IsBuildQueueEmpty()) return true;
            }

            foreach (var connection in Connections.GetConnections())
            {
                if (connection.IsSyncing()) return true;
            }

            return false;
        }

        /// <summary>
        /// Shares the specified profile with the given connection.
        /// </summary>
        /// <param name="connectionId">ID of the connection</param>
        /// <param name="profileId">ID of the profile to share</param>
        public bool ShareProfile(Guid connectionId, Guid profileId)
        {
            if (Profiles.GetProfile(profileId, out var profile))
            {
                return TrySend(connectionId, new ProfileShareMessage(profileId, profile));
            }

            return false;
        }

        /// <summary>
        /// Starts the sync process on a given profile with a given connection.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="profileId">ID of the profile to sync</param>
        /// <param name="isResponse">Is it a response message. Should always be false.</param>
        public bool SyncProfile(Guid connectionId, Guid profileId, bool isResponse = false)
        {
            if (!Profiles.GetProfile(profileId, out var profile))
            {
                return false;
            }

            SyncProfileState profileState;

            if (profile.AllowReceive)
            {
                var localState = SyncProfileState.Create(profileId, profile);

                if (localState != null)
                {
                    profileState = localState;
                }
                else
                {
                    Profiles.OnProfileError(profileId, profile, SyncProfileError.FailedToGetState);
                    return false;
                }
            }
            else
            {
                profileState = new SyncProfileState(profileId, profile.Key, profile.AllowDelete, null);
            }

            return TrySend(connectionId, new SyncProfileMessage(profileState, isResponse));
        }

        internal bool TrySend(Guid connectionId, NetMessage message)
        {
            if (Connections.TryGetConnection(connectionId, out ISyncerConnection? connection))
            {
                ((SyncerConnection)connection).Send(message);
                return true;
            }
            else
            {
                return false;
            }
        }

        internal FileBuilder GetFileBuilder()
        {
            return fileBuilder.Value;
        }

        internal void OnPairingRequest(PairingRequestEventArgs e)
        {
            PairingRequest?.Invoke(this, e);
        }

        internal void OnNewPairAdded(NewPairAddedEventArgs e)
        {
            NewPairAdded?.Invoke(this, e);
        }

        internal void OnFileBuilt(FileBuiltEventArgs e)
        {
            Task.Run(() => FileBuilt?.Invoke(this, e));
        }

        #region IDisposable Support
        private bool disposedValue;

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    if (fileBuilder.IsValueCreated)
                    {
                        fileBuilder.Value.Dispose();
                    }

                    Connections.Dispose();
                }

                // Free unmanaged resources (unmanaged objects) and override finalizer
                // Set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
