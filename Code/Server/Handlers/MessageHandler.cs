// TODO TV5: Điều phối HELLO/PING/CREATE_ROOM/JOIN_ROOM/MOVE/CHAT/LEAVE_ROOM.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;

namespace Caro.Server.Handlers
{
    public sealed class MessageHandler
    {
        private readonly Dictionary<string, Func<NetworkMessage, Task>>
            _handlers = new(StringComparer.OrdinalIgnoreCase);

        public MessageHandler()
        {
            RegisterDefaultMessages();
        }

        // Dang ky bo xu ly cho tung loai thong diep
        private void RegisterDefaultMessages()
        {
            Register(MessageTypes.Hello, HandleHelloAsync);
            Register(MessageTypes.Ping, HandlePingAsync);
            Register(MessageTypes.CreateRoom, HandleCreateRoomAsync);
            Register(MessageTypes.JoinRoom, HandleJoinRoomAsync);
            Register(MessageTypes.Move, HandleMoveAsync);
            Register(MessageTypes.Chat, HandleChatAsync);
            Register(MessageTypes.LeaveRoom, HandleLeaveRoomAsync);
        }

        public void Register(
            string type,
            Func<NetworkMessage, Task> handler)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException(
                    "Message type cannot be empty.", nameof(type));

            _handlers[type] = handler
                ?? throw new ArgumentNullException(nameof(handler));
        }

        // Diem vao chung cua bo dieu phoi
        public async Task HandleAsync(NetworkMessage? message)
        {
            if (!MessageValidator.IsValid(message, out string error))
            {
                Console.WriteLine($"Invalid message: {error}");
                return;
            }

            if (!_handlers.TryGetValue(message!.Type, out var handler))
            {
                Console.WriteLine(
                    $"Unsupported message type: {message.Type}");
                return;
            }

            try
            {
                await handler(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error handling {message.Type}: {ex.Message}");
                throw;
            }
        }

        // Cac phuong thuc nay se ket noi voi service cua Server
        private Task HandleHelloAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: HELLO");
            return Task.CompletedTask;
        }

        private Task HandlePingAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: PING");
            return Task.CompletedTask;
        }

        private Task HandleCreateRoomAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: CREATE_ROOM");
            return Task.CompletedTask;
        }

        private Task HandleJoinRoomAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: JOIN_ROOM");
            return Task.CompletedTask;
        }

        private Task HandleMoveAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: MOVE");
            return Task.CompletedTask;
        }

        private Task HandleChatAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: CHAT");
            return Task.CompletedTask;
        }

        private Task HandleLeaveRoomAsync(NetworkMessage message)
        {
            Console.WriteLine("Dispatch: LEAVE_ROOM");
            return Task.CompletedTask;
        }
    }
}
