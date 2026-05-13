using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameData.Core.Enums;
using Xunit;

namespace BotRunner.Tests.Spec;

/// <summary>
/// Drift test for <see cref="FailureReason"/>.
///
/// Asserts 1:1 mapping between values declared in code
/// (<c>Exports/GameData.Core/Enums/FailureReason.cs</c>) and the canonical
/// list in <c>docs/Spec/12_ERROR_TAXONOMY.md</c>. The doc is the source
/// of truth — if these tests fire, update the doc first, then mirror
/// into the enum.
/// </summary>
public sealed class FailureReasonCatalogTests
{
    [Fact]
    public void EveryEnumValueIsDocumented()
    {
        var enumValues = Enum.GetNames(typeof(FailureReason)).ToHashSet();
        var docPath = LocateSpecDoc();
        var docContent = File.ReadAllText(docPath);

        foreach (var name in enumValues)
        {
            Assert.True(
                docContent.Contains(name, StringComparison.Ordinal),
                $"FailureReason.{name} is declared in code but NOT mentioned in docs/Spec/12_ERROR_TAXONOMY.md. " +
                "Per Spec/12 the doc is the source of truth — add the value to the doc or remove it from the enum.");
        }
    }

    [Fact]
    public void EveryDocumentedValueExistsInEnum()
    {
        var enumValues = Enum.GetNames(typeof(FailureReason)).ToHashSet();
        var docPath = LocateSpecDoc();
        var docContent = File.ReadAllText(docPath);
        var enumBlock = ExtractFencedCsharpBlock(docContent);
        var declaredInDoc = ExtractSnakeCaseTokens(enumBlock).ToList();

        Assert.NotEmpty(declaredInDoc);

        foreach (var docToken in declaredInDoc)
        {
            Assert.True(
                enumValues.Contains(docToken),
                $"docs/Spec/12_ERROR_TAXONOMY.md declares `{docToken}` but no such value exists in FailureReason enum. " +
                "Per Spec/12 the doc is the source of truth — add the value to the enum.");
        }
    }

    private static string LocateSpecDoc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "Spec", "12_ERROR_TAXONOMY.md");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate docs/Spec/12_ERROR_TAXONOMY.md by walking up from " +
            $"'{AppContext.BaseDirectory}'. The drift test needs the spec doc to run.");
    }

    private static string ExtractFencedCsharpBlock(string content)
    {
        var match = Regex.Match(content, @"(?s)```csharp\s*\r?\n(.*?)\r?\n```");
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "docs/Spec/12_ERROR_TAXONOMY.md does not contain a ```csharp fenced code block. " +
                "Cannot extract documented enum values.");
        }
        return match.Groups[1].Value;
    }

    private static IEnumerable<string> ExtractSnakeCaseTokens(string block)
    {
        var pattern = new Regex(@"^\s+([a-z][a-z0-9_]*)\s*,\s*$", RegexOptions.Multiline);
        foreach (Match m in pattern.Matches(block))
        {
            yield return m.Groups[1].Value;
        }
    }
}
