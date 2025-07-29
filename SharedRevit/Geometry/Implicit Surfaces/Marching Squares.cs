using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry.Implicit_Surfaces
{
    public class MarchingSquares
    {
        public delegate float SampleFunction(Vector2 point);

        public static List<List<Vector2>> GenerateContours(
            SampleFunction sample,
            BoundingBox2D bounds,
            int resolution,
            float isoLevel = 0f)
        {
            float dx = bounds.Size.X / (resolution - 1);
            float dy = bounds.Size.Y / (resolution - 1);

            var contours = new List<List<Vector2>>();

            for (int y = 0; y < resolution - 1; y++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    Vector2 p0 = bounds.Min + new Vector2(x * dx, y * dy);
                    Vector2 p1 = p0 + new Vector2(dx, 0);
                    Vector2 p2 = p0 + new Vector2(dx, dy);
                    Vector2 p3 = p0 + new Vector2(0, dy);

                    float v0 = sample(p0);
                    float v1 = sample(p1);
                    float v2 = sample(p2);
                    float v3 = sample(p3);

                    int caseIndex = 0;
                    if (v0 < isoLevel) caseIndex |= 1;
                    if (v1 < isoLevel) caseIndex |= 2;
                    if (v2 < isoLevel) caseIndex |= 4;
                    if (v3 < isoLevel) caseIndex |= 8;

                    var edges = GetEdges(caseIndex, p0, p1, p2, p3, v0, v1, v2, v3, isoLevel);
                    if (edges != null)
                        contours.Add(edges);
                }
            }

            return contours;
        }

        private static List<Vector2> GetEdges(int caseIndex, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
                                              float v0, float v1, float v2, float v3, float iso)
        {
            Vector2 Interp(Vector2 a, Vector2 b, float va, float vb)
            {
                float t = (iso - va) / (vb - va);
                return a + t * (b - a);
            }

            switch (caseIndex)
            {
                case 1: return new List<Vector2> { Interp(p0, p1, v0, v1), Interp(p0, p3, v0, v3) };
                case 2: return new List<Vector2> { Interp(p1, p2, v1, v2), Interp(p0, p1, v0, v1) };
                case 3: return new List<Vector2> { Interp(p1, p2, v1, v2), Interp(p0, p3, v0, v3) };
                case 4: return new List<Vector2> { Interp(p2, p3, v2, v3), Interp(p1, p2, v1, v2) };
                case 5:
                    return new List<Vector2> {
                Interp(p0, p1, v0, v1), Interp(p1, p2, v1, v2),

}
