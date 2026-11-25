using UnityEngine;

namespace AmazingTrack.Views
{
    /// <summary>
    /// Cached view wiring on the pooled block root: part transforms and
    /// renderers captured once per pooled instance, plus the persistent
    /// EntityLinkView re-bound to a live entity on every acquire. Removes all
    /// per-spawn component searches and additions from BlockViewSystem.
    /// Must stay in a file matching the class name (Unity component rule).
    /// </summary>
    public class BlockView : MonoBehaviour
    {
        /// <summary>Part children in prefab order (index = part index).</summary>
        [HideInInspector] public Transform[] Parts = System.Array.Empty<Transform>();

        /// <summary>Renderers of the part children, same indices as Parts.</summary>
        [HideInInspector] public MeshRenderer[] PartRenderers = System.Array.Empty<MeshRenderer>();

        /// <summary>Physics -> entity mapping on the root, re-bound on acquire.</summary>
        [HideInInspector] public EntityLinkView Link;

        /// <summary>Captures part children and the entity link (once per pooled instance).</summary>
        public void Capture()
        {
            int count = transform.childCount;
            Parts = new Transform[count];
            PartRenderers = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var child = transform.GetChild(i);
                Parts[i] = child;
                PartRenderers[i] = child.GetComponent<MeshRenderer>();
            }
            Link = GetComponent<EntityLinkView>();
        }
    }
}
