using UnityEngine;
// using System.Collections;

namespace UTJ {

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class DebrisRenderer : MonoBehaviour {

    [SerializeField] UnityEngine.Camera camera_;
    [SerializeField] Material material_;
    private MeshFilter mf_;
    private MeshRenderer mr_;

    void Start()
    {
        Debris.Instance.init(material_);
        mf_ = GetComponent<MeshFilter>();
        mr_ = GetComponent<MeshRenderer>();
        mf_.sharedMesh = Debris.Instance.getMesh();
        mr_.sharedMaterial = Debris.Instance.getMaterial();
    }

    void Update()
    {
        Debris.Instance.render(camera_, Time.GetCurrent());
    }
}

} // namespace UTJ {

/*
 * End of DebrisRenderer.cs
 */
