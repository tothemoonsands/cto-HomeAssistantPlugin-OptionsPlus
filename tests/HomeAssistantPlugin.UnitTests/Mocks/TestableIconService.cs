using System;
using System.Collections.Generic;

using Loupedeck;
using Loupedeck.HomeAssistantPlugin.Services;

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
        /// Creates a fallback icon without native dependencies for testing.
        /// This should never be called in tests as it requires SkiaSharp native DLL.
        /// Tests should provide proper resource mappings to avoid this path.
        /// </summary>
        /// <returns>Never returns; always throws to indicate test configuration issue.</returns>
        protected override BitmapImage CreateFallbackIcon()
        {
            // Cannot create BitmapImage without SkiaSharp native DLL
            // Tests that reach this point need to provide proper icon mappings
            throw new InvalidOperationException(
                "CreateFallbackIcon() was called in test environment. " +
                "This requires SkiaSharp native libraries which are not available. " +
                "Ensure all icon IDs used in tests are mapped in the resource map."
            );
        }
    }
}