using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess;

[CancelAfter(20000)]
public class UnixSocketTawkControlTests
{
    private FakeTawkServer _server = null!;

    [SetUp]
    public void SetUp() => _server = new FakeTawkServer(FakeTawkServer.TempSocketPath());

    [TearDown]
    public async Task TearDown() => await _server.DisposeAsync();

    [Test]
    public async Task Says_hello_first_and_reports_what_tawk_said()
    {
        _server.Start();
        await using var stack = new LiveStack(_server.SocketPath);
        await stack.StartAsync();

        var hello = await stack.Control.ConnectAsync(CancellationToken.None);
        var first = _server.Received.First();

        Assert.Multiple(() =>
        {
            Assert.That(hello.Tawk, Is.EqualTo("0.6.4"));
            Assert.That((string?)first["op"], Is.EqualTo("hello"));
            Assert.That((string?)first["args"]!["origin"], Is.EqualTo("mcp"));
            Assert.That((int?)first["args"]!["protocol"], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Matches_answers_that_arrive_out_of_order()
    {
        _server.Hold("unread_summary").Hold("list_chats").Start();
        await using var stack = new LiveStack(_server.SocketPath);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var first = stack.Control.RequestAsync("unread_summary", null, CancellationToken.None);
        var second = stack.Control.RequestAsync("list_chats", null, CancellationToken.None);
        var firstId = (string)(await _server.WaitForAsync("unread_summary"))["id"]!;
        var secondId = (string)(await _server.WaitForAsync("list_chats"))["id"]!;
        await _server.SendAsync($$$"""{"id":"{{{secondId}}}","ok":true,"result":{"chats":[]}}""");
        await _server.SendAsync($$$"""{"id":"{{{firstId}}}","ok":true,"result":{"total":7,"mentions":0,"chats":[]}}""");

        Assert.Multiple(async () =>
        {
            Assert.That((await second).TryGetProperty("chats", out _), Is.True);
            Assert.That((await first).GetProperty("total").GetInt32(), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Delivers_notifications_to_every_reader()
    {
        _server.Start();
        await using var stack = new LiveStack(_server.SocketPath);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();
        using var cts = new CancellationTokenSource(5000);
        var reader = FirstMessageAsync(stack.Control, cts.Token);
        await Task.Delay(100);

        await _server.SendAsync(Samples.MessageEvent("hello there"));

        var message = await reader;
        Assert.That(message.Message.Text, Is.EqualTo("hello there"));
    }

    [Test]
    public async Task An_admin_instance_gets_a_waiting_write_back_and_approves_it_itself()
    {
        _server.Hold("send_message").Hold("approve").Start();
        await using var stack = new LiveStack(_server.SocketPath, parkWaitingWrites: true);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var send = stack.Control.RequestAsync("send_message", new JsonObject { ["chat"] = "Mom", ["text"] = "hi" }, null, CancellationToken.None);
        var id = (string)(await _server.WaitForAsync("send_message"))["id"]!;
        await _server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        var parked = Assert.ThrowsAsync<ApprovalWaitingException>(async () => await send);
        var waiting = stack.Control.TakeWaiting();

        var approve = stack.Control.ApproveAsync(id, "secret", CancellationToken.None);
        var asked = await _server.WaitForAsync("approve");
        await _server.SendAsync($$$"""{"id":"{{{id}}}","ok":true,"result":{"id":"3EB0D41C22"}}""");
        await _server.SendAsync($$$"""{"id":"{{{(string)asked["id"]!}}}","ok":true,"result":{"approved":true}}""");
        var result = await approve;

        Assert.Multiple(() =>
        {
            Assert.That(parked!.RequestId, Is.EqualTo(id));
            Assert.That(parked.Op, Is.EqualTo("send_message"));
            Assert.That(waiting, Has.Count.EqualTo(1));
            Assert.That(waiting[0].Outcome, Is.Null);
            Assert.That((string?)asked["args"]!["id"], Is.EqualTo(id));
            Assert.That((string?)asked["args"]!["admin_token"], Is.EqualTo("secret"));
            Assert.That(result.GetProperty("id").GetString(), Is.EqualTo("3EB0D41C22"));
            Assert.That(stack.Control.TakeWaiting(), Is.Empty);
        });
    }

    [Test]
    public async Task A_refused_approval_leaves_the_request_waiting_and_an_answer_in_tawk_is_reported_once()
    {
        _server.Hold("send_message").Hold("approve").Start();
        await using var stack = new LiveStack(_server.SocketPath, parkWaitingWrites: true);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var send = stack.Control.RequestAsync("send_message", new JsonObject { ["chat"] = "Mom", ["text"] = "hi" }, null, CancellationToken.None);
        var id = (string)(await _server.WaitForAsync("send_message"))["id"]!;
        await _server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        Assert.ThrowsAsync<ApprovalWaitingException>(async () => await send);

        var approve = stack.Control.ApproveAsync(id, "wrong", CancellationToken.None);
        var asked = await _server.WaitForAsync("approve");
        await _server.SendAsync($$$"""{"id":"{{{(string)asked["id"]!}}}","ok":false,"error":{"code":"bad_token","message":"The admin token is wrong or out of date"}}""");
        var refused = Assert.ThrowsAsync<TawkControlException>(async () => await approve);
        var stillWaiting = stack.Control.TakeWaiting();

        await _server.SendAsync($$$"""{"id":"{{{id}}}","ok":false,"error":{"code":"declined","message":"Declined in tawk"}}""");
        await Task.Delay(200);
        var answered = stack.Control.TakeWaiting();

        Assert.Multiple(() =>
        {
            Assert.That(refused!.Code, Is.EqualTo(ControlErrorCode.BadToken));
            Assert.That(stillWaiting, Has.Count.EqualTo(1));
            Assert.That(answered, Has.Count.EqualTo(1));
            Assert.That(answered[0].Outcome, Is.EqualTo("declined"));
            Assert.That(stack.Control.TakeWaiting(), Is.Empty);
        });
    }

    [Test]
    public async Task A_write_answered_at_once_is_not_parked()
    {
        _server.Answer("react", """{"ok":true}""").Start();
        await using var stack = new LiveStack(_server.SocketPath, parkWaitingWrites: true);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var result = await stack.Control.RequestAsync("react", new JsonObject { ["message_id"] = "M1", ["emoji"] = "+" }, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("ok").GetBoolean(), Is.True);
            Assert.That(stack.Control.TakeWaiting(), Is.Empty);
        });
    }

    [Test]
    public async Task Reports_the_approval_wait_and_then_takes_a_late_answer()
    {
        _server.Hold("send_message").Start();
        await using var stack = new LiveStack(_server.SocketPath, requestTimeout: TimeSpan.FromMilliseconds(200));
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();
        var waited = 0;

        var send = stack.Control.RequestAsync("send_message", new JsonObject { ["chat"] = "Mom", ["text"] = "hi" }, () => waited++, CancellationToken.None);
        var id = (string)(await _server.WaitForAsync("send_message"))["id"]!;
        await _server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        await _server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        await Task.Delay(500); // longer than the read timeout: writes are exempt
        await _server.SendAsync($$$"""{"id":"{{{id}}}","ok":true,"result":{"id":"3EB0D41C22"}}""");

        var result = await send;
        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("id").GetString(), Is.EqualTo("3EB0D41C22"));
            Assert.That(waited, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Fails_fast_with_a_clear_error_when_tawk_is_not_running()
    {
        await using var stack = new LiveStack(_server.SocketPath, connectWait: TimeSpan.FromMilliseconds(300));
        await stack.StartAsync();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var error = Assert.ThrowsAsync<TawkControlException>(() => stack.Control.RequestAsync("list_chats", null, CancellationToken.None))!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(ControlErrorCode.NotRunning));
            Assert.That(error.Message, Does.StartWith("tawk is not running, or its control socket is off (Settings > Automation > Control socket)."));
            Assert.That(error.Message, Does.Contain(_server.SocketPath));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        });
    }

    [Test]
    public async Task Starting_tawk_later_works_without_restarting_tawk_mcp()
    {
        await using var stack = new LiveStack(_server.SocketPath, connectWait: TimeSpan.FromMilliseconds(200));
        await stack.StartAsync();
        Assert.ThrowsAsync<TawkControlException>(() => stack.Control.RequestAsync("list_chats", null, CancellationToken.None));

        _server.Answer("list_chats", """{"chats":[]}""").Start();
        await stack.WaitUntilConnectedAsync();
        var result = await stack.Control.RequestAsync("list_chats", null, CancellationToken.None);

        Assert.That(result.TryGetProperty("chats", out _), Is.True);
        Assert.That(stack.Logger.Lines, Has.Some.StartsWith("Connected to tawk 0.6.4"));
    }

    [Test]
    public async Task A_request_in_flight_when_tawk_dies_fails_with_offline()
    {
        _server.Hold("read_messages").Start();
        await using var stack = new LiveStack(_server.SocketPath);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var read = stack.Control.RequestAsync("read_messages", new JsonObject { ["chat"] = "Mom" }, CancellationToken.None);
        await _server.WaitForAsync("read_messages");
        await _server.StopAsync();

        var error = Assert.ThrowsAsync<TawkControlException>(async () => await read)!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(ControlErrorCode.Offline));
            Assert.That(error.Message, Does.Contain("tawk quit"));
        });
    }

    [Test]
    public async Task A_write_waiting_for_approval_when_tawk_dies_may_or_may_not_have_gone()
    {
        _server.Hold("send_message").Start();
        await using var stack = new LiveStack(_server.SocketPath);
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var send = stack.Control.RequestAsync("send_message", new JsonObject { ["chat"] = "Mom", ["text"] = "hi" }, null, CancellationToken.None);
        var id = (string)(await _server.WaitForAsync("send_message"))["id"]!;
        await _server.SendAsync($$$"""{"evt":"approval","id":"{{{id}}}","state":"waiting"}""");
        await Task.Delay(100);
        await _server.StopAsync();

        var error = Assert.ThrowsAsync<TawkControlException>(async () => await send)!;
        Assert.That(error.Message, Does.Contain("may or may not have gone"));
    }

    [Test]
    public async Task A_read_with_no_answer_times_out_counts_against_the_breaker_and_reconnects()
    {
        _server.Hold("list_chats").Start();
        await using var stack = new LiveStack(_server.SocketPath, requestTimeout: TimeSpan.FromMilliseconds(300));
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        var error = Assert.ThrowsAsync<TawkControlException>(() => stack.Control.RequestAsync("list_chats", null, CancellationToken.None))!;

        Assert.That(error.Code, Is.EqualTo(ControlErrorCode.Offline));
        Assert.That(error.Message, Does.Contain("looks hung"));
        await _server.WaitForAsync("hello", 2);
    }

    [Test]
    public async Task Bye_closes_the_connection_and_tawk_mcp_comes_back_when_tawk_does()
    {
        _server.Start();
        await using var stack = new LiveStack(_server.SocketPath, connectWait: TimeSpan.FromMilliseconds(200));
        await stack.StartAsync();
        await stack.WaitUntilConnectedAsync();

        await _server.SendAsync("""{"evt":"bye"}""");
        await _server.StopAsync();
        await Task.Delay(200);
        Assert.That(stack.Control.State, Is.Not.EqualTo(TawkConnectionState.Connected));

        _server.Start();
        await stack.WaitUntilConnectedAsync();
        Assert.That(stack.Logger.Lines, Has.Some.EqualTo("tawk went away; retrying"));
    }

    [Test]
    public async Task While_the_circuit_is_open_requests_fail_fast_without_connecting()
    {
        await using var stack = new LiveStack(_server.SocketPath, connectWait: TimeSpan.FromSeconds(5), breakerThreshold: 2);
        await stack.StartAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (stack.Breaker.State != CircuitState.Open && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var error = Assert.ThrowsAsync<TawkControlException>(() => stack.Control.RequestAsync("list_chats", null, CancellationToken.None))!;

        Assert.Multiple(() =>
        {
            Assert.That(stack.Control.State, Is.EqualTo(TawkConnectionState.CircuitOpen));
            Assert.That(error.Message, Does.Match(@"^tawk has not answered \d+ attempts; next try in \d+s \(or as soon as its control socket appears\)"));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
        });
    }

    [Test]
    public async Task The_socket_appearing_ends_an_open_circuit_at_once()
    {
        await using var stack = new LiveStack(_server.SocketPath, breakerThreshold: 2);
        await stack.StartAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (stack.Breaker.State != CircuitState.Open && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        _server.Start();

        await stack.WaitUntilConnectedAsync(3000);
        Assert.That(stack.Breaker.State, Is.EqualTo(CircuitState.Closed));
    }

    private static async Task<MessageEvent> FirstMessageAsync(ITawkControl control, CancellationToken cancellationToken)
    {
        await foreach (var item in control.Events.WithCancellation(cancellationToken))
        {
            if (item is MessageEvent message)
            {
                return message;
            }
        }

        throw new InvalidOperationException("The stream ended.");
    }
}
