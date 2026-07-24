namespace SaaSAgentSample.Web;

/// <summary>
/// Marker type for the app's shared localization resources
/// (<c>Resources/SharedResource.&lt;culture&gt;.resx</c>). Inject
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{SharedResource}"/> to
/// resolve UI strings. English is the default; keys are the English text, so a missing
/// translation safely falls back to English.
///
/// Kept at the project root (not under Resources/) so MSBuild names the .resx by its folder
/// (<c>SaaSAgentSample.Web.Resources.SharedResource</c>), matching the ResourcesPath = "Resources"
/// prefix the localizer looks under.
/// </summary>
public sealed class SharedResource;
