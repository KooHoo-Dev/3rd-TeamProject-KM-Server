using System.Net.WebSockets;
using System.Text;

namespace HelloServer;

public class MemberConnection
{
    private readonly WebSocket socket;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    
    public MemberConnection(WebSocket socket)
    {
        this.socket = socket;
    }

    public async Task<string> ReceiveTextAsync(CancellationToken token)
    {
        StringBuilder stringBuilder = new();
        byte[] buffer = new byte[4096];

        while (true)
        {
            WebSocketReceiveResult result = await socket
                .ReceiveAsync(new ArraySegment<byte>(buffer), token);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            
            stringBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            
            if (result.EndOfMessage) 
                return stringBuilder.ToString();
        }
    }

    public async Task SendAsync(string text)
    {
        if (socket.State != WebSocketState.Open) return;
        
        await sendLock.WaitAsync();

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            
            await socket.SendAsync(
                new ArraySegment<byte>(bytes), 
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None);
        }
        catch (WebSocketException) { }
        finally
        {
            sendLock.Release();
        }
    }
    
    public Task CloseAsync(WebSocketCloseStatus status, string desc, CancellationToken token)
    {
        return socket.CloseOutputAsync(status, desc, token);
    }
}