using System.Text;
using System.Text.RegularExpressions;

namespace SqlGenerator
{
    /// <summary>
    /// Simple parser + generator. Keep small and extendable.
    /// Syntax (per-line):
    /// Name Type [NULL|NOT NULL] [DEFAULT(...)] "Description"
    ///
    /// Examples:
    /// Name NVARCHAR(250) NULL "Firmenname (lokalisiert)"
    /// IsActive BIT NOT NULL DEFAULT(1) "Aktiv-Flag"
    /// </summary>
    public class SqlQuery
    {
        /// <summary>
        /// Captures Name, Type (with parens), Properties (middle), and optional Description (quotes at end).
        /// </summary>
        private static readonly Regex LineRegex = new Regex(
            @"^\s*(?<name>\S+)\s+" +                          // 1. Name
            @"(?<type>[\w\[\]]+(?:\s*\([^)]+\))?)" +          // 2. Type (allows parens like DECIMAL(10,2))
            @"(?<rest>.*)$",                                  // 3. The Rest (Properties + Description)
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public IList<ColumnDef> ParseColumns(string input)
        {
            var list = new List<ColumnDef>();
            var lines = (input ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Select(x => x.Trim()))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--")) continue;

                var m = LineRegex.Match(line);
                if (!m.Success) continue;

                string name = m.Groups["name"].Value;
                string type = m.Groups["type"].Value;
                string rest = m.Groups["rest"].Value.Trim();

                string description = string.Empty;
                string props = rest;

                // --- STRATEGY: Separate Props from Description ---

                // Case A: Explicit Comment "--"
                int commentIndex = rest.IndexOf("--");
                if (commentIndex >= 0)
                {
                    description = rest.Substring(commentIndex + 2).Trim();
                    props = rest.Substring(0, commentIndex).Trim();
                }
                // Case B: Explicit Quotes "..."
                else if (rest.Contains("\""))
                {
                    var quoteMatch = Regex.Match(rest, @"\""(?<desc>.*)\""");
                    if (quoteMatch.Success)
                    {
                        description = quoteMatch.Groups["desc"].Value;
                        props = rest.Replace(quoteMatch.Value, "").Trim();
                    }
                }
                // Case C: Implicit / No Delimiter
                else
                {
                    // We assume "props" are keywords (NULL, NOT NULL, DEFAULT). 
                    // Anything that isn't a keyword is the description.

                    // 1. Remove DEFAULT(...) from the string temporarily to protect it
                    string tempProps = props;
                    string defaultValue = null;
                    var defaultMatch = Regex.Match(props, @"DEFAULT\s*\(?.*?\)?", RegexOptions.IgnoreCase);
                    if (defaultMatch.Success)
                    {
                        defaultValue = defaultMatch.Value;
                        tempProps = tempProps.Replace(defaultValue, "");
                    }

                    // 2. Remove NULL / NOT NULL
                    bool isNotNull = tempProps.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isNotNull) tempProps = Regex.Replace(tempProps, "NOT NULL", "", RegexOptions.IgnoreCase);

                    bool isNull = !isNotNull && tempProps.IndexOf("NULL", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isNull) tempProps = Regex.Replace(tempProps, "NULL", "", RegexOptions.IgnoreCase);

                    // 3. What is left is the Description!
                    description = tempProps.Trim();
                }

                // --- FINAL PROPERTY PARSING ---
                // Now re-evaluate 'props' (or the original context) for the standard flags

                string finalNullable = "NULL";
                if (props.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0)
                    finalNullable = "NOT NULL";

                string finalDefault = null;
                var defMatch = Regex.Match(props, @"DEFAULT\s*\(?.*?\)?", RegexOptions.IgnoreCase);
                if (defMatch.Success) finalDefault = defMatch.Value;

                list.Add(new ColumnDef(name, type, finalNullable, finalDefault, description));
            }

            return list;
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
            // Since we ALWAYS add a PK constraint at the end, every column line can safely have a comma.
            foreach (var col in columns)
            {
                sb.Append("    " + BuildColumnDefinition(col) + ","); // Append Comma here

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

        private static string BuildColumnDefinition(ColumnDef c)
        {
            var cleanName = c.Name.Trim('[', ']', ' ');
            var sb = new StringBuilder();
            sb.Append($"[{cleanName}] {c.Type}");

            if (!string.IsNullOrEmpty(c.Nullable)) sb.Append(" " + c.Nullable);
            if (!string.IsNullOrEmpty(c.Default)) sb.Append(" " + c.Default);

            return sb.ToString();
        }

        /// <summary>
        /// Generates the extended property.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <param name="description">The description.</param>
        /// <returns>Returns SQL query part for extended Properties</returns>
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
