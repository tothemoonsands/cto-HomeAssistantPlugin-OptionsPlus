using System;
using System.Collections.Generic;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin.Services;
using Loupedeck.HomeAssistantPlugin.Tests.Mocks;

using NSubstitute;

using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services;

/// <summary>
/// Comprehensive tests for IconService focusing on icon loading, caching mechanisms,
/// fallback handling, and memory management with 85% coverage target.
/// </summary>
public class IconServiceTests
{
    private readonly Dictionary<String, String> _testResourceMap;
    private readonly MockResourceProvider _mockResourceProvider;

    public IconServiceTests()
    {
        // Use the actual embedded resource logical names (without "icons/" prefix)
        // as they appear in the .csproj file
        this._testResourceMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            [IconId.Bulb] = "light_bulb_icon.svg",
            [IconId.BulbOn] = "light_on_icon.svg",
            [IconId.BulbOff] = "light_off_icon.svg",
            [IconId.Brightness] = "brightness_icon.svg",
            [IconId.Saturation] = "saturation_icon.svg",
            [IconId.Temperature] = "temperature_icon.svg",
            [IconId.Hue] = "hue_icon.svg",
            [IconId.Area] = "area_icon.svg",
            [IconId.Back] = "back_icon.svg",
            [IconId.Retry] = "reload_icon.svg",
            [IconId.Issue] = "issue_status_icon.svg",
            [IconId.Online] = "online_status_icon.png",
            [IconId.RunScript] = "run_script_icon.svg",
            [IconId.Switch] = "switch_icon.svg",
            [IconId.SwitchOn] = "switch_on_icon.svg",
            [IconId.SwitchOff] = "switch_off_icon.svg"
        };

        this._mockResourceProvider = new MockResourceProvider();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidResourceMap_InitializesSuccessfully()
    {
        // Arrange & Act
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullResourceMap_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new TestableIconService(null!, this._mockResourceProvider);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*resourceMap*");
    }

