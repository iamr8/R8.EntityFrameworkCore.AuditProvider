using System.Text.Json;

using FluentAssertions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    // Direct tests for EntityFrameworkAuditProviderInterceptor.IsEqual (internal). This runs on every
    // target framework, so the same assertions validate BOTH implementations of IsEqual:
    //   * net8.0 / net10.0 -> System.Text.Json 9+ JsonElement.DeepEquals
    //   * net6.0           -> the manual recursive comparison
    // Keeping them behaviourally identical for these cases is exactly what we want to guarantee.
    public class IsEqual_Tests
    {
        private static bool Eq(string json1, string json2)
        {
            using var d1 = JsonDocument.Parse(json1);
            using var d2 = JsonDocument.Parse(json2);
            return EntityFrameworkAuditProviderInterceptor.IsEqual(d1.RootElement, d2.RootElement);
        }

        [Theory]
        // scalars — equal
        [InlineData("1", "1", true)]
        [InlineData("\"a\"", "\"a\"", true)]
        [InlineData("true", "true", true)]
        [InlineData("false", "false", true)]
        [InlineData("null", "null", true)]
        // numbers — semantic normalization
        [InlineData("1.0", "1", true)]
        [InlineData("1.50", "1.5", true)]
        [InlineData("1", "2", false)]
        [InlineData("true", "false", false)]
        // strings — exact, case-sensitive
        [InlineData("\"a\"", "\"b\"", false)]
        [InlineData("\"a\"", "\"A\"", false)]
        // kind mismatches that both implementations reject without throwing
        [InlineData("1", "\"1\"", false)]
        [InlineData("1", "null", false)]
        [InlineData("null", "0", false)]
        [InlineData("\"1\"", "1", false)]
        // objects — order-insensitive
        [InlineData("{\"a\":1,\"b\":2}", "{\"a\":1,\"b\":2}", true)]
        [InlineData("{\"a\":1,\"b\":2}", "{\"b\":2,\"a\":1}", true)]
        [InlineData("{\"a\":1}", "{\"a\":2}", false)]
        [InlineData("{\"a\":1}", "{\"b\":1}", false)]
        [InlineData("{\"a\":1}", "{\"a\":1,\"b\":2}", false)]
        [InlineData("{}", "{}", true)]
        [InlineData("{}", "{\"a\":1}", false)]
        // nested
        [InlineData("{\"a\":{\"x\":1}}", "{\"a\":{\"x\":1}}", true)]
        [InlineData("{\"a\":{\"x\":1}}", "{\"a\":{\"x\":2}}", false)]
        // arrays — order-sensitive
        [InlineData("[1,2,3]", "[1,2,3]", true)]
        [InlineData("[1,2,3]", "[3,2,1]", false)]
        [InlineData("[1,2]", "[1,2,3]", false)]
        [InlineData("[]", "[]", true)]
        [InlineData("[{\"a\":1}]", "[{\"a\":1}]", true)]
        [InlineData("[{\"a\":1}]", "[{\"a\":2}]", false)]
        // mixed & deeply nested — every ValueKind combination
        [InlineData("[1,\"a\",true,null]", "[1,\"a\",true,null]", true)]
        [InlineData("[1,\"a\",true,null]", "[1,\"a\",false,null]", false)]
        [InlineData("{\"a\":[1,2],\"b\":{\"c\":\"x\"}}", "{\"b\":{\"c\":\"x\"},\"a\":[1,2]}", true)]
        [InlineData("{\"a\":[1,2]}", "{\"a\":[2,1]}", false)]
        [InlineData("{\"a\":{\"b\":{\"c\":[1,{\"d\":null}]}}}", "{\"a\":{\"b\":{\"c\":[1,{\"d\":null}]}}}", true)]
        [InlineData("{\"a\":{\"b\":{\"c\":[1,{\"d\":null}]}}}", "{\"a\":{\"b\":{\"c\":[1,{\"d\":0}]}}}", false)]
        // same key, value of a different kind
        [InlineData("{\"a\":1}", "{\"a\":\"1\"}", false)]
        [InlineData("{\"a\":[1]}", "{\"a\":{\"0\":1}}", false)]
        public void IsEqual_matches_expected(string json1, string json2, bool expected)
        {
            Eq(json1, json2).Should().Be(expected);
        }

#if NET8_0_OR_GREATER
        [Fact]
        public void DeepEquals_handles_cases_the_manual_comparison_cannot()
        {
            // JsonElement.DeepEquals compares numbers outside the decimal range without throwing (the
            // net6 manual path uses GetDecimal() and would overflow), and rejects bool-vs-number kind
            // mismatches instead of throwing. These are the robustness wins of adopting DeepEquals.
            Eq("1e400", "1e400").Should().BeTrue();
            Eq("1e400", "1e401").Should().BeFalse();
            Eq("true", "1").Should().BeFalse();
        }
#endif
    }
}
