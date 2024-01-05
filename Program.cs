using Microsoft.AspNetCore.Builder;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:6969");
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseWebSockets();

List<WebSocket> webSockets = new List<WebSocket>();

app.Map("/ws", async (context) =>
{

    if (context.WebSockets.IsWebSocketRequest)
    {
        Console.WriteLine(WebSocketState.None);

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        Console.WriteLine(ws.State);

        var msgConnection = "Connection Session User...";
        var bytesConnectionMsg = Encoding.UTF8.GetBytes($"{msgConnection}");
        ArraySegment<byte> arraySegmentbytesConnectionMsg = new ArraySegment<byte>(bytesConnectionMsg, 0, bytesConnectionMsg.Length);

        await ws.SendAsync(arraySegmentbytesConnectionMsg, WebSocketMessageType.Text, true, CancellationToken.None);

        byte[] buffer = new byte[1024 * 4];
        var receivedResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None);

        while (!receivedResult.CloseStatus.HasValue)
        {
            ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            await ws.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);

            Console.WriteLine(ws.State);

            receivedResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None);
        }

        Console.WriteLine(ws.State);

        var msgClosed = "Closed Session User...";
        var bytesCloseMsg = Encoding.UTF8.GetBytes($"{msgClosed}");
        ArraySegment<byte> arraySegmentClosed = new ArraySegment<byte>(bytesCloseMsg, 0, bytesCloseMsg.Length);

        await ws.SendAsync(arraySegmentClosed, WebSocketMessageType.Text, true, CancellationToken.None);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, ws.CloseStatusDescription, CancellationToken.None);

        Console.WriteLine(ws.State);

    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }


});

app.Map("/ws/room", async (context) =>
{

    if (context.WebSockets.IsWebSocketRequest)
    {

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        webSockets.Add(ws);

        byte[] buffer = new byte[1024 * 4];

        var receivedResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None);

        while(!receivedResult.CloseStatus.HasValue && ws.State == WebSocketState.Open)
        {
            ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            foreach(var socket in webSockets)
            {
                await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            Console.WriteLine(ws.State);

            receivedResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None);
        }

        webSockets.Remove(ws);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, ws.CloseStatusDescription, CancellationToken.None);

    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.Map("/ws/chat", async (context) =>
{

    if (context.WebSockets.IsWebSocketRequest)
    {
        Console.WriteLine(WebSocketState.None);

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        webSockets.Add(ws);

        await BroadcastMessage($"Joined the room ");
        await ReceiveMessage(ws, async (result, buffer) =>
        {
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await BroadcastMessage(message);
            }
            else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
            {
                webSockets.Remove(ws);
                await BroadcastMessage("left the room");
                await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
        });

    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }


});

async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        handleMessage(result, buffer);
    }
}

async Task BroadcastMessage(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    foreach (var socket in webSockets)
    {
        if (socket.State == WebSocketState.Open)
        {
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

}

await app.RunAsync();

