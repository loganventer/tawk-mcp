using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.ResourceAccess;

public class ControlSocketLocatorTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [Test]
    public void Uses_the_runtime_folder_when_set()
    {
        var locator = new ControlSocketLocator(null, Env(("XDG_RUNTIME_DIR", "/run/user/1000")), "/home/me");

        Assert.That(locator.Locate(), Is.EqualTo("/run/user/1000/tawk/control.sock"));
    }

    [Test]
    public void Falls_back_to_the_state_folder()
    {
        var locator = new ControlSocketLocator(null, Env(), "/home/me");

        Assert.That(locator.Locate(), Is.EqualTo("/home/me/.local/state/tawk/control.sock"));
    }

    [Test]
    public void The_environment_variable_overrides_the_default()
    {
        var locator = new ControlSocketLocator(null, Env(("XDG_RUNTIME_DIR", "/run/user/1000"), ("TAWK_CONTROL_SOCKET", "/run/tawk/control.sock")), "/home/me");

        Assert.That(locator.Locate(), Is.EqualTo("/run/tawk/control.sock"));
    }

    [Test]
    public void The_socket_flag_overrides_everything()
    {
        var locator = new ControlSocketLocator("/tmp/x.sock", Env(("TAWK_CONTROL_SOCKET", "/run/tawk/control.sock")), "/home/me");

        Assert.That(locator.Locate(), Is.EqualTo("/tmp/x.sock"));
    }
}
