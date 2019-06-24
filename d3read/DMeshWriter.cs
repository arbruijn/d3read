using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using d3read;
using System.Collections;

public static class Exts
{
    public static void WriteProp<T>(this JsonWriter writer, string name, T value)
    {
        writer.WritePropertyName(name);
        writer.WriteValue(value);
    }
}

namespace ObjConv
{
    static class NameConv
    {
        private static Regex rx = new Regex("[<>:\\\"/\\\\|?*]", RegexOptions.Compiled);

        public static string Conv(string name)
        {
            return name; //"conv_" + rx.Replace(name.ToLowerInvariant(), "_");
        }
    }

    class Tuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
        public Tuple(T1 Item1, T2 Item2)
        {
            this.Item1 = Item1;
            this.Item2 = Item2;
        }
    }

    class Model
    {
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector3> norms = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<Tuple<int[][], int>> faces = new List<Tuple<int[][], int>>();
        public List<string> mtls = new List<string>();
    }

    class DMeshConverter : JsonConverter
    {
        //public static readonly DMeshConverter Instance = new DMeshConverter();
        public float vertScale = 1.0f; //0.0254f

        private static readonly Type ModelType = typeof(Model);

        public override bool CanConvert(Type objectType)
        {
            return ModelType.IsAssignableFrom(objectType);
        }

        private static void WriteXYZ(JsonWriter writer, Vector3 xyz, float scale = 1)
        {
            writer.WriteStartObject();
            writer.WriteProp("x", (float)xyz.x * scale);
            writer.WriteProp("y", (float)xyz.y * scale);
            writer.WriteProp("z", -(float)xyz.z * scale);
            writer.WriteEndObject();
        }

        private static void WriteUV(JsonWriter writer, Vector2 uv)
        {
            writer.WriteStartObject();
            writer.WriteProp("u", uv.x);
            writer.WriteProp("v", (1.0f - (float)uv.y));
            writer.WriteEndObject();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var model = (Model)value;

            var uvEmpty = new Vector2(0, 0);

            writer.WriteStartObject();
            writer.WritePropertyName("verts");
            writer.WriteStartArray();
            foreach (var vert in model.verts)
                WriteXYZ(writer, vert, vertScale);
            writer.WriteEndArray();

            writer.WritePropertyName("polys");
            writer.WriteStartObject();
            writer.WriteEndObject();

            writer.WritePropertyName("triangles");
            writer.WriteStartObject();
            int i = 0;
            foreach (var face in model.faces)
            {
                var idxs = face.Item1;
                writer.WritePropertyName(i.ToString());
                i++;
                writer.WriteStartObject();
                writer.WriteProp("tex_index", face.Item2);
                writer.WriteProp("flags", 0);
                writer.WritePropertyName("verts");
                writer.WriteStartArray();
                for (var j = 2; j >= 0; j--)
                    writer.WriteValue(idxs[j][0]);
                writer.WriteEndArray();

                writer.WritePropertyName("normals");
                writer.WriteStartArray();
                for (var j = 2; j >= 0; j--)
                    WriteXYZ(writer, model.norms[idxs[j][2]]);
                writer.WriteEndArray();

                writer.WritePropertyName("uvs");
                writer.WriteStartArray();
                for (var j = 2; j >= 0; j--)
                    WriteUV(writer, idxs[j][1] >= 0 ? model.uvs[idxs[j][1]] : uvEmpty);
                writer.WriteEndArray();

                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("tex_names");
            writer.WriteStartArray();
            foreach (var x in model.mtls)
                writer.WriteValue(NameConv.Conv(x));
            writer.WriteEndArray();

            writer.WritePropertyName("lights");
            writer.WriteStartObject();
            for (int j = 0; j < 4; j++)
            {
                writer.WritePropertyName(j.ToString());
                writer.WriteStartObject();
                writer.WriteProp("enabled", false);
                writer.WriteProp("style", "POINT");
                writer.WriteProp("flare", "NONE");
                writer.WritePropertyName("position");
                writer.WriteStartArray();
                for (int k = 0; k < 3; k++)
                    writer.WriteValue(0.0);
                writer.WriteEndArray();
                writer.WriteProp("rot_yaw", 0.0);
                writer.WriteProp("rot_pitch", 0.0);
                writer.WriteProp("color_index", 0);
                writer.WriteProp("intensity", 1.0);
                writer.WriteProp("range", 10.0);
                writer.WriteProp("angle", 45.0);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("colors");
            writer.WriteStartArray();
            for (int j = 0; j < 4; j++)
            {
                writer.WriteStartObject();
                writer.WriteProp("r", 255);
                writer.WriteProp("g", 255);
                writer.WriteProp("b", 255);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteProp("smooth_diff", 0);
            writer.WriteProp("smooth_same", 0);

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object value, JsonSerializer serializer)
        {
            return null;
        }
    }


    class Program
    {
        static void ConvMtlLib()
        {
            string filename = @"C:\temp\3d\cartoonforest\Enviroment_GroupTrees.mtl";
            var dstbase = @"c:\prog\overloadleveleditor\decaltextures\";
            var srcbase = Directory.GetParent(filename) + @"\";
            string cur = null;
            foreach (var line in File.ReadAllLines(filename))
            {
                var ic = line.IndexOf("#");
                if (ic == 0 || line.Length == 0)
                    continue;
                var parts = ic >= 0 ? line.Substring(0, ic).TrimEnd().Split(new char[0], StringSplitOptions.RemoveEmptyEntries) :
                        line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "newmtl":
                        cur = parts[1];
                        break;
                    case "map_Kd":
                        var src = srcbase + parts[1];
                        var dst = dstbase + NameConv.Conv(cur) + ".png";
                        File.Copy(src, dst);
                        break;
                }
            }
        }

        static void MainObjConv(string[] args)
        {
            //string filename = @"C:\temp\3d\cartoonforest\Enviroment_GroupTrees.obj";
            //string filename = @"C:\users\arne\downloads\tardis.obj";
            string filename = @"C:\users\arne\downloads\OverloadMiningMachine5.obj";
            //string filename = @"C:\temp\3d\box\box_obj.obj";
            string mtllib = null;
            //var verts = new List<double[]>();
            //var norms = new List<double[]>();
            //var uvs = new List<double[]>();
            //var faces = new List<int[][]>();
            var model = new Model();
            var sepSlash = new char[] { '/' };
            var facePartLists = new IList[] { model.verts, model.uvs, model.norms };
            var mtlIdx = new Dictionary<string, int>();
            int curMtl = 0;
            foreach (var rline in File.ReadAllLines(filename))
            {
                var line = rline;
                var ic = line.IndexOf("#");
                if (ic == 0 || line.Length == 0)
                    continue;
                if (ic >= 0)
                    line = line.Substring(0, ic).TrimEnd();
                var parts = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                float[] dparts;
                switch (parts[0])
                {
                    case "mtllib":
                        if (mtllib != null)
                            throw new Exception("dup mtllib");
                        if (parts.Count() > 2)
                            throw new Exception("mtllib too many arguments");
                        mtllib = parts[1];
                        break;
                    case "usemtl":
                        if (parts.Count() > 2)
                            throw new Exception("usemtl too many arguments");
                        string mtl = parts[1];
                        if (!mtlIdx.TryGetValue(parts[1], out curMtl))
                        {
                            curMtl = model.mtls.Count;
                            mtlIdx.Add(mtl, curMtl);
                            model.mtls.Add(mtl);
                            Debug.WriteLine(mtl + ": " + curMtl);
                        }
                        break;
                    case "v":
                        dparts = parts.Skip(1).Select(x => Single.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                        model.verts.Add(new Vector3(dparts[0], dparts[1], dparts[2]));
                        break;
                    case "vn":
                        dparts = parts.Skip(1).Select(x => Single.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                        model.norms.Add(new Vector3(dparts[0], dparts[1], dparts[2]));
                        break;
                    case "vt":
                        dparts = parts.Skip(1).Select(x => Single.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                        model.uvs.Add(new Vector2(dparts[0], dparts[1]));
                        break;
                    case "f":
                        int[][] idx = parts.Skip(1).Select(x => x.Split(sepSlash).Select((y, yi) => {
                            var n = y.Length != 0 ? int.Parse(y, CultureInfo.InvariantCulture) : 0;
                            return n < 0 ? n + facePartLists[yi].Count : n - 1;
                        }).ToArray()).ToArray();
                        if (idx.Length > 3)
                        {
                            model.faces.Add(new Tuple<int[][], int>(new int[][] { idx[0], idx[1], idx[2] }, curMtl));
                            for (int i = 0; i + 3 < idx.Length; i++)
                                model.faces.Add(new Tuple<int[][], int>(new int[][] { idx[i], idx[i + 2], idx[i + 3] }, curMtl));
                        }
                        else
                        {
                            model.faces.Add(new Tuple<int[][], int>(idx, curMtl));
                        }
                        break;
                    case "s":
                    case "g":
                    case "o":
                    case "l":
                        break;
                    default:
                        throw new Exception("Unknown line " + line);
                }
            }
            // 0.0254f
            File.WriteAllText(filename + ".dmesh", JsonConvert.SerializeObject(model, Formatting.Indented, new DMeshConverter() { vertScale = 1.0f }));
            //ConvMtlLib();
        }
    }
}
