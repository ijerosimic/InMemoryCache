using Xunit;
using FluentAssertions;
using InMemoryCache;
using Microsoft.Extensions.Options;

namespace InMemoryCacheTests;

public class InMemoryCacheTests
{
    private readonly (string key, object value) _kvp1 = ("key-1", new { Val = "value-1" });
    private readonly (string key, object value) _kvp2 = ("key-2", new { Val = "value-2" });
    private readonly (string key, object value) _kvp3 = ("key-3", new { Val = "value-3" });
    private readonly (string key, object value) _kvp4 = ("key-4", new { Val = "value-4" });

    [Fact]
    public void It_Sets_MaxItems_Correctly()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 3 });
        var sut = new InMemoryCache<object>(mockOptions);
        sut.GetThreshold().Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void It_Throws_ArgumentOutOfRange_Exception_When_MaxItems_Param_Is_Invalid(int maxItems)
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = maxItems });
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryCache<object>(mockOptions));
        ex.Message.Should().Contain("Invalid limit for max cache storage. Must be >= 1.");
    }

    [Fact]
    public void It_Sets_The_Item_In_The_Memory_Cache()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Set(_kvp1.key, _kvp1.value, out var evictedKey);
        
        sut.Get(_kvp1.key).Value.Should().Be(_kvp1.value);
        evictedKey.Should().BeNull();
        sut.GetCount().Should().Be(1);
        sut.GetThreshold().Should().Be(1);
    }

    [Fact]
    public void It_Overwrites_The_Item_In_The_Memory_Cache_With_The_Same_Key()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Set(_kvp1.key, _kvp1.value, out var evictedKey);
        
        sut.Get(_kvp1.key).HasValue.Should().BeTrue();
        sut.Get(_kvp1.key).Value.Should().Be(_kvp1.value);
        evictedKey.Should().BeNull();

        sut.Set(_kvp1.key, "new-value", out evictedKey);
        
        sut.Get(_kvp1.key).Value.Should().Be("new-value");
        evictedKey.Should().BeNull();

        sut.GetCount().Should().Be(1);
        sut.GetThreshold().Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void It_Throws_ArgumentNullException_When_Setting_An_Item_With_An_Invalid_Key(string key)
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        var ex = Assert.Throws<ArgumentNullException>(() => sut.Set(key, _kvp1.value, out _));
        ex.Message.Should().Contain("Cache key must have a value.");
    }

    [Fact]
    public void It_Throws_ArgumentNullException_When_Setting_An_Item_With_An_Invalid_Value()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        object? invalidValue = null;
        var ex = Assert.Throws<ArgumentNullException>(() => sut.Set(_kvp1.key, invalidValue!, out _));
        ex.Message.Should().Contain("Cannot cache null value.");
    }

    [Fact]
    public void It_Gets_Correct_Result_When_Cache_Is_Empty()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Get(_kvp1.key).HasValue.Should().BeFalse();
        sut.Get(_kvp1.key).Value.Should().BeNull();
    }

    [Fact]
    public void It_Gets_Correct_Result_When_Key_Is_NonExistent()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Set(_kvp1.key, _kvp1.value, out _);

        sut.Get("non-existent-key").HasValue.Should().BeFalse();
        sut.Get("non-existent-key").Value.Should().BeNull();
    }

    [Fact]
    public void It_Returns_Correct_Result_When_Key_Is_Valid_And_In_Use()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Set(_kvp1.key, _kvp1.value, out _);
        
        sut.Get(_kvp1.key).HasValue.Should().BeTrue();
        sut.Get(_kvp1.key).Value.Should().Be(_kvp1.value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void It_Throws_ArgumentNullException_When_Getting_An_Item_With_An_Invalid_Key(string key)
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 1 });
        var sut = new InMemoryCache<object>(mockOptions);

        var ex = Assert.Throws<ArgumentNullException>(() => sut.Get(key));
        ex.Message.Should().Contain("Cache key must have a value.");
    }

    [Fact]
    public void It_Evicts_The_Least_Recently_Used_Item_When_Setting_New_Item_Above_Threshold()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 3 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Set(_kvp1.key, _kvp1.value, out _);
        sut.Set(_kvp2.key, _kvp2.value, out _);
        sut.Set(_kvp3.key, _kvp3.value, out _);

        // Refresh positions
        sut.Get(_kvp1.key);
        sut.Get(_kvp2.key);

        // Set new item - trigger LRU eviction for "key-3"
        sut.Set(_kvp4.key, _kvp4.value, out var evictedKey);

        // LRU is evicted
        evictedKey.Should().Be(_kvp3.key);
        sut.Get(_kvp3.key).HasValue.Should().BeFalse();

        sut.GetCount().Should().Be(3);
        sut.GetThreshold().Should().Be(3);

        sut.Get(_kvp1.key).Value.Should().Be(_kvp1.value);
        sut.Get(_kvp2.key).Value.Should().Be(_kvp2.value);
        sut.Get(_kvp4.key).Value.Should().Be(_kvp4.value);
    }

    [Fact]
    public void It_Flushes_The_Cache()
    {
        var mockOptions = Options.Create(new InMemoryCacheOptions { MaxItems = 3 });
        var sut = new InMemoryCache<object>(mockOptions);

        sut.Set(_kvp1.key, _kvp1.value, out _);
        sut.Set(_kvp2.key, _kvp2.value, out _);
        sut.Set(_kvp3.key, _kvp3.value, out _);

        sut.Flush();
        sut.GetCount().Should().Be(0);

        sut.Get(_kvp1.key).HasValue.Should().BeFalse();
        sut.Get(_kvp2.key).HasValue.Should().BeFalse();
        sut.Get(_kvp3.key).HasValue.Should().BeFalse();
    }
}