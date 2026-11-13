using Fusion;
using UnityEngine;

public class TrainSeatInteractable : NetworkBehaviour
{
    [Header("Mốc vị trí ngồi và Điều khiển tàu")]
    public Transform driverSeatPoint;
    public TrainSplineMover trainMover;

    [Header("Đồng bộ mạng qua Fusion")]
    [Networked] public PlayerRef CurrentDriver { get; set; } = PlayerRef.None;

    public bool IsOccupied => CurrentDriver != PlayerRef.None;

    public override void Spawned()
    {
        if (trainMover != null)
        {
            trainMover.enabled = true;
            trainMover.isDriverSeated = false;
        }
    }

    public void RequestOccupy(FirstPersonController player)
    {
        if (player == null || IsOccupied) return;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            PlayerRef playerRef = player.Object.InputAuthority.IsValid ? player.Object.InputAuthority : Runner.LocalPlayer;
            RPC_YeuCauNgoiGhe(playerRef, netObj);
        }
    }

    public void RequestVacate(FirstPersonController player)
    {
        if (player == null) return;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            PlayerRef playerRef = player.Object.InputAuthority.IsValid ? player.Object.InputAuthority : Runner.LocalPlayer;
            RPC_YeuCauRoiGhe(playerRef, netObj);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_YeuCauNgoiGhe(PlayerRef requestingPlayer, NetworkObject playerNetObj)
    {
        if (IsOccupied && CurrentDriver != requestingPlayer) return;

        CurrentDriver = requestingPlayer;

        if (playerNetObj != null)
        {
            FirstPersonController controller = playerNetObj.GetComponent<FirstPersonController>();
            if (controller != null)
            {
                Transform mount = driverSeatPoint != null ? driverSeatPoint : transform;
                controller.ThietLapVaoGhe(this, mount);

                // Cấp quyền nhận phím W/S cho người ngồi lái
                if (trainMover != null)
                {
                    trainMover.isDriverSeated = true;
                }
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_YeuCauRoiGhe(PlayerRef requestingPlayer, NetworkObject playerNetObj)
    {
        if (trainMover != null && !trainMover.IsStopped) return;

        CurrentDriver = PlayerRef.None;

        if (playerNetObj != null)
        {
            FirstPersonController controller = playerNetObj.GetComponent<FirstPersonController>();
            if (controller != null)
            {
                controller.ThietLapRoiGhe();

                // Ngắt nhận phím lái, tuyệt đối không tắt trainMover để tàu giữ tốc độ chạy tiếp
                if (trainMover != null)
                {
                    trainMover.isDriverSeated = false;
                }
            }
        }
    }
}
