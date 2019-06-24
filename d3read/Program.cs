using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ObjConv;
using static d3read.Level;

namespace d3read
{
    class Program
    {
        void AddFace(Model m, Room r, Face f, int vertIdxOfs, Dictionary<int, int> matMap)
        {
            Vector3[] verts = r.verts;
            var vertIdx = f.face_verts;
            Vector3 bestNorm = default(Vector3);
            float bestNormMag = 0;
            for (int i = 1; i + 1 < vertIdx.Length; i++)
            {
                float mag = Vector3.GetNorm(verts[vertIdx[0]], verts[vertIdx[i]], verts[vertIdx[i + 1]], out Vector3 norm);
                if (mag > bestNormMag)
                {
                    bestNorm = norm;
                    bestNormMag = mag;
                }
            }
            var uvIdx = new int[vertIdx.Length];
            for (int i = 0; i < vertIdx.Length; i++)
            {
                uvIdx[i] = m.uvs.Count;
                m.uvs.Add(new Vector2(f.face_uvls[i].u, f.face_uvls[i].v));
            }
            int normIdx = m.norms.Count;
            m.norms.Add(bestNorm);
            int curMtl = matMap[f.tmapIdx]; //0; //f.tmapIdx;
            for (int i = 1; i + 1 < vertIdx.Length; i++)
                m.faces.Add(new Tuple<int[][], int>(new int[][] { new[] { vertIdx[0] + vertIdxOfs, uvIdx[0], normIdx },
                    new[] { vertIdx[i] + vertIdxOfs, uvIdx[i], normIdx },
                    new[] { vertIdx[i + 1] + vertIdxOfs, uvIdx[i + 1], normIdx } }, curMtl));
        }

        void Run()
        {
            var level = Level.Read(@"c:\temp\d3\seoulcity.d3l");
            var model = new Model();
            var matMap = new Dictionary<int, int>();
            var matIdx = new List<int>();
            foreach (var room in level.rooms)
                foreach (var face in room.faces)
                    if (!matMap.ContainsKey(face.tmapIdx))
                    {
                        matMap.Add(face.tmapIdx, model.mtls.Count);
                        //matIdx.Add(face.tmapIdx);
                        string n = level.texture_xlate[face.tmapIdx].n;
                        int i = n.IndexOf('.');
                        model.mtls.Add("d3_" + n.Substring(0, i < 0 ? n.Length : i));
                    }
            //model.mtls.AddRange(level.texture_xlate.Select(x => x.n));
            foreach (var room in level.rooms)
            {
                int vertIdxOfs = model.verts.Count;
                model.verts.AddRange(room.verts);
                foreach (var face in room.faces)
                    AddFace(model, room, face, vertIdxOfs, matMap);
            }
            Vector3 v = default(Vector3);
            foreach (var vert in model.verts)
                v += vert;
            v /= model.verts.Count;
            for (int i = 0, l = model.verts.Count; i < l; i++)
                model.verts[i] = (model.verts[i] - v) / 10;
            Debug.WriteLine("Total verts " + model.verts.Count);
            File.WriteAllText(@"c:\temp\d3\seoulcity.dmesh", JsonConvert.SerializeObject(model, Formatting.Indented, new DMeshConverter() { vertScale = 1.0f }));
        }

        static void Main(string[] args)
        {
            new Program().Run();

            return;
            Hog hog = Hog.OpenHog(@"C:\Program Files (x86)\GOG Galaxy\Games\Descent 3\d3.hog");
            var tableData = TableData.Read(hog.Open("table.gam"));
            Debug.WriteLine(tableData.textures.Count);
            foreach (var tex in tableData.textures)
                if (tex.filename.EndsWith(".ogf", StringComparison.InvariantCultureIgnoreCase))
                    Bitmap.Read(new BinaryReader(hog.Open(tex.filename))).WritePNG(@"c:\temp\d3\img\d3_" + tex.name + ".png");
#if false
            Hog hog = Hog.OpenHog(@"C:\Program Files (x86)\GOG Galaxy\Games\Descent 3\d3.hog");
            foreach (var entry in hog.Entries)
                if (entry.name.EndsWith(".ogf", StringComparison.InvariantCultureIgnoreCase))
                    Bitmap.Read(new BinaryReader(hog.Open(entry.name))).WritePNG(@"c:\temp\d3\img\d3_" + entry.name.Substring(0, entry.name.Length - 4) + ".png");
#endif
        }
    }
}
