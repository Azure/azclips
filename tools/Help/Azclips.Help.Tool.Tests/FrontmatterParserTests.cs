// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli.Help;

namespace Azclips.Help.Tool.Tests
{
    /// <summary>
    /// Tests for the hand-rolled <see cref="FrontmatterParser"/>
    /// </summary>
    public class FrontmatterParserTests
    {
        [Fact]
        public void Parse_ShouldReturnEmptyDoc_WhenContentIsNullOrEmpty()
        {
            var (doc1, body1) = FrontmatterParser.Parse(null);
            Assert.Equal(FrontmatterDoc.Empty, doc1);
            Assert.Equal(string.Empty, body1);

            var (doc2, body2) = FrontmatterParser.Parse(string.Empty);
            Assert.Equal(FrontmatterDoc.Empty, doc2);
            Assert.Equal(string.Empty, body2);
        }

        [Fact]
        public void Parse_ShouldReturnEmptyDoc_WhenNoFrontmatterDelimiter()
        {
            var content = "Description: just a body\n\n### Example\n```bash\nx\n```\n";

            var (doc, body) = FrontmatterParser.Parse(content);

            Assert.Equal(string.Empty, doc.Summary);
            Assert.Equal(string.Empty, doc.Schema);
            Assert.Equal(content, body);
        }

        [Fact]
        public void Parse_ShouldExtractAllKnownFields()
        {
            var content = string.Join("\n",
                "---",
                "summary: One-line description.",
                "status: preview",
                "schema: 1.0.0",
                "online version: https://learn.microsoft.com/foo",
                "---",
                "",
                "Description: body");

            var (doc, body) = FrontmatterParser.Parse(content);

            Assert.Equal("One-line description.", doc.Summary);
            Assert.Equal("preview", doc.Status);
            Assert.Equal("1.0.0", doc.Schema);
            Assert.Equal("https://learn.microsoft.com/foo", doc.OnlineVersion);
            Assert.Equal("Description: body", body);
        }

        [Fact]
        public void Parse_ShouldDefaultStatusToGa_WhenStatusOmitted()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "---",
                "",
                "body");

            var (doc, _) = FrontmatterParser.Parse(content);

            Assert.Equal("ga", doc.Status);
        }

        [Fact]
        public void Parse_ShouldParseRelatedLinksList()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "relatedLinks:",
                "  - az logout",
                "  - az group list",
                "---",
                "",
                "body");

            var (doc, _) = FrontmatterParser.Parse(content);

            Assert.Equal(2, doc.RelatedLinks.Count);
            Assert.Equal("az logout", doc.RelatedLinks[0]);
            Assert.Equal("az group list", doc.RelatedLinks[1]);
        }

        [Fact]
        public void Parse_ShouldReturnEmptyDoc_WhenClosingDelimiterMissing()
        {
            // Unterminated frontmatter → graceful degradation: empty doc, original body.
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "",
                "Description: body without close");

            var (doc, body) = FrontmatterParser.Parse(content);

            Assert.Equal(string.Empty, doc.Summary);
            Assert.Equal(content, body);
        }

        [Fact]
        public void Parse_ShouldHandleCrLfLineEndings()
        {
            var content = "---\r\nsummary: x\r\nschema: 1.0.0\r\n---\r\n\r\nDescription: body\r\n";

            var (doc, body) = FrontmatterParser.Parse(content);

            Assert.Equal("x", doc.Summary);
            Assert.Equal("1.0.0", doc.Schema);
            Assert.Contains("Description: body", body, StringComparison.Ordinal);
        }

        [Fact]
        public void Parse_ShouldIgnoreUnknownKeys()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "unknownKey: something",
                "---",
                "",
                "body");

            var (doc, _) = FrontmatterParser.Parse(content);

            Assert.Equal("x", doc.Summary);
            Assert.Equal("1.0.0", doc.Schema);
        }

        [Fact]
        public void ValidStatuses_ShouldContainExpectedValues()
        {
            Assert.Contains("ga", FrontmatterParser.ValidStatuses);
            Assert.Contains("preview", FrontmatterParser.ValidStatuses);
            Assert.Contains("deprecated", FrontmatterParser.ValidStatuses);
            Assert.Contains("internal", FrontmatterParser.ValidStatuses);
        }
    }
}
