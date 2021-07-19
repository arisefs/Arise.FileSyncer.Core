using System;
using System.Threading;
using System.Threading.Tasks;
using Arise.FileSyncer.Core.FileSync;
using Arise.FileSyncer.Core.Messages;
using Arise.FileSyncer.Core.Peer;
using Arise.FileSyncer.Core.Plugins;

namespace Arise.FileSyncer.Core
{
    /// <summary>
    /// Core of the syncer. Stores connections, profiles and handles communication.
    /// </summary>
    public class SyncerPeer : IDisposable
    {
#pragma warning disable CA2211 // Non-constant fields should not be visible
        // TODO: Move it to a more suitable location
        /// <summary>
        /// Is the local device supports file timestamp get and set.
        /// </summary>
        public static bool SupportTimestamp = true;
#pragma warning restore CA2211 // Non-constant fields should not be visible



        /// <summary>
        /// [Async] Called when a profile got changed or updated.
        /// </summary>
        public event EventHandler<ProfileEventArgs> ProfileChanged;

        /// <summary>
        /// Called when a new profile got added.
        /// </summary>
        public event EventHandler<ProfileEventArgs> ProfileAdded;

        /// <summary>
        /// Called when a profile got removed.
        /// </summary>
        public event EventHandler<ProfileEventArgs> ProfileRemoved;

        /// <summary>
        /// Called when a profile encountered an error.
        /// </summary>
        public event EventHandler<ProfileErrorEventArgs> ProfileError;

        /// <summary>
        /// Called when a new profile got received from a remote device.
        /// </summary>
        public event EventHandler<ProfileReceivedEventArgs> ProfileReceived;

        /// <summary>
        /// Called when received a pairing request from a remote device.
        /// Containes callback to accept/refuse the request.
        /// </summary>
        public event EventHandler<PairingRequestEventArgs> PairingRequest;

        /// <summary>
        /// Called when a new pair has been successfully created.
        /// </summary>
        public event EventHandler<NewPairAddedEventArgs> NewPairAdded;

        /// <summary>
        /// [Async] Called when the file builder completed a file.
        /// </summary>
        public event EventHandler<FileBuiltEventArgs> FileBuilt;

        /// <summary>
        /// Allow sending and receiving pairing requests.
        /// </summary>
        public bool AllowPairing
        {
            get => Interlocked.Read(ref _allowPairing) == 1;
            set => Interlocked.Exchange(ref _allowPairing, Convert.ToInt64(value));
        }

        /// <summary>
        /// Manager of the peer's connections
        /// </summary>
        public PeerConnections Connections { get; }

        /// <summary>
        /// Manager of paired devices keys
        /// </summary>
        public PeerDeviceKeys DeviceKeys { get; }

        /// <summary>
        /// The peer settings class.
        /// </summary>
        public SyncerPeerSettings Settings { get; }

        /// <summary>
        /// The plugin manager class.
        /// </summary>
        public PluginManager Plugins { get; }

        private readonly Lazy<FileBuilder> fileBuilder;

        private long _allowPairing = 0;

        /// <summary>
        /// Creates a new peer with the specified settings.
        /// </summary>
        public SyncerPeer(SyncerPeerSettings settings)
        {
            Settings = settings;
            AllowPairing = false;

            Connections = new PeerConnections(this);
            DeviceKeys = new PeerDeviceKeys();
            Plugins = new PluginManager();

            fileBuilder = new Lazy<FileBuilder>(() => new FileBuilder(this));
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
            if (Settings.Profiles.TryGetValue(profileId, out var profile))
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
            if (!Settings.Profiles.TryGetValue(profileId, out var profile))
            {
                return false;
            }

            SyncProfileState profileState;

            if (profile.AllowReceive)
            {
                profileState = SyncProfileState.Create(profileId, profile);

                if (profileState == null)
                {
                    OnProfileError(new ProfileErrorEventArgs(profileId, profile, SyncProfileError.FailedToGetState));
                    return false;
                }
            }
            else
            {
                profileState = new SyncProfileState(profileId, profile.Key, profile.AllowDelete, null);
            }

            return TrySend(connectionId, new SyncProfileMessage(profileState, isResponse));
        }

        /// <summary>
        /// Adds a new profile to the peer settings.
        /// </summary>
        /// <param name="profileId">ID of the profile to add</param>
        /// <param name="newProfile">The new profile</param>
        public bool AddProfile(Guid profileId, SyncProfile newProfile)
        {
            if (Settings.Profiles.TryAdd(profileId, newProfile))
            {
                Log.Info($"Profile added: {newProfile.Name}");
                OnProfileAdded(new ProfileEventArgs(profileId, newProfile));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a profile form the peer settings.
        /// </summary>
        /// <param name="profileId">ID of the profile to remove</param>
        public bool RemoveProfile(Guid profileId)
        {
            if (Settings.Profiles.TryRemove(profileId, out var profile))
            {
                Log.Info($"Profile removed: {profile.Name}");
                OnProfileRemoved(new ProfileEventArgs(profileId, profile));
                return true;
            }

            Log.Warning($"Profile remove failed! ID: {profileId}");
            return false;
        }

        /// <summary>
        /// Updates a selected profile
        /// </summary>
        /// <param name="profileId">ID of the profile to update</param>
        /// <param name="newProfile">Updated profile</param>
        /// <returns></returns>
        public bool UpdateProfile(Guid profileId, SyncProfile newProfile)
        {
            if (Settings.Profiles.TryGetValue(profileId, out var profile))
            {
                if (Settings.Profiles.TryUpdate(profileId, newProfile, profile))
                {
                    Log.Info($"Profile updated: {profileId} - {profile.Name}");
                    OnProfileChanged(new ProfileEventArgs(profileId, newProfile));
                    return true;
                }
            }

            Log.Warning($"Profile update failed! ID: {profileId}");
            return false;
        }

        internal bool TrySend(Guid connectionId, NetMessage message)
        {
            bool found = Connections.TryGetConnection(connectionId, out ISyncerConnection connection);
            if (found) (connection as SyncerConnection).Send(message);
            return found;
        }

        internal FileBuilder GetFileBuilder()
        {
            return fileBuilder.Value;
        }



        internal virtual void OnProfileChanged(ProfileEventArgs e)
        {
            Task.Run(() => ProfileChanged?.Invoke(this, e));
        }

        internal virtual void OnProfileAdded(ProfileEventArgs e)
        {
            ProfileAdded?.Invoke(this, e);
        }

        internal virtual void OnProfileRemoved(ProfileEventArgs e)
        {
            ProfileRemoved?.Invoke(this, e);
        }

        internal virtual void OnProfileError(ProfileErrorEventArgs e)
        {
            ProfileError?.Invoke(this, e);
        }

        internal virtual void OnProfileReceived(ProfileReceivedEventArgs e)
        {
            ProfileReceived?.Invoke(this, e);
        }

        internal virtual void OnPairingRequest(PairingRequestEventArgs e)
        {
            PairingRequest?.Invoke(this, e);
        }

        internal virtual void OnNewPairAdded(NewPairAddedEventArgs e)
        {
            NewPairAdded?.Invoke(this, e);
        }

        internal virtual void OnFileBuilt(FileBuiltEventArgs e)
        {
            Task.Run(() => FileBuilt?.Invoke(this, e));
        }


        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
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
