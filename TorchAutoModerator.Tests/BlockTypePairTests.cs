using System;
using AutoModerator.Core;
using NUnit.Framework;

namespace TorchAutoModerator.Tests
{
    [TestFixture]
    public class BlockTypePairTests
    {
        BlockTypePairCollection _pairs;

        [SetUp]
        public void Setup()
        {
            _pairs = new BlockTypePairCollection();
        }

        [Test]
        public void TestEmpty()
        {
            Assert.False(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestBlank()
        {
            Assert.False(_pairs.TryAdd(""));
        }

        [Test]
        public void TestSlash()
        {
            Assert.False(_pairs.TryAdd("/"));
        }

        [Test]
        public void TestWhitespaceInput()
        {
            Assert.False(_pairs.TryAdd(" "));
        }

        [Test]
        public void TestContainsNoMatch()
        {
            Assert.True(_pairs.TryAdd("Nuu/Woo"));
            Assert.False(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestContainsAll()
        {
            Assert.True(_pairs.TryAdd("Foo"));
            Assert.True(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestContainsAllSlash()
        {
            Assert.True(_pairs.TryAdd("Foo/"));
            Assert.True(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestContainsPartialMatch()
        {
            Assert.True(_pairs.TryAdd("Foo/Baz"));
            Assert.False(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestContainsCompleteMatch()
        {
            Assert.True(_pairs.TryAdd("Foo/Bar"));
            Assert.True(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestContainsCompleteMatchWithOthers()
        {
            Assert.True(_pairs.TryAdd("Foo/Bar"));
            Assert.True(_pairs.TryAdd("Foo/Baz"));
            Assert.True(_pairs.Contains("Foo", "Bar"));
        }

        [Test]
        public void TestContainsCompleteMatchWithOthersDifferentType()
        {
            Assert.True(_pairs.TryAdd("Nuu/Bar"));
            Assert.True(_pairs.TryAdd("Foo/Bar"));
            Assert.True(_pairs.Contains("Foo", "Bar"));
        }
    }
}