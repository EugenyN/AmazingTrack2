using System;
using Leopotam.EcsLite;

namespace AmazingTrack
{
    /// <summary>
    /// Singleton-entity access helpers: return the single entity matched by
    /// a filter (or its component) and throw an InvalidOperationException
    /// when the singleton invariant is violated (zero or multiple matches)
    /// instead of silently taking the first match. The filter overloads
    /// touch only the caller's cached filter and pool: no allocations.
    /// </summary>
    public static class EcsExtensions
    {
        /// <summary>
        /// The single entity matched by the filter.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The filter matched zero or more than one entity.
        /// </exception>
        public static int GetSingletonEntity(this EcsFilter filter)
        {
            int count = filter.GetEntitiesCount();
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Singleton access expects exactly 1 entity, but the filter matched {count}.");
            }
            return filter.GetRawEntities()[0];
        }

        /// <summary>
        /// The component of the single entity matched by the filter.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The filter matched zero or more than one entity.
        /// </exception>
        public static ref T GetSingletonComponent<T>(this EcsFilter filter, EcsPool<T> pool) where T : struct
        {
            return ref pool.Get(GetSingletonEntity(filter));
        }

        /// <summary>
        /// The component of the single entity carrying T in the world.
        /// Builds a filter per call: for init-time or rare reads - cache the
        /// filter and use the filter overload in per-frame paths.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Zero or more than one entity carries T.
        /// </exception>
        public static ref T GetSingletonComponent<T>(this EcsWorld world) where T : struct
        {
            var filter = world.Filter<T>().End();
            return ref world.GetPool<T>().Get(SingletonEntityOfType<T>(filter));
        }

        /// <summary>
        /// The component of the single entity carrying T in the world, with
        /// its entity id as output. Builds a filter per call: for init-time
        /// or rare reads - cache the filter in per-frame paths.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Zero or more than one entity carries T.
        /// </exception>
        public static ref T GetSingletonComponent<T>(this EcsWorld world, out int entity) where T : struct
        {
            var filter = world.Filter<T>().End();
            entity = SingletonEntityOfType<T>(filter);
            return ref world.GetPool<T>().Get(entity);
        }

        /// <summary>Throws with the component type named on any count != 1.</summary>
        private static int SingletonEntityOfType<T>(EcsFilter filter) where T : struct
        {
            int count = filter.GetEntitiesCount();
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Component {typeof(T).Name} is expected on exactly one entity, but found on {count}.");
            }
            return filter.GetRawEntities()[0];
        }
    }
}
