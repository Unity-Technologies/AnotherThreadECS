using UnityEngine;

namespace UTJ {

public class Dolly : MonoBehaviour {

    float distance_;

    void Start()
    {
        var pos = transform.position;
        distance_ = pos.magnitude;
        transform.rotation = Quaternion.LookRotation(-pos);
    }

    void Update()
    {
        Vector2 motion = TouchUtil.GetMotion();
        if (motion != Vector2.zero) {
            const float SCALE = 1000f;
            transform.Rotate(-motion.y*SCALE, motion.x*SCALE, 0f);
            transform.position = transform.TransformVector(new Vector3(0f, 0f, -distance_));
            
        }
    }
}

} // namespace UTJ {
