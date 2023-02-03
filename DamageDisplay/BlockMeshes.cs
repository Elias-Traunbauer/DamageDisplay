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
new Vector3(0f, 1.203201f, 0f),
new Vector3(0.746304f, 1.203201f, 0.746304f),
new Vector3(1.072114f, 1.203201f, 0f),
new Vector3(-0.746304f, 1.203201f, 0.746304f),
new Vector3(-1.072114f, 1.203201f, 0f),
new Vector3(-0.746304f, 1.203201f, -0.746304f),
new Vector3(0f, 1.203201f, -1.114011f),
new Vector3(0.746304f, 1.203201f, -0.746304f),
new Vector3(0f, -0.448009f, 0f),
new Vector3(0.588118f, -0.448009f, 0.588118f),
new Vector3(0f, -0.448009f, 0.877887f),
new Vector3(-0.588118f, -0.448009f, 0.588118f),
new Vector3(-0.84487f, -0.448009f, 0f),
new Vector3(-0.588118f, -0.448009f, -0.588118f),
new Vector3(0.588118f, -0.448009f, -0.588118f),
new Vector3(0f, -0.448009f, -0.877886f),
new Vector3(0.84487f, -0.448009f, 0f),
new Vector3(0f, 1.203201f, 1.114011f),
new Vector3(-1.251654f, -0.988329f, 1.251654f),
new Vector3(-0.636849f, 0.285549f, -0.650382f),
new Vector3(-1.251654f, -0.988329f, -1.251654f),
new Vector3(-0.636849f, 0.285549f, 0.650382f),
new Vector3(0.636849f, 0.285549f, -0.650382f),
new Vector3(0.636849f, 0.285549f, 0.650382f),
new Vector3(1.251654f, -0.988329f, -1.251654f),
new Vector3(1.251654f, -0.988329f, 1.251654f),
};

        public int[] hydrogen_thruster_small_triangles = new int[132] {
0, 1, 2,
3, 0, 4,
5, 0, 6,
0, 7, 6,
8, 9, 10,
8, 11, 12,
13, 8, 12,
14, 8, 15,
5, 15, 13,
1, 16, 2,
5, 12, 4,
3, 12, 11,
1, 10, 9,
3, 10, 17,
7, 15, 6,
7, 16, 14,
18, 19, 20,
21, 22, 19,
23, 24, 22,
25, 20, 24,
22, 20, 19,
21, 25, 23,
0, 17, 1,
3, 17, 0,
5, 4, 0,
0, 2, 7,
8, 16, 9,
8, 10, 11,
13, 15, 8,
14, 16, 8,
5, 6, 15,
1, 9, 16,
5, 13, 12,
3, 4, 12,
1, 17, 10,
3, 11, 10,
7, 14, 15,
7, 2, 16,
18, 21, 19,
21, 23, 22,
23, 25, 24,
25, 18, 20,
22, 24, 20,
21, 18, 25,
};
    }
}
