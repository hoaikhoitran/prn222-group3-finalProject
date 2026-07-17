using System.Net.Http.Json;
using AcademicDocumentRagSystem.Services.DTOs.Rag;

namespace AcademicDocumentRagSystem.Services.RagIntegration
{
    public class RagApiClient : IRagClient
    {
        private static readonly TimeSpan IndexTimeout = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(180);

        private readonly HttpClient _httpClient;

        public RagApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RagIndexResponse> IndexDocumentAsync(RagIndexRequest request)
        {
            using var timeout = new CancellationTokenSource(IndexTimeout);

            var response = await _httpClient.PostAsJsonAsync(
                "/rag/index-document",
                request,
                timeout.Token);

            var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"RAG index failed. Status: {(int)response.StatusCode}. Body: {responseBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<RagIndexResponse>(
                cancellationToken: timeout.Token);

            if (result == null)
            {
                throw new Exception("RAG service returned empty response.");
            }

            return result;
        }
        public async Task<RagAskResponse> AskAsync(RagAskRequest request)
        {
            using var timeout = new CancellationTokenSource(AskTimeout);

            var response = await _httpClient.PostAsJsonAsync(
                "/rag/ask",
                request,
                timeout.Token);

            var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"RAG ask failed. Status: {(int)response.StatusCode}. Body: {responseBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<RagAskResponse>(
                cancellationToken: timeout.Token);

            if (result == null)
            {
                throw new Exception("RAG service returned empty response.");
            }

            return result;
        }
    }
}
