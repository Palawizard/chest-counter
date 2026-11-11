using System.Net;

namespace ChestCounter.Tests;

public sealed class ObsServerTests : IDisposable
{
    private const int TestPort = 38470;

    private readonly string _root = Directory.CreateTempSubdirectory("chest-counter-test-").FullName;
    private readonly HttpClient _http = new();

    public ObsServerTests()
    {
        File.WriteAllText(Path.Combine(_root, "obs.html"), "<p>page</p>");
        File.WriteAllText(Path.Combine(_root, "secret.txt"), "not served");
    }

    public void Dispose()
    {
        _http.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Page_and_state_are_served_on_the_loopback_address()
    {
        using var server = ObsServer.Start(_root, () => "{\"total\":5076}", TestPort);

        Assert.Equal("<p>page</p>", await _http.GetStringAsync(server.Address));
        Assert.Equal("{\"total\":5076}", await _http.GetStringAsync(server.Address + "state"));
        Assert.StartsWith("http://127.0.0.1:", server.Address);
    }

    [Fact]
    public async Task Request_with_a_foreign_host_is_refused()
    {
        using var server = ObsServer.Start(_root, () => "{}", TestPort);
        using var request = new HttpRequestMessage(HttpMethod.Get, server.Address + "state");
        request.Headers.Host = "evil.example";

        using var response = await _http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Only_get_is_answered()
    {
        using var server = ObsServer.Start(_root, () => "{}", TestPort);

        using var response = await _http.PostAsync(server.Address + "state", new StringContent("x"));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("secret.txt")]
    [InlineData("..%2Fsecret.txt")]
    [InlineData("fonts/../secret.txt")]
    public async Task Only_the_page_files_are_served(string path)
    {
        using var server = ObsServer.Start(_root, () => "{}", TestPort);

        using var response = await _http.GetAsync(server.Address + path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Taken_port_moves_to_the_next_one()
    {
        using var first = ObsServer.Start(_root, () => "{}", TestPort);
        using var second = ObsServer.Start(_root, () => "{}", TestPort);

        Assert.Equal(first.Port + 1, second.Port);
    }
}
