
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using BIM4D.AI.Generative;

public class AIChatbotIntegration : MonoBehaviour
{
    [Header("XkiroBrain")]
    public MonoBehaviour brain;

    public string[] brainTypeNames =
    {
        "XkiroBrain",
        "MockBrain"
    };

    [Header("Send Settings")]
    public string sendMethodName = "AskAsync";

    public float timeoutSeconds = 100f;

    [Header("Debug")]
    public bool debugLog = true;

    private Action<string> _onReply;
    private Action<string> _onError;

    private bool _waiting;

    private float _deadline;

    private MethodInfo _sendMethod;

    public bool IsBusy => _waiting;

    private void Awake()
    {
        ResolveBrain();
    }

    private void OnDestroy()
    {
        UnhookEvents();
    }

    private void ResolveBrain()
    {
        if (brain != null) return;

#if UNITY_2022_2_OR_NEWER
        var all = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        var all = FindObjectsOfType<MonoBehaviour>(true);
#endif

        foreach (var typeName in brainTypeNames)
        {
            foreach (var mb in all)
            {
                if (mb == null) continue;

                if (mb.GetType().Name != typeName)
                    continue;

                brain = mb;

                if (debugLog)
                {
                    Debug.Log(
                        "[Chatbot] Found Brain: " +
                        mb.GetType().Name +
                        " on " +
                        mb.gameObject.name
                    );
                }

                return;
            }
        }

        if (debugLog)
        {
            Debug.LogWarning(
                "[Chatbot] XkiroBrain not found. Assign Brain in Inspector."
            );
        }
    }

    public void Ask(
        string text,
        Action<string> onReply,
        Action<string> onError)
    {
        if (_waiting)
        {
            onError?.Invoke(
                "AI đang bận, vui lòng đợi."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            onError?.Invoke(
                "Nội dung tin nhắn không được để trống."
            );

            return;
        }

        if (brain == null)
        {
            ResolveBrain();
        }

        if (brain == null)
        {
            onError?.Invoke(
                "Không tìm thấy XkiroBrain. Hãy kéo Chat Bot vào ô Brain."
            );

            return;
        }

        _onReply = onReply;
        _onError = onError;

        _waiting = true;

        _deadline =
            Time.realtimeSinceStartup + timeoutSeconds;

        _sendMethod = FindSendMethod();

        if (_sendMethod == null)
        {
            Fail(
                "Không tìm thấy hàm AskAsync trong XkiroBrain."
            );

            return;
        }

        if (debugLog)
        {
            Debug.Log(
                "[Chatbot] Sending message through " +
                _sendMethod.Name
            );
        }

        StartCoroutine(
            Run(_sendMethod, text.Trim())
        );
    }

    private IEnumerator Run(
        MethodInfo method,
        string text)
    {
        object result = null;

        try
        {
            result = method.Invoke(
                brain,
                new object[]
                {
                    text,
                    default(System.Threading.CancellationToken)
                }
            );
        }
        catch (Exception exception)
        {
            Fail(
                (exception.InnerException ?? exception).Message
            );

            yield break;
        }

        if (result is Task<string> task)
        {
            while (!task.IsCompleted)
            {
                if (TimedOut())
                    yield break;

                yield return null;
            }

            if (task.IsCanceled)
            {
                Fail("Yêu cầu AI đã bị hủy.");
                yield break;
            }

            if (task.IsFaulted)
            {
                Fail(
                    task.Exception.GetBaseException().Message
                );

                yield break;
            }

            string response = task.Result;

            if (!string.IsNullOrWhiteSpace(response))
            {
                OnBrainReply(response);
            }
            else
            {
                Fail("AI trả về nội dung rỗng.");
            }

            yield break;
        }

        if (result is Task genericTask)
        {
            while (!genericTask.IsCompleted)
            {
                if (TimedOut())
                    yield break;

                yield return null;
            }

            if (genericTask.IsFaulted)
            {
                Fail(
                    genericTask.Exception.GetBaseException().Message
                );

                yield break;
            }

            var resultProperty =
                genericTask.GetType().GetProperty("Result");

            if (resultProperty != null)
            {
                string response =
                    resultProperty.GetValue(genericTask) as string;

                if (!string.IsNullOrWhiteSpace(response))
                {
                    OnBrainReply(response);
                }
                else
                {
                    Fail("AI trả về nội dung rỗng.");
                }
            }

            yield break;
        }

        if (result is string directResponse)
        {
            OnBrainReply(directResponse);
            yield break;
        }

        if (result is IEnumerator enumerator)
        {
            yield return StartCoroutine(enumerator);
        }

        if (_waiting)
        {
            Fail(
                "Hàm gửi tin không trả về kết quả trực tiếp."
            );
        }
    }

    private bool TimedOut()
    {
        if (!_waiting)
            return true;

        if (Time.realtimeSinceStartup <= _deadline)
            return false;

        Fail(
            "AI không phản hồi sau " +
            timeoutSeconds +
            " giây. Kiểm tra mạng hoặc API."
        );

        return true;
    }

    private void OnBrainReply(string reply)
    {
        if (!_waiting)
            return;

        _waiting = false;

        if (debugLog)
        {
            Debug.Log(
                "[Chatbot] AI Response: " + reply
            );
        }

        _onReply?.Invoke(reply);

        ClearCallbacks();
    }

    private void Fail(string message)
    {
        if (!_waiting)
            return;

        _waiting = false;

        if (debugLog)
        {
            Debug.LogWarning(
                "[Chatbot] " + message
            );
        }

        _onError?.Invoke(message);

        ClearCallbacks();
    }

    private void ClearCallbacks()
    {
        _onReply = null;
        _onError = null;
    }

    private MethodInfo FindSendMethod()
    {
        if (brain == null)
            return null;

        Type type = brain.GetType();

        var flags =
            BindingFlags.Public |
            BindingFlags.Instance;

        if (!string.IsNullOrWhiteSpace(sendMethodName))
        {
            var specified = type.GetMethod(
                sendMethodName,
                flags,
                null,
                new Type[]
                {
                    typeof(string),
                    typeof(System.Threading.CancellationToken)
                },
                null
            );

            if (specified != null)
                return specified;
        }

        var askAsync = type.GetMethod(
            "AskAsync",
            flags,
            null,
            new Type[]
            {
                typeof(string),
                typeof(System.Threading.CancellationToken)
            },
            null
        );

        if (askAsync != null)
            return askAsync;

        var ask = type.GetMethod(
            "Ask",
            flags,
            null,
            new Type[]
            {
                typeof(string)
            },
            null
        );

        if (ask != null)
            return ask;

        return null;
    }

    private void UnhookEvents()
    {
        _waiting = false;
        ClearCallbacks();
    }
}
