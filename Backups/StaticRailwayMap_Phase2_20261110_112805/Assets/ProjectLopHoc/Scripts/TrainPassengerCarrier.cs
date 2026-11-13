using UnityEngine;

public class TrainPassengerCarrier : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player != null)
        {
            // Gắn nhân vật làm con của thân tàu
            player.transform.SetParent(transform, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player != null)
        {
            // Trả nhân vật về root tự do nếu rời khỏi tàu
            player.transform.SetParent(null, true);
        }
    }
}