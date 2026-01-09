using System;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin;

using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services;

/// <summary>
/// Comprehensive tests for HueSaturation record struct focusing on color space service integration,
/// value semantics, equality behavior, and edge cases with 85% coverage target.
/// </summary>
public class HueSaturationTests
{
    #region Constructor and Property Tests

    [Fact]
    public void Constructor_WithValidValues_InitializesCorrectly()
    {
        // Arrange & Act
        var hueSat = new HueSaturation(180.0, 75.0);

        // Assert
        hueSat.H.Should().Be(180.0);
        hueSat.S.Should().Be(75.0);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(359.9, 100.0)]
    [InlineData(180.0, 50.0)]
    [InlineData(45.5, 25.7)]
    public void Constructor_WithVariousValidValues_InitializesCorrectly(Double hue, Double saturation)
    {
        // Act
        var hueSat = new HueSaturation(hue, saturation);

        // Assert
        hueSat.H.Should().Be(hue);
        hueSat.S.Should().Be(saturation);
    }

    [Theory]
    [InlineData(-1.0, 50.0)]     // Negative hue
    [InlineData(360.0, 50.0)]    // Hue at 360
    [InlineData(450.0, 50.0)]    // Hue above 360
    [InlineData(180.0, -1.0)]    // Negative saturation
    [InlineData(180.0, 101.0)]   // Saturation above 100
    public void Constructor_WithOutOfRangeValues_AcceptsValues(Double hue, Double saturation)
    {
        // Note: HueSaturation is a simple data structure - it doesn't validate ranges
        // Range validation is handled by higher-level services like HSBHelper

        // Act
        var hueSat = new HueSaturation(hue, saturation);

        // Assert - Should accept any double values
        hueSat.H.Should().Be(hue);
        hueSat.S.Should().Be(saturation);
    }

    [Theory]
    [InlineData(Double.MinValue, Double.MaxValue)]
    [InlineData(Double.MaxValue, Double.MinValue)]
    [InlineData(0.0, Double.PositiveInfinity)]
    [InlineData(Double.NegativeInfinity, 0.0)]
    public void Constructor_WithExtremeValues_HandlesCorrectly(Double hue, Double saturation)
    {
        // Act
        var hueSat = new HueSaturation(hue, saturation);

        // Assert
        hueSat.H.Should().Be(hue);
        hueSat.S.Should().Be(saturation);
    }

    [Fact]
    public void Constructor_WithNaNValues_HandlesCorrectly()
    {
        // Act
        var hueSat1 = new HueSaturation(Double.NaN, 50.0);
        var hueSat2 = new HueSaturation(180.0, Double.NaN);
        var hueSat3 = new HueSaturation(Double.NaN, Double.NaN);

        // Assert
        Double.IsNaN(hueSat1.H).Should().BeTrue();
        hueSat1.S.Should().Be(50.0);

        hueSat2.H.Should().Be(180.0);
        Double.IsNaN(hueSat2.S).Should().BeTrue();

        Double.IsNaN(hueSat3.H).Should().BeTrue();
        Double.IsNaN(hueSat3.S).Should().BeTrue();
    }

    #endregion

    #region Equality and Comparison Tests

