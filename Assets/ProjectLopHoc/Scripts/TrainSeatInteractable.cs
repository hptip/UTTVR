using Fusion;
using UnityEngine;

public class TrainSeatInteractable : NetworkBehaviour
{
    [Header("Mốc vị trí ngồi và Điều khiển tàu")]
    public Transform driverSeatPoint;
    public TrainSplineMover trainMover;

    [Header("Đồng bộ mạng qua Fusion")]
    [Networked] public PlayerRef CurrentDriver { get; set; } = PlayerRef.None;
    private bool _singlePlayerOccupied;
    private FirstPersonController _singlePlayerDriver;

    public bool IsOccupied => _singlePlayerOccupied ||
        (Object != null && Object.IsValid && CurrentDriver != PlayerRef.None);

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
        if (netObj == null)
            return;

        // Offline single-player scenes are often entered directly. Handle the
        // local seat transition without relying on a replicated RPC round trip.
        if (player.Runner != null && player.Runner.IsRunning &&
            player.Runner.GameMode == GameMode.Single && player.HasStateAuthority)
        {
            _singlePlayerOccupied = true;
            _singlePlayerDriver = player;
            ApplyOccupy(netObj);
            return;
        }

        if (Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning)
            return;

        PlayerRef playerRef = player.Object.InputAuthority.IsValid ? player.Object.InputAuthority : Runner.LocalPlayer;
        RPC_YeuCauNgoiGhe(playerRef, netObj);
    }

    public void RequestVacate(FirstPersonController player)
    {
        if (player == null) return;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null)
            return;

        if (_singlePlayerDriver == player && player.Runner != null &&
            player.Runner.GameMode == GameMode.Single && player.HasStateAuthority)
        {
            if (trainMover != null && !trainMover.IsStopped) return;
            _singlePlayerOccupied = false;
            _singlePlayerDriver = null;
            player.ThietLapRoiGhe();
            if (trainMover != null)
                trainMover.isDriverSeated = false;
            return;
        }

        if (Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning)
            return;

        PlayerRef playerRef = player.Object.InputAuthority.IsValid ? player.Object.InputAuthority : Runner.LocalPlayer;
        RPC_YeuCauRoiGhe(playerRef, netObj);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_YeuCauNgoiGhe(PlayerRef requestingPlayer, NetworkObject playerNetObj)
    {
        if (IsOccupied && CurrentDriver != requestingPlayer) return;

        CurrentDriver = requestingPlayer;
        ApplyOccupy(playerNetObj);
    }

    private void ApplyOccupy(NetworkObject playerNetObj)
    {
        if (trainMover != null)
            trainMover.isDriverSeated = true;

        if (playerNetObj != null)
        {
            FirstPersonController controller = playerNetObj.GetComponent<FirstPersonController>();
            if (controller != null)
            {
                Transform mount = driverSeatPoint != null ? driverSeatPoint : transform;
                controller.ThietLapVaoGhe(this, mount);

            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_YeuCauRoiGhe(PlayerRef requestingPlayer, NetworkObject playerNetObj)
    {
        if (trainMover != null && !trainMover.IsStopped) return;

        _singlePlayerOccupied = false;
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
