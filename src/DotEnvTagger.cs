using Microsoft.VisualStudio.Extensibility.Editor;
using System.Text.RegularExpressions;

namespace DotENV;

#pragma warning disable VSEXTPREVIEW_TAGGERS // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

internal class DotEnvTagger(
    DotEnvTaggerProvider provider,
    Uri documentUri) : TextViewTagger<ClassificationTag>
{
    private const string CommentMatchName = "comment";
    private const string KeywordMatchName = "keyword";
    private const string VariableMatchName = "variable";
    private const string ConstantMatchName = "constant";
    private const string NumericMatchName = "numeric";
    private const string AssignmentMatchName = "assignment";
    private const string StringMatchName = "string";
    private const string InterpolationMatchName = "interpolation";
    private const string EscapeMatchName = "escape";

    private static readonly Regex CommentRegex = new(
        @"\s*(?<comment>#.*$)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex AssignmentRegex = new(
        @"(?<=[\w])\s*(?<assignment>=)\s*",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex VariableRegex = new(
        @"(?<variable>[\w]+)(?=\s?\=)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex KeywordRegex = new(
        @"(?i)\s?(?<keyword>export)\s+",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex ConstantRegex = new(
        @"(?i)(?<=\=\s?)(?<constant>true|false|null)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex NumericRegex = new(
        @"(?<numeric>[+-]?\b((0(x|X)[0-9a-fA-F]*)|(([0-9]+\.?[0-9]*)|(\.[0-9]+))((e|E)(\+|-)?[0-9]+)?)\b)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex EscapeRegex = new(
        @"(?<escape>\\[nrt\\\$\""\'])",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex InterpolationRegex = new(
        @"(?<interpolation>{\$?\w+?}|\${\w+?}|\$\w+)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex StringRegex = new(
        $"(?<string>\".*?)({InterpolationRegex}?{EscapeRegex}?(?<string>[^\\${{\\\\\"]*))*(?<string>[^\\${{\\\\\"]*?\")|(?<string>'.*')",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static readonly Regex LineRegex = new(
        $@"^({KeywordRegex})?({VariableRegex})?({AssignmentRegex})({ConstantRegex})?({NumericRegex})?({StringRegex})?({CommentRegex})?",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    public override void Dispose()
    {
        provider.RemoveTagger(documentUri, this);
        base.Dispose();
    }

    public async Task TextViewChangedAsync(ITextViewSnapshot textView, IReadOnlyList<TextEdit> edits, CancellationToken cancellationToken)
    {
        if (edits.Count == 0)
        {
            return;
        }

        var allRequestedRanges = await GetAllRequestedRangesAsync(textView.Document, cancellationToken);
        await CreateTagsAsync(
            textView.Document,
            allRequestedRanges.Intersect(// Use Intersect to only create tags for ranges that VS has previously expressed interested in.
                edits.Select(e =>
                    EnsureNotEmpty(// Fix empty ranges to be at least 1 character long so that they are not ignored when intersected (empty ranges are the result of text deletion).
                        e.Range.TranslateTo(textView.Document, TextRangeTrackingMode.ExtendForwardAndBackward))))); // Translate the range to the new document version.
    }

    protected override async Task RequestTagsAsync(NormalizedTextRangeCollection requestedRanges, bool recalculateAll, CancellationToken cancellationToken)
    {
        if (requestedRanges.Count == 0)
        {
            return;
        }

        await CreateTagsAsync(requestedRanges.TextDocumentSnapshot!, requestedRanges);
    }

    private static TextRange EnsureNotEmpty(TextRange range)
    {
        if (range.Length > 0)
        {
            return range;
        }

        int start = Math.Max(0, range.Start - 1);
        int end = Math.Min(range.Document.Length, range.Start + 1);

        return new(range.Document, start, end - start);
    }

    // VisualStudio.Extensibility doesn't support defining text colors for
    // new classification types yet, so we must use existing classification
    // types.
    private async Task CreateTagsAsync(ITextDocumentSnapshot document, IEnumerable<TextRange> requestedRanges)
    {
        List<TaggedTrackingTextRange<ClassificationTag>> tags = [];
        List<TextRange> updatedRanges = [];

        foreach (var lineNumber in requestedRanges.SelectMany(r =>
        {
            // Convert the requested range to line numbers.
            var startLine = r.Document.GetLineNumberFromPosition(r.Start);
            var endLine = r.Document.GetLineNumberFromPosition(r.End);
            return Enumerable.Range(startLine, endLine - startLine + 1);

            // Use Distinct to avoid processing the same line multiple times.
        }).Distinct())
        {
            var line = document.Lines[lineNumber];
            var lineText = line.Text.CopyToString();

            if (line.Text.StartsWith("#"))
            {
                tags.Add(new(
                    new(document, line.Text.Start, line.Text.Length, TextRangeTrackingMode.ExtendNone),
                    new(ClassificationType.KnownValues.Comment)));
            }

            var lineMatch = LineRegex.Match(line.Text.CopyToString());

            if (lineMatch.Success)
            {
                foreach (Capture capture in lineMatch.Groups[CommentMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Comment);
                }

                foreach (Capture capture in lineMatch.Groups[KeywordMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Keyword);
                }

                foreach (Capture capture in lineMatch.Groups[VariableMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Identifier);
                }

                foreach (Capture capture in lineMatch.Groups[ConstantMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Literal);
                }

                foreach (Capture capture in lineMatch.Groups[NumericMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Number);
                }

                foreach (Capture capture in lineMatch.Groups[AssignmentMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Operator);
                }

                foreach (Capture capture in lineMatch.Groups[StringMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.String);
                }

                foreach (Capture capture in lineMatch.Groups[InterpolationMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.KnownValues.Identifier);
                }

                foreach (Capture capture in lineMatch.Groups[EscapeMatchName].Captures)
                {
                    AddTag(capture, ClassificationType.Custom("string - escape character"));
                }
            }

            void AddTag(Capture capture, ClassificationType classificationType)
            {
                tags.Add(new(
                    new(document, line.Text.Start + capture.Index, capture.Length, TextRangeTrackingMode.ExtendNone),
                    new(classificationType)));
            }

            // Add the range to the list of ranges we have calculated tags for. We add the range even if no tags
            // were created for it, this takes care of clearing any tags that were previously created for this
            // range and are not valid anymore.
            updatedRanges.Add(new(document, line.TextIncludingLineBreak.Start, line.TextIncludingLineBreak.Length));
        }

        // Return the ranges we have calculated tags for and the tags themselves.
        await UpdateTagsAsync(updatedRanges, tags, CancellationToken.None);
    }
}
