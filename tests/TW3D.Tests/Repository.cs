using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TW3D.Shared;

namespace TW3D.Tests
{
    /// <summary>The files of the repository the tests read: the content of the game.</summary>
    static class Repository
    {
        /// <summary>The root of the repository (where TW3D.slnx is), found upwards from the tests.</summary>
        public static readonly string Root = FindRoot();

        public static readonly string Content = Path.Combine(Root, "content");

        public static readonly string Levels = Path.Combine(Content, "Levels");

        public static Catalog LoadCatalog() => Catalog.Load(Path.Combine(Content, Catalog.FileName));

        public static CollisionShapes LoadCollisionShapes()
        {
            using (Stream stream = File.OpenRead(Path.Combine(Content, CollisionShapes.FileName)))
                return CollisionShapes.Load(stream);
        }

        /// <summary>The files content/Content.mgcb builds, relative to content/ with forward slashes.</summary>
        public static HashSet<string> BuiltContent()
        {
            return File.ReadAllLines(Path.Combine(Content, "Content.mgcb"))
                .Where(line => line.StartsWith("/build:", StringComparison.Ordinal))
                .Select(line => line.Substring("/build:".Length).Trim())
                .ToHashSet();
        }

        /// <summary>The names of the shipped levels, in the order of the level select.</summary>
        public static List<string> LevelNames() => LevelFile.FindLevels(Levels);

        public static string LevelPath(string name) => Path.Combine(Levels, name + LevelFile.Extension);

        private static string FindRoot()
        {
            for (DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "TW3D.slnx")))
                    return directory.FullName;
            throw new DirectoryNotFoundException("The repository (TW3D.slnx) was not found above " + AppContext.BaseDirectory);
        }
    }
}
