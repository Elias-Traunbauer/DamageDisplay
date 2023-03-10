using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace IngameScript
{
    class BlockMeshes
    {
public Vector3[] hydrogen_thruster_small_vertices = new Vector3[26] {
new Vector3(-0.970823f, -0.306772f, 0f),
new Vector3(-0.841014f, 1.199243f, 0.841014f),
new Vector3(-1.181781f, 1.199243f, 0f),
new Vector3(0f, 1.199243f, 0f),
new Vector3(0.841014f, 1.199243f, 0.841014f),
new Vector3(1.181781f, 1.199243f, 0f),
new Vector3(0.771292f, -0.306772f, 0.771292f),
new Vector3(1.249151f, -0.98572f, 0f),
new Vector3(0.970823f, -0.306772f, 0f),
new Vector3(0f, -0.98572f, 1.249151f),
new Vector3(-1.249151f, -0.98572f, 0f),
new Vector3(0f, -0.98572f, 0f),
new Vector3(0.771292f, -0.306772f, -0.771292f),
new Vector3(0f, -0.98572f, -1.249151f),
new Vector3(0f, -0.306772f, -0.970823f),
new Vector3(-0.771292f, -0.306772f, 0.771292f),
new Vector3(0f, -0.306772f, 0.970823f),
new Vector3(0f, 1.199243f, 1.181781f),
new Vector3(0.841014f, 1.199243f, -0.841014f),
new Vector3(0f, 1.199243f, -1.181781f),
new Vector3(-0.841014f, 1.199243f, -0.841014f),
new Vector3(-0.771292f, -0.306772f, -0.771292f),
new Vector3(1.249151f, -0.98572f, 1.249151f),
new Vector3(-1.249151f, -0.98572f, -1.249151f),
new Vector3(1.249151f, -0.98572f, -1.249151f),
new Vector3(-1.249151f, -0.98572f, 1.249151f),
};

        public int[] hydrogen_thruster_small_triangles = new int[144] {
0, 1, 2,
3, 4, 5,
6, 7, 8,
9, 10, 11,
12, 13, 14,
15, 9, 16,
1, 16, 17,
18, 14, 19,
4, 8, 5,
10, 15, 0,
20, 14, 21,
4, 16, 6,
6, 9, 22,
21, 13, 23,
22, 11, 7,
1, 3, 2,
20, 3, 19,
7, 13, 24,
10, 21, 23,
18, 8, 12,
11, 23, 13,
12, 7, 24,
3, 18, 19,
0, 20, 21,
0, 15, 1,
3, 17, 4,
6, 22, 7,
9, 25, 10,
12, 24, 13,
15, 25, 9,
1, 15, 16,
18, 12, 14,
4, 6, 8,
10, 25, 15,
20, 19, 14,
4, 17, 16,
6, 16, 9,
21, 14, 13,
22, 9, 11,
1, 17, 3,
20, 2, 3,
7, 11, 13,
10, 0, 21,
18, 5, 8,
11, 10, 23,
12, 8, 7,
3, 5, 18,
0, 2, 20,
};
    }
}
