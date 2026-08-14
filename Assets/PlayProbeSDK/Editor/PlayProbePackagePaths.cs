// Copyright PlayProbe.io 2026. All rights reserved

using System;
using UnityEditor;

namespace PlayProbe.Editor
{
    /// <summary>
    /// Where the package lives on disk, worked out from where this script lives rather than written
    /// down.
    /// <para>
    /// The editor tools write prefabs into the package's own <c>Resources</c> folder and read the
    /// UI sprites out of its <c>Textures</c> folder. Both used to be hardcoded to
    /// <c>Assets/unity-sdk</c>, so renaming the package folder pointed the prefab builder at a path
    /// that no longer existed — it recreated it, leaving two Resources folders holding prefabs of
    /// the same name, which makes <c>Resources.Load</c> ambiguous. Deriving the root means a rename
    /// or a relocation just works.
    /// </para>
    /// </summary>
    internal static class PlayProbePackagePaths
    {
        // Only used if the anchor lookup somehow fails; it is also the shipped location.
        private const string FallbackRoot = "Assets/PlayProbeSDK";

        // This file's own path relative to the package root. Keep the two in step if it ever moves.
        private const string AnchorSuffix = "/Editor/PlayProbePackagePaths.cs";

        private static string _root;

        /// <summary>The package folder, e.g. <c>Assets/PlayProbeSDK</c>. No trailing slash.</summary>
        internal static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root))
                {
                    _root = ResolveRoot();
                }

                return _root;
            }
        }

        /// <summary>Where the generated UI prefabs are written, and loaded from at runtime.</summary>
        internal static string ResourcesFolder => $"{Root}/Resources";

        /// <summary>Where the shipped textures live.</summary>
        internal static string TexturesFolder => $"{Root}/Textures";

        /// <summary>Where the nine UI shape sprites live.</summary>
        internal static string UiSpritesFolder => $"{Root}/Textures/UI";

        internal static string PrefabPath(string prefabName) => $"{ResourcesFolder}/{prefabName}.prefab";

        private static string ResolveRoot()
        {
            foreach (string guid in AssetDatabase.FindAssets("PlayProbePackagePaths t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Match the full suffix, not just the filename: a copy of the SDK elsewhere in the
                // project would otherwise be able to win the search.
                if (!string.IsNullOrEmpty(path) && path.EndsWith(AnchorSuffix, StringComparison.Ordinal))
                {
                    return path.Substring(0, path.Length - AnchorSuffix.Length);
                }
            }

            return FallbackRoot;
        }
    }
}
