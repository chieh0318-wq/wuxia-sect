using UnityEngine;

namespace Wuxia
{
    public sealed class M2RuntimeAssets : ScriptableObject
    {
        public Texture2D map;
        public AudioClip[] music, sfx;
        private static M2RuntimeAssets loaded;
        public static M2RuntimeAssets Load() => loaded != null ? loaded : loaded = Resources.Load<M2RuntimeAssets>("M2RuntimeAssets");
    }
}
