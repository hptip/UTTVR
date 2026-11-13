using UnityEngine;

public class TrainInteractable : InteractableObject
{
    [Header("Dữ liệu thông số tàu")]
    public string trainName = "Đoàn Tàu Đô Thị Tuyến 01";
    
    [TextArea(5, 12)]
    public string trainSpecifications = 
        "Vận tốc thiết kế: 80 km/h\n" +
        "Số toa xe: 4 toa tiêu chuẩn\n" +
        "Chiều dài toàn đoàn: 79 m\n" +
        "Sức chứa tối đa: 960 hành khách\n" +
        "Nguồn điện cấp: 750V DC Ray thứ ba";

    public override void OnInteract()
    {
        // Để trống targetSceneName để không bị đổi Scene
        targetSceneName = ""; 

        if (TrainInfoPopupUI.Instance != null)
        {
            TrainInfoPopupUI.Instance.OpenPopup(trainName, trainSpecifications);
        }
    }
}