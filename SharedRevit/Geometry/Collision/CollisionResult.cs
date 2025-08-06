using SharedRevit.Geometry.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRevit.Geometry.Collision
{
    public class CollisionResult
    {

        public MeshGeometryData A { get; set; }
        public MeshGeometryData B { get; set; }
        public SimpleMesh Intersection
        {
            get; set;
        }

        public List<Face> Faces 
        {
            get; set; 
        }
    }
}
