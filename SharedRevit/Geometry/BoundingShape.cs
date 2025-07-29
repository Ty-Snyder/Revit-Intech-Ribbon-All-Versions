using Autodesk.Revit.DB;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using SharedRevit.Geometry.Implicit_Surfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry
{
    internal class BoundingShape
    {
        public static IShape SimpleMeshToIShape(SimpleMesh mesh)
        {
            var (axes, translation) = ComputePrincipalAxes(mesh);
            return ShapeOptimize(axes, translation, mesh);
        }

        public static (Vector3[] Axes, Vector3 Translation) ComputePrincipalAxes(SimpleMesh mesh)
        {
            if (mesh.Vertices == null || mesh.Vertices.Count < 3)
                throw new ArgumentException("Mesh must contain at least 3 vertices.");

            // Convert List<Vector3> to matrix rows
            var rows = mesh.Vertices.Select(v => new double[] { v.X, v.Y, v.Z });
            var data = Matrix<double>.Build.DenseOfRows(mesh.Vertices.Count, 3, rows);

            // Compute mean (centroid)
            var mean = data.ColumnSums() / data.RowCount;
            var translation = new Vector3((float)mean[0], (float)mean[1], (float)mean[2]);

            // Center the data
            for (int i = 0; i < data.RowCount; i++)
            {
                var centeredRow = data.Row(i) - mean;
                data.SetRow(i, centeredRow);
            }

            // Covariance matrix
            var covariance = data.TransposeThisAndMultiply(data) / (data.RowCount - 1);

            // Eigen decomposition
            var evd = covariance.Evd(Symmetricity.Symmetric);

            // Sort eigenvectors by eigenvalues descending
            var eigenPairs = evd.EigenValues
                .Select((val, idx) => new { Value = val.Real, Vector = evd.EigenVectors.Column(idx) })
                .OrderByDescending(e => e.Value)
                .ToArray();

            // Convert to Vector3
            var axes = eigenPairs.Select(e => new Vector3(
                (float)e.Vector[0],
                (float)e.Vector[1],
                (float)e.Vector[2]
            )).ToArray();

            return (axes, translation);
        }

        public static IShape ShapeOptimize(Vector3[]  axes, Vector3 translation, SimpleMesh mesh)
        {

            Matrix4x4 transform = new Matrix4x4(
                    axes[0].X, axes[1].X, axes[2].X, 0,
                    axes[0].Y, axes[1].Y, axes[2].Y, 0,
                    axes[0].Z, axes[1].Z, axes[2].Z, 0,
                    -translation.X, -translation.Y, -translation.Z, 1
                );




            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            float capsuleMinX = float.MaxValue;
            float capsuleMaxX = float.MinValue;
            float maxRadiusSq = 0f;


            foreach (Vector3 vert in mesh.Vertices)
            {
                // Transform vertex to PCA space
                Vector3 v = Vector3.Transform(vert, transform);

                // Update bounding box
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);

                // Track X extents (along principal axis)
                if (v.X < capsuleMinX) capsuleMinX = v.X;
                if (v.X > capsuleMaxX) capsuleMaxX = v.X;

                // Track max radius in Y-Z plane
                float radiusSq = v.Y * v.Y + v.Z * v.Z;
                if (radiusSq > maxRadiusSq) maxRadiusSq = radiusSq;

                // Update bounding cylinder radius (Y-Z plane)
                if (radiusSq > maxRadiusSq)
                    maxRadiusSq = radiusSq;
            }

            float capsuleHeight = capsuleMaxX - capsuleMinX;
            float capsuleRadius = Math.Sqrt(maxRadiusSq);

            // Subtract 2 * radius from height to get cylinder part
            float cylinderHeight = MathF.Max(0f, capsuleHeight - 2f * capsuleRadius);

            float capsuleVolume =
                MathF.PI * capsuleRadius * capsuleRadius * cylinderHeight + // cylinder
                (4f / 3f) * MathF.PI * MathF.Pow(capsuleRadius, 3);         // 2 hemispheres

            return null;
        }

    }
}