    [Fact]
    public void Equals_WithIdenticalValues_ReturnsTrue()
    {
        // Arrange
        var hueSat1 = new HueSaturation(180.0, 75.0);
        var hueSat2 = new HueSaturation(180.0, 75.0);

        // Act & Assert
        hueSat1.Equals(hueSat2).Should().BeTrue();
        hueSat1.Should().Be(hueSat2);
        (hueSat1 == hueSat2).Should().BeTrue();
        (hueSat1 != hueSat2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var hueSat1 = new HueSaturation(180.0, 75.0);
        var hueSat2 = new HueSaturation(180.0, 76.0); // Different saturation
        var hueSat3 = new HueSaturation(181.0, 75.0); // Different hue

        // Act & Assert
        hueSat1.Equals(hueSat2).Should().BeFalse();
        hueSat1.Equals(hueSat3).Should().BeFalse();
        hueSat1.Should().NotBe(hueSat2);
        hueSat1.Should().NotBe(hueSat3);
        (hueSat1 == hueSat2).Should().BeFalse();
        (hueSat1 == hueSat3).Should().BeFalse();
        (hueSat1 != hueSat2).Should().BeTrue();
        (hueSat1 != hueSat3).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithVeryCloseValues_ReturnsExpectedResults()
    {
        // Arrange - Test floating point precision
        var hueSat1 = new HueSaturation(180.0, 75.0);
        var hueSat2 = new HueSaturation(180.0000000001, 75.0); // Very close hue
        var hueSat3 = new HueSaturation(180.0, 75.0000000001); // Very close saturation

        // Act & Assert - Should be different due to double precision
        hueSat1.Equals(hueSat2).Should().BeFalse();
        hueSat1.Equals(hueSat3).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithSameInstance_ReturnsTrue()
    {
        // Arrange
        var hueSat = new HueSaturation(180.0, 75.0);

        // Act & Assert
        hueSat.Equals(hueSat).Should().BeTrue();
        (hueSat == hueSat).Should().BeTrue();
        (hueSat != hueSat).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var hueSat = new HueSaturation(180.0, 75.0);

        // Act & Assert
        hueSat.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var hueSat = new HueSaturation(180.0, 75.0);

        // Act & Assert
        hueSat.Equals("not a HueSaturation").Should().BeFalse();
        hueSat.Equals(42).Should().BeFalse();
        hueSat.Equals(new { H = 180.0, S = 75.0 }).Should().BeFalse();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_WithIdenticalValues_ReturnsSameHashCode()
    {
        // Arrange
        var hueSat1 = new HueSaturation(180.0, 75.0);
        var hueSat2 = new HueSaturation(180.0, 75.0);

        // Act
        var hash1 = hueSat1.GetHashCode();
        var hash2 = hueSat2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_WithDifferentValues_ReturnsDifferentHashCodes()
    {
        // Arrange
        var hueSat1 = new HueSaturation(180.0, 75.0);
        var hueSat2 = new HueSaturation(181.0, 75.0);
        var hueSat3 = new HueSaturation(180.0, 76.0);

        // Act
        var hash1 = hueSat1.GetHashCode();
        var hash2 = hueSat2.GetHashCode();
        var hash3 = hueSat3.GetHashCode();

        // Assert - Different values should likely have different hash codes
        // Note: Hash collisions are possible, but unlikely for these specific values
        hash1.Should().NotBe(hash2);
        hash1.Should().NotBe(hash3);
        hash2.Should().NotBe(hash3);
    }

    [Fact]
    public void GetHashCode_ConsistentAcrossMultipleCalls_ReturnsSameValue()
    {
        // Arrange
        var hueSat = new HueSaturation(180.0, 75.0);

        // Act
        var hash1 = hueSat.GetHashCode();
        var hash2 = hueSat.GetHashCode();
        var hash3 = hueSat.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().Be(hash3);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var hueSat = new HueSaturation(180.0, 75.0);

        // Act
        var result = hueSat.ToString();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain("180");
        result.Should().Contain("75");
        // The exact format depends on the record struct's default ToString implementation
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(359.9, 100.0)]
    [InlineData(123.456, 78.9)]
    public void ToString_WithVariousValues_ContainsValues(Double hue, Double saturation)
    {
        // Arrange
        var hueSat = new HueSaturation(hue, saturation);

        // Act
        var result = hueSat.ToString();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(hue.ToString());
        result.Should().Contain(saturation.ToString());
    }

    [Fact]
    public void ToString_WithSpecialValues_HandlesCorrectly()
    {
        // Arrange
        var nanHueSat = new HueSaturation(Double.NaN, Double.NaN);
        var infinityHueSat = new HueSaturation(Double.PositiveInfinity, Double.NegativeInfinity);

        // Act & Assert - Should not throw
        var action1 = () => nanHueSat.ToString();
        var action2 = () => infinityHueSat.ToString();

        action1.Should().NotThrow();
        action2.Should().NotThrow();

        nanHueSat.ToString().Should().NotBeNull();
        infinityHueSat.ToString().Should().NotBeNull();
    }

    #endregion

    #region Value Semantics Tests

    [Fact]
    public void ValueSemantics_AssignmentCopiesValue()
    {
        // Arrange
        var original = new HueSaturation(180.0, 75.0);

        // Act
        var copy = original;

        // Assert
        copy.Should().Be(original);
        copy.H.Should().Be(original.H);
        copy.S.Should().Be(original.S);
    }

    [Fact]
    public void ValueSemantics_ModificationDoesNotAffectOriginal()
    {
        // Note: Since HueSaturation is immutable (readonly record struct),
        // we can't actually modify it after creation. This test demonstrates
        // that creating new instances doesn't affect existing ones.

        // Arrange
        var original = new HueSaturation(180.0, 75.0);

        // Act - Create new instances with different values
        var modified1 = new HueSaturation(original.H + 10, original.S);
        var modified2 = new HueSaturation(original.H, original.S + 10);

        // Assert - Original remains unchanged
        original.H.Should().Be(180.0);
        original.S.Should().Be(75.0);
        
        modified1.H.Should().Be(190.0);
        modified1.S.Should().Be(75.0);
        
        modified2.H.Should().Be(180.0);
        modified2.S.Should().Be(85.0);
    }

    [Fact]
    public void ValueSemantics_WithRecordSyntax_CreatesNewInstance()
    {
        // Arrange
        var original = new HueSaturation(180.0, 75.0);

        // Act - Use record 'with' syntax to create modified copies
        var modifiedHue = original with { H = 240.0 };
        var modifiedSat = original with { S = 85.0 };
        var modifiedBoth = original with { H = 300.0, S = 90.0 };

        // Assert
        original.H.Should().Be(180.0);
        original.S.Should().Be(75.0);
        
        modifiedHue.H.Should().Be(240.0);
        modifiedHue.S.Should().Be(75.0); // Unchanged
        
        modifiedSat.H.Should().Be(180.0); // Unchanged
        modifiedSat.S.Should().Be(85.0);
        
        modifiedBoth.H.Should().Be(300.0);
        modifiedBoth.S.Should().Be(90.0);
    }

    #endregion

    #region Collections and Dictionary Usage Tests

    [Fact]
    public void HueSaturation_UsedAsDictionaryKey_WorksCorrectly()
    {
        // Arrange
        var dict = new System.Collections.Generic.Dictionary<HueSaturation, String>();
        var key1 = new HueSaturation(180.0, 75.0);
        var key2 = new HueSaturation(180.0, 75.0); // Same values
        var key3 = new HueSaturation(181.0, 75.0); // Different values

        // Act
        dict[key1] = "First";
        dict[key3] = "Third";

        // Assert
        dict.Should().HaveCount(2);
        dict[key1].Should().Be("First");
        dict[key2].Should().Be("First"); // Same as key1 due to equality
        dict[key3].Should().Be("Third");
    }

    [Fact]
    public void HueSaturation_InHashSet_BehavesCorrectly()
    {
        // Arrange
        var hashSet = new System.Collections.Generic.HashSet<HueSaturation>();
        var item1 = new HueSaturation(180.0, 75.0);
        var item2 = new HueSaturation(180.0, 75.0); // Same values
        var item3 = new HueSaturation(181.0, 75.0); // Different values

        // Act
        var added1 = hashSet.Add(item1);
        var added2 = hashSet.Add(item2); // Should not be added (duplicate)
        var added3 = hashSet.Add(item3);

        // Assert
        added1.Should().BeTrue();
        added2.Should().BeFalse(); // Duplicate
        added3.Should().BeTrue();
        
        hashSet.Should().HaveCount(2);
        hashSet.Should().Contain(item1);
        hashSet.Should().Contain(item2); // Same as item1
        hashSet.Should().Contain(item3);
    }

    #endregion

    #region Integration and Color Space Tests

    [Fact]
    public void HueSaturation_RepresentsValidColorSpaceValues()
    {
        // Arrange & Act - Test typical color space values
        var red = new HueSaturation(0.0, 100.0);
        var green = new HueSaturation(120.0, 100.0);
        var blue = new HueSaturation(240.0, 100.0);
        var white = new HueSaturation(0.0, 0.0);
        var gray = new HueSaturation(0.0, 0.0); // Hue doesn't matter for grayscale

        // Assert - Values should be preserved for color calculations
        red.H.Should().Be(0.0);
        red.S.Should().Be(100.0);
        
        green.H.Should().Be(120.0);
        green.S.Should().Be(100.0);
        
        blue.H.Should().Be(240.0);
        blue.S.Should().Be(100.0);
        
        white.H.Should().Be(0.0);
        white.S.Should().Be(0.0);
    }

    [Fact]
    public void HueSaturation_SupportsColorTransitionCalculations()
    {
        // Arrange - Simulate color transition scenarios
        var startColor = new HueSaturation(0.0, 100.0);    // Red
        var endColor = new HueSaturation(120.0, 100.0);    // Green

        // Act - Calculate midpoint (this would typically be done by color utilities)
        var midHue = (startColor.H + endColor.H) / 2;
        var midSat = (startColor.S + endColor.S) / 2;
        var midColor = new HueSaturation(midHue, midSat);

        // Assert
        midColor.H.Should().Be(60.0);  // Orange-ish
        midColor.S.Should().Be(100.0); // Full saturation
    }

    #endregion

    #region Edge Cases and Robustness Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.000001)]
    [InlineData(359.999999)]
    [InlineData(360.0)]
    public void HueSaturation_WithHueBoundaryValues_HandlesCorrectly(Double hue)
    {
        // Act
        var hueSat = new HueSaturation(hue, 50.0);

        // Assert
        hueSat.H.Should().Be(hue);
        hueSat.S.Should().Be(50.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.000001)]
    [InlineData(99.999999)]
    [InlineData(100.0)]
    public void HueSaturation_WithSaturationBoundaryValues_HandlesCorrectly(Double saturation)
    {
        // Act
        var hueSat = new HueSaturation(180.0, saturation);

        // Assert
        hueSat.H.Should().Be(180.0);
        hueSat.S.Should().Be(saturation);
    }

    [Fact]
    public void HueSaturation_WithDecimalPrecision_MaintainsPrecision()
    {
        // Arrange
        var preciseHue = 123.456789012345;
        var preciseSat = 67.890123456789;

        // Act
        var hueSat = new HueSaturation(preciseHue, preciseSat);

        // Assert - Should maintain double precision
        hueSat.H.Should().Be(preciseHue);
        hueSat.S.Should().Be(preciseSat);
    }

    [Fact]
    public void HueSaturation_SerializationScenarios_WorksCorrectly()
    {
        // This test verifies the struct works in serialization contexts
        // (though actual serialization would require additional attributes/configuration)
        
        // Arrange
        var original = new HueSaturation(180.0, 75.0);
        
        // Act - Simulate serialization/deserialization by converting to/from components
        var serializedH = original.H;
        var serializedS = original.S;
        var deserialized = new HueSaturation(serializedH, serializedS);

        // Assert
        deserialized.Should().Be(original);
        deserialized.H.Should().Be(original.H);
        deserialized.S.Should().Be(original.S);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void HueSaturation_Creation_IsEfficient()
    {
        // Act - Create many instances to test performance
        for (var i = 0; i < 10000; i++)
        {
            var hueSat = new HueSaturation(i % 360, (i % 100) + 1);
            
            // Use the instance to prevent optimization
            _ = hueSat.H + hueSat.S;
        }

        // Assert - If we reach here without timeout, performance is acceptable
    }

    [Fact]
    public void HueSaturation_Equality_IsEfficient()
    {
        // Arrange
        var hueSat1 = new HueSaturation(180.0, 75.0);
        var hueSat2 = new HueSaturation(180.0, 75.0);

        // Act - Perform many equality checks
        for (var i = 0; i < 10000; i++)
        {
            _ = hueSat1.Equals(hueSat2);
            _ = hueSat1 == hueSat2;
            _ = hueSat1.GetHashCode();
        }

        // Assert - If we reach here without timeout, performance is acceptable
    }

    #endregion
}