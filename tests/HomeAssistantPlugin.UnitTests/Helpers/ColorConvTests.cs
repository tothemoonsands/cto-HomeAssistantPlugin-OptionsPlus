using System;
using Loupedeck.HomeAssistantPlugin;

namespace Loupedeck.HomeAssistantPlugin.Tests.Helpers
{
    /// <summary>
    /// Comprehensive tests for ColorConv color utility methods.
    /// Covers CIE XYZ color space conversions, sRGB gamma correction, and brightness scaling.
    /// </summary>
    public class ColorConvTests
    {
        #region XyBriToRgb Tests

        [Fact]
        public void XyBriToRgb_StandardWhitePoint_ReturnsWhite()
        {
            // Arrange - CIE 1931 standard illuminant D65 white point
            var x = 0.3127;
            var y = 0.3290;
            var brightness = 255;

            // Act
            var (r, g, b) = ColorConv.XyBriToRgb(x, y, brightness);

            // Assert - Should produce white or very close to white
            r.Should().BeGreaterThan(200); // Allow for slight variations due to color space conversion
            g.Should().BeGreaterThan(200);
            b.Should().BeGreaterThan(200);
            
            // RGB components should be reasonably balanced for white
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            (max - min).Should().BeLessThan(50); // Should be fairly balanced
        }

        [Theory]
        [InlineData(0.7006, 0.2993, 255)] // Red-ish xy coordinates
        [InlineData(0.1724, 0.7468, 255)] // Green-ish xy coordinates  
        [InlineData(0.1270, 0.0346, 255)] // Blue-ish xy coordinates
        public void XyBriToRgb_KnownColorCoordinates_ProducesExpectedColors(double x, double y, int brightness)
        {
            // Act
            var (r, g, b) = ColorConv.XyBriToRgb(x, y, brightness);

            // Assert - Should produce valid RGB values
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);

            // At full brightness, at least one component should be reasonably high
            var maxComponent = Math.Max(r, Math.Max(g, b));
            maxComponent.Should().BeGreaterThan(100);
        }

        [Theory]
        [InlineData(0.3127, 0.3290, 0)]   // Zero brightness
        [InlineData(0.3127, 0.3290, 64)]  // 25% brightness
        [InlineData(0.3127, 0.3290, 128)] // 50% brightness
        [InlineData(0.3127, 0.3290, 192)] // 75% brightness
        [InlineData(0.3127, 0.3290, 255)] // Full brightness
        public void XyBriToRgb_VariousBrightnessLevels_ScalesCorrectly(double x, double y, int brightness)
        {
            // Act
            var (r, g, b) = ColorConv.XyBriToRgb(x, y, brightness);

            // Assert
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);

