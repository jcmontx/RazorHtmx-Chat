
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RazorHtmx_Chat.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    // A thread-safe collection to store connected clients
    private static ConcurrentDictionary<string, SseClient> clients = new ConcurrentDictionary<string, SseClient>();

    [BindProperty]
    public ChatMessageModel ChatMessage { get; set; }

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }

    // SSE endpoint method
    public async Task OnGetSse()
    {
        var clientId = Guid.NewGuid().ToString();

        // Set headers for SSE
        Response.Headers.TryAdd("Cache-Control", "no-cache");
        Response.Headers.TryAdd("Content-Type", "text/event-stream");
        Response.Headers.TryAdd("Connection", "keep-alive");

        // Create a new client
        var client = new SseClient
        {
            ClientId = clientId,
            Response = Response
        };

        // Add the client to the collection
        clients.TryAdd(clientId, client);

        try
        {
            // Keep the connection open indefinitely
            await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted);
        }
        finally
        {
            // Remove the client when the connection is closed
            clients.TryRemove(clientId, out _);
        }
    }

    // Method to broadcast messages to all connected clients
    public static async Task BroadcastMessageAsync(string message)
    {
        var data = $"event: newMessage\n";
        data += $"data: {message}\n\n";
        var bytes = Encoding.UTF8.GetBytes(data);

        foreach (var client in clients.Values)
        {
            try
            {
                await client.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                await client.Response.Body.FlushAsync();
            }
            catch
            {
                // Handle exceptions (e.g., client disconnected)
            }
        }
    }
    public async Task<IActionResult> OnPostSendMessage()
    {
        Console.WriteLine($"User: {ChatMessage?.User}, Message: {ChatMessage?.Message}");
        if (ChatMessage == null)
            return BadRequest(ModelState);

        // Add message to the database
        var message = new ChatMessage
        {
            User = ChatMessage.User,
            Message = ChatMessage.Message,
            Timestamp = DateTime.UtcNow
        };

        // Broadcast the new message
        var messageHtml = RenderMessageHtml(message);
        await BroadcastMessageAsync(messageHtml);

        return new OkResult();
    }

    // Method to render the message as HTML
    private string RenderMessageHtml(ChatMessage message)
    {
        return $"<div class=\"message\"><strong>{message.User}:</strong> {message.Message}</div>";
    }
}

// Helper class to represent an SSE client
public class SseClient
{
    public string ClientId { get; set; }
    public HttpResponse Response { get; set; }
}
public class ChatMessageModel
{
    public string User { get; set; }
    public string Message { get; set; }
}

public class ChatMessage
{
    public int Id { get; set; }
    public string User { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
