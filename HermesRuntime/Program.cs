using Hermes.Runtime;

var exitCode = 0;

try
{
    var configPath = RuntimeConfigResolver.Resolve(args);
    var host = new RuntimeHost(configPath);
    host.Run();
}
catch (Exception ex)
{
    exitCode = 1;
    Console.Error.WriteLine("Hermes Runtime failed to start.");
    Console.Error.WriteLine(ex.Message);
}

return exitCode;
