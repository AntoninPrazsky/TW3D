using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TW3D.Shared;

namespace TW3D.Editor
{
    /// <summary>A level file of the levels folder as the Open dialog lists it: what it says, or why it cannot be read.</summary>
    sealed class LevelEntry
    {
        public LevelEntry(string file, LevelData data, string error)
        {
            File = file;
            Name = Path.GetFileNameWithoutExtension(file);
            Error = error;
            if (data != null)
            {
                Order = data.Order;
                Mood = data.Mood;
                Blocks = data.Middle.Count;
            }
        }

        /// <summary>The full path of the file.</summary>
        public string File { get; }

        /// <summary>The name of the level: the file name without the extension.</summary>
        public string Name { get; }

        /// <summary>The place of the level in the level select; null when it has none.</summary>
        public int? Order { get; }

        /// <summary>The mood of the level (its lighting).</summary>
        public LevelMood Mood { get; }

        /// <summary>The number of blocks of the middle layer.</summary>
        public int Blocks { get; }

        /// <summary>Why the file cannot be read; null when it can.</summary>
        public string Error { get; }

        /// <summary>The file can be read as a level.</summary>
        public bool Readable => Error == null;
    }

    /// <summary>
    /// The folder the editor opens levels from and saves them into: the folder given by <c>--levels</c>; else
    /// content/Levels of the repository the program was built in (found upwards from the program, where TW3D.slnx is),
    /// so that the author edits the levels of the game itself; else TW3D/Levels in the user's application data (created
    /// when the first level is saved). It turns the level argument of <c>--editor</c> and the names typed in Save as into
    /// files, lists the levels for the Open dialog and knows which levels the scripts of tools/levelgen generate.
    /// </summary>
    sealed class LevelFolder
    {
        //The file that marks the root of the repository
        private const string RepositoryMarker = "TW3D.slnx";
        //Where the levels are in the repository; their previews are in a folder of it
        private static readonly string RepositoryLevels = Path.Combine("content", "Levels");
        private const string PreviewFolder = "levimgs";
        private const string PreviewExtension = ".png";
        //The scripts that generate levels, in the repository
        private const string GeneratorFolder = "tools/levelgen";
        private const string GeneratorExtension = ".js";

        /// <param name="path">The folder of the levels.</param>
        /// <param name="repository">The root of the repository; null when the program is not in one.</param>
        /// <param name="buildLevels">The levels of the build output, where a level saved into the repository is copied; null for none.</param>
        public LevelFolder(string path, string repository, string buildLevels)
        {
            FullPath = Normalize(path);
            Repository = repository != null ? Normalize(repository) : null;
            BuildLevels = buildLevels != null ? Normalize(buildLevels) : null;
            IsRepository = Repository != null && SamePath(FullPath, Path.Combine(Repository, RepositoryLevels));
        }

        /// <summary>The full path of the folder.</summary>
        public string FullPath { get; }

        /// <summary>The root of the repository the program is in; null when it is not in one.</summary>
        public string Repository { get; }

        /// <summary>The folder is content/Levels of the repository: the levels saved into it are the game's own.</summary>
        public bool IsRepository { get; }

        /// <summary>Content/Levels of the build output, which the game reads its levels from.</summary>
        public string BuildLevels { get; }

