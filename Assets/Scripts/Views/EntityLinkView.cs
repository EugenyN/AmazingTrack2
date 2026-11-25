using UnityEngine;
using Leopotam.EcsLite;

namespace AmazingTrack.Views
{
    /// <summary>
    /// Maps a collider (block view) back to its ECS entity for physics queries.
    /// Attached to every spawned view by view systems.
    /// </summary>
    public class EntityLinkView : MonoBehaviour
    {
        /// <summary>Runtime-only link, set in Init; never serialized.</summary>
        [System.NonSerialized]
        public EcsWorld World;
        public int Entity;
        public short EntityGen;

        /// <summary>Initializes the link from the world and a live entity.</summary>
        public void Init(EcsWorld world, int entity)
        {
            World = world;
            Entity = entity;
            EntityGen = world.GetEntityGen(entity);
        }

        /// <summary>Whether the linked entity is still alive (not recycled).</summary>
        public bool IsAlive()
            => World != null && World.GetEntityGen(Entity) == EntityGen;

        /// <summary>Packs the linked entity for safe storage in components.</summary>
        public EcsPackedEntity Pack()
            => World != null && IsAlive() ? World.PackEntity(Entity) : default;
    }
}
