/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlParserTests
 * FILE:        ParserTests.cs
 * PURPOSE:     Your file purpose here
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlParser;
using System.Linq;

namespace SqlParserTests
{
    [TestClass]
    public class ParserTests
    {
        private Executor _executor;

        [TestInitialize]
        public void Setup()
        {
            _executor = new Executor();
        }

        [TestMethod]
        public void Parse_SimpleColumn_ReturnsCorrectly()
        {
            // Arrange
            string input = "[FirstName] NVARCHAR(200)";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("[FirstName]", result.Name);
            Assert.AreEqual("NVARCHAR(200)", result.Type);
            Assert.AreEqual("NULL", result.Nullable); // Default
        }

        [TestMethod]
        public void Parse_ComplexType_WithParens()
        {
            // Arrange
            string input = "Rate DECIMAL(18, 2) NOT NULL";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            Assert.AreEqual("DECIMAL(18,2)", result.Type); // Note: Our tokenizer strips spaces inside parens
            Assert.AreEqual("NOT NULL", result.Nullable);
        }

        [TestMethod]
        public void Parse_DefaultValue_WithParens()
        {
            // Arrange
            string input = "IsActive BIT DEFAULT(1)";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            Assert.AreEqual("DEFAULT(1)", result.Default);
        }

        [TestMethod]
        public void Parse_DefaultValue_WithoutParens_StandardSql()
        {
            // Arrange
            string input = "Status INT DEFAULT 0";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            Assert.AreEqual("DEFAULT 0", result.Default);
        }

        [TestMethod]
        public void Parse_Description_ExplicitQuotes()
        {
            // Arrange
            string input = "Age INT \"User Age in Years\"";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            Assert.AreEqual("User Age in Years", result.Description);
        }

        [TestMethod]
        public void Parse_Description_Implicit_NoQuotes()
        {
            // Arrange
            string input = "Age INT User Age";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            // The parser collects trailing tokens as description
            Assert.AreEqual("User Age", result.Description);
        }

        [TestMethod]
        public void Parse_FullKitchenSink()
        {
            // Arrange
            string input = "[CreatedDate] DATETIME NOT NULL DEFAULT(GETDATE()) \"Creation Timestamp\"";

            // Act
            var result = _executor.ParseLine(input);

            // Assert
            Assert.AreEqual("[CreatedDate]", result.Name);
            Assert.AreEqual("DATETIME", result.Type);
            Assert.AreEqual("NOT NULL", result.Nullable);
            Assert.AreEqual("DEFAULT(GETDATE())", result.Default);
            Assert.AreEqual("Creation Timestamp", result.Description);
        }
    }
}