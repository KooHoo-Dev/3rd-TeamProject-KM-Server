using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace HelloServer;

public partial class Room
{
    private class Member
    {
        public User User;
        public MemberConnection Connection;

        public int MovesSinceLog;
        public DateTime LastLogAt;
    }

    private const int MAX_MEMBERS = 4;

    private readonly ConcurrentDictionary<string, Member> members = new();
    private readonly List<string> memberOrder = new(); // ID를 방에 들어온 순서대로 담아둠

    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly string code;
    private readonly int logMovesPerSecond;

    private readonly Dictionary<string, Func<Member, string, Task>> handlers = new();
    
    public bool IsEmpty => members.IsEmpty;

    public Room(string code, int logMovesPerSecond)
    {
        this.code = code;
        this.logMovesPerSecond = logMovesPerSecond;

        RegisterLobbyHandlers();
        RegisterGameHandlers();
    }
    
    #region LIFECYCLE

    public async Task HandleAsync(WebSocket socket, string id, CancellationToken token)
    {
        MemberConnection connection = new(socket);

        Member member = await JoinAsync(connection, id, token);
        if (member == null) return;

        try
        {
            await ReceiveLoopAsync(member, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await LeaveAsync(member);
        }
    }

    private async Task ReceiveLoopAsync(Member member, CancellationToken token)
    {
        while (true)
        {
            if (token.IsCancellationRequested) break;

            string text = await member.Connection.ReceiveTextAsync(token);
            if (string.IsNullOrWhiteSpace(text)) return;

            await HandleReceivedAsync(member, text);
        }
    }
    
    #endregion
    
    #region HANDLE_RECEIVED

    private Task HandleReceivedAsync(Member member, string json)
    {
        string type = JsonSerializer.Deserialize<MessageHeader>(json)?.Type;

        if (type == null) return Task.CompletedTask;
        if (handlers.TryGetValue(type, out Func<Member, string, Task> handler) == false)
            return Task.CompletedTask;
        
        return handler(member, json);
    }

    private void RegisterHandler<T>(string type, Func<Member, T, Task> handler)
    {
        handlers.Add(type, (member, json) =>
        {
            T message = JsonSerializer.Deserialize<T>(json);
            return handler(member, message);
        });
    }
    
    #endregion
    
    #region SEND_MESSAGE

    private Task SendAsync(Member member, object message)
    {
        string json = JsonSerializer.Serialize(message, message.GetType());
        return member.Connection.SendAsync(json);
    }

    private async Task BroadcastAsync(object message, string exceptId = null)
    {
        string json = JsonSerializer.Serialize(message, message.GetType());

        List<Task> sending = new();

        foreach (Member member in members.Values)
        {
            if (member.User.Id == exceptId) continue;
            sending.Add(member.Connection.SendAsync(json));
        }
        
        await Task.WhenAll(sending);
    }
    
    #endregion
}