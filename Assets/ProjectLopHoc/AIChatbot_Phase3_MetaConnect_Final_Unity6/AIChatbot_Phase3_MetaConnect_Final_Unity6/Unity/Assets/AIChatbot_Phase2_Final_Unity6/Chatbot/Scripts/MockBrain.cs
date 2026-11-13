using System;
using System.Collections;
using UnityEngine;

/// <summary>Brain giả để chạy thử Demo khi chưa có XkiroBrain. Đừng dùng trong game thật.</summary>
public class MockBrain : MonoBehaviour
{
    public event Action<string> OnResponse;
    public float delay = 1.2f;

    public void Ask(string question)
    {
        StartCoroutine(Reply(question));
    }

    IEnumerator Reply(string q)
    {
        yield return new WaitForSeconds(delay);
        OnResponse?.Invoke("(MockBrain) Bạn vừa hỏi: \"" + q + "\". Khi nối XkiroBrain thật, câu trả lời của AI sẽ hiện ở đây.");
    }
}
