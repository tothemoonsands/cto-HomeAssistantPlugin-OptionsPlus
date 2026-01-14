namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Production implementation of IResourceProvider that loads resources 
    /// from the plugin assembly using the PluginResources static helper.
    /// </summary>
    internal sealed class PluginResourceProvider : IResourceProvider
    {
        private readonly Assembly _assembly;
        private Boolean _initialized;

        /// <summary>
        /// Initializes a new instance of the PluginResourceProvider class.
        /// </summary>
        /// <param name="assembly">The assembly containing embedded resources.</param>
        public PluginResourceProvider(Assembly assembly)
        {
            this._assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }

        /// <summary>
        /// Loads a bitmap image from an embedded resource.
        /// Initializes PluginResources if not already done.
        /// </summary>
        /// <param name="resourceName">The logical name of the embedded resource.</param>
        /// <returns>A bitmap image if found; otherwise, null.</returns>
        public BitmapImage? LoadImage(String resourceName)
        {
            if (String.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            try
            {
                // Ensure plugin resources are initialized (idempotent)
                if (!this._initialized)
                {
                    PluginResources.Init(this._assembly);
                    this._initialized = true;
                }

                return PluginResources.ReadImage(resourceName);
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[PluginResourceProvider] Failed to load image resource: '{resourceName}'");
                return null;
            }
        }
    }
}