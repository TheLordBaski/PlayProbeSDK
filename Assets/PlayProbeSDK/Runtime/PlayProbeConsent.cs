// Copyright PlayProbe.io 2026. All rights reserved

using System;
using UnityEngine;

namespace PlayProbe
{
    /// <summary>
    /// Whether the player has agreed to PlayProbe collecting data from this device.
    /// </summary>
    public enum PlayProbeConsentStatus
    {
        /// <summary>Nobody has answered yet. With <c>requireConsent</c> on, nothing is collected.</summary>
        Unknown = 0,

        /// <summary>The player agreed. Collection proceeds.</summary>
        Granted = 1,

        /// <summary>The player refused, or withdrew a previous agreement.</summary>
        Denied = 2,
    }

    /// <summary>
    /// Stores the player's consent decision and remembers it between runs.
    ///
    /// PlayProbe never shows a consent prompt itself: inside your game YOU are the data controller
    /// and we are your processor, so the wording, timing and legal basis of the prompt are yours to
    /// decide. Show your own UI, then tell the SDK what the player chose:
    ///
    /// <code>
    /// PlayProbeManager.Instance.SetConsent(true);   // player agreed
    /// PlayProbeManager.Instance.SetConsent(false);  // player refused, or withdrew later
    /// </code>
    ///
    /// The decision is persisted in <see cref="PlayerPrefs"/> so you only have to ask once.
    /// </summary>
    public class PlayProbeConsent
    {
        internal const string PlayerPrefsKey = "playprobe_consent";

        private PlayProbeConsentStatus _status;

        internal PlayProbeConsent()
        {
            _status = Load();
        }

        /// <summary>Raised whenever the stored decision changes.</summary>
        public event Action<PlayProbeConsentStatus> Changed;

        /// <summary>The player's current decision, restored from a previous run when present.</summary>
        public PlayProbeConsentStatus Status
        {
            get => _status;
            private set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                Persist(value);

                try
                {
                    Changed?.Invoke(value);
                }
                catch (Exception exception)
                {
                    // A throwing subscriber must not take the SDK (or the game) down with it.
                    Debug.LogWarning($"[PlayProbe] Consent change handler threw: {exception.Message}");
                }
            }
        }

        /// <summary>True once the player has actually answered, either way.</summary>
        public bool HasAnswered => _status != PlayProbeConsentStatus.Unknown;

        /// <summary>Records the player's decision. Pass false to withdraw a previous agreement.</summary>
        public void Set(bool granted)
        {
            Status = granted ? PlayProbeConsentStatus.Granted : PlayProbeConsentStatus.Denied;
        }

        /// <summary>
        /// Forgets the stored decision so the player is asked again next time. Useful for a
        /// "reset privacy choices" button in your options menu.
        /// </summary>
        public void Clear()
        {
            bool changed = _status != PlayProbeConsentStatus.Unknown;

            // Assign the field directly: going through the Status setter would call Persist and write
            // the key straight back after we deleted it.
            _status = PlayProbeConsentStatus.Unknown;

            try
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Could not clear stored consent: {exception.Message}");
            }

            if (!changed)
            {
                return;
            }

            try
            {
                Changed?.Invoke(PlayProbeConsentStatus.Unknown);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Consent change handler threw: {exception.Message}");
            }
        }

        private static PlayProbeConsentStatus Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(PlayerPrefsKey))
                {
                    return PlayProbeConsentStatus.Unknown;
                }

                int stored = PlayerPrefs.GetInt(PlayerPrefsKey, (int)PlayProbeConsentStatus.Unknown);
                return Enum.IsDefined(typeof(PlayProbeConsentStatus), stored)
                    ? (PlayProbeConsentStatus)stored
                    : PlayProbeConsentStatus.Unknown;
            }
            catch (Exception exception)
            {
                // Never let a storage failure be read as "consent granted".
                Debug.LogWarning($"[PlayProbe] Could not read stored consent: {exception.Message}");
                return PlayProbeConsentStatus.Unknown;
            }
        }

        private static void Persist(PlayProbeConsentStatus status)
        {
            try
            {
                PlayerPrefs.SetInt(PlayerPrefsKey, (int)status);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayProbe] Could not persist consent: {exception.Message}");
            }
        }
    }
}
