using System.Collections.Generic;

namespace Loyalty.Business.Dtos.Chatbot
{
    /// <summary>
    /// Super admin AI simulyatoru: sualı REAL pipeline-dan keçirir və hər mərhələnin
    /// trace-ini qaytarır (prompt, tool-lar, model raw cavabları, filter nəticələri).
    /// </summary>
    public class ChatbotSimulateRequest : ChatbotAskRequest
    {
        /// <summary>true → anonim (login olmamış) istifadəçi yolu simulyasiya olunur (account tool-ları bağlı).</summary>
        public bool ForceAnonymous { get; set; } = false;
    }

    public class ChatbotSimulationResult
    {
        public AssistantReply Reply { get; set; } = new();
        public SimulationTrace Trace { get; set; } = new();
    }

    /// <summary>
    /// Pipeline-ın tam trace-i. Raw payload-lar JSON string kimi saxlanır —
    /// UI JSON.parse edib pretty göstərir, serializer-dən asılı deyil.
    /// </summary>
    public class SimulationTrace
    {
        public SimulationConfig? Config { get; set; }          // null → konfiqurasiya yoxdur
        public bool IsAuth { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string ToolSchemasJson { get; set; } = string.Empty;
        public List<SimulationIteration> Iterations { get; set; } = new();
        public string? FinalReply { get; set; }
        public List<PlaceResultDto> Places { get; set; } = new();
        public long TotalDurationMs { get; set; }
        public string? Error { get; set; }                     // "not_configured" | "http_error" | exception mesajı
    }

    public class SimulationConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Deployment { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string ReasoningEffort { get; set; } = string.Empty;
        public string ApiKeyMasked { get; set; } = string.Empty;
    }

    public class SimulationIteration
    {
        public int Index { get; set; }
        public int HttpStatus { get; set; }
        public long DurationMs { get; set; }
        /// <summary>Bu iterasiyada modelə GÖNDƏRİLƏN yeni mesajlar (iterasiya 0 = tam ilkin massiv).</summary>
        public string RequestMessagesDeltaJson { get; set; } = string.Empty;
        /// <summary>choices[0].message — olduğu kimi (content və/və ya tool_calls).</summary>
        public string RawModelMessageJson { get; set; } = string.Empty;
        public string? Content { get; set; }
        public SimulationUsage? Usage { get; set; }
        public List<SimulationToolCall> ToolCalls { get; set; } = new();
        /// <summary>Non-2xx cavabda response body.</summary>
        public string? ErrorBody { get; set; }
    }

    public class SimulationToolCall
    {
        public string Name { get; set; } = string.Empty;
        public string ArgumentsJson { get; set; } = string.Empty;
        public string ResultJson { get; set; } = string.Empty;
        public long DurationMs { get; set; }
    }

    public class SimulationUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