            if (brightness == 0)
            {
                r.Should().Be(0);
                g.Should().Be(0);
                b.Should().Be(0);
            }
            else
            {
                // At least one component should be proportional to brightness
                var maxComponent = Math.Max(r, Math.Max(g, b));
                maxComponent.Should().BeGreaterThan(0);
            }
        }

        [Theory]
        [InlineData(-0.1, 0.3290, 128)]   // Negative x
        [InlineData(0.3127, -0.1, 128)]   // Negative y
        [InlineData(1.1, 0.3290, 128)]    // x > 1
        [InlineData(0.3127, 1.1, 128)]    // y > 1
        [InlineData(0.0001, 0.9999, 128)] // Edge values
        [InlineData(0.9999, 0.0001, 128)] // Edge values
        public void XyBriToRgb_OutOfRangeCoordinates_ClampsAndHandlesGracefully(double x, double y, int brightness)
        {
            // Act & Assert - Should not throw exception
            var action = () => ColorConv.XyBriToRgb(x, y, brightness);
            action.Should().NotThrow();

            var (r, g, b) = ColorConv.XyBriToRgb(x, y, brightness);
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);
        }

        [Theory]
        [InlineData(0.3127, 0.3290, -10)]  // Negative brightness
        [InlineData(0.3127, 0.3290, 300)]  // Brightness > 255
        public void XyBriToRgb_OutOfRangeBrightness_ClampsCorrectly(double x, double y, int brightness)
        {
            // Act
            var (r, g, b) = ColorConv.XyBriToRgb(x, y, brightness);

            // Assert
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);
        }

        #endregion

        #region SrgbToLinear01 Tests

        [Theory]
        [InlineData(0.0, 0.0)]           // Black
        [InlineData(1.0, 1.0)]           // White
        [InlineData(0.5, 0.214041140)]   // Mid gray (approximate)
        [InlineData(0.04045, 0.00313080)] // Threshold value
        [InlineData(0.8, 0.603827338)]   // Light value
        [InlineData(0.2, 0.033104766)]   // Dark value
        public void SrgbToLinear01_KnownValues_ReturnsExpectedLinear(double srgb, double expectedLinear)
        {
            // Act
            var result = ColorConv.SrgbToLinear01(srgb);

            // Assert
            result.Should().BeApproximately(expectedLinear, 0.000001);
        }

        [Theory]
        [InlineData(-0.1)]  // Negative value
        [InlineData(1.1)]   // Value > 1
        [InlineData(0.0)]   // Minimum
        [InlineData(1.0)]   // Maximum
        public void SrgbToLinear01_BoundaryValues_HandlesCorrectly(double srgb)
        {
            // Act & Assert - Should not throw exception
            var action = () => ColorConv.SrgbToLinear01(srgb);
            action.Should().NotThrow();

            var result = ColorConv.SrgbToLinear01(srgb);
            // Note: Implementation allows negative results for negative inputs - this is mathematically correct
            // The gamma correction formula can produce negative linear values for negative sRGB inputs
            result.Should().NotBe(double.NaN, "Should not return NaN for valid inputs");
            result.Should().NotBe(double.PositiveInfinity, "Should not return positive infinity");
            result.Should().NotBe(double.NegativeInfinity, "Should not return negative infinity");
        }

        #endregion

        #region LinearToSrgb01 Tests

        [Theory]
        [InlineData(0.0, 0.0)]           // Black
        [InlineData(1.0, 1.0)]           // White
        [InlineData(0.214041140, 0.5)]   // Mid gray (approximate reverse)
        [InlineData(0.00313080, 0.04045)] // Threshold value (approximate)
        [InlineData(0.603827338, 0.8)]   // Light value (approximate reverse)
        [InlineData(0.033104766, 0.2)]   // Dark value (approximate reverse)
        public void LinearToSrgb01_KnownValues_ReturnsExpectedSrgb(double linear, double expectedSrgb)
        {
            // Act
            var result = ColorConv.LinearToSrgb01(linear);

            // Assert
            result.Should().BeApproximately(expectedSrgb, 0.000001);
        }

        [Theory]
        [InlineData(-0.1)]  // Negative value
        [InlineData(1.5)]   // Value > 1
        [InlineData(0.0)]   // Minimum
        [InlineData(1.0)]   // Maximum
        public void LinearToSrgb01_BoundaryValues_ClampsAndHandlesCorrectly(double linear)
        {
            // Act & Assert - Should not throw exception
            var action = () => ColorConv.LinearToSrgb01(linear);
            action.Should().NotThrow();

            var result = ColorConv.LinearToSrgb01(linear);
            result.Should().BeInRange(0.0, 1.0);
        }

        #endregion

        #region Round-trip Conversion Tests

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.2)]
        [InlineData(0.4)]
        [InlineData(0.5)]
        [InlineData(0.6)]
        [InlineData(0.8)]
        [InlineData(1.0)]
        public void SrgbToLinear_LinearToSrgb_RoundTrip_MaintainsAccuracy(double originalSrgb)
        {
            // Arrange
            const double tolerance = 0.000001;

            // Act - Convert sRGB -> Linear -> sRGB
            var linear = ColorConv.SrgbToLinear01(originalSrgb);
            var convertedSrgb = ColorConv.LinearToSrgb01(linear);

            // Assert
            convertedSrgb.Should().BeApproximately(originalSrgb, tolerance);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.1)]
        [InlineData(0.3)]
        [InlineData(0.5)]
        [InlineData(0.7)]
        [InlineData(0.9)]
        [InlineData(1.0)]
        public void LinearToSrgb_SrgbToLinear_RoundTrip_MaintainsAccuracy(double originalLinear)
        {
            // Arrange
            const double tolerance = 0.000001;

            // Act - Convert Linear -> sRGB -> Linear
            var srgb = ColorConv.LinearToSrgb01(originalLinear);
            var convertedLinear = ColorConv.SrgbToLinear01(srgb);

            // Assert
            convertedLinear.Should().BeApproximately(originalLinear, tolerance);
        }

        #endregion

        #region ApplyBrightnessLinear Tests

        [Fact]
        public void ApplyBrightnessLinear_FullBrightness_ReturnsSameColor()
        {
            // Arrange
            var inputColor = (R: 128, G: 64, B: 192);
            var brightness = 255;

            // Act
            var result = ColorConv.ApplyBrightnessLinear(inputColor, brightness);

            // Assert - Should return same or very similar color
            result.R.Should().BeCloseTo(inputColor.R, 2); // Allow small rounding differences
            result.G.Should().BeCloseTo(inputColor.G, 2);
            result.B.Should().BeCloseTo(inputColor.B, 2);
        }

        [Fact]
        public void ApplyBrightnessLinear_ZeroBrightness_ReturnsBlack()
        {
            // Arrange
            var inputColor = (R: 200, G: 150, B: 100);
            var brightness = 0;

            // Act
            var result = ColorConv.ApplyBrightnessLinear(inputColor, brightness);

            // Assert
            result.R.Should().Be(0);
            result.G.Should().Be(0);
            result.B.Should().Be(0);
        }

        [Theory]
        [InlineData(255, 0, 0, 128)]    // Red at 50% brightness
        [InlineData(0, 255, 0, 128)]    // Green at 50% brightness
        [InlineData(0, 0, 255, 128)]    // Blue at 50% brightness
        [InlineData(255, 255, 255, 128)] // White at 50% brightness
        [InlineData(128, 128, 128, 64)] // Gray at 25% brightness
        public void ApplyBrightnessLinear_VariousColorsAndBrightness_ScalesCorrectly(
            int r, int g, int b, int brightness)
        {
            // Arrange
            var inputColor = (R: r, G: g, B: b);

            // Act
            var result = ColorConv.ApplyBrightnessLinear(inputColor, brightness);

            // Assert
            result.R.Should().BeInRange(0, 255);
            result.G.Should().BeInRange(0, 255);
            result.B.Should().BeInRange(0, 255);

            // Verify scaling is proportional (darker than original)
            if (brightness < 255)
            {
                result.R.Should().BeLessOrEqualTo(r);
                result.G.Should().BeLessOrEqualTo(g);
                result.B.Should().BeLessOrEqualTo(b);
            }
        }

        [Theory]
        [InlineData(200, 150, 100, 64)]   // 25% brightness
        [InlineData(200, 150, 100, 128)]  // 50% brightness
        [InlineData(200, 150, 100, 192)]  // 75% brightness
        public void ApplyBrightnessLinear_DifferentBrightnessLevels_ProducesMonotonicResults(
            int r, int g, int b, int brightness)
        {
            // Arrange
            var inputColor = (R: r, G: g, B: b);
            
            // Act - Get results for current brightness and slightly higher
            var result1 = ColorConv.ApplyBrightnessLinear(inputColor, brightness);
            var result2 = ColorConv.ApplyBrightnessLinear(inputColor, brightness + 32);

            // Assert - Higher brightness should produce brighter or equal results
            result2.R.Should().BeGreaterOrEqualTo(result1.R);
            result2.G.Should().BeGreaterOrEqualTo(result1.G);
            result2.B.Should().BeGreaterOrEqualTo(result1.B);
        }

        [Theory]
        [InlineData(100, 50, 25, -10)]  // Negative brightness
        [InlineData(100, 50, 25, 300)]  // Brightness > 255
        public void ApplyBrightnessLinear_OutOfRangeBrightness_ClampsCorrectly(
            int r, int g, int b, int brightness)
        {
            // Arrange
            var inputColor = (R: r, G: g, B: b);

            // Act & Assert - Should not throw exception
            var action = () => ColorConv.ApplyBrightnessLinear(inputColor, brightness);
            action.Should().NotThrow();

            var result = ColorConv.ApplyBrightnessLinear(inputColor, brightness);
            result.R.Should().BeInRange(0, 255);
            result.G.Should().BeInRange(0, 255);
            result.B.Should().BeInRange(0, 255);
        }

        #endregion

        #region Gamma Correction Mathematical Tests

        [Fact]
        public void GammaCorrection_LinearSpace_PreservesRelativeIntensity()
        {
            // Arrange - Test that linear space operations preserve perceptual relationships
            var darkGray = (R: 64, G: 64, B: 64);
            var lightGray = (R: 192, G: 192, B: 192);
            var brightness = 128; // 50%

            // Act
            var darkResult = ColorConv.ApplyBrightnessLinear(darkGray, brightness);
            var lightResult = ColorConv.ApplyBrightnessLinear(lightGray, brightness);

            // Assert - Light gray should still be lighter than dark gray after scaling
            var darkLuminance = darkResult.R + darkResult.G + darkResult.B;
            var lightLuminance = lightResult.R + lightResult.G + lightResult.B;
            lightLuminance.Should().BeGreaterThan(darkLuminance);
        }

        [Theory]
        [InlineData(0.0)]    // Black
        [InlineData(0.04)]   // Near threshold
        [InlineData(0.045)]  // Just above threshold
        [InlineData(0.5)]    // Mid-range
        [InlineData(1.0)]    // White
        public void SrgbGammaCorrection_FollowsIecStandard(double srgbValue)
        {
            // Act
            var linear = ColorConv.SrgbToLinear01(srgbValue);
            var backToSrgb = ColorConv.LinearToSrgb01(linear);

            // Assert - Should follow IEC 61966-2-1 standard precisely
            backToSrgb.Should().BeApproximately(srgbValue, 0.000001);

            // Verify threshold behavior
            if (srgbValue <= 0.04045)
            {
                // Linear portion: C_linear = C_srgb / 12.92
                var expectedLinear = srgbValue / 12.92;
                linear.Should().BeApproximately(expectedLinear, 0.000001);
            }
            else
            {
                // Gamma portion: C_linear = ((C_srgb + 0.055) / 1.055) ^ 2.4
                var expectedLinear = Math.Pow((srgbValue + 0.055) / 1.055, 2.4);
                linear.Should().BeApproximately(expectedLinear, 0.000001);
            }
        }

        #endregion

        #region Edge Cases and Special Values

        [Fact]
        public void XyBriToRgb_ExtremeCoordinates_HandlesGracefully()
        {
            // Arrange - Test coordinates at the edge of the visible spectrum
            var extremeCoordinates = new[]
            {
                (x: 0.0001, y: 0.0001), // Near origin
                (x: 0.9999, y: 0.0001), // Far red
                (x: 0.0001, y: 0.9999), // Far green
                (x: 0.7347, y: 0.2653), // Deep red
                (x: 0.0001, y: 0.9999)  // Deep green
            };

            foreach (var (x, y) in extremeCoordinates)
            {
                // Act & Assert - Should not throw and produce valid output
                var action = () => ColorConv.XyBriToRgb(x, y, 255);
                action.Should().NotThrow();

                var (r, g, b) = ColorConv.XyBriToRgb(x, y, 255);
                r.Should().BeInRange(0, 255);
                g.Should().BeInRange(0, 255);
                b.Should().BeInRange(0, 255);
            }
        }

        [Theory]
        [InlineData(Double.NaN)]
        [InlineData(Double.PositiveInfinity)]
        [InlineData(Double.NegativeInfinity)]
        public void ColorConversion_InvalidDoubleValues_HandlesGracefully(double invalidValue)
        {
            // Act & Assert - Should not throw exceptions with special double values
            var action1 = () => ColorConv.SrgbToLinear01(invalidValue);
            action1.Should().NotThrow();

            var action2 = () => ColorConv.LinearToSrgb01(invalidValue);
            action2.Should().NotThrow();

            var action3 = () => ColorConv.XyBriToRgb(invalidValue, 0.3290, 128);
            action3.Should().NotThrow();

            var action4 = () => ColorConv.XyBriToRgb(0.3127, invalidValue, 128);
            action4.Should().NotThrow();
        }

        #endregion

        #region Performance and Consistency Tests

        [Fact]
        public void ColorConversion_MultipleOperations_RemainsConsistent()
        {
            // Arrange - Test consistency across multiple operations
            var testColor = (R: 123, G: 87, B: 210);
            var brightness = 180;

            // Act - Perform same operation multiple times
            var results = new (int R, int G, int B)[10];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = ColorConv.ApplyBrightnessLinear(testColor, brightness);
            }

            // Assert - All results should be identical
            for (int i = 1; i < results.Length; i++)
            {
                results[i].R.Should().Be(results[0].R);
                results[i].G.Should().Be(results[0].G);
                results[i].B.Should().Be(results[0].B);
            }
        }

        #endregion
    }
}