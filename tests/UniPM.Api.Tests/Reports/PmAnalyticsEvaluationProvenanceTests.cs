using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UniPM.PmAnalytics.InterpretationEval;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsEvaluationProvenanceTests
{
    [Fact]
    public async Task Verifier_accepts_the_actual_HEAD_of_a_disposable_repository()
    {
        await using var repository = await TemporaryGitRepository.CreateAsync();

        var verifiedSha = await GitSourceShaVerifier.VerifyAsync(repository.Path, repository.Head);

        Assert.Equal(repository.Head, verifiedSha);
    }

    [Fact]
    public async Task Verifier_rejects_a_mismatched_source_SHA_with_a_safe_code()
    {
        await using var repository = await TemporaryGitRepository.CreateAsync();

        var exception = await Assert.ThrowsAsync<GitSourceShaVerificationException>(() =>
            GitSourceShaVerifier.VerifyAsync(repository.Path, new string('0', 40)));

        Assert.Equal("SourceShaMismatch", exception.Code);
    }

    [Fact]
    public async Task Verifier_fails_closed_when_git_or_a_repository_revision_is_unavailable()
    {
        await using var repository = await TemporaryGitRepository.CreateAsync();
        var missingGit = Path.Combine(repository.Path, "missing-git.exe");

        var unavailableGit = await Assert.ThrowsAsync<GitSourceShaVerificationException>(() =>
            GitSourceShaVerifier.VerifyAsync(
                repository.Path,
                repository.Head,
                gitExecutable: missingGit));
        Assert.Equal("GitUnavailable", unavailableGit.Code);

        var temporaryDirectory = Directory.CreateTempSubdirectory("unipm-nla-no-repo-");
        try
        {
            var nonRepositoryPath = Path.Combine(temporaryDirectory.FullName, "plain-directory");
            Directory.CreateDirectory(nonRepositoryPath);
            var missingPath = Path.Combine(temporaryDirectory.FullName, "missing-directory");

            foreach (var path in new[] { nonRepositoryPath, missingPath })
            {
                var exception = await Assert.ThrowsAsync<GitSourceShaVerificationException>(() =>
                    GitSourceShaVerifier.VerifyAsync(path, repository.Head));

                Assert.Equal("SourceRevisionUnavailable", exception.Code);
            }
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory.FullName, Path.GetTempPath());
        }
    }

    [Fact]
    public async Task Mismatched_source_stops_before_dataset_provider_and_report_work()
    {
        await using var repository = await TemporaryGitRepository.CreateAsync(includeDatasetMarker: true);
        using var providerListener = new TcpListener(IPAddress.Loopback, 0);
        providerListener.Start();
        var providerPort = ((IPEndPoint)providerListener.LocalEndpoint).Port;
        var outputPath = Path.Combine(repository.Path, "artifacts", "report.json");
        var missingDatasetPath = Path.Combine(repository.Path, "missing-cases.jsonl");
        var evaluatorAssembly = typeof(GitSourceShaVerifier).Assembly.Location;
        var testRuntimeConfig = Path.Combine(AppContext.BaseDirectory, "UniPM.Api.Tests.runtimeconfig.json");
        var testDependencies = Path.Combine(AppContext.BaseDirectory, "UniPM.Api.Tests.deps.json");

        Assert.True(File.Exists(evaluatorAssembly));
        Assert.True(File.Exists(testRuntimeConfig));
        Assert.True(File.Exists(testDependencies));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repository.Path,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = repository.GlobalConfigPath;
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(testRuntimeConfig);
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(testDependencies);
        startInfo.ArgumentList.Add(evaluatorAssembly);
        startInfo.ArgumentList.Add("--split");
        startInfo.ArgumentList.Add("dev");
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("ollama");
        startInfo.ArgumentList.Add("--source-sha");
        startInfo.ArgumentList.Add(new string('0', 40));
        startInfo.ArgumentList.Add("--base-address");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{providerPort}/");
        startInfo.ArgumentList.Add("--dataset");
        startInfo.ArgumentList.Add(missingDatasetPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the evaluator test process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        Assert.Equal(2, process.ExitCode);
        Assert.Empty(await standardOutput);
        Assert.Equal("Evaluation setup failed: SourceShaMismatch", (await standardError).Trim());
        Assert.False(File.Exists(outputPath));
        Assert.False(providerListener.Pending());
    }

    private sealed class TemporaryGitRepository : IAsyncDisposable
    {
        private readonly string _temporaryRoot;

        private TemporaryGitRepository(string temporaryRoot, string path, string globalConfigPath, string head)
        {
            _temporaryRoot = temporaryRoot;
            Path = path;
            GlobalConfigPath = globalConfigPath;
            Head = head;
        }

        internal string Path { get; }

        internal string GlobalConfigPath { get; }

        internal string Head { get; }

        internal static async Task<TemporaryGitRepository> CreateAsync(bool includeDatasetMarker = false)
        {
            var temporaryRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"unipm-nla-git-{Guid.NewGuid():N}");
            var repositoryPath = System.IO.Path.Combine(temporaryRoot, "repository");
            var globalConfigPath = System.IO.Path.Combine(temporaryRoot, "empty-global-config");
            Directory.CreateDirectory(repositoryPath);
            await File.WriteAllTextAsync(globalConfigPath, string.Empty);

            try
            {
                if (includeDatasetMarker)
                {
                    var markerPath = System.IO.Path.Combine(
                        repositoryPath,
                        "reference",
                        "evaluation",
                        "pm-analytics-interpretation",
                        "v1",
                        "cases.jsonl");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(markerPath)!);
                    await File.WriteAllTextAsync(markerPath, "{}\n");
                }

                await File.WriteAllTextAsync(System.IO.Path.Combine(repositoryPath, "seed.txt"), "synthetic test fixture");
                await RunGitAsync(repositoryPath, globalConfigPath, "init", "--quiet", "--initial-branch=main");
                await RunGitAsync(repositoryPath, globalConfigPath, "add", "--all");
                await RunGitAsync(
                    repositoryPath,
                    globalConfigPath,
                    "-c",
                    "user.name=UniPM test",
                    "-c",
                    "user.email=unipm-tests@example.invalid",
                    "commit",
                    "--quiet",
                    "--no-gpg-sign",
                    "-m",
                    "synthetic fixture");
                var head = await RunGitAsync(
                    repositoryPath,
                    globalConfigPath,
                    "rev-parse",
                    "--verify",
                    "HEAD^{commit}");

                return new TemporaryGitRepository(temporaryRoot, repositoryPath, globalConfigPath, head);
            }
            catch
            {
                if (Directory.Exists(temporaryRoot))
                {
                    DeleteTemporaryDirectory(temporaryRoot, System.IO.Path.GetTempPath());
                }

                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                DeleteTemporaryDirectory(_temporaryRoot, System.IO.Path.GetTempPath());
            }

            return ValueTask.CompletedTask;
        }

        private static async Task<string> RunGitAsync(
            string workingDirectory,
            string globalConfigPath,
            params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            startInfo.Environment["GIT_CONFIG_GLOBAL"] = globalConfigPath;
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Git for the test fixture.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }

                _ = await standardOutput;
                _ = await standardError;
                throw;
            }

            _ = await standardError;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Could not prepare an isolated local Git fixture.");
            }

            return (await standardOutput).Trim();
        }
    }

    private static void DeleteTemporaryDirectory(string targetPath, string expectedParent)
    {
        var fullTarget = System.IO.Path.GetFullPath(targetPath);
        var fullParent = System.IO.Path.GetFullPath(expectedParent);
        var relativeTarget = System.IO.Path.GetRelativePath(fullParent, fullTarget);
        if (relativeTarget == "."
            || System.IO.Path.IsPathRooted(relativeTarget)
            || relativeTarget == ".."
            || relativeTarget.StartsWith($"..{System.IO.Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || relativeTarget.StartsWith($"..{System.IO.Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a directory outside the test temp root.");
        }

        if (!Directory.Exists(fullTarget))
        {
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(fullTarget, "*", SearchOption.AllDirectories).Prepend(fullTarget))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(fullTarget, recursive: true);
    }
}
