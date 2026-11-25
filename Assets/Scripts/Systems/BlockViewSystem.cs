using Leopotam.EcsLite;
using UnityEngine;

namespace AmazingTrack
{
    /// <summary>
    /// Spawns and syncs block views for composite blocks: acquires the Block
    /// prefab root from the shared pool (BlockView + EntityLinkView live there,
    /// captured once per pooled instance; the root's own renderer stays
    /// disabled and carries no collider), then activates the prefab's baked
    /// 1..3 unit part cubes laid along -Right from the head cell. A hole
    /// part (HolePart) gets no cube - its child stays inactive: no mesh, no
    /// collider. All parts share one random pastel HSV color through a
    /// reusable MaterialPropertyBlock. EntityLinkView is re-initialized with the
    /// live entity on every acquire; a BlockViewComponent alongside the
    /// ViewLinkComponent carries the typed instance for release paths.
    /// Views of dead entities are released to
    /// the pool by their killers (BlockFallViewSystem, GameFlowSystem).
    /// The spawn path allocates nothing and searches no components.
    /// </summary>
    public class BlockViewSystem : IEcsInitSystem, IEcsRunSystem
    {
        private SharedData _shared;
        private EcsWorld _world;
        private EcsFilter _newBlocks;
        private EcsPool<BlockComponent> _blockPool;
        private EcsPool<ViewLinkComponent> _viewPool;
        private EcsPool<BlockViewComponent> _blockViewPool;

        /// <summary>Reusable property block: set per block, never reallocated.</summary>
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Init(IEcsSystems systems)
        {
            _world = systems.GetWorld();
            _shared = systems.GetShared<SharedData>();

            // Blocks without a view link yet.
            _newBlocks = _world.Filter<BlockComponent>().Exc<ViewLinkComponent>().End();
            _blockPool = _world.GetPool<BlockComponent>();
            _viewPool = _world.GetPool<ViewLinkComponent>();
            _blockViewPool = _world.GetPool<BlockViewComponent>();
        }

        public void Run(IEcsSystems systems)
        {
            foreach (int entity in _newBlocks)
            {
                ref var block = ref _blockPool.Get(entity);

                // Root: acquired from the pool as the typed BlockView; the
                // prefab's baked part children carry visuals+physics
                // (captured once per pooled instance).
                var blockView = _shared.BlockPool.Get();
                var go = blockView.gameObject;
                go.transform.SetParent(_shared.ViewRoot, false);
                go.transform.position = block.Pos;
                go.transform.rotation = Quaternion.LookRotation(block.Forward, Vector3.up);
                go.transform.localScale = Vector3.one;

                var pres = _shared.PresentationConfig;
                var gp = _shared.GameplayConfig;

                // One random pastel HSV color for the whole block, set on
                // the reusable property block.
                float hue = Random.value;
                float sat = Random.Range(pres.colorSaturation.x, pres.colorSaturation.y);
                float val = Random.Range(pres.colorValue.x, pres.colorValue.y);
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, Color.HSVToRGB(hue, sat, val));

                // Activate the baked part cubes from the head cell backwards
                // along -Right; the hole part (and everything beyond Parts)
                // stays inactive.
                var parts = blockView.Parts;
                var renderers = blockView.PartRenderers;
                for (int i = 0; i < parts.Length; i++)
                {
                    bool isHole = i == block.HolePart;
                    bool inParts = i < block.Parts;
                    bool active = inParts && !isHole;
                    var part = parts[i];
                    if (part.gameObject.activeSelf != active)
                        part.gameObject.SetActive(active);
                    if (!active)
                        continue;

                    var partPos = block.Pos + block.Right * ((block.Parts - 1) * 0.5f - i);
                    part.SetPositionAndRotation(partPos, Quaternion.LookRotation(block.Forward, Vector3.up));
                    part.localScale = new Vector3(1f, gp.blockHeight, gp.blockLength);

                    var partRenderer = renderers[i];
                    if (partRenderer != null)
                        partRenderer.SetPropertyBlock(_mpb);
                }

                // Physics -> entity mapping on the root: the link persists
                // on the pooled view (captured by BlockView at create time),
                // re-bound to the live entity on every acquire.
                blockView.Link.Init(_world, entity);

                ref var view = ref _viewPool.Add(entity);
                view.View = go;
                view.Transform = go.transform;

                ref var blockViewRef = ref _blockViewPool.Add(entity);
                blockViewRef.View = blockView;
            }
        }
    }
}
