using System;
using System.Collections.Generic;

using Loupedeck;
using Loupedeck.HomeAssistantPlugin.Services;

using NSubstitute;

namespace Loupedeck.HomeAssistantPlugin.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IResourceProvider for unit testing.
    /// Creates mock BitmapImage objects using NSubstitute instead of native Loupedeck SDK methods.
    /// </summary>
    internal sealed class MockResourceProvider : IResourceProvider
    {
        private readonly Dictionary<String, BitmapImage> _mockImages = new(StringComparer.OrdinalIgnoreCase);
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
        /// Loads a bitmap image, creating a mock BitmapImage using NSubstitute.
        /// </summary>
        /// <param name="resourceName">The logical name of the resource.</param>
        /// <returns>A mock bitmap image if simulateFailure is false; otherwise, null.</returns>
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

            // Return cached mock image if we already created one for this resource
            if (this._mockImages.TryGetValue(resourceName, out var cachedImage))
            {
                return cachedImage;
            }

            try
            {
                // Create a mock BitmapImage using NSubstitute to avoid native dependencies
                var mockImage = Substitute.For<BitmapImage>();
                
                // Store it in cache for consistent return values
                this._mockImages[resourceName] = mockImage;
                return mockImage;
            }
            catch (Exception)
            {
                // If mock image generation fails, return null
                return null;
            }
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