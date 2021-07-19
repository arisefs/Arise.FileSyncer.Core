using System;
using System.Collections.Concurrent;

namespace Arise.FileSyncer.Core.Peer
{
    public class PeerDeviceKeys
    {
        // (Remote Device Id, Verification Key)
        private readonly ConcurrentDictionary<Guid, Guid> deviceKeys;

        public PeerDeviceKeys()
        {
            deviceKeys = new ConcurrentDictionary<Guid, Guid>(1, 0);
        }

        /// <summary>
        /// Gets the verification key for the specified device
        /// </summary>
        /// <param name="deviceId">Remote device ID</param>
        public bool TryGetVerificationKey(Guid deviceId, out Guid verificationKey)
        {
            return deviceKeys.TryGetValue(deviceId, out verificationKey);
        }

        /// <summary>
        /// Add a new or update a device verification key
        /// </summary>
        /// <param name="deviceId">Remote device ID</param>
        /// <param name="verificationKey">Verification key</param>
        public void Add(Guid deviceId, Guid verificationKey)
        {
            deviceKeys.AddOrUpdate(deviceId, verificationKey, (k, v) => verificationKey);
        }
    }
}