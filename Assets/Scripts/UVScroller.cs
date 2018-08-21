using UnityEngine;
// using System.Collections;

namespace UTJ {

[RequireComponent(typeof(MeshRenderer))]
public class UVScroller : MonoBehaviour
{
	const float ScrollSpeed = 2f;
	private MeshRenderer mr_;
    private Material material_;
    private float offset_;
	static readonly int material_MainTex = Shader.PropertyToID("_MainTex");

	void Start()
	{
		mr_ = GetComponent<MeshRenderer>();
        material_ = mr_.sharedMaterial;
        offset_ = 0f;
	}

    void Update ()
	{
        offset_ += ScrollSpeed * Time.GetDT();
        offset_ = Mathf.Repeat(offset_, 1f);
        material_.SetTextureOffset(material_MainTex, new Vector2(offset_, 0f));
	}
}

} // namespace UTJ {
