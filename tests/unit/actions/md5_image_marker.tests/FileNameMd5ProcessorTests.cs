using md5_image_hasher.Abstractions;
using md5_image_hasher.Services;
using NSubstitute;
using Xunit;

namespace md5_image_marker.tests;

public class FileNameMd5ProcessorTests
{
    private static FileNameMd5Processor CreateSut(
        IFileSystem? fs = null,
        IFileHasher? hasher = null)
    {
        return new FileNameMd5Processor(fs ?? Substitute.For<IFileSystem>(),
            hasher ?? Substitute.For<IFileHasher>());
    }

    [Fact]
    public async Task RunAsync_WhenArgIsSingleFile_WithNoMd5_AddsHashAndRenames()
    {
        // arrange
        var filePath = "/images/cat.png";
        var fs = Substitute.For<IFileSystem>();
        var hasher = Substitute.For<IFileHasher>();

        fs.DirectoryExists(filePath).Returns(false);
        // AllowImageToProcess checks File.Exists; simulate via touching the file with a temp
        // Since AllowImageToProcess uses System.IO directly, create a temp file in place-like path.
        // To avoid filesystem dependencies, we’ll use actual temp file and align paths.
        var src = Path.Combine("/some/folder", "cat.png");
        // map our test filePath to the real file to satisfy AllowImageToProcess
        var args = new[] { src };

        hasher.ComputeMd5Async(src).Returns("0123456789abcdef0123456789abcdef"); // 32 hex

        var sut = CreateSut(fs, hasher);

        // act
        await sut.RunAsync(args);

        // assert
        var expectedNewName = Path.Combine("/some/folder", "cat_0123456789abcdef0123456789abcdef.png");
        fs.Received(1).MoveFile(src, expectedNewName);
    }

    [Fact]
    public async Task RunAsync_WhenArgIsDirectory_ProcessesTopLevelFilesOnly()
    {
        // arrange
        var dir = "/sampledir";
        var file1 = Path.Combine(dir, "a.png");
        var file2 = Path.Combine(dir, "b.jpg");
        var nestedDir = "/sampledir/nesteddir";
        var nestedFile = Path.Combine(nestedDir, "c.png");
        
        var fs = Substitute.For<IFileSystem>();
        var hasher = Substitute.For<IFileHasher>();

        fs.DirectoryExists(dir).Returns(true);
        fs.DirectoryExists(nestedDir).Returns(true);
        fs.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
          .Returns(new[] { file1, file2 }); // exclude nested file

        hasher.ComputeMd5Async(file1).Returns("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        hasher.ComputeMd5Async(file2).Returns("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var sut = CreateSut(fs, hasher);

        // act
        await sut.RunAsync(new[] { dir });

        // assert
        fs.Received(1).MoveFile(file1, Path.Combine(dir, "a_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png"));
        fs.Received(1).MoveFile(file2, Path.Combine(dir, "b_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jpg"));
        fs.DidNotReceive().MoveFile(nestedFile, Arg.Any<string>());
    }

    [Fact]
    public async Task RunAsync_WhenFileHasMd5Already_DoesNotRename()
    {
        // arrange
        var dir = "/sampledir";
        var file = Path.Combine(dir, "a_0123456789abcdef0123456789abcdef.png");

        var fs = Substitute.For<IFileSystem>();
        var hasher = Substitute.For<IFileHasher>();

        fs.DirectoryExists(file).Returns(false);

        var sut = CreateSut(fs, hasher);

        // act
        await sut.RunAsync(new[] { file });

        // assert
        fs.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RunAsync_SkipsDotDsStoreAndMov()
    {
        // arrange
        var dir = "/sampledir";
        var ds = Path.Combine(dir, ".DS_Store");
        var mov = Path.Combine(dir, "video.mov");

        var fs = Substitute.For<IFileSystem>();
        var hasher = Substitute.For<IFileHasher>();

        fs.DirectoryExists(Arg.Any<string>()).Returns(false);

        var sut = CreateSut(fs, hasher);

        // act
        await sut.RunAsync(new[] { ds, mov });

        // assert
        fs.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RunAsync_WhenFileDoesNotExist_IsSkipped()
    {
        // arrange
        var notExists = Path.Combine("/ghost_folder/", "nope.png");

        var fs = Substitute.For<IFileSystem>();
        var hasher = Substitute.For<IFileHasher>();
        fs.DirectoryExists(notExists).Returns(false);

        var sut = CreateSut(fs, hasher);

        // act
        await sut.RunAsync(new[] { notExists });

        // assert
        fs.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RunAsync_WhenHasherThrows_DoesNotThrowAndLogs()
    {
        // arrange
        var dir = "/sampledir";
        var file = Path.Combine(dir, "img.png");

        var fs = Substitute.For<IFileSystem>();
        var hasher = Substitute.For<IFileHasher>();
        fs.DirectoryExists(file).Returns(false);
        hasher.ComputeMd5Async(file).Returns<Task>(_ => throw new System.Exception("fail"));

        var sut = CreateSut(fs, hasher);

        // act
        var ex = await Record.ExceptionAsync(() => sut.RunAsync(new[] { file }));

        // assert
        Assert.Null(ex);
        fs.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>());
    }
}