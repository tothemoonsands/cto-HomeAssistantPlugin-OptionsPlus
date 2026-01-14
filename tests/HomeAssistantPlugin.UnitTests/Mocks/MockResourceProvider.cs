using System;
using System.Collections.Generic;

using Loupedeck;
using Loupedeck.HomeAssistantPlugin.Services;

namespace Loupedeck.HomeAssistantPlugin.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IResourceProvider for unit testing.
    /// Creates TestBitmapImage instances instead of using native Loupedeck SDK methods.
    /// </summary>
    internal sealed class MockResourceProvider : IResourceProvider
    {
        private readonly Dictionary<String, BitmapImage?> _mockImages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Boolean _simulateFailure;

        /// <summary>
        /// Initializes a new instance of the MockResourceProvider class.
        /// </summary>
        /// <param name="simulateFailure">If true, LoadImage will return null to simulate missing resources.</param>
        public MockResourceProvider(Boolean simulateFailure = false)
        {
            this._simulateFailure = simulateFailure;
        }

        /// <summary>
        /// Loads a bitmap image. Returns null because creating BitmapImage instances
        /// requires SkiaSharp native libraries which are not available in test environment.
        /// Tests should provide all necessary resource mappings to avoid needing fallback icons.
        /// </summary>
        /// <param name="resourceName">The logical name of the resource.</param>
        /// <returns>Always returns null to avoid SkiaSharp native library dependency.</returns>
        public BitmapImage? LoadImage(String resourceName)
        {
            if (String.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            if (this._simulateFailure)
            {
                return null;
            }

            // Return cached value if we already attempted to load this resource
            if (this._mockImages.TryGetValue(resourceName, out var cachedImage))
            {
                return cachedImage;
            }

            // Cannot create BitmapImage instances without SkiaSharp native libraries
            // Tests should ensure all required icons are mapped to avoid hitting this path
            this._mockImages[resourceName] = null;
            return null;
        }

        /// <summary>
        /// Gets any cached image, useful for fallback scenarios.
        /// </summary>
        /// <returns>Always returns null as no images can be created in test environment.</returns>
        public BitmapImage? GetAnyCachedImage()
        {
            return null;
        }

        /// <summary>
        /// Clears the cached mock images.
        /// </summary>
        public void ClearCache()
        {
            this._mockImages.Clear();
        }
    }
}