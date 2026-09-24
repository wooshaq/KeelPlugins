using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BetterSceneLoader
{
    public class SceneRoot
    {
        public string Label;
        public string Path;
        public bool IsDefault;
    }

    /// <summary>
    /// Builds the list of scene root folders: the game's own scene folder plus
    /// extra folders from the "Extra Scene Folders" setting.
    /// Format: entries separated by '|', each entry is "Path" or "Name=Path".
    /// </summary>
    public static class SceneRoots
    {
        private static readonly HashSet<string> warnedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static List<SceneRoot> Get(string defaultPath, string extraConfig)
        {
            var roots = new List<SceneRoot>
            {
                new SceneRoot { Label = System.IO.Path.GetFileName(defaultPath), Path = defaultPath, IsDefault = true }
            };

            if(string.IsNullOrEmpty(extraConfig))
                return roots;

            var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { roots[0].Label };

            // BepInEx unescapes config strings, so "J:\test" typed into the .cfg becomes "J:<TAB>est".
            // Windows paths never contain control characters, so turn them back into backslash sequences.
            extraConfig = RestoreBackslashEscapes(extraConfig);

            foreach(var rawEntry in extraConfig.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = rawEntry.Trim();
                if(entry.Length == 0)
                    continue;

                string label = null;
                var path = entry;

                // "Name=Path" - only treat '=' as a separator when it's before the drive/root part
                var eq = entry.IndexOf('=');
                if(eq > 0 && !entry.Substring(0, eq).Contains(":") && !entry.Substring(0, eq).Contains("\\") && !entry.Substring(0, eq).Contains("/"))
                {
                    label = entry.Substring(0, eq).Trim();
                    path = entry.Substring(eq + 1).Trim();
                }

                path = path.Trim().Trim('"').Trim();
                if(path.Length == 0)
                    continue;

                try
                {
                    path = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                }
                catch(Exception ex)
                {
                    WarnOnce(path, $"Invalid extra scene folder path \"{path}\": {ex.Message}");
                    continue;
                }

                if(!Directory.Exists(path))
                {
                    WarnOnce(path, $"Extra scene folder does not exist: \"{path}\"");
                    continue;
                }

                if(roots.Any(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if(string.IsNullOrEmpty(label))
                    label = MakeDefaultLabel(path);

                var uniqueLabel = label;
                var n = 2;
                while(!usedLabels.Add(uniqueLabel))
                    uniqueLabel = $"{label} ({n++})";

                roots.Add(new SceneRoot { Label = uniqueLabel, Path = path, IsDefault = false });
            }

            return roots;
        }

        /// <summary>
        /// "D:\Games\KKS2\UserData\Studio\scene" -> "KKS2", otherwise the last folder name.
        /// </summary>
        private static string MakeDefaultLabel(string path)
        {
            var parts = path.Split(new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            for(var i = parts.Length - 1; i > 0; i--)
            {
                if(string.Equals(parts[i], "UserData", StringComparison.OrdinalIgnoreCase))
                    return parts[i - 1];
            }

            return parts.Length > 0 ? parts[parts.Length - 1] : path;
        }

        private static string RestoreBackslashEscapes(string value)
        {
            var sb = new System.Text.StringBuilder(value.Length + 8);
            foreach(var c in value)
            {
                switch(c)
                {
                    case '\t': sb.Append("\\t"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\v': sb.Append("\\v"); break;
                    case '\0': sb.Append("\\0"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static void WarnOnce(string key, string message)
        {
            if(warnedPaths.Add(key))
                Log.Warning(message);
        }
    }
}
