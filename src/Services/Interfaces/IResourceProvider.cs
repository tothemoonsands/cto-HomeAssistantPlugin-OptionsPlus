namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;

    /// <summary>
    /// Abstraction for resource loading to enable testing and dependency injection.
    /// Provides bitmap image loading functionality that can be mocked or stubbed.
    /// </summary>
    public interface IResourceProvider
    {
        /// <summary>
        /// Loads a bitmap image from an embedded resource.
        /// </summary>
        /// <param name="resourceName">The logical name of the embedded resource.</param>
        /// <returns>A bitmap image if found; otherwise, null.</returns>
        BitmapImage? LoadImage(String resourceName);
    }
}