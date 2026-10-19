using Buscarr.Server.Import;

namespace Buscarr.Server.Tests.Import;

public sealed class DirectoryMoveTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("buscarr-move-").FullName;

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Move_MovesTheFolderWithItsTree()
    {
        var source = Stage();
        var target = Path.Combine(_root, "destino");

        DirectoryMove.Move(source, target);

        Assert.False(Directory.Exists(source));
        Assert.Equal("um", File.ReadAllText(Path.Combine(target, "01.mp3")));
        Assert.Equal("dois", File.ReadAllText(Path.Combine(target, "cd2", "02.mp3")));
    }

    [Fact]
    public void Move_FallsBackToCopyingWhenTheRenameFails()
    {
        var source = Stage();
        var target = Directory.CreateDirectory(Path.Combine(_root, "destino")).FullName;

        DirectoryMove.Move(source, target);

        Assert.False(Directory.Exists(source));
        Assert.Equal("um", File.ReadAllText(Path.Combine(target, "01.mp3")));
        Assert.Equal("dois", File.ReadAllText(Path.Combine(target, "cd2", "02.mp3")));
    }

    private string Stage()
    {
        var source = Path.Combine(_root, "origem");
        Directory.CreateDirectory(Path.Combine(source, "cd2"));
        File.WriteAllText(Path.Combine(source, "01.mp3"), "um");
        File.WriteAllText(Path.Combine(source, "cd2", "02.mp3"), "dois");

        return source;
    }
}
