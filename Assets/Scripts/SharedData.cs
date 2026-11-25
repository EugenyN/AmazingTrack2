using Leopotam.EcsLite;
using UnityEngine;
using UnityEngine.Pool;
using AmazingTrack.UI;
using AmazingTrack.Views;

namespace AmazingTrack
{
    /// <summary>
    /// Shared data passed to all systems through EcsSystems(world, shared).
    /// Engine-level services only (config, prefabs, camera, audio, UI, pools);
    /// all game state lives in ECS components.
    /// </summary>
    public class SharedData
    {
        public GameplayConfig GameplayConfig;
        public PresentationConfig PresentationConfig;
        public Camera Camera;
        public AudioSource Audio;
        public UiRoot Ui;

        /// <summary>Spawned views are parented here (kept tidy in hierarchy).</summary>
        public Transform ViewRoot;

        /// <summary>Sleeping pooled views are parked here (deactivated).</summary>
        public Transform PoolRoot;

        /// <summary>LayerMask of track blocks for physics queries.</summary>
        public LayerMask BlockMask;

        /// <summary>Ball prefab reference (spawned once per session).</summary>
        public GameObject BallPrefab;

        /// <summary>
        /// The ECS pipeline this shared data is passed to. Set by Bootstrap
        /// after all systems are added and before Init, so every system can
        /// resolve cross-references by type via GetSystem&lt;T&gt; during Init.
        /// </summary>
        public IEcsSystems Systems;

        /// <summary>
        /// Returns a system previously added to the pipeline. Call from Init
        /// (cache the result in a field), not per frame - the lookup scans
        /// the system list. Throws when absent: a misconfigured composition
        /// root must fail loudly at scene load.
        /// </summary>
        public T GetSystem<T>() where T : class
        {
            if (Systems != null)
                foreach (var system in Systems.GetAllSystems())
                    if (system is T typed)
                        return typed;
            throw new System.InvalidOperationException(
                $"System {typeof(T).Name} is not added to the ECS pipeline.");
        }

        /// <summary>Pool of block root views (created in Bootstrap).</summary>
        public ObjectPool<Views.BlockView> BlockPool;

        /// <summary>Pool of crystal views (created in Bootstrap).</summary>
        public ObjectPool<GameObject> CrystalPool;

        /// <summary>Pool of golden crystal views (created in Bootstrap).</summary>
        public ObjectPool<GameObject> GoldenCrystalPool;

        /// <summary>Pool of pickup FX instances (created in Bootstrap).</summary>
        public ObjectPool<Views.PooledFxView> FxPool;

        /// <summary>Crystal variant materials, cached once for FX tinting (regular, golden).</summary>
        public Material CrystalFxMaterial;
        public Material GoldenCrystalFxMaterial;

        /// <summary>
        /// Caches the crystal prefabs' materials for pickup FX tinting
        /// (init-time asset reads, no play-time work).
        /// </summary>
        public void CacheCrystalMaterials()
        {
            CrystalFxMaterial = GetPrefabMaterial(PresentationConfig.crystalPrefab);
            GoldenCrystalFxMaterial = GetPrefabMaterial(PresentationConfig.goldenCrystalPrefab);
        }

        private static Material GetPrefabMaterial(GameObject prefab)
        {
            if (prefab == null)
                return null;
            var renderer = prefab.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        /// <summary>
        /// Creates the view pools. createFunc instantiates the prefab under
        /// PoolRoot, captures cached view wiring (BlockView part refs) and
        /// adds the persistent EntityLinkView, then deactivates; onGet activates;
        /// onRelease deactivates and parks under PoolRoot; onDestroy destroys
        /// (scene teardown). collectionCheck catches double-release in the
        /// Editor. Destroy callbacks are null-safe: with domain reload
        /// disabled, the Editor's PoolManager.Reset() clears pools after the
        /// scene teardown has already destroyed the pooled views, so already
        /// destroyed views are skipped instead of throwing
        /// MissingReferenceException.
        /// </summary>
        public void CreatePools()
        {
            BlockPool = new ObjectPool<BlockView>(
                () =>
                {
                    var go = Object.Instantiate(PresentationConfig.blockPrefab, PoolRoot);
                    var view = go.GetComponent<BlockView>();
                    view.Capture();
                    go.SetActive(false);
                    return view;
                },
                view => view.gameObject.SetActive(true),
                view =>
                {
                    view.gameObject.SetActive(false);
                    view.transform.SetParent(PoolRoot, false);
                },
                view => { if (view != null) Object.Destroy(view.gameObject); },
                collectionCheck: true);

            CrystalPool = new ObjectPool<GameObject>(
                () => Object.Instantiate(PresentationConfig.crystalPrefab, PoolRoot),
                go => go.SetActive(true),
                go =>
                {
                    go.SetActive(false);
                    go.transform.SetParent(PoolRoot, false);
                },
                go => { if (go != null) Object.Destroy(go); },
                collectionCheck: true);

            GoldenCrystalPool = new ObjectPool<GameObject>(
                () => Object.Instantiate(PresentationConfig.goldenCrystalPrefab, PoolRoot),
                go => go.SetActive(true),
                go =>
                {
                    go.SetActive(false);
                    go.transform.SetParent(PoolRoot, false);
                },
                go => { if (go != null) Object.Destroy(go); },
                collectionCheck: true);

            FxPool = new ObjectPool<PooledFxView>(
                () =>
                {
                    var go = Object.Instantiate(PresentationConfig.crystalPickupFxPrefab, PoolRoot);
                    return go.GetComponent<PooledFxView>();
                },
                fx => fx.gameObject.SetActive(true),
                fx =>
                {
                    fx.gameObject.SetActive(false);
                    fx.transform.SetParent(PoolRoot, false);
                },
                fx => { if (fx != null) Object.Destroy(fx.gameObject); },
                collectionCheck: true);
        }
    }
}
