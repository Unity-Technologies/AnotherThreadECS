using UnityEngine;

namespace UTJ {

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class Grids : MonoBehaviour
{
	[SerializeField] Material material_;
    Mesh mesh_;
    
    void Start()
    {
        const int LINE_NUM = 16;
        const float GRID_WIDTH = 1f;
        Vector3[] vertices = new Vector3[LINE_NUM*2*LINE_NUM];
        var vidx = 0;
        for (var z = 0; z < LINE_NUM; ++z) {
            for (var x = 0; x < LINE_NUM; ++x) {
                float posx = (x-LINE_NUM/2) * GRID_WIDTH;
                float posy0 = (-LINE_NUM/2) * GRID_WIDTH;
                float posy1 = (LINE_NUM/2) * GRID_WIDTH;
                float posz = (z-LINE_NUM/2) * GRID_WIDTH;
                vertices[vidx] = new Vector3(posx, posy0, posz); ++vidx;
                vertices[vidx] = new Vector3(posx, posy1, posz); ++vidx;
            }
        }
		var indices = new int[LINE_NUM*LINE_NUM*2];
		for (var i = 0; i < LINE_NUM*LINE_NUM*2; ++i) {
			indices[i] = i;
		}

		mesh_ = new Mesh();
		mesh_.vertices = vertices;
		mesh_.SetIndices(indices, MeshTopology.Lines, 0 /* submesh */);
		mesh_.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);

        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = mesh_;
        var mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = material_;
    }
}
    
} // namespace UTJ {
