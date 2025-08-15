using System.Net.Http.Headers;

while (true)
{
    try
    {
        // send empty an array in MessagePack format
        var content = new ByteArrayContent([0x90]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");

        // add these headers to every request
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Datadog-Meta-Tracer-Version", "3.25.0.0");
        client.DefaultRequestHeaders.Add("Datadog-Meta-Lang", ".NET");
        client.DefaultRequestHeaders.Add("Datadog-Meta-Lang-Interpreter", ".NET"); // ".NET", ".NET Core", or ".NET Framework"
        client.DefaultRequestHeaders.Add("Datadog-Meta-Lang-Version", Environment.Version.ToString());
        client.DefaultRequestHeaders.Add("X-Datadog-Trace-Count", "0");

        // try to send the empty trace to the trace agent
        var response = await client.PostAsync("http://localhost:8126/v0.4/traces", content);
        response.EnsureSuccessStatusCode();

        Console.WriteLine("Successfully sent empty trace to Datadog agent");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send trace: {ex.Message}");
    }

    await Task.Delay(TimeSpan.FromSeconds(1));
}
