/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlGenerator
 * FILE:        SqlQuery.cs
 * PURPOSE:     Creates sql queries for table creation using the robust SqlParser.
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

using SqlParser;
using System.Text;

namespace SqlGenerator
{
    /// <summary>
    /// Generator class that converts parsed column definitions into T-SQL scripts.
    /// Delegates parsing to SqlParser.Executor.
    /// </summary>
    public class SqlQuery
    {
        /// <summary>
        /// Parses the input string into column definitions using the SqlParser.
        /// </summary>
        /// <param name="input">The raw input string (Name Type Properties...).</param>
        /// <returns>List of parsed columns.</returns>
        public static IList<ColumnDef> ParseColumns(string input)
        {
            // We use the robust Executor/Tokenizer we built
            var executor = new Executor();

            // "ParsingOnly" is just a label for the debug log, since we don't need the table name here
            var result = executor.ParseAndGenerate(input, "ParsingOnly");

            // Note: result.DebugTrace contains logs/errors if you ever want to expose them
            return result.Columns;
        }

        /// <summary>
        /// Generates the create script.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="columns">The columns.</param>
        /// <param name="includeAudit">if set to <c>true</c> [include audit]. Optional.</param>
        /// <param name="indexColumn">The index column. Optional</param>
        /// <returns>
        /// SQL Query for table with optional Audit fields.
        /// </returns>
        public string GenerateCreateScript(string database, string tableName, IEnumerable<ColumnDef> columns, bool includeAudit = true, string indexColumn = null)
        {
            var sb = new StringBuilder();

            // --- Header ---
            sb.AppendLine($"USE [{database}]");
            sb.AppendLine("GO\n");
            sb.AppendLine($"IF OBJECT_ID('dbo.{tableName}', 'U') IS NOT NULL");
            sb.AppendLine($"    DROP TABLE [dbo].[{tableName}];");
            sb.AppendLine("GO\n");
            sb.AppendLine("SET QUOTED_IDENTIFIER ON");
            sb.AppendLine("GO\n");

            // --- Table Definition ---
            sb.AppendLine($"CREATE TABLE [dbo].[{tableName}]");
            sb.AppendLine("(");

            // 1. ID (Always has a comma because columns follow)
            sb.AppendLine("    [ID] INT IDENTITY(1,1) NOT NULL,");

            // 2. Columns
            foreach (var col in columns)
            {
                // Build the definition: [Name] Type NULL...
                sb.Append("    " + BuildColumnDefinition(col) + ",");

                // Add the comment if exists
                if (!string.IsNullOrWhiteSpace(col.Description))
                {
                    sb.Append(" -- " + col.Description.Trim('"', ' '));
                }
                sb.AppendLine();
            }

            // 3. Audit
            if (includeAudit)
            {
                sb.AppendLine("    -- Audit/system columns");
                sb.AppendLine("    [CreateType] INT NOT NULL DEFAULT (0),");
                sb.AppendLine("    [DTUpdSSF] DATETIME NULL,");
                sb.AppendLine("    [UpdSource] NVARCHAR(50) NULL,");
                sb.AppendLine("    [DTUpd] DATETIME NOT NULL DEFAULT (GETUTCDATE()),");
                sb.AppendLine("    [DTTrans] DATETIME NOT NULL DEFAULT (GETUTCDATE()),");
                sb.AppendLine("    [Del] BIT NOT NULL DEFAULT (0),");
                sb.AppendLine("    [DTDel] DATETIME NULL,");
                sb.AppendLine("    [His] BIT NULL,");
                sb.AppendLine("    [DTHis] DATETIME NULL,");
                sb.AppendLine("    [LastModifiedDateTime] DATETIME NULL DEFAULT (GETUTCDATE()),");
            }

            // 4. PK Constraint (Last item, NO comma)
            sb.AppendLine($"    CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]");

            sb.AppendLine(") ON [PRIMARY]");
            sb.AppendLine("GO");

            // 3. Smart/Manual Index Generation
            if (includeAudit)
            {
                // Priority: 1. Manual Parameter -> 2. Name/Key/No Suffix -> 3. GUID/Int
                var targetCol = indexColumn;

                if (string.IsNullOrEmpty(targetCol))
                {
                    targetCol = columns.FirstOrDefault(c =>
                                        c.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                                        c.Name.EndsWith("Key", StringComparison.OrdinalIgnoreCase) ||
                                        c.Name.EndsWith("No", StringComparison.OrdinalIgnoreCase))?.Name
                                    ?? columns.FirstOrDefault(c => c.Type.Contains("UNIQUEIDENTIFIER", StringComparison.OrdinalIgnoreCase))?.Name
                                    ?? columns.FirstOrDefault(c => c.Type.Contains("INT", StringComparison.OrdinalIgnoreCase))?.Name;
                }

                if (!string.IsNullOrEmpty(targetCol))
                {
                    var cleanColName = targetCol.Trim('[', ']');
                    sb.AppendLine($"CREATE NONCLUSTERED INDEX [IX_{tableName}_{cleanColName}_ActiveOnly]");
                    sb.AppendLine($"ON [dbo].[{tableName}] ([{cleanColName}] ASC)");
                    sb.AppendLine("WHERE [Del] = 0;");
                    sb.AppendLine("GO\n");
                }
            }

            // 4. Extended Properties (Metadata)
            sb.AppendLine($"-- Extended properties for dbo.{tableName}\n");

            // PK Description
            sb.AppendLine(GenerateExtendedProperty(tableName, "ID", $"Eindeutige ID für den Datensatz in {tableName}"));

            if (includeAudit)
            {
                sb.AppendLine(GenerateExtendedProperty(tableName, "CreateType", "Typ der Erstellung (0 = Standard)"));
                sb.AppendLine(GenerateExtendedProperty(tableName, "DTInsert", "Datum/Uhrzeit der Ersteinfügung"));
                sb.AppendLine(GenerateExtendedProperty(tableName, "DTUpd", "Datum/Uhrzeit der letzten Änderung"));
                sb.AppendLine(GenerateExtendedProperty(tableName, "Del", "Löschkennzeichen (0 = aktiv, 1 = gelöscht)"));
                // ... add others as needed
            }

            foreach (var c in columns)
            {
                var desc = string.IsNullOrWhiteSpace(c.Description) ? c.Name : c.Description;
                sb.AppendLine(GenerateExtendedProperty(tableName, c.Name, desc));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the column definition string.
        /// </summary>
        private static string BuildColumnDefinition(ColumnDef c)
        {
            // Normalize Name: Ensure it's clean so we can wrap it in brackets ourselves
            var cleanName = c.Name.Trim('[', ']', ' ');

            var sb = new StringBuilder();
            sb.Append($"[{cleanName}] {c.Type}");

            if (!string.IsNullOrEmpty(c.Nullable)) sb.Append(" " + c.Nullable);
            if (!string.IsNullOrEmpty(c.Default)) sb.Append(" " + c.Default);

            return sb.ToString();
        }

        /// <summary>
        /// Generates the sp_addextendedproperty script.
        /// </summary>
        private string GenerateExtendedProperty(string tableName, string columnName, string description)
        {
            var cleanTableName = tableName.Trim('[', ']');
            var cleanColumnName = columnName.Trim('[', ']');
            var cleanDesc = description.Trim('"', ' ', ',');

            return $@"EXEC sys.sp_addextendedproperty 
                    @name=N'MS_Description', @value=N'{EscapeSingleQuotes(cleanDesc)}', 
                    @level0type=N'SCHEMA', @level0name=N'dbo', 
                    @level1type=N'TABLE',  @level1name=N'{cleanTableName}', 
                    @level2type=N'COLUMN', @level2name=N'{cleanColumnName}';
                GO
                ";
        }

        private static string EscapeSingleQuotes(string s) => (s ?? string.Empty).Replace("'", "''");
    }
}