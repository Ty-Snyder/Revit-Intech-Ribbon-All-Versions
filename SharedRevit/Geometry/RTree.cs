using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry
{
    internal class Partition
    {
        public List<MeshGeometryData> GroupA { get; } = new List<MeshGeometryData>();
        public List<MeshGeometryData> GroupB { get; } = new List<MeshGeometryData>();

        public void Add(MeshGeometryData record)
        {
            if (record.Role == GeometryRole.A) GroupA.Add(record);
            else GroupB.Add(record);
        }

        public bool ShouldProcess() => GroupA.Count > 0 && GroupB.Count > 0;

    }
}
