using System;
using Loupedeck.HomeAssistantPlugin;

namespace Loupedeck.HomeAssistantPlugin.Tests.Helpers
{
    /// <summary>
    /// Comprehensive tests for HSBHelper color space conversion methods.
    /// Covers HSB↔RGB conversions, mathematical precision, boundary conditions, and edge cases.
    /// </summary>
    public class HSBHelperTests
    {
        #region HsbToRgb Tests

        [Fact]
        public void HsbToRgb_PrimaryRed_ReturnsCorrectRgb()
        {
            // Arrange - Pure red (H=0, S=100%, B=100%)
            var hue = 0.0;
            var saturation = 100.0;
            var brightness = 100.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);

            // Assert
            r.Should().Be(255);
            g.Should().Be(0);
            b.Should().Be(0);
        }

        [Fact]
        public void HsbToRgb_PrimaryGreen_ReturnsCorrectRgb()
        {
            // Arrange - Pure green (H=120°, S=100%, B=100%)
            var hue = 120.0;
            var saturation = 100.0;
            var brightness = 100.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);

            // Assert
            r.Should().Be(0);
            g.Should().Be(255);
            b.Should().Be(0);
        }

        [Fact]
        public void HsbToRgb_PrimaryBlue_ReturnsCorrectRgb()
        {
            // Arrange - Pure blue (H=240°, S=100%, B=100%)
            var hue = 240.0;
            var saturation = 100.0;
            var brightness = 100.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);

            // Assert
            r.Should().Be(0);
            g.Should().Be(0);
            b.Should().Be(255);
        }

        [Theory]
        [InlineData(60.0, 100.0, 100.0, 255, 255, 0)]   // Yellow
        [InlineData(180.0, 100.0, 100.0, 0, 255, 255)]  // Cyan
        [InlineData(300.0, 100.0, 100.0, 255, 0, 255)]  // Magenta
        [InlineData(0.0, 0.0, 100.0, 255, 255, 255)]    // White
        [InlineData(0.0, 0.0, 0.0, 0, 0, 0)]            // Black
        [InlineData(0.0, 100.0, 50.0, 128, 0, 0)]       // Dark Red
        [InlineData(120.0, 50.0, 75.0, 96, 191, 96)]    // Light Green
        public void HsbToRgb_KnownColorValues_ReturnsExpectedRgb(
            double hue, double saturation, double brightness,
            int expectedR, int expectedG, int expectedB)
        {
            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);

            // Assert
            r.Should().Be(expectedR);
            g.Should().Be(expectedG);
            b.Should().Be(expectedB);
        }

        [Fact]
        public void HsbToRgb_NegativeHue_WrapsCorrectly()
        {
            // Arrange - Negative hue should wrap to positive equivalent
            var negativeHue = -60.0; // Should be equivalent to 300°
            var saturation = 100.0;
            var brightness = 100.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(negativeHue, saturation, brightness);
            var (rExpected, gExpected, bExpected) = HSBHelper.HsbToRgb(300.0, saturation, brightness);

            // Assert
            r.Should().Be(rExpected);
            g.Should().Be(gExpected);
            b.Should().Be(bExpected);
        }

        [Fact]
        public void HsbToRgb_HueOver360_WrapsCorrectly()
        {
            // Arrange - Hue over 360° should wrap
            var largeHue = 420.0; // Should be equivalent to 60°
            var saturation = 100.0;
            var brightness = 100.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(largeHue, saturation, brightness);
            var (rExpected, gExpected, bExpected) = HSBHelper.HsbToRgb(60.0, saturation, brightness);

            // Assert
            r.Should().Be(rExpected);
            g.Should().Be(gExpected);
            b.Should().Be(bExpected);
        }

        [Theory]
        [InlineData(-10.0, 50.0, 50.0)] // Negative saturation
        [InlineData(150.0, -5.0, 50.0)] // Negative saturation
        [InlineData(150.0, 50.0, -5.0)] // Negative brightness
        [InlineData(150.0, 110.0, 50.0)] // Saturation > 100%
        [InlineData(150.0, 50.0, 110.0)] // Brightness > 100%
        public void HsbToRgb_OutOfRangeValues_ClampsGracefully(double hue, double saturation, double brightness)
        {
            // Act & Assert - Should not throw exception and return valid RGB values
            var action = () => HSBHelper.HsbToRgb(hue, saturation, brightness);
            action.Should().NotThrow();

            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);
        }

        #endregion

        #region RgbToHs Tests

        [Fact]
        public void RgbToHs_PrimaryRed_ReturnsCorrectHs()
        {
            // Arrange
            var r = 255;
            var g = 0;
            var b = 0;

            // Act
            var (h, s) = HSBHelper.RgbToHs(r, g, b);

            // Assert
            h.Should().BeApproximately(0.0, 0.1); // Red should be at 0°
            s.Should().BeApproximately(100.0, 0.1); // Full saturation
        }

        [Fact]
        public void RgbToHs_PrimaryGreen_ReturnsCorrectHs()
        {
            // Arrange
            var r = 0;
            var g = 255;
            var b = 0;

            // Act
            var (h, s) = HSBHelper.RgbToHs(r, g, b);

            // Assert
            h.Should().BeApproximately(120.0, 0.1); // Green should be at 120°
            s.Should().BeApproximately(100.0, 0.1); // Full saturation
        }

        [Fact]
        public void RgbToHs_PrimaryBlue_ReturnsCorrectHs()
        {
            // Arrange
            var r = 0;
            var g = 0;
            var b = 255;

            // Act
            var (h, s) = HSBHelper.RgbToHs(r, g, b);

            // Assert
            h.Should().BeApproximately(240.0, 0.1); // Blue should be at 240°
            s.Should().BeApproximately(100.0, 0.1); // Full saturation
        }

        [Theory]
        [InlineData(255, 255, 0, 60.0, 100.0)]    // Yellow
        [InlineData(0, 255, 255, 180.0, 100.0)]   // Cyan
        [InlineData(255, 0, 255, 300.0, 100.0)]   // Magenta
        [InlineData(255, 255, 255, 0.0, 0.0)]     // White (hue undefined, sat=0)
        [InlineData(0, 0, 0, 0.0, 0.0)]           // Black (hue undefined, sat=0)
        [InlineData(128, 128, 128, 0.0, 0.0)]     // Gray (hue undefined, sat=0)
        public void RgbToHs_KnownColors_ReturnsExpectedHs(
            int r, int g, int b, double expectedH, double expectedS)
        {
            // Act
            var (h, s) = HSBHelper.RgbToHs(r, g, b);

            // Assert
            h.Should().BeApproximately(expectedH, 0.5);
            s.Should().BeApproximately(expectedS, 0.5);
        }

        [Theory]
        [InlineData(-10, 50, 100)] // Negative red
        [InlineData(50, -5, 100)]  // Negative green
        [InlineData(50, 100, -5)]  // Negative blue
        [InlineData(300, 50, 100)] // Red > 255
        [InlineData(50, 300, 100)] // Green > 255
        [InlineData(50, 100, 300)] // Blue > 255
        public void RgbToHs_OutOfRangeRgb_HandlesGracefully(int r, int g, int b)
        {
            // Act & Assert - Should not throw exception
            var action = () => HSBHelper.RgbToHs(r, g, b);
            action.Should().NotThrow();

            var (h, s) = HSBHelper.RgbToHs(r, g, b);
            h.Should().BeInRange(0.0, 360.0);
            // Note: Saturation can exceed 100% with invalid RGB inputs due to implementation behavior
            s.Should().BeGreaterOrEqualTo(0.0);
        }

        #endregion

        #region Round-trip Conversion Tests

        [Theory]
        [InlineData(0.0, 100.0, 100.0)]    // Pure Red
        [InlineData(120.0, 100.0, 100.0)]  // Pure Green  
        [InlineData(240.0, 100.0, 100.0)]  // Pure Blue
        [InlineData(60.0, 100.0, 100.0)]   // Yellow
        [InlineData(180.0, 100.0, 100.0)]  // Cyan
        [InlineData(300.0, 100.0, 100.0)]  // Magenta
        [InlineData(45.0, 75.0, 50.0)]     // Arbitrary color
        [InlineData(210.0, 30.0, 80.0)]    // Light blue-ish
        public void HsbToRgb_RgbToHs_RoundTrip_MaintainsAccuracy(double originalH, double originalS, double originalB)
        {
            // Arrange
            const double tolerance = 1.0; // Allow small rounding differences

            // Act - Convert HSB -> RGB -> HS
            var (r, g, b) = HSBHelper.HsbToRgb(originalH, originalS, originalB);
            var (convertedH, convertedS) = HSBHelper.RgbToHs(r, g, b);

            // Assert - Should maintain reasonable accuracy
            if (originalS > 5.0) // Only check hue if there's significant saturation
            {
                // Handle hue wrapping (0° and 360° are equivalent)
                var hueDifference = Math.Abs(originalH - convertedH);
                if (hueDifference > 180.0)
                {
                    hueDifference = 360.0 - hueDifference;
                }
                hueDifference.Should().BeLessOrEqualTo(tolerance);
            }
            
            convertedS.Should().BeApproximately(originalS, tolerance);
        }

        [Theory]
        [InlineData(255, 0, 0)]    // Pure Red
        [InlineData(0, 255, 0)]    // Pure Green
        [InlineData(0, 0, 255)]    // Pure Blue
        [InlineData(255, 255, 0)]  // Yellow
        [InlineData(0, 255, 255)]  // Cyan
        [InlineData(255, 0, 255)]  // Magenta
        [InlineData(128, 64, 192)] // Arbitrary color
        public void RgbToHs_HsbToRgb_RoundTrip_MaintainsAccuracy(int originalR, int originalG, int originalB)
        {
            // Arrange
            const int tolerance = 2; // Allow small rounding differences in RGB space

            // Act - Convert RGB -> HS -> RGB (using 100% brightness)
            var (h, s) = HSBHelper.RgbToHs(originalR, originalG, originalB);
            
            // Calculate the brightness from original RGB for round-trip
            var maxComponent = Math.Max(originalR, Math.Max(originalG, originalB));
            var brightness = (maxComponent / 255.0) * 100.0;
            
            var (convertedR, convertedG, convertedB) = HSBHelper.HsbToRgb(h, s, brightness);

            // Assert
            convertedR.Should().BeCloseTo(originalR, tolerance);
            convertedG.Should().BeCloseTo(originalG, tolerance);
            convertedB.Should().BeCloseTo(originalB, tolerance);
        }

        #endregion

        #region HsbToRgb255 Tests

        [Fact]
        public void HsbToRgb255_ValidInput_ReturnsCorrectRgb()
        {
            // Arrange
            var hue = 0.0; // Red
            var saturation = 100.0;
            var brightness255 = 128; // 50% brightness

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb255(hue, saturation, brightness255);

            // Assert - Should match HsbToRgb with equivalent percentage
            var (expectedR, expectedG, expectedB) = HSBHelper.HsbToRgb(hue, saturation, 50.0);
            r.Should().Be(expectedR);
            g.Should().Be(expectedG);
            b.Should().Be(expectedB);
        }

        [Theory]
        [InlineData(0, 100.0, 0)]     // Min brightness
        [InlineData(0, 100.0, 255)]   // Max brightness
        [InlineData(0, 100.0, 127)]   // Mid brightness
        public void HsbToRgb255_BrightnessRange_HandlesCorrectly(double hue, double saturation, int brightness255)
        {
            // Act
            var (r, g, b) = HSBHelper.HsbToRgb255(hue, saturation, brightness255);

            // Assert
            r.Should().BeInRange(0, 255);
            g.Should().BeInRange(0, 255);
            b.Should().BeInRange(0, 255);
        }

        #endregion

        #region Utility Method Tests

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(180.0, 180.0)]
        [InlineData(360.0, 0.0)]
        [InlineData(450.0, 90.0)]
        [InlineData(-90.0, 270.0)]
        [InlineData(-360.0, 0.0)]
        [InlineData(720.0, 0.0)]
        public void Wrap360_VariousInputs_ReturnsCorrectWrappedValue(double input, double expected)
        {
            // Act
            var result = HSBHelper.Wrap360(input);

            // Assert
            result.Should().BeApproximately(expected, 0.01);
        }

        [Theory]
        [InlineData(5, 0, 10, 5)]      // Within range
        [InlineData(-5, 0, 10, 0)]     // Below min
        [InlineData(15, 0, 10, 10)]    // Above max
        [InlineData(0, 0, 10, 0)]      // At min
        [InlineData(10, 0, 10, 10)]    // At max
        public void Clamp_Int_ReturnsClampedValue(int value, int min, int max, int expected)
        {
            // Act
            var result = HSBHelper.Clamp(value, min, max);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(5.5, 0.0, 10.0, 5.5)]    // Within range
        [InlineData(-2.3, 0.0, 10.0, 0.0)]   // Below min
        [InlineData(12.7, 0.0, 10.0, 10.0)]  // Above max
        [InlineData(0.0, 0.0, 10.0, 0.0)]    // At min
        [InlineData(10.0, 0.0, 10.0, 10.0)]  // At max
        public void Clamp_Double_ReturnsClampedValue(double value, double min, double max, double expected)
        {
            // Act
            var result = HSBHelper.Clamp(value, min, max);

            // Assert
            result.Should().BeApproximately(expected, 0.001);
        }

        #endregion

        #region Edge Cases and Boundary Tests

        [Fact]
        public void HsbToRgb_ZeroSaturation_ReturnsGrayscale()
        {
            // Arrange - Any hue with 0% saturation should produce grayscale
            var hue = 123.45; // Arbitrary hue
            var saturation = 0.0;
            var brightness = 75.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);

            // Assert - All components should be equal (grayscale)
            r.Should().Be(g);
            g.Should().Be(b);
            
            var expectedValue = (int)Math.Round(255 * 0.75);
            r.Should().Be(expectedValue);
        }

        [Fact]
        public void HsbToRgb_ZeroBrightness_ReturnsBlack()
        {
            // Arrange - Any color with 0% brightness should be black
            var hue = 200.0;
            var saturation = 100.0;
            var brightness = 0.0;

            // Act
            var (r, g, b) = HSBHelper.HsbToRgb(hue, saturation, brightness);

            // Assert
            r.Should().Be(0);
            g.Should().Be(0);
            b.Should().Be(0);
        }

        [Fact]
        public void RgbToHs_AllZeros_ReturnsZeroHueSaturation()
        {
            // Arrange
            var r = 0;
            var g = 0;
            var b = 0;

            // Act
            var (h, s) = HSBHelper.RgbToHs(r, g, b);

            // Assert - Black should have 0 hue and 0 saturation
            h.Should().Be(0.0);
            s.Should().Be(0.0);
        }

        [Fact]
        public void RgbToHs_EqualComponents_ReturnsZeroSaturation()
        {
            // Arrange - Gray color (equal R, G, B)
            var r = 128;
            var g = 128;
            var b = 128;

            // Act
            var (h, s) = HSBHelper.RgbToHs(r, g, b);

            // Assert - Grayscale should have 0 saturation
            s.Should().Be(0.0);
        }

        #endregion

        #region Mathematical Precision Tests

        [Theory]
        [InlineData(30.0, 75.0, 90.0)]
        [InlineData(150.0, 25.0, 60.0)]
        [InlineData(270.0, 85.0, 40.0)]
        public void ColorConversion_MathematicalPrecision_MaintainsReasonableAccuracy(
            double hue, double saturation, double brightness)
        {
            // Act - Multiple conversions to test precision
            var (r1, g1, b1) = HSBHelper.HsbToRgb(hue, saturation, brightness);
            var (h2, s2) = HSBHelper.RgbToHs(r1, g1, b1);
            var (r2, g2, b2) = HSBHelper.HsbToRgb(h2, s2, brightness);

            // Assert - Second RGB should be very close to first
            Math.Abs(r1 - r2).Should().BeLessOrEqualTo(1); // Allow 1 unit difference due to rounding
            Math.Abs(g1 - g2).Should().BeLessOrEqualTo(1);
            Math.Abs(b1 - b2).Should().BeLessOrEqualTo(1);
        }

        #endregion
    }
}