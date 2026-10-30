using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Clipper2Lib;
using TW3D.Shared;

namespace TW3D.CollisionGen
{
    /// <summary>
    /// Generates the collision shapes of the models (content/collision.json), see README.md.
    /// Usage: CollisionGen [content folder] [--svg folder]
    /// </summary>
    static class Program
    {
        //Models of the car: its body and the holder of the wheels collide with the car's own shapes (the whole model),
        //their width and the width of the wheels decide which parts of the track they touch
        private static readonly string[] CarBodies = { "car", "car2" };
        private const string Holder = "holder";
        private const string Wheel = "wheel";

        //A body differing from the wheels in less than this area has the same shape (square model units)
        private const double SameShapeArea = 0.001;

        static int Main(string[] args)
        {
            try
            {
                string content = null, svg = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--svg" && i + 1 < args.Length)
                        svg = args[++i];
                    else
                        content = args[i];
                }
                content = Path.GetFullPath(content ?? FindContent());
                Generate(content, svg);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("CollisionGen : error CG0: " + e.Message);
                return 1;
            }
        }

        private static void Generate(string content, string svg)
        {
            Catalog catalog = Catalog.Load(Path.Combine(content, Catalog.FileName));
            ObjMesh Load(string model) => ObjMesh.Load(Path.Combine(content, model + ".obj"));

            //The car across the physics plane: the body (with the holders) in the middle, the wheels at both sides
            //(the car and the track are drawn at the same scale, both centred on the plane z = 0)
            double body = Math.Max(Load(CarBodies[0]).MaxAbsZ, Load(Holder).MaxAbsZ);
            ObjMesh wheel = Load(Wheel);
            double wheelsInner = Math.Min(Math.Abs(wheel.MinZ), Math.Abs(wheel.MaxZ));
            double wheelsOuter = wheel.MaxAbsZ;
            Console.WriteLine(FormattableString.Invariant(
                $"Car body |z| <= {body:0.###}, wheels {wheelsInner:0.###} <= |z| <= {wheelsOuter:0.###}"));

            CollisionShapes shapes = new CollisionShapes();
            Dictionary<string, PathsD> outlines = new Dictionary<string, PathsD>();
            int polygons = 0;

            List<float[]> Shape(string model, PathsD region, string part)
            {
                List<float[]> result = Decomposition.ConvexPolygons(region, out double lost);
                if (lost > 0.001)
                    Warn(content, model, FormattableString.Invariant($"{lost:P1} of the {part} shape is lost in the convex polygons"));
                if (result.Count == 0)
                    Warn(content, model, "the " + part + " shape is empty");
                polygons += result.Count;
                return result;
            }

            PathsD Band(string model, ObjMesh mesh, double inner, double outer)
            {
                PathsD region = Section.Band(mesh, inner, outer, out int openChains);
                if (openChains > 0)
                    Warn(content, model, openChains + " open cuts through the model (holes in the mesh)");
                return region;
            }

            //Blocks: the body of the car touches what is in its band, the wheels what is in theirs;
            //for most blocks (straight along z) the two are the same
            foreach (BlockType block in catalog.Blocks.Where(block => block.Collision))
            {
                ObjMesh mesh = Load(block.ModelAsset);
                PathsD bodyRegion = Band(block.ModelAsset, mesh, 0, body);
                PathsD wheelsRegion = Band(block.ModelAsset, mesh, wheelsInner, wheelsOuter);
                double difference = Math.Abs(Clipper.Area(Clipper.Xor(bodyRegion, wheelsRegion, FillRule.NonZero, 6)));
                if (difference < SameShapeArea)
                {
                    shapes.Models[block.ModelAsset] = new CollisionShape { Shape = Shape(block.ModelAsset, bodyRegion, "") };
                    outlines[block.ModelAsset] = bodyRegion;
                }
                else
                {
                    shapes.Models[block.ModelAsset] = new CollisionShape
                    {
                        Body = Shape(block.ModelAsset, bodyRegion, "body"),
                        Wheels = Shape(block.ModelAsset, wheelsRegion, "wheels")
                    };
                    outlines[block.ModelAsset] = Clipper.Union(bodyRegion, wheelsRegion, FillRule.NonZero, 6);
                    Console.WriteLine($"  {block.ModelAsset}: the body and the wheels collide with different parts");
                }
            }

            //Dynamic objects: what is within the whole width of the car
            foreach (string model in catalog.Objects.Where(type => type.Shape == ObjectShape.Model).SelectMany(type => type.Models).Distinct())
            {
                string asset = ObjectType.ModelAsset(model);
                PathsD region = Band(asset, Load(asset), 0, wheelsOuter);
                shapes.Models[asset] = new CollisionShape { Shape = Shape(asset, region, "") };
                outlines[asset] = region;
            }

            //The car: the whole models
            foreach (string model in CarBodies.Append(Holder))
            {
                PathsD region = Band(model, Load(model), 0, double.PositiveInfinity);
                shapes.Models[model] = new CollisionShape { Shape = Shape(model, region, "") };
                outlines[model] = region;
            }

            //Replaced at once, the other projects copy the file while the game builds
            string path = Path.Combine(content, CollisionShapes.FileName);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, shapes.ToJson(), new UTF8Encoding(false));
            File.Move(temporary, path, true);
            Console.WriteLine($"{shapes.Models.Count} models, {polygons} convex polygons -> {path}");

