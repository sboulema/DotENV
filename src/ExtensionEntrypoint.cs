using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

namespace DotENV;

[VisualStudioContribution]
internal class ExtensionEntrypoint : Extension
{
    /// <inheritdoc/>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        LoadedWhen = ActivationConstraint.ClientContext(
                ClientContextKey.Shell.ActiveEditorFileName,
                @"\.(env|env-sample|env\.example|env\.local|env\.dev|env\.test|env\.testing|env\.production)$"),
        Metadata = new(
                id: "01e49a68-16fa-4117-93da-ab2c38191fd2",
                version: ExtensionAssemblyVersion,
                publisherName: "Samir Boulema",
                displayName: "DotENV",
                description: "Provide syntax highlighting for .env files")
        {
            MoreInfo = "https://github.com/sboulema/DotENV",
            ReleaseNotes = "https://github.com/sboulema/DotENV/releases",
            Tags = ["dotenv", "env", "config", "syntax", "highlight"],
            // TODO: BUG? License file is missing
            //License = "Resources/License.txt",
            Icon = "Resources/icon.png",
            PreviewImage = "Resources/Screenshot-light.png",
        },
    };

    /// <inheritdoc />
    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        base.InitializeServices(serviceCollection);

        // You can configure dependency injection here by adding services to the serviceCollection.
    }
}
