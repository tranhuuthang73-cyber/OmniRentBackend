using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using OmniRentBackend.Data;
using OmniRentBackend.Models;

namespace OmniRentBackend.Hubs
{
    public class ChatHub : Hub
    {
        private readonly OmniRentDbContext _context;
        
        // Map userId -> connectionId
        public static readonly ConcurrentDictionary<string, string> ActiveConnections = new ConcurrentDictionary<string, string>();

        public ChatHub(OmniRentDbContext context)
        {
            _context = context;
        }

        public override Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var userId = httpContext?.Request.Query["userId"].ToString();

            if (!string.IsNullOrEmpty(userId))
            {
                ActiveConnections[userId] = Context.ConnectionId;
                Console.WriteLine($"User connected: {userId} with connection ID: {Context.ConnectionId}");
            }

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var item = ActiveConnections.FirstOrDefault(x => x.Value == connectionId);
            if (item.Key != null)
            {
                ActiveConnections.TryRemove(item.Key, out _);
                Console.WriteLine($"User disconnected: {item.Key}");
            }

            return base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string senderId, string receiverId, string content)
        {
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId) || string.IsNullOrWhiteSpace(content))
                return;

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var payload = new
            {
                id = message.Id,
                senderId = message.SenderId,
                receiverId = message.ReceiverId,
                content = message.Content,
                createdAt = message.CreatedAt
            };

            // Emit to receiver if online
            if (ActiveConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", payload);
            }

            // Emit back to sender
            await Clients.Caller.SendAsync("ReceiveMessage", payload);
        }

        public async Task Typing(string senderId, string receiverId, bool isTyping)
        {
            if (ActiveConnections.TryGetValue(receiverId, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("typingStatus", new
                {
                    senderId,
                    isTyping
                });
            }
        }
    }
}
