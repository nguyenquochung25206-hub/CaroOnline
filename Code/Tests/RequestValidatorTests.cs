
using System;
using Xunit;
using Caro.Server.Validation;

// TV6 - Kiem thu RequestValidator
public class RequestValidatorTests
{
    // TEST 1: Tin nhan CHAT hop le
    [Fact]
    public void Chat_Valid_ReturnsTrue()
    {
        bool result = RequestValidator.ValidateChat(
            "Xin chao", out string error);

        Assert.True(result);
        Assert.Equal("", error);
    }

    // TEST 2: Tin nhan rong
    [Fact]
    public void Chat_Empty_ReturnsFalse()
    {
        bool result = RequestValidator.ValidateChat(
            "", out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // TEST 3: Tin nhan chi co khoang trang
    [Fact]
    public void Chat_Whitespace_ReturnsFalse()
    {
        bool result = RequestValidator.ValidateChat(
            "   ", out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // TEST 4: Tin nhan dung 500 ky tu
    [Fact]
    public void Chat_500Characters_ReturnsTrue()
    {
        string content = new string('A', 500);

        bool result = RequestValidator.ValidateChat(
            content, out string error);

        Assert.True(result);
        Assert.Equal("", error);
    }

    // TEST 5: Tin nhan vuot qua 500 ky tu
    [Fact]
    public void Chat_501Characters_ReturnsFalse()
    {
        string content = new string('A', 501);

        bool result = RequestValidator.ValidateChat(
            content, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // TEST 6: Ma phong hop le
    [Fact]
    public void RoomId_Valid_ReturnsTrue()
    {
        bool result = RequestValidator.ValidateRoomId(
            "ROOM001", out string error);

        Assert.True(result);
        Assert.Equal("", error);
    }

    // TEST 7: Ma phong rong
    [Fact]
    public void RoomId_Empty_ReturnsFalse()
    {
        bool result = RequestValidator.ValidateRoomId(
            "", out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // TEST 8: Ma phong chi co khoang trang
    [Fact]
    public void RoomId_Whitespace_ReturnsFalse()
    {
        bool result = RequestValidator.ValidateRoomId(
            "   ", out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }
}
