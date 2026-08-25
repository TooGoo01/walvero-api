using Loyalty.Business.Dtos.Chatbot;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Chatbot beyni: LLM (Azure OpenAI) tool-calling loop-u. Provider-agnostik interfeys —
    /// gələcəkdə Claude/başqa provider-a keçid tək implementasiya faylı ilə mümkündür.
    /// </summary>
    public interface IPlaceAssistantService
    {
        Task<AssistantReply> AskAsync(ChatbotAskRequest request, CancellationToken ct = default);
    }
}
