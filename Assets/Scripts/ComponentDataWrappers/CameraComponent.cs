using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace UTJ {

public class CameraComponent : ComponentDataWrapper<Camera>
{
    public void setup(Matrix4x4 projection_matrix)
    {
        var goe = GetComponent<GameObjectEntity>();
        var entity_manager = goe.EntityManager;
        var entity = goe.Entity;
        entity_manager.SetComponentData(entity, Camera.Create(ref projection_matrix));
    }

    void OnEnable()
    {
        var camera = GetComponent<UnityEngine.Camera>();
        setup(camera.projectionMatrix);
    }
}

} // namespace UTJ {
