using PortManager.Utils;

namespace PortManager.Core.Tests.Utils;

public class DateTimeUtilsTests
{
    [Fact]
    public void GetTimeAgo_ReturnsJustNow_WhenLessThanOneMinute()
    {
        // Arrange
        var dateTime = DateTime.Now.AddSeconds(-30);

        // Act
        var result = DateTimeUtils.GetTimeAgo(dateTime);

        // Assert
        Assert.Equal("刚刚", result);
    }

    [Fact]
    public void GetTimeAgo_ReturnsMinutes_WhenLessThanOneHour()
    {
        // Arrange
        var dateTime = DateTime.Now.AddMinutes(-5);

        // Act
        var result = DateTimeUtils.GetTimeAgo(dateTime);

        // Assert
        Assert.Equal("5分钟前", result);
    }

    [Fact]
    public void GetTimeAgo_ReturnsHours_WhenLessThanOneDay()
    {
        // Arrange
        var dateTime = DateTime.Now.AddHours(-3);

        // Act
        var result = DateTimeUtils.GetTimeAgo(dateTime);

        // Assert
        Assert.Equal("3小时前", result);
    }

    [Fact]
    public void GetTimeAgo_ReturnsDays_WhenMoreThanOneDay()
    {
        // Arrange
        var dateTime = DateTime.Now.AddDays(-2);

        // Act
        var result = DateTimeUtils.GetTimeAgo(dateTime);

        // Assert
        Assert.Equal("2天前", result);
    }

    [Fact]
    public void GetUptime_ReturnsMinutes_WhenLessThanOneHour()
    {
        // Arrange
        var startTime = DateTime.Now.AddMinutes(-45);

        // Act
        var result = DateTimeUtils.GetUptime(startTime);

        // Assert
        Assert.Equal("45分钟", result);
    }

    [Fact]
    public void GetUptime_ReturnsHoursAndMinutes_WhenLessThanOneDay()
    {
        // Arrange
        var startTime = DateTime.Now.AddHours(-5).AddMinutes(-30);

        // Act
        var result = DateTimeUtils.GetUptime(startTime);

        // Assert
        Assert.Equal("5小时30分钟", result);
    }

    [Fact]
    public void GetUptime_ReturnsDaysAndHours_WhenMoreThanOneDay()
    {
        // Arrange
        var startTime = DateTime.Now.AddDays(-3).AddHours(-5);

        // Act
        var result = DateTimeUtils.GetUptime(startTime);

        // Assert
        Assert.Equal("3天5小时", result);
    }
}
