using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace UTJ {

public class GameCameraComponent : ComponentDataWrapper<GameCamera>
{
    public void setup(Entity player_entity)
    {
        var goe = GetComponent<GameObjectEntity>();
        var entity_manager = goe.EntityManager;
        var entity = goe.Entity;
        entity_manager.SetComponentData(entity, new Position { Value = new float3(0f, 0f, -8f), });
        entity_manager.SetComponentData(entity, GameCamera.Create(player_entity));
    }
} 

} // namespace UTJ {
