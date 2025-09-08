using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace DotENV;

#pragma warning disable VSEXTPREVIEW_TAGGERS // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

[VisualStudioContribution]
internal class DotEnvTaggerProvider
    : ExtensionPart, ITextViewTaggerProvider<ClassificationTag>, ITextViewChangedListener
{
    private readonly object lockObject = new();
    private readonly Dictionary<Uri, List<DotEnvTagger>> taggers = [];

    [VisualStudioContribution]
    public static DocumentTypeConfiguration DotENVDocumentType => new("DotENV")
    {
        FileExtensions = [
            ".env",
            ".env-sample",
            ".env.example",
            ".env.local",
            ".env.dev",
            ".env.test",
            ".env.testing",
            ".env.production",
        ],
        BaseDocumentType = DocumentType.KnownValues.PlainText,
    };

    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = [DocumentFilter.FromDocumentType(DotENVDocumentType)],
    };

    public async Task TextViewChangedAsync(TextViewChangedArgs args, CancellationToken cancellationToken)
    {
        List<Task> tasks = [];
        lock (lockObject)
        {
            if (this.taggers.TryGetValue(args.AfterTextView.Uri, out var taggers))
            {
                foreach (var tagger in taggers)
                {
                    tasks.Add(tagger.TextViewChangedAsync(args.AfterTextView, args.Edits, cancellationToken));
                }
            }
        }

        await Task.WhenAll(tasks);
    }

    Task<TextViewTagger<ClassificationTag>> ITextViewTaggerProvider<ClassificationTag>.CreateTaggerAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        var tagger = new DotEnvTagger(this, textView.Document.Uri);
        lock (lockObject)
        {
            if (!this.taggers.TryGetValue(textView.Document.Uri, out var taggers))
            {
                taggers = [];
                this.taggers[textView.Document.Uri] = taggers;
            }

            taggers.Add(tagger);
        }

        return Task.FromResult<TextViewTagger<ClassificationTag>>(tagger);
    }

    internal void RemoveTagger(Uri documentUri, DotEnvTagger toBeRemoved)
    {
        lock (lockObject)
        {
            if (this.taggers.TryGetValue(documentUri, out var taggers))
            {
                taggers.Remove(toBeRemoved);
                if (taggers.Count == 0)
                {
                    this.taggers.Remove(documentUri);
                }
            }
        }
    }
}
