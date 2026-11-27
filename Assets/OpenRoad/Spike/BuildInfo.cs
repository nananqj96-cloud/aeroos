using UnityEngine;

namespace OpenRoad.Spike
{
    public static class BuildInfo
    {
        public static string Version => string.IsNullOrWhiteSpace(Application.version) ? "0.1.0-dev" : Application.version;

        /// <summary>
        /// GameCI writes the Actions run number into the semver build-metadata suffix, for example
        /// 0.1.0+42. Local/editor builds fall back to a readable label.
        /// </summary>
        public static string BuildNumber
        {
            get
            {
                int plus = Version.LastIndexOf('+');
                return plus >= 0 && plus + 1 < Version.Length ? Version.Substring(plus + 1) : "local";
            }
        }

        public static string ShortBuildId
        {
            get
            {
                string guid = Application.buildGUID;
                return string.IsNullOrEmpty(guid) ? "editor" : guid.Substring(0, Mathf.Min(8, guid.Length));
            }
        }
    }
}
