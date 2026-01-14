using System;
using System.Collections.Generic;

using Loupedeck;
using Loupedeck.HomeAssistantPlugin.Services;

using NSubstitute;

namespace Loupedeck.HomeAssistantPlugin.Tests.Mocks
{
    /// <summary>
    /// Testable version of IconService that overrides fallback icon creation
    /// to avoid native dependencies in the test environment.
    /// </summary>
    internal sealed class TestableIconService : IconService
    {
        /// <summary>
        /// Initializes a new instance of the TestableIconService class.
        /// </summary>
        /// <param name="resourceMap">logical id → embedded resource filename</param>
        /// <param name="resourceProvider">Resource provider for loading images</param>
        public TestableIconService(IDictionary<String, String> resourceMap, IResourceProvider resourceProvider)
            : base(resourceMap, resourceProvider)
        {
        }

        /// <summary>
        /// Creates a simple fallback icon without native dependencies for testing.
        /// </summary>
        /// <returns>A simple BitmapImage suitable for testing.</returns>
        protected override BitmapImage CreateFallbackIcon()
        {
            // Return null for testing to avoid native dependencies completely
            // Tests should handle this gracefully
            return null!;
        }
    }
}