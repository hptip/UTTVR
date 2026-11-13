
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace BIM4D.AI.Generative
{
    [DisallowMultipleComponent]
    public sealed class XkiroBrain : MonoBehaviour
    {
        [Header("Xkiro API")]
        private string apiKey = "";
        [SerializeField] private string baseUrl = "https://api.xkiro.com/v1";
        [SerializeField] private string model = "qwen/qwen3.8-max:free";

        [Header("System Instructions")]
        [SerializeField, TextArea(3, 8)]
        private string systemPrompt =
            "You are a helpful BIM assistant. Reply clearly and concisely in the user's language.";

        [SerializeField]
        private TextAsset assistantInstructions;

        [SerializeField]
        private TextAsset modelContext;

        [Header("Request Settings")]
        [SerializeField, Min(1)]
        private int timeoutSeconds = 90;

        [SerializeField, Range(1, 50)]
        private int historyTurns = 10;


        [Header("Events")]
        [SerializeField]
        private UnityEvent<string> onResponse = new UnityEvent<string>();

        [SerializeField]
        private UnityEvent<string> onError = new UnityEvent<string>();

        private readonly List<Message> history = new List<Message>();

        public UnityEvent<string> OnResponse => onResponse;
        public UnityEvent<string> OnError => onError;

        public bool IsThinking { get; private set; }

        public string LastResponse { get; private set; } = "";

        private void OnEnable()
        {
            // Voice transcripts are intentionally NOT auto-submitted.
            // The user reviews the transcript and presses Send in the chatbot UI.
        }

        public async void Ask(string text)
        {
            try
            {
                await AskAsync(text);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (this == null) return;

                Debug.LogException(
                    new InvalidOperationException(
                        "Xkiro brain request failed: " + exception.Message,
                        exception
                    ),
                    this
                );

                onError.Invoke(exception.Message);
            }
        }

        public async Task<string> AskAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (IsThinking)
            {
                throw new InvalidOperationException(
                    "The brain is already processing a request."
                );
            }

            apiKey = BIMAISecrets.XkiroApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Thiếu Xkiro API Key. Mở Tools > BIM AI > Create or Open Local API Config và cấu hình xkiroApiKey."
                );
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "The prompt cannot be empty.",
                    nameof(text)
                );
            }

            if (!Uri.TryCreate(
                baseUrl,
                UriKind.Absolute,
                out var endpoint
            ) || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "The brain base URL must be an absolute HTTPS URL."
                );
            }

            using var lifetime =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    destroyCancellationToken
                );

            var token = lifetime.Token;

            token.ThrowIfCancellationRequested();

            IsThinking = true;

            try
            {
                var messages = new List<Message>();

                string instructions = BuildSystemPrompt();

                if (!string.IsNullOrWhiteSpace(instructions))
                {
                    messages.Add(
                        new Message("system", instructions)
                    );
                }

                int start = Math.Max(
                    0,
                    history.Count - historyTurns * 2
                );

                for (int i = start; i < history.Count; i++)
                {
                    messages.Add(history[i]);
                }

                var userMessage = new Message(
                    "user",
                    text.Trim()
                );

                messages.Add(userMessage);

                var payload = new ChatRequest
                {
                    model = model,
                    messages = messages.ToArray()
                };

                using var request = new UnityWebRequest(
                    baseUrl.TrimEnd('/') + "/chat/completions",
                    "POST"
                );

                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(
                        JsonUtility.ToJson(payload)
                    )
                );

                request.downloadHandler =
                    new DownloadHandlerBuffer();

                request.SetRequestHeader(
                    "Content-Type",
                    "application/json"
                );

                request.SetRequestHeader(
                    "Authorization",
                    "Bearer " + apiKey.Trim()
                );

                request.timeout = timeoutSeconds;

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                token.ThrowIfCancellationRequested();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(
                        $"Xkiro chat failed (HTTP {request.responseCode}): {request.error}\n{request.downloadHandler.text}"
                    );
                }

                var response = JsonUtility.FromJson<ChatResponse>(
                    request.downloadHandler.text
                );

                if (response?.choices == null ||
                    response.choices.Length == 0 ||
                    string.IsNullOrWhiteSpace(
                        response.choices[0]?.message?.content
                    ))
                {
                    throw new InvalidOperationException(
                        "Xkiro returned no text response."
                    );
                }

                LastResponse =
                    response.choices[0].message.content.Trim();

                history.Add(userMessage);

                history.Add(
                    new Message("assistant", LastResponse)
                );

                int excess = history.Count - historyTurns * 2;

                if (excess > 0)
                {
                    history.RemoveRange(0, excess);
                }

                onResponse.Invoke(LastResponse);

                return LastResponse;
            }
            finally
            {
                IsThinking = false;
            }
        }

        private string BuildSystemPrompt()
        {
            var prompt = new StringBuilder(systemPrompt);

            if (assistantInstructions != null &&
                !string.IsNullOrWhiteSpace(assistantInstructions.text))
            {
                prompt.Append("\n\n");
                prompt.Append(assistantInstructions.text);
            }

            if (modelContext != null &&
                !string.IsNullOrWhiteSpace(modelContext.text))
            {
                prompt.Append("\n\n");
                prompt.Append(modelContext.text);
            }

            return prompt.ToString();
        }

        [ContextMenu("Clear Conversation")]
        public void ClearConversation()
        {
            if (IsThinking)
            {
                throw new InvalidOperationException(
                    "Wait for the current reply before clearing the conversation."
                );
            }

            history.Clear();
            LastResponse = "";
        }

        [Serializable]
        private sealed class Message
        {
            public string role;
            public string content;

            public Message(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        [Serializable]
        private sealed class ChatRequest
        {
            public string model;
            public Message[] messages;
        }

        [Serializable]
        private sealed class ChatResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        private sealed class Choice
        {
            public Message message;
        }
    }
}
