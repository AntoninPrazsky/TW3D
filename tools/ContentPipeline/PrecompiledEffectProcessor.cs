using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

namespace TW3D.ContentPipeline
{
    /// <summary>
    /// The effect processor of MonoGame, which also keeps the compiled code of each effect for DesktopGL in the
    /// repository, for the builds that cannot compile it. The effect compiler of MonoGame runs only on Windows
    /// (it needs the shader compiler of Direct3D; elsewhere it runs it through Wine), so the game could not be built
    /// on Linux, a Raspberry Pi included. The code for DesktopGL (OpenGL) is the same on every system, so
    /// the build on Windows writes it next to the effect: <c>Effects/DesktopGL/Scene.mgfx</c> for
    /// <c>Effects/Scene.fx</c>, with <c>Scene.txt</c>, which says which source it was compiled from (a SHA-256 of the
    /// effect with LF line endings) and by which version of MonoGame. A build elsewhere takes that code when it
    /// is of the same source and version, and fails with an explanation when it is not.
    /// </summary>
    /// <remarks>
    /// The build on Windows rewrites the files only when the source or the version changes, not when its compiler
    /// makes other bytes of the same source (another version of the Direct3D compiler), so that CI, which checks
    /// that the committed files are up to date, does not fail over a code that is just as good. The effects of the
    /// other platforms (Android) are compiled as before and kept nowhere.
    /// </remarks>
    [ContentProcessor(DisplayName = "Effect - TW3D (kept compiled for DesktopGL)")]
    public class PrecompiledEffectProcessor : EffectProcessor
    {
        public override CompiledEffectContent Process(EffectContent input, ContentProcessorContext context)
        {
            string source = input.Identity?.SourceFilename;
            if (context.TargetPlatform != TargetPlatform.DesktopGL || source == null)
                return base.Process(input, context);

            string folder = Path.Combine(Path.GetDirectoryName(source), "DesktopGL");
            string name = Path.GetFileNameWithoutExtension(source);
            string codeFile = Path.Combine(folder, name + ".mgfx");
            string stampFile = Path.Combine(folder, name + ".txt");
            string stamp = Stamp(source);
            bool kept = File.Exists(codeFile) && File.Exists(stampFile) && Normalized(File.ReadAllText(stampFile)) == stamp;

            if (!OperatingSystem.IsWindows())
            {
                if (kept)
                {
                    //The effect is built again when the kept code changes
                    context.AddDependency(codeFile);
                    context.AddDependency(stampFile);
                    context.Logger.Log($"{Path.GetFileName(source)}: the code compiled on Windows ({codeFile})");
                    return new CompiledEffectContent(File.ReadAllBytes(codeFile));
                }
                try
                {
                    //Only with Wine
                    return Keep(base.Process(input, context), codeFile, stampFile, stamp, kept);
                }
                catch (Exception e)
                {
                    throw new InvalidContentException(
                        $"{Path.GetFileName(source)} has no code compiled for DesktopGL of this source and this version of MonoGame " +
                        $"in {folder}, and shaders can be compiled only on Windows (elsewhere through Wine). Build the game once on " +
                        "Windows and commit the files it writes there. (" + e.Message + ")", input.Identity, e);
                }
            }

            return Keep(base.Process(input, context), codeFile, stampFile, stamp, kept);
        }

        /// <summary>Writes the compiled code and its stamp, unless the files are of this source and version already.</summary>
        private static CompiledEffectContent Keep(CompiledEffectContent compiled, string codeFile, string stampFile, string stamp, bool kept)
        {
            if (!kept)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(codeFile));
                File.WriteAllBytes(codeFile, compiled.GetEffectCode());
                File.WriteAllText(stampFile, stamp);
            }
            return compiled;
        }

        /// <summary>
        /// What the kept code was compiled from: the source (a SHA-256 of the effect, its line endings LF, as git
        /// checks it out on Windows with CRLF and elsewhere with LF) and the version of MonoGame (a new version may
        /// read only the code of its own compiler).
        /// </summary>
        private static string Stamp(string source)
        {
            string text = Normalized(File.ReadAllText(source));
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            Version version = typeof(EffectProcessor).Assembly.GetName().Version;
            return "The code compiled for DesktopGL in " + Path.GetFileNameWithoutExtension(source) + ".mgfx, by the build on " +
                "Windows, for the builds that cannot compile it (tools/ContentPipeline/PrecompiledEffectProcessor.cs).\n" +
                "Source: " + Path.GetFileName(source) + ", SHA-256 with LF line endings " + hash + "\n" +
                "MonoGame: " + version + "\n";
        }

        private static string Normalized(string text) => text.Replace("\r\n", "\n");
    }
}