            if (svg != null)
            {
                Directory.CreateDirectory(svg);
                foreach (KeyValuePair<string, CollisionShape> model in shapes.Models)
                    File.WriteAllText(Path.Combine(svg, model.Key.Replace('/', '_') + ".svg"), Svg.Draw(model.Value, outlines[model.Key]));
                Console.WriteLine("Images -> " + svg);
            }
        }

        private static void Warn(string content, string model, string message)
            => Console.WriteLine($"{Path.Combine(content, model + ".obj")} : warning CG1: {message}");

        /// <summary>The content folder of the repository, from the current folder or the folder of the tool.</summary>
        private static string FindContent()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "content", Catalog.FileName)))
                        return Path.Combine(dir.FullName, "content");
            throw new DirectoryNotFoundException("The content folder was not found; give it as the first argument.");
        }
    }

    /// <summary>Pictures of the shapes for checking them by eye: the outline and the convex polygons.</summary>
    static class Svg
    {
        private const double Scale = 40;

        public static string Draw(CollisionShape shape, PathsD outline)
        {
            List<float[]> all = (shape.Shape ?? new List<float[]>()).Concat(shape.Body ?? new List<float[]>())
                .Concat(shape.Wheels ?? new List<float[]>()).ToList();
            //At least one block cell (-4.5 to 4.5), so that the blocks are drawn at the same scale
            double minX = Math.Min(-4.5, all.SelectMany(p => p.Where((_, i) => i % 2 == 0)).DefaultIfEmpty(0).Min()) - 0.5;
            double maxX = Math.Max(4.5, all.SelectMany(p => p.Where((_, i) => i % 2 == 0)).DefaultIfEmpty(0).Max()) + 0.5;
            double minY = Math.Min(-4.5, all.SelectMany(p => p.Where((_, i) => i % 2 == 1)).DefaultIfEmpty(0).Min()) - 0.5;
            double maxY = Math.Max(4.5, all.SelectMany(p => p.Where((_, i) => i % 2 == 1)).DefaultIfEmpty(0).Max()) + 0.5;

            StringBuilder svg = new StringBuilder();
            string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            string Point(double x, double y) => N((x - minX) * Scale) + "," + N((maxY - y) * Scale);
            svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N((maxX - minX) * Scale)}\" height=\"{N((maxY - minY) * Scale)}\">\n");
            svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>\n");
            svg.Append($"<rect x=\"{N((-4.5 - minX) * Scale)}\" y=\"{N((maxY - 4.5) * Scale)}\" width=\"{N(9 * Scale)}\" height=\"{N(9 * Scale)}\" fill=\"none\" stroke=\"#bbb\" stroke-dasharray=\"6 4\"/>\n");

            void Polygons(List<float[]> polygons, string fill)
            {
                if (polygons == null)
                    return;
                foreach (float[] p in polygons)
                {
                    IEnumerable<string> points = Enumerable.Range(0, p.Length / 2).Select(i => Point(p[2 * i], p[2 * i + 1]));
                    svg.Append($"<polygon points=\"{string.Join(" ", points)}\" fill=\"{fill}\" fill-opacity=\"0.45\" stroke=\"#555\" stroke-width=\"0.7\"/>\n");
                }
            }
            Polygons(shape.Shape, "#7a9cc6");
            Polygons(shape.Wheels, "#e0a040");
            Polygons(shape.Body, "#7a9cc6");
            foreach (PathD path in outline)
                svg.Append($"<polygon points=\"{string.Join(" ", path.Select(p => Point(p.x, p.y)))}\" fill=\"none\" stroke=\"black\" stroke-width=\"1.5\"/>\n");
            svg.Append("</svg>\n");
            return svg.ToString();
        }
    }
}