        /// <summary>The levels folder of the game: by <c>--levels</c>, the repository above the program or the application data.</summary>
        /// <param name="levels">The folder given by <c>--levels</c>; null when none was.</param>
        public static LevelFolder Find(string levels)
        {
            return Find(levels, AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }

        /// <summary>The levels folder: the one given, else content/Levels of the repository above the program, else TW3D/Levels in the application data.</summary>
        /// <param name="levels">The folder given by <c>--levels</c>; null when none was.</param>
        /// <param name="programFolder">The folder of the program: the search for the repository starts there, and the build output is there.</param>
        /// <param name="localData">The application data of the user.</param>
        public static LevelFolder Find(string levels, string programFolder, string localData)
        {
            string repository = FindRepository(programFolder);
            string buildLevels = Path.Combine(programFolder, "Content", "Levels");
            string path = levels ?? (repository != null ? Path.Combine(repository, RepositoryLevels) : Path.Combine(localData, "TW3D", "Levels"));
            return new LevelFolder(path, repository, buildLevels);
        }

        /// <summary>The root of the repository (the folder with TW3D.slnx) at a folder or above it; null when there is none.</summary>
        public static string FindRepository(string start)
        {
            for (DirectoryInfo folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
                if (System.IO.File.Exists(Path.Combine(folder.FullName, RepositoryMarker)))
                    return folder.FullName;
            return null;
        }

        /// <summary>The file of a level of the folder by its name.</summary>
        public string FileOf(string name)
        {
            return Path.Combine(FullPath, name + LevelFile.Extension);
        }

        /// <summary>
        /// The file a level argument means (of <c>--editor</c>, or typed in the Open dialog): an existing file as it is,
        /// otherwise a level of the folder by its name (with or without .json). The file may not exist.
        /// </summary>
        public string Resolve(string argument)
        {
            if (System.IO.File.Exists(argument))
                return Path.GetFullPath(argument);
            return FileOf(NormalName(argument));
        }

        /// <summary>A name typed for a level, trimmed and without a trailing .json.</summary>
        public static string NormalName(string name)
        {
            string normal = (name ?? "").Trim();
            if (normal.EndsWith(LevelFile.Extension, StringComparison.OrdinalIgnoreCase))
                normal = normal.Substring(0, normal.Length - LevelFile.Extension.Length).Trim();
            return normal;
        }

        /// <summary>Why a name typed for a level cannot be the name of its file; null when it can (as <see cref="NormalName"/> makes it).</summary>
        public static string CheckName(string name)
        {
            string normal = NormalName(name);
            if (normal.Length == 0)
                return "Type a name for the level.";
            if (normal == "." || normal == "..")
                return "A level cannot be called \"" + normal + "\".";
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in normal)
            {
                if (Array.IndexOf(invalid, c) < 0)
                    continue;
                return char.IsControl(c) ? "A level name cannot contain control characters."
                    : "A level name cannot contain \"" + c + "\".";
            }
            //Windows takes such a name, even with an extension, for a device: a level saved as nul.json would go nowhere
            //without an error and be marked saved
            int dot = normal.IndexOf('.');
            string stem = (dot >= 0 ? normal.Substring(0, dot) : normal).TrimEnd(' ');
            if (IsReservedName(stem))
                return "Windows reserves the name \"" + stem + "\".";
            return null;
        }

        /// <summary>A name Windows keeps for a device: CON, PRN, AUX, NUL, COM1 to COM9, LPT1 to LPT9, in any case.</summary>
        private static bool IsReservedName(string stem)
        {
            string upper = stem.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
                return true;
            return upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal))
                && upper[3] >= '1' && upper[3] <= '9';
        }

        /// <summary>The file is right in this folder (not in a folder of it).</summary>
        public bool Contains(string file)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(file));
            return folder != null && SamePath(folder, FullPath);
        }

        /// <summary>
        /// The levels (*.json) of the folder by name, with what they say; files that cannot be read are listed with the
        /// reason. A missing folder has none.
        /// </summary>
        /// <param name="error">Why the folder itself cannot be listed (a share gone, no right to list it); null when it can. The list is empty then.</param>
        public List<LevelEntry> List(out string error)
        {
            error = null;
            List<LevelEntry> entries = new List<LevelEntry>();
            if (!Directory.Exists(FullPath))
                return entries;
            IEnumerable<string> files;
            //The folder may vanish or become unreadable after the check (a network share, a removable drive)
            try
            {
                files = Directory.GetFiles(FullPath, "*" + LevelFile.Extension)
                    .Where(file => file.EndsWith(LevelFile.Extension, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return entries;
            }
            foreach (string file in files)
            {
                try
                {
                    entries.Add(new LevelEntry(file, LevelFile.Load(file), null));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException || e is JsonException)
                {
                    entries.Add(new LevelEntry(file, null, e.Message));
                }
            }
            return entries;
        }

        /// <summary>
        /// The preview image of a level (levimgs/name.png in the folder); null when it has none. The previews of 2016,
        /// built into .xnb files, are not images and are left out.
        /// </summary>
        public string Preview(string name)
        {
            string file = Path.Combine(FullPath, PreviewFolder, name + PreviewExtension);
            return System.IO.File.Exists(file) ? file : null;
        }

        /// <summary>
        /// The script of tools/levelgen that generates a level of the repository's content/Levels (relative to the
        /// repository); null when the level is not generated. Running the script again overwrites the file.
        /// </summary>
        public string GeneratorOf(string file)
        {
            if (Repository == null || file == null)
                return null;
            string folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (folder == null || !SamePath(folder, Path.Combine(Repository, RepositoryLevels)))
                return null;
            string script = GeneratorFolder + "/" + Path.GetFileNameWithoutExtension(file) + GeneratorExtension;
            return System.IO.File.Exists(Path.Combine(Repository, script)) ? script : null;
        }

        /// <summary>
        /// Copies a level saved into the repository's content/Levels to the levels of the build output, so that the level
        /// select and the game see it without a rebuild. Does nothing for files elsewhere. Throws what copying a file throws.
        /// </summary>
        /// <returns>The copy made; null when none was.</returns>
        public string CopyToBuild(string file)
        {
            if (!IsRepository || BuildLevels == null || !Contains(file))
                return null;
            string copy = Path.Combine(BuildLevels, Path.GetFileName(file));
            Directory.CreateDirectory(BuildLevels);
            System.IO.File.Copy(file, copy, true);
            return copy;
        }

        /// <summary>Two paths name the same file or folder (Windows does not tell the case apart).</summary>
        public static bool SamePath(string a, string b)
        {
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
