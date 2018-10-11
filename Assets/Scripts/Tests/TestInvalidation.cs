using Unity.Entities;
using Unity.Jobs;

namespace TestInterface {
    public abstract class ComponentSystem {
        public EntityCommandBuffer PostUpdateCommands;
        protected abstract void OnUpdate();
    }
    public abstract class JobComponentSystem {
        protected abstract JobHandle OnUpdate(JobHandle inputDep);
    }
}