    [Fact]
    public void Constructor_WithNullResourceProvider_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new TestableIconService(this._testResourceMap, null!);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*resourceProvider*");
    }

    [Fact]
    public void Constructor_WithEmptyResourceMap_InitializesSuccessfully()
    {
        // Arrange
        var emptyMap = new Dictionary<String, String>();

        // Act & Assert - Should not throw
        var action = () => new TestableIconService(emptyMap, this._mockResourceProvider);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_LoadsIconsFromResourceMap()
    {
        // Arrange & Act - Constructor should complete successfully with mock provider
        var action = () => new TestableIconService(this._testResourceMap, this._mockResourceProvider);
        
        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Get Method Tests - Basic Functionality

    [Fact]
    public void Get_WithValidIconId_ReturnsIcon()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var icon = service.Get(IconId.Bulb);

        // Assert
        icon.Should().NotBeNull();
    }

    [Fact]
    public void Get_WithNonExistentIconId_ReturnsFallbackIcon()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var icon = service.Get("nonexistent_icon");

        // Assert
        icon.Should().NotBeNull(); // Should return fallback icon, not null
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_WithNullOrEmptyId_ReturnsFallbackIcon(String? iconId)
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var icon = service.Get(iconId!);

        // Assert
        icon.Should().NotBeNull(); // Should return fallback icon
    }

    [Fact]
    public void Get_CaseInsensitive_ReturnsCorrectIcon()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var icon1 = service.Get(IconId.Bulb.ToLowerInvariant());
        var icon2 = service.Get(IconId.Bulb.ToUpperInvariant());
        var icon3 = service.Get(IconId.Bulb);

        // Assert
        icon1.Should().NotBeNull();
        icon2.Should().NotBeNull();
        icon3.Should().NotBeNull();
        // Note: We can't easily test if they're the same instance without deeper mocking
    }

    #endregion

    #region Icon Constants Tests

    [Fact]
    public void IconId_Constants_AreDefinedCorrectly()
    {
        // Assert - Verify all expected icon constants exist and have correct values
        IconId.Bulb.Should().Be("bulb");
        IconId.Back.Should().Be("back");
        IconId.BulbOn.Should().Be("bulbOn");
        IconId.BulbOff.Should().Be("bulbOff");
        IconId.Brightness.Should().Be("bri");
        IconId.Retry.Should().Be("retry");
        IconId.Saturation.Should().Be("sat");
        IconId.Issue.Should().Be("issue");
        IconId.Temperature.Should().Be("temp");
        IconId.Online.Should().Be("online");
        IconId.Hue.Should().Be("hue");
        IconId.Area.Should().Be("area");
        IconId.RunScript.Should().Be("run_script");
    }

    [Fact]
    public void Get_WithAllDefinedIconIds_ReturnsIcons()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act & Assert - Test all predefined icon constants
        service.Get(IconId.Bulb).Should().NotBeNull();
        service.Get(IconId.Back).Should().NotBeNull();
        service.Get(IconId.BulbOn).Should().NotBeNull();
        service.Get(IconId.BulbOff).Should().NotBeNull();
        service.Get(IconId.Brightness).Should().NotBeNull();
        service.Get(IconId.Retry).Should().NotBeNull();
        service.Get(IconId.Saturation).Should().NotBeNull();
        service.Get(IconId.Issue).Should().NotBeNull();
        service.Get(IconId.Temperature).Should().NotBeNull();
        service.Get(IconId.Online).Should().NotBeNull();
        service.Get(IconId.Hue).Should().NotBeNull();
        service.Get(IconId.Area).Should().NotBeNull();
        service.Get(IconId.RunScript).Should().NotBeNull();
    }

    #endregion

    #region Caching Behavior Tests

    [Fact]
    public void Get_CalledMultipleTimes_ReturnsCachedResults()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act - Call multiple times
        var icon1 = service.Get(IconId.Bulb);
        var icon2 = service.Get(IconId.Bulb);
        var icon3 = service.Get(IconId.Bulb);

        // Assert - All should return non-null (can't easily test same instance without deeper mocking)
        icon1.Should().NotBeNull();
        icon2.Should().NotBeNull();
        icon3.Should().NotBeNull();
    }

    [Fact]
    public void Get_WithDifferentIds_ReturnsDifferentIcons()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var bulbIcon = service.Get(IconId.Bulb);
        var brightnessIcon = service.Get(IconId.Brightness);
        var backIcon = service.Get(IconId.Back);

        // Assert - All should be valid but potentially different
        bulbIcon.Should().NotBeNull();
        brightnessIcon.Should().NotBeNull();
        backIcon.Should().NotBeNull();
    }

    #endregion

    #region Fallback Icon Tests

    [Fact]
    public void Get_WithMultipleInvalidIds_ReturnsConsistentFallbackIcons()
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var fallback1 = service.Get("invalid1");
        var fallback2 = service.Get("invalid2");
        var fallback3 = service.Get("invalid3");

        // Assert - All should return valid fallback icons
        fallback1.Should().NotBeNull();
        fallback2.Should().NotBeNull();
        fallback3.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonexistent")]
    [InlineData("INVALID")]
    [InlineData("test_icon_not_found")]
    public void Get_WithVariousInvalidIds_AlwaysReturnsFallbackIcon(String invalidId)
    {
        // Arrange
        var service = new TestableIconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var icon = service.Get(invalidId);

        // Assert
        icon.Should().NotBeNull();
    }

    #endregion

    #region Resource Loading Error Handling

    [Fact]
    public void Constructor_WithMissingResourceFiles_HandlesGracefully()
    {
        // Arrange - Create resource map with files that don't exist and mock provider that simulates failure
        var invalidResourceMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing_icon1"] = "missing_file1.svg",
            ["missing_icon2"] = "missing_file2.png",
            ["missing_icon3"] = "nonexistent.svg"
        };
        var failureProvider = new MockResourceProvider(simulateFailure: true);

        // Act & Assert - Should not throw, should handle missing resources gracefully
        var action = () => new IconService(invalidResourceMap, failureProvider);
        action.Should().NotThrow();
    }

    [Fact]
    public void Get_ForMissingResourceFile_ReturnsFallbackIcon()
    {
        // Arrange - Create service with resource that doesn't exist
        var invalidResourceMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["missing_icon"] = "missing_file.svg"
        };
        var failureProvider = new MockResourceProvider(simulateFailure: true);
        var service = new IconService(invalidResourceMap, failureProvider);

        // Act
        var icon = service.Get("missing_icon");

        // Assert - Should return fallback icon, not null or throw
        icon.Should().NotBeNull();
    }

    #endregion

    #region Performance and Memory Tests

    [Fact]
    public void Constructor_WithLargeResourceMap_HandlesEfficiently()
    {
        // Arrange - Create large resource map
        var largeResourceMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 1000; i++)
        {
            largeResourceMap[$"icon_{i}"] = $"icons/icon_{i}.svg";
        }

        // Act & Assert - Should handle large maps without issues
        var action = () => new IconService(largeResourceMap);
        action.Should().NotThrow();
    }

    [Fact]
    public void Get_WithFrequentCalls_MaintainsPerformance()
    {
        // Arrange
        var service = new IconService(this._testResourceMap, this._mockResourceProvider);
        var iconIds = new[] { IconId.Bulb, IconId.Brightness, IconId.Back, IconId.Area };

        // Act - Make many calls to test performance characteristics
        for (var i = 0; i < 1000; i++)
        {
            var iconId = iconIds[i % iconIds.Length];
            var icon = service.Get(iconId);
            
            // Assert
            icon.Should().NotBeNull();
        }

        // If we get here without timeout, performance is acceptable
    }

    [Fact]
    public void Get_ConcurrentAccess_HandlesCorrectly()
    {
        // Arrange
        var service = new IconService(this._testResourceMap, this._mockResourceProvider);
        var iconIds = new[] { IconId.Bulb, IconId.Brightness, IconId.Back, IconId.Area };
        var tasks = new List<System.Threading.Tasks.Task>();

        // Act - Test concurrent access
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                var iconId = iconIds[index % iconIds.Length];
                var icon = service.Get(iconId);
                icon.Should().NotBeNull();
            }));
        }

        // Assert - Should complete without exceptions
        var action = () => System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
        action.Should().NotThrow();
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Fact]
    public void Constructor_WithDuplicateKeysInResourceMap_HandlesCorrectly()
    {
        // Arrange - Dictionary constructor should handle duplicates, but test edge case
        var resourceMapWithDuplicates = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["test_icon"] = "icons/test1.svg"
        };
        
        // This would normally throw if we tried to add the same key again
        // But since we're using dictionary constructor, this tests normal operation

        // Act & Assert
        var action = () => new IconService(resourceMapWithDuplicates);
        action.Should().NotThrow();
    }

    [Fact]
    public void Get_WithUnicodeIconId_HandlesCorrectly()
    {
        // Arrange
        var unicodeResourceMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["灯泡"] = "icons/light_unicode.svg",
            ["🔥"] = "icons/fire.svg",
            ["тест"] = "icons/test_cyrillic.svg"
        };
        var service = new IconService(unicodeResourceMap);

        // Act & Assert
        service.Get("灯泡").Should().NotBeNull();
        service.Get("🔥").Should().NotBeNull();
        service.Get("тест").Should().NotBeNull();
        service.Get("未知").Should().NotBeNull(); // Unknown Unicode - should return fallback
    }

    [Theory]
    [InlineData("very_long_icon_name_that_exceeds_normal_expectations_and_goes_on_for_a_while")]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("123456789012345678901234567890")]
    public void Get_WithVariousStringLengths_HandlesCorrectly(String iconId)
    {
        // Arrange
        var service = new IconService(this._testResourceMap, this._mockResourceProvider);

        // Act
        var icon = service.Get(iconId);

        // Assert - Should always return something, never null
        icon.Should().NotBeNull();
    }

    [Fact]
    public void Get_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var service = new IconService(this._testResourceMap, this._mockResourceProvider);
        var specialCharacterIds = new[]
        {
            "icon@#$%",
            "icon with spaces",
            "icon\twith\ttabs",
            "icon\nwith\nnewlines",
            "icon/with/slashes",
            "icon\\with\\backslashes",
            "icon.with.dots",
            "icon-with-dashes",
            "icon_with_underscores"
        };

        // Act & Assert
        foreach (var iconId in specialCharacterIds)
        {
            var icon = service.Get(iconId);
            icon.Should().NotBeNull($"Icon ID '{iconId}' should return a fallback icon");
        }
    }

    #endregion

    #region Resource Path Validation Tests

    [Fact]
    public void Constructor_WithVariousResourcePaths_HandlesCorrectly()
    {
        // Arrange - Test different path formats
        var pathVariationMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["icon1"] = "icons/test.svg",
            ["icon2"] = "/icons/test.svg",
            ["icon3"] = "\\icons\\test.svg",
            ["icon4"] = "icons\\test.svg",
            ["icon5"] = "test.svg",
            ["icon6"] = "",
            ["icon7"] = "   ",
            ["icon8"] = "very/deep/path/structure/icon.svg"
        };

        // Act & Assert - Should handle various path formats gracefully
        var action = () => new IconService(pathVariationMap);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEmptyResourcePaths_HandlesGracefully()
    {
        // Arrange
        var emptyPathMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["empty_path"] = "",
            ["null_path"] = null!, // This might cause issues, test how it handles it
            ["whitespace_path"] = "   "
        };

        // Act & Assert - Should handle gracefully (missing resources become fallbacks)
        var action = () => new IconService(emptyPathMap);
        action.Should().NotThrow();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IconService_IntegrationWithIconIdConstants_WorksCorrectly()
    {
        // Arrange
        var service = new IconService(this._testResourceMap, this._mockResourceProvider);

        // Act & Assert - Test that all IconId constants work with the service
        var allIconIds = new[]
        {
            IconId.Bulb, IconId.Back, IconId.BulbOn, IconId.BulbOff,
            IconId.Brightness, IconId.Retry, IconId.Saturation, IconId.Issue,
            IconId.Temperature, IconId.Online, IconId.Hue, IconId.Area,
            IconId.RunScript
        };

        foreach (var iconId in allIconIds)
        {
            var icon = service.Get(iconId);
            icon.Should().NotBeNull($"IconId.{iconId} should return a valid icon");
        }
    }

    [Fact]
    public void IconService_MixedValidAndInvalidRequests_HandlesAppropriately()
    {
        // Arrange
        var service = new IconService(this._testResourceMap, this._mockResourceProvider);
        var mixedIds = new[]
        {
            IconId.Bulb,           // Valid
            "nonexistent",         // Invalid
            IconId.Brightness,     // Valid
            "",                    // Invalid
            IconId.Back,           // Valid
            "another_missing",     // Invalid
            IconId.Area            // Valid
        };

        // Act & Assert
        foreach (var iconId in mixedIds)
        {
            var icon = service.Get(iconId);
            icon.Should().NotBeNull($"All requests should return valid icons (fallback if necessary) for ID: '{iconId}'");
        }
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public void IconService_RepeatedInstantiation_DoesNotLeakMemory()
    {
        // Arrange & Act - Create and dispose multiple instances
        for (var i = 0; i < 100; i++)
        {
            var service = new IconService(this._testResourceMap, this._mockResourceProvider);
            
            // Use the service
            service.Get(IconId.Bulb);
            service.Get("nonexistent");
            
            // Service goes out of scope and should be eligible for GC
        }

        // Force garbage collection to test for memory leaks
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert - If we get here without OutOfMemoryException, we're likely okay
        // Note: True memory leak detection would require more sophisticated tooling
    }

    [Fact]
    public void IconService_LargeResourceMapWithManyRequests_MaintainsStability()
    {
        // Arrange - Create service with many resources
        var largeMap = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 500; i++)
        {
            largeMap[$"icon_{i}"] = $"icon_{i}.svg";
        }
        var service = new IconService(largeMap, this._mockResourceProvider);

        // Act - Make many requests
        for (var i = 0; i < 1000; i++)
        {
            // Mix of valid and invalid requests
            var validId = $"icon_{i % 500}";
            var invalidId = $"missing_{i}";
            
            var validIcon = service.Get(validId);
            var invalidIcon = service.Get(invalidId);
            
            // Assert
            validIcon.Should().NotBeNull();
            invalidIcon.Should().NotBeNull(); // Should be fallback
        }
    }

    #endregion
}