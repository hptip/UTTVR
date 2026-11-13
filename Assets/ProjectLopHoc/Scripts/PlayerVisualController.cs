using Fusion;
using UnityEngine;

namespace ProjectLopHoc.Features.Player
{
    public class PlayerVisualController : NetworkBehaviour
    {
        [Header("Danh sách Renderer của Model")]
        [SerializeField] private Renderer[] renderers;

        public override void Spawned()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            // Ở chế độ offline, người chơi thường chỉ có StateAuthority; trong mạng
            // dùng InputAuthority để nhận diện avatar cục bộ.
            bool isLocalPlayer = Object.HasInputAuthority ||
                                 (Runner != null && Runner.GameMode == GameMode.Single && Object.HasStateAuthority);
            SetModelVisibility(!isLocalPlayer);
        }

        public void SetModelVisibility(bool visible)
        {
            if (renderers == null) return;

            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }
    }
}
